using System;
using System.Collections.Generic;
using RagNextPlayer.Runtime.Models;
using UnityEngine;

namespace RagNextPlayer.Runtime
{
    /// <summary>
    /// Lightweight runtime context passed to every ActionStep during execution.
    /// Holds mutable game state references without duplicating data.
    /// Mirrors RagsCore.Actions.ActionContext.
    /// </summary>
    public class GameExecutionContext
    {
        public GameData       Game        { get; }
        public RoomData?      CurrentRoom { get; set; }
        public GameObjectData? FocusObject{ get; }
        public object?        FocusEntity { get; }

        public PlayerData Player => Game.Player;

        public GameExecutionContext(GameData game, RoomData? currentRoom = null, GameObjectData? focusObject = null, object? focusEntity = null)
        {
            Game        = game ?? throw new ArgumentNullException(nameof(game));
            CurrentRoom = currentRoom;
            FocusObject = focusObject;
            FocusEntity = focusEntity ?? focusObject;
        }

        public GameVariableData? GetVariable(string name) =>
            Game.Variables.Find(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));

        public void SetVariable(string name, string? value)
        {
            var v = GetVariable(name);
            if (v is null)
            {
                v = new GameVariableData { Name = name, Value = value };
                Game.Variables.Add(v);
            }
            else
            {
                v.Value = value;
            }
        }

