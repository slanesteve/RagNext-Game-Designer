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
    public class ActionRunner
    {
        private Stack<IEnumerator<ActionStepData>> _scopes = new();
        private GameExecutionContext _ctx;
        private IGameEventSink? _sink;
        
        public bool IsSuspended { get; private set; }
        
        public ActionRunner(ActionData action, GameExecutionContext ctx, IGameEventSink? sink)
        {
            _ctx = ctx;
            _sink = sink;
            _scopes.Push(action.Nodes.GetEnumerator());
        }
        
        public void Resume()
        {
            IsSuspended = false;
            _ctx.SetVariable("system.prompt.active", "false");
            ExecuteNext();
        }
        
        public void ExecuteNext()
        {
            while (_scopes.Count > 0 && !IsSuspended)
            {
                var currentScope = _scopes.Peek();
                if (currentScope.MoveNext())
                {
                    var node = currentScope.Current;
                    if (node is null) continue;
                    
                    if (node is CommandData cmd)
                    {
                        try
                        {
                            if (cmd is CallFunctionCommandData callCmd)
                            {
                                var func = _ctx.Game.Functions.Find(f => f.Id == callCmd.FunctionId || string.Equals(f.Name, callCmd.FunctionId, System.StringComparison.OrdinalIgnoreCase));
                                if (func != null && func.Nodes != null && func.Nodes.Count > 0)
                                {
                                    _scopes.Push(func.Nodes.GetEnumerator());
                                }
                            }
                            else
                            {
                                ActionExecutor.ExecuteCommand(cmd, _ctx);
                                _sink?.OnCommandExecuted(cmd, _ctx);
                            }

                            if (_ctx.GetVariable("system.prompt.active")?.Value == "true")
                            {
                                IsSuspended = true;
                                break;
                            }
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
                            bool result = ActionExecutor.EvaluateCondition(cond, _ctx);
                            _sink?.OnConditionEvaluated(cond, result, _ctx);
                            
                            var branch = result ? cond.TrueBranch : cond.FalseBranch;
                            if (branch != null && branch.Count > 0)
                            {
                                _scopes.Push(branch.GetEnumerator());
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[ActionExecutor] Error evaluating {node.Type}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    currentScope.Dispose();
                    _scopes.Pop();
                }
            }
        }
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
        public static ActionRunner? ActiveRunner { get; set; }

        public static void Execute(ActionData action, GameExecutionContext ctx, IGameEventSink? sink = null)
        {
            if (action is null || ctx is null) return;
            
            var runner = new ActionRunner(action, ctx, sink);
            ActiveRunner = runner;
            runner.ExecuteNext();
        }

        // ── Command Dispatch ──────────────────────────────────────────────────

        internal static void ExecuteCommand(CommandData cmd, GameExecutionContext ctx)
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

                case LockRoomExitCommandData c:
                    {
                        var resolvedRoom = ctx.Resolve(c.RoomId);
                        var resolvedDir  = ctx.Resolve(c.Direction);
                        var rId = string.IsNullOrWhiteSpace(resolvedRoom) ? ctx.CurrentRoom?.Id : resolvedRoom;
                        var room = ctx.Game.Rooms.Find(r => r.Id == rId);
                        if (room is not null && !string.IsNullOrWhiteSpace(resolvedDir))
                        {
                            room.LockedExits[resolvedDir] = true;
                        }
                    }
                    break;

                case UnlockRoomExitCommandData c:
                    {
                        var resolvedRoom = ctx.Resolve(c.RoomId);
                        var resolvedDir  = ctx.Resolve(c.Direction);
                        var rId = string.IsNullOrWhiteSpace(resolvedRoom) ? ctx.CurrentRoom?.Id : resolvedRoom;
                        var room = ctx.Game.Rooms.Find(r => r.Id == rId);
                        if (room is not null && !string.IsNullOrWhiteSpace(resolvedDir))
                        {
                            room.LockedExits[resolvedDir] = false;
                        }
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
                        ctx.SetVariable("media.lastSoundLoop", c.Loop.ToString().ToLower());
                    }
                    break;

                case StopSoundEffectCommandData c:
                    {
                        var resolved = ctx.Resolve(c.SoundId);
                        ctx.SetVariable("media.stopSoundId", resolved);
                        ctx.SetVariable("media.stopAllLooping", c.StopAllLooping.ToString().ToLower());
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
                    ctx.SetVariable("system.prompt.name", ctx.Resolve(c.PromptName));
                    ctx.SetVariable("system.prompt.text", ctx.Resolve(c.PromptText));
                    ctx.SetVariable("system.prompt.type", c.InputType);
                    ctx.SetVariable("system.prompt.options", c.CustomOptions);
                    ctx.SetVariable("system.prompt.targetVar", c.StoreVariableName);
                    ctx.SetVariable("system.prompt.active", "true");
                    break;

                case AddCustomChoiceCommandData c:
                    {
                        var promptName = ctx.Resolve(c.PromptName);
                        var choiceText = ctx.Resolve(c.ChoiceText);
                        var varName = ctx.Resolve(c.VariableName);
                        ctx.Game.CustomChoices.Add(new RuntimeCustomChoice { PromptName = promptName, ChoiceText = choiceText, VariableName = varName });
                    }
                    break;

                case ClearCustomChoiceCommandData c:
                    {
                        var promptName = ctx.Resolve(c.PromptName);
                        ctx.Game.CustomChoices.RemoveAll(ch => string.Equals(ch.PromptName, promptName, System.StringComparison.OrdinalIgnoreCase));
                    }
                    break;

                case RemoveCustomChoiceCommandData c:
                    {
                        var promptName = ctx.Resolve(c.PromptName);
                        var choiceText = ctx.Resolve(c.ChoiceText);
                        ctx.Game.CustomChoices.RemoveAll(ch => string.Equals(ch.PromptName, promptName, System.StringComparison.OrdinalIgnoreCase) && string.Equals(ch.ChoiceText, choiceText, System.StringComparison.OrdinalIgnoreCase));
                    }
                    break;

                case StartDialogueCommandData c:
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

                case DamageCharacterCommandData c:
                    {
                        var resolvedChar = ctx.Resolve(c.CharacterId);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        if (character == null)
                        {
                            character = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        }
                        if (character is not null)
                        {
                            character.Properties.TryGetValue("Health", out var hpStr);
                            int hp = int.TryParse(hpStr, out var val) ? val : 100;
                            hp += c.Amount;
                            character.Properties["Health"] = hp.ToString();
                            ctx.SetVariable($"char.{character.Id}.Health", hp.ToString());

                            if (hp <= 0)
                            {
                                character.Properties["State"] = "Dead";
                                ctx.SetVariable($"char.{character.Id}.State", "Dead");
                                var gm = RagNextPlayer.Managers.GameManager.Instance;
                                if (gm != null)
                                {
                                    foreach (var action in character.Actions)
                                    {
                                        if (string.Equals(action.Trigger, "OnCharacterKilled", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ActionExecutor.Execute(action, gm.MakeContext(character), gm.GetComponent<RagNextPlayer.Managers.CommandEffectRouter>());
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;

                case SetCharacterStateCommandData c:
                    {
                        var resolvedChar = ctx.Resolve(c.CharacterId);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        if (character == null)
                        {
                            character = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        }
                        if (character is not null)
                        {
                            character.Properties["State"] = c.State;
                            ctx.SetVariable($"char.{character.Id}.State", c.State);
                            if (string.Equals(c.State, "Dead", StringComparison.OrdinalIgnoreCase))
                            {
                                var gm = RagNextPlayer.Managers.GameManager.Instance;
                                if (gm != null)
                                {
                                    foreach (var action in character.Actions)
                                    {
                                        if (string.Equals(action.Trigger, "OnCharacterKilled", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ActionExecutor.Execute(action, gm.MakeContext(character), gm.GetComponent<RagNextPlayer.Managers.CommandEffectRouter>());
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;

                case TriggerTurnTickCommandData:
                    {
                        var gm = RagNextPlayer.Managers.GameManager.Instance;
                        if (gm != null)
                        {
                            // Run global OnTurnTick actions on the Player
                            if (gm.ActiveGame?.Player?.Actions != null)
                            {
                                foreach (var action in gm.ActiveGame.Player.Actions)
                                {
                                    if (string.Equals(action.Trigger, "OnTurnTick", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ActionExecutor.Execute(action, gm.MakeContext(), gm.GetComponent<RagNextPlayer.Managers.CommandEffectRouter>());
                                    }
                                }
                            }
                            // Run OnRoomTick on the current room
                            if (gm.CurrentRoom?.Actions != null)
                            {
                                foreach (var action in gm.CurrentRoom.Actions)
                                {
                                    if (string.Equals(action.Trigger, "OnRoomTick", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ActionExecutor.Execute(action, gm.MakeContext(), gm.GetComponent<RagNextPlayer.Managers.CommandEffectRouter>());
                                    }
                                }
                            }
                        }
                    }
                    break;

                case ObjectDisplayDescriptionCommandData c:
                    {
                        var resolved = ctx.Resolve(c.ObjectId);
                        var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolved, StringComparison.OrdinalIgnoreCase));
                        if (obj != null)
                        {
                            ctx.SetVariable("system.lastDisplayedText", ctx.Resolve(obj.Description));
                        }
                    }
                    break;

                case ObjectMoveToCharacterCommandData c:
                    {
                        var resolvedObj = ctx.Resolve(c.ObjectId);
                        var resolvedChar = ctx.Resolve(c.CharacterId);
                        RemoveObjectFromEverywhere(resolvedObj, ctx);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        if (character == null)
                        {
                            character = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        }
                        if (character != null && !character.Inventory.Exists(i => string.Equals(i.Id, resolvedObj, StringComparison.OrdinalIgnoreCase)))
                        {
                            var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedObj, StringComparison.OrdinalIgnoreCase));
                            if (obj != null)
                                character.Inventory.Add(obj);
                        }
                    }
                    break;

                case ObjectMoveToInventoryCommandData c:
                    {
                        var resolvedObj = ctx.Resolve(c.ObjectId);
                        RemoveObjectFromEverywhere(resolvedObj, ctx);
                        if (!ctx.Player.Inventory.Exists(i => string.Equals(i.Id, resolvedObj, StringComparison.OrdinalIgnoreCase)))
                        {
                            var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedObj, StringComparison.OrdinalIgnoreCase));
                            if (obj != null)
                                ctx.Player.Inventory.Add(obj);
                        }
                    }
                    break;

                case ObjectMoveInsideObjectCommandData c:
                    {
                        var resolvedObj = ctx.Resolve(c.ObjectId);
                        var resolvedContainer = ctx.Resolve(c.ContainerObjectId);
                        RemoveObjectFromEverywhere(resolvedObj, ctx);
                        var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedObj, StringComparison.OrdinalIgnoreCase));
                        if (obj != null)
                        {
                            obj.Properties["ParentContainerId"] = resolvedContainer;
                        }
                    }
                    break;

                case SetCharacterAttributeCommandData c:
                    {
                        var resolvedChar = ctx.Resolve(c.CharacterId);
                        var resolvedVal = ctx.Resolve(c.Value);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        if (character == null)
                        {
                            character = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        }
                        if (character is not null)
                        {
                            character.Attributes[c.AttributeName] = resolvedVal;
                            ctx.SetVariable($"char.{resolvedChar}.{c.AttributeName}", resolvedVal);
                        }
                    }
                    break;

                case SetPlayerAttributeCommandData c:
                    {
                        var resolvedVal = ctx.Resolve(c.Value);
                        ctx.Player.Attributes[c.AttributeName] = resolvedVal;
                        ctx.SetVariable($"player.{c.AttributeName}", resolvedVal);
                    }
                    break;

                case SetTimerAttributeCommandData c:
                    {
                        var resolvedTimer = ctx.Resolve(c.TimerId);
                        var resolvedVal = ctx.Resolve(c.Value);
                        var timer = ctx.Game.Timers.Find(t => string.Equals(t.Name, resolvedTimer, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Id, resolvedTimer, StringComparison.OrdinalIgnoreCase));
                        if (timer is not null)
                        {
                            timer.Attributes[c.AttributeName] = resolvedVal;
                            ctx.SetVariable($"timer.{resolvedTimer}.{c.AttributeName}", resolvedVal);
                        }
                    }
                    break;

                case SetItemAttributeCommandData c:
                    {
                        var resolvedItem = ctx.Resolve(c.ItemId);
                        var resolvedVal = ctx.Resolve(c.Value);
                        var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedItem, StringComparison.OrdinalIgnoreCase));
                        if (obj is not null)
                        {
                            obj.Attributes[c.AttributeName] = resolvedVal;
                            ctx.SetVariable($"obj.{resolvedItem}.{c.AttributeName}", resolvedVal);
                        }
                    }
                    break;

                default:
                    Debug.LogWarning($"[ActionExecutor] Unhandled command type: {cmd.Type}");
                    break;
            }
        }

        // ── Condition Dispatch ────────────────────────────────────────────────

        internal static bool EvaluateCondition(ConditionData cond, GameExecutionContext ctx)
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

                IsRoomExitLockedConditionData c =>
                    (ctx.Game.Rooms.Find(r => r.Id == (string.IsNullOrWhiteSpace(c.RoomId) ? ctx.CurrentRoom?.Id : ctx.Resolve(c.RoomId)))
                        ?.LockedExits.TryGetValue(ctx.Resolve(c.Direction), out var isLocked) ?? false) && isLocked,

                _ => false
            };
        }

        private static void RemoveObjectFromEverywhere(string oId, GameExecutionContext ctx)
        {
            foreach (var r in ctx.Game.Rooms)
            {
                r.ObjectIds.Remove(oId);
            }
            foreach (var ch in ctx.Game.Characters)
            {
                ch.Inventory.RemoveAll(i => string.Equals(i.Id, oId, StringComparison.OrdinalIgnoreCase));
            }
            ctx.Player.Inventory.RemoveAll(i => string.Equals(i.Id, oId, StringComparison.OrdinalIgnoreCase));
            
            foreach (var o in ctx.Game.Objects)
            {
                if (o.ContainedObjectIds != null)
                {
                    o.ContainedObjectIds.RemoveAll(id => string.Equals(id, oId, StringComparison.OrdinalIgnoreCase));
                }
            }

            var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, oId, StringComparison.OrdinalIgnoreCase));
            if (obj != null)
            {
                obj.Properties.Remove("ParentContainerId");
            }
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