        /// <summary>Returns the resolved text of {token} patterns in a string.</summary>
        public string Resolve(string? text) =>
            TemplateResolver.Resolve(text, Game, CurrentRoom, FocusEntity);

    }

    /// <summary>
    /// Executes a game action (list of ActionSteps) recursively, propagating
    /// results to the caller through IGameEventSink.
    ///
    /// Design principles (ported from MAUI ActionExecutor):
    ///   - Model objects mutate state only — no Unity API calls inside Execute().
    ///   - The sink (CommandEffectRouter) drives all UI/audio side-effects.
    ///   - Conditions recurse fully: TrueBranch / FalseBranch can contain further
    ///     commands or deeply-nested conditions with no depth limit.
    ///   - Exceptions are caught per-node so a single bad command never aborts
    ///     the rest of the action sequence.
    /// </summary>
    public static class ActionExecutor
    {
        public static void Execute(ActionData action, GameExecutionContext ctx, IGameEventSink? sink = null)
        {
            if (action is null || ctx is null) return;
            foreach (var node in action.Nodes)
                ExecuteNode(node, ctx, sink);
        }

        private static void ExecuteNode(ActionStepData node, GameExecutionContext ctx, IGameEventSink? sink)
        {
            if (node is null) return;

            if (node is CommandData cmd)
            {
                try
                {
                    ExecuteCommand(cmd, ctx);
                    sink?.OnCommandExecuted(cmd, ctx);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActionExecutor] Error executing {node.Type}: {ex.Message}");
                }
            }
            else if (node is ConditionData cond)
            {
                try
                {
                    bool result = EvaluateCondition(cond, ctx);
                    sink?.OnConditionEvaluated(cond, result, ctx);

                    var branch = result ? cond.TrueBranch : cond.FalseBranch;
                    foreach (var step in branch)
                        ExecuteNode(step, ctx, sink);  // ← full recursion, unlimited depth
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ActionExecutor] Error evaluating {node.Type}: {ex.Message}");
                }
            }
        }

        // ── Command Dispatch ──────────────────────────────────────────────────

        private static void ExecuteCommand(CommandData cmd, GameExecutionContext ctx)
        {
            switch (cmd)
            {
                case DisplayTextCommandData c:
                    ctx.SetVariable("system.lastDisplayedText", ctx.Resolve(c.Text));
                    break;

                case SetVariableCommandData c:
                    ctx.SetVariable(c.Name, ctx.Resolve(c.Value));
                    break;

                case VariableIncrementCommandData c:
                    {
                        var resolvedVal = ctx.Resolve(c.Value);
                        var existing = ctx.GetVariable(c.Name)?.Value;
                        if (double.TryParse(existing, out double a) && double.TryParse(resolvedVal, out double b))
                            ctx.SetVariable(c.Name, (a + b).ToString(System.Globalization.CultureInfo.InvariantCulture));
                        else
                            ctx.SetVariable(c.Name, (existing ?? "") + resolvedVal);
                    }
                    break;

                case VariableDecrementCommandData c:
                    {
                        var resolvedVal = ctx.Resolve(c.Value);
                        var existing = ctx.GetVariable(c.Name)?.Value;
                        if (double.TryParse(existing, out double a) && double.TryParse(resolvedVal, out double b))
                            ctx.SetVariable(c.Name, (a - b).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    break;

                case VariableSetToVariableCommandData c:
                    ctx.SetVariable(c.Name, ctx.GetVariable(c.SourceName)?.Value);
                    break;

                case SetNumericRandomlyCommandData c:
                    {
                        var val = UnityEngine.Random.Range((int)c.Minimum, (int)c.Maximum + 1);
                        ctx.SetVariable(c.Name, val.ToString());
                    }
                    break;

                case MovePlayerToRoomCommandData c:
                    {
                        var resolved = ctx.Resolve(c.RoomId);
                        ctx.SetVariable("player.currentRoomId", resolved);
                    }
                    break;

                case AddObjectToRoomCommandData c:
                    {
                        var resolvedRoom = ctx.Resolve(c.RoomId);
                        var resolvedObj  = ctx.Resolve(c.ObjectId);
                        var room = ctx.Game.Rooms.Find(r => r.Id == resolvedRoom);
                        if (room is not null && !room.ObjectIds.Contains(resolvedObj))
                            room.ObjectIds.Add(resolvedObj);
                    }
                    break;

                case RemoveObjectFromRoomCommandData c:
                    {
                        var resolvedRoom = ctx.Resolve(c.RoomId);
                        var resolvedObj  = ctx.Resolve(c.ObjectId);
                        var room = ctx.Game.Rooms.Find(r => r.Id == resolvedRoom);
                        room?.ObjectIds.Remove(resolvedObj);
                    }
                    break;

                case SetRoomExitCommandData c:
                    {
                        var resolvedRoom = ctx.Resolve(c.RoomId);
                        var resolvedDir  = ctx.Resolve(c.Direction);
                        var resolvedDest = ctx.Resolve(c.DestinationRoomId);
                        var rId = string.IsNullOrWhiteSpace(resolvedRoom) ? ctx.CurrentRoom?.Id : resolvedRoom;
                        var room = ctx.Game.Rooms.Find(r => r.Id == rId);
                        if (room is null || string.IsNullOrWhiteSpace(resolvedDir)) break;
                        if (string.IsNullOrWhiteSpace(resolvedDest))
                            room.Exits.Remove(resolvedDir);
                        else
                            room.Exits[resolvedDir] = resolvedDest;
                    }
                    break;

                case DisableRoomExitCommandData c:
                    {
                        var resolvedRoom = ctx.Resolve(c.RoomId);
                        var resolvedDir  = ctx.Resolve(c.Direction);
                        var rId = string.IsNullOrWhiteSpace(resolvedRoom) ? ctx.CurrentRoom?.Id : resolvedRoom;
                        var room = ctx.Game.Rooms.Find(r => r.Id == rId);
                        room?.Exits.Remove(resolvedDir);
                    }
                    break;

                case PlayerSetNameCommandData c:
                    ctx.Player.Name = c.Name;
                    break;

                case PlayerSetDescriptionCommandData c:
                    ctx.Player.Description = c.Description;
                    break;

                case PlayerSetGenderCommandData c:
                    ctx.Player.Gender = c.Gender;
                    break;

                case PlayerSetPortraitMediaCommandData c:
                    {
                        var resolved = ctx.Resolve(c.MediaId);
                        var asset = ctx.Game.MediaAssets.Find(a => a.Id == resolved);
                        ctx.Player.PortraitImagePath = asset?.RelativePath ?? resolved;
                    }
                    break;

                case CharacterMoveToRoomCommandData c:
                    ctx.SetVariable($"char.{ctx.Resolve(c.CharacterId)}.currentRoomId", ctx.Resolve(c.RoomId));
                    break;

                case CharacterDisplayPortraitCommandData c:
                    ctx.SetVariable($"char.{ctx.Resolve(c.CharacterId)}.displayedPortraitId", ctx.Resolve(c.PortraitId));
                    break;

                case PlaySoundEffectCommandData c:
                    {
                        var resolved = ctx.Resolve(c.SoundId);
                        ctx.SetVariable("media.lastSoundId", resolved);
                        ctx.SetVariable("media.lastSoundVolume", c.Volume.ToString());
                    }
                    break;

                case DisplayMultimediaCommandData c:
                    ctx.SetVariable("media.lastDisplayedMediaId", ctx.Resolve(c.MediaId));
                    break;

                case AddCommentCommandData:
                    break; // Design-time only — no runtime effect

                case EndGameCommandData c:
                    ctx.SetVariable("system.isGameOver", "true");
                    ctx.SetVariable("system.endGameMessage", ctx.Resolve(c.FinalMessage));
                    break;

                case PromptPlayerInputCommandData c:
                    ctx.SetVariable("system.prompt.text", ctx.Resolve(c.PromptText));
                    ctx.SetVariable("system.prompt.type", c.InputType);
                    ctx.SetVariable("system.prompt.options", c.CustomOptions);
                    ctx.SetVariable("system.prompt.targetVar", c.StoreVariableName);
                    ctx.SetVariable("system.prompt.active", "true");
                    break;

                case OpenContainerCommandData c:
                    {
                        var id = ctx.Resolve(c.ObjectId);
                        var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
                        if (obj is not null)
                        {
                            obj.ContainerOpen = true;
                            ctx.SetVariable($"obj.{id}.containerOpen", "true");
                        }
                    }
                    break;

                case CloseContainerCommandData c:
                    {
                        var id = ctx.Resolve(c.ObjectId);
                        var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
                        if (obj is not null)
                        {
                            obj.ContainerOpen = false;
                            ctx.SetVariable($"obj.{id}.containerOpen", "false");
                        }
                    }
                    break;

                default:
                    Debug.LogWarning($"[ActionExecutor] Unhandled command type: {cmd.Type}");
                    break;
            }
        }

        // ── Condition Dispatch ────────────────────────────────────────────────

        private static bool EvaluateCondition(ConditionData cond, GameExecutionContext ctx)
        {
            return cond switch
            {
                VariableEqualsConditionData c =>
                    c.CaseInsensitive
                        ? string.Equals(ctx.GetVariable(c.Name)?.Value, ctx.Resolve(c.Value), StringComparison.OrdinalIgnoreCase)
                        : string.Equals(ctx.GetVariable(c.Name)?.Value, ctx.Resolve(c.Value), StringComparison.Ordinal),

                VariableComparisonConditionData c =>
                    CompareValues(ctx.GetVariable(c.Name)?.Value ?? "", ctx.Resolve(c.Value) ?? "", c.Comparison),

                VariableComparisonToVariableConditionData c =>
                    CompareValues(ctx.GetVariable(c.NameA)?.Value ?? "", ctx.GetVariable(c.NameB)?.Value ?? "", c.Comparison),

                PlayerInRoomConditionData c =>
                    string.Equals(ctx.CurrentRoom?.Id, ctx.Resolve(c.RoomId), StringComparison.OrdinalIgnoreCase),

                RoomHasObjectConditionData c =>
                    ctx.Game.Rooms.Find(r => r.Id == ctx.Resolve(c.RoomId))
                        ?.ObjectIds.Contains(ctx.Resolve(c.ObjectId)) ?? false,

                ItemInRoomConditionData c =>
                    ctx.Game.Rooms.Find(r => r.Id == ctx.Resolve(c.RoomId))
                        ?.ObjectIds.Contains(ctx.Resolve(c.ItemId)) ?? false,

                ItemHeldByPlayerConditionData c =>
                    ctx.Player.Inventory.Exists(i => i.Id == ctx.Resolve(c.ItemId)),

                ItemNotHeldByPlayerConditionData c =>
                    !ctx.Player.Inventory.Exists(i => i.Id == ctx.Resolve(c.ItemId)),

                ItemHeldByCharacterConditionData c =>
                    ctx.Game.Characters.Find(ch => ch.Id == ctx.Resolve(c.CharacterId))
                        ?.Inventory.Exists(i => i.Id == ctx.Resolve(c.ItemId)) ?? false,

                PlayerInSameRoomAsConditionData c =>
                    string.Equals(
                        ctx.GetVariable($"char.{ctx.Resolve(c.CharacterId)}.currentRoomId")?.Value,
                        ctx.CurrentRoom?.Id, StringComparison.OrdinalIgnoreCase),

                CharacterInRoomConditionData c =>
                    string.Equals(
                        ctx.GetVariable($"char.{ctx.Resolve(c.CharacterId)}.currentRoomId")?.Value,
                        ctx.Resolve(c.RoomId), StringComparison.OrdinalIgnoreCase),

                CharacterGenderConditionData c =>
                    string.Equals(
                        ctx.Game.Characters.Find(ch => ch.Id == ctx.Resolve(c.CharacterId))
                            ?.Properties.GetValueOrDefault("Gender", "Male"),
                        c.Gender, StringComparison.OrdinalIgnoreCase),

                PlayerGenderConditionData c =>
                    string.Equals(ctx.Player.Gender, c.Gender, StringComparison.OrdinalIgnoreCase),

                _ => false
            };
        }

        private static bool CompareValues(string a, string b, string op)
        {
            if (double.TryParse(a, out double na) && double.TryParse(b, out double nb))
            {
                return op switch
                {
                    "="  => na == nb,
                    "!=" => na != nb,
                    ">"  => na > nb,
                    ">=" => na >= nb,
                    "<"  => na < nb,
                    "<=" => na <= nb,
                    _    => false
                };
            }
            return op switch
            {
                "="  => string.Equals(a, b, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(a, b, StringComparison.OrdinalIgnoreCase),
                _    => false
            };
        }
    }
}
