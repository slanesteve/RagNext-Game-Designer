using System;
using System.Text.Json.Serialization;
using RagsCore.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace RagsCore.Actions
{
    public enum ActionStepKind { Condition, Command }
    // Polymorphic base for both conditions and commands.
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    // Conditions
    [JsonDerivedType(typeof(VariableEqualsCondition), "var.equals")]
    [JsonDerivedType(typeof(PlayerInRoomCondition), "player.inRoom")]
    [JsonDerivedType(typeof(RoomHasObjectCondition), "room.hasObject")]
    [JsonDerivedType(typeof(PlayerInSameRoomAsCondition), "player.sameRoom")]
    [JsonDerivedType(typeof(ItemHeldByPlayerCondition), "item.heldByPlayer")]
    [JsonDerivedType(typeof(VariableComparisonCondition), "var.compare")]
    [JsonDerivedType(typeof(CharacterGenderCondition), "char.gender")]
    [JsonDerivedType(typeof(CharacterInRoomCondition), "char.inRoom")]
    [JsonDerivedType(typeof(ItemInRoomCondition), "item.inRoom")]
    [JsonDerivedType(typeof(PlayerGenderCondition), "player.gender")]
    [JsonDerivedType(typeof(ItemHeldByCharacterCondition), "item.heldByChar")]
    [JsonDerivedType(typeof(ItemInObjectCondition), "item.inObject")]
    [JsonDerivedType(typeof(ItemNotHeldByPlayerCondition), "item.notHeldByPlayer")]
    [JsonDerivedType(typeof(ItemNotInObjectCondition), "item.notInObject")]
    [JsonDerivedType(typeof(VariableComparisonToVariableCondition), "var.compareVar")]
    [JsonDerivedType(typeof(IsRoomExitLockedCondition), "room.isExitLocked")]
    [JsonDerivedType(typeof(CharacterAttributeCheckCondition), "char.attributeCheck")]
    [JsonDerivedType(typeof(CharacterAttributeCheckCondition), "char.customPropertyCheck")]
    [JsonDerivedType(typeof(ItemAttributeCheckCondition), "item.attributeCheck")]
    [JsonDerivedType(typeof(ItemAttributeCheckCondition), "item.customPropertyCheck")]
    [JsonDerivedType(typeof(PlayerAttributeCheckCondition), "player.attributeCheck")]
    [JsonDerivedType(typeof(PlayerAttributeCheckCondition), "player.customPropertyCheck")]
    [JsonDerivedType(typeof(RoomAttributeCheckCondition), "room.attributeCheck")]
    [JsonDerivedType(typeof(RoomAttributeCheckCondition), "room.customPropertyCheck")]
    [JsonDerivedType(typeof(TimerActiveCondition), "timer.isActive")]
    // Commands
    [JsonDerivedType(typeof(SetVariableCommand), "var.set")]
    [JsonDerivedType(typeof(MovePlayerToRoomCommand), "player.moveTo")]
    [JsonDerivedType(typeof(AddObjectToRoomCommand), "room.addObject")]
    [JsonDerivedType(typeof(RemoveObjectFromRoomCommand), "room.removeObject")]
    [JsonDerivedType(typeof(ObjectDisplayDescriptionCommand), "object.displayDescription")]
    [JsonDerivedType(typeof(ObjectMoveToCharacterCommand), "object.moveToCharacter")]
    [JsonDerivedType(typeof(ObjectMoveToInventoryCommand), "object.moveToInventory")]
    [JsonDerivedType(typeof(ObjectMoveInsideObjectCommand), "object.moveInsideObject")]
    [JsonDerivedType(typeof(DisplayTextCommand), "general.displayText")]
    [JsonDerivedType(typeof(AddCommentCommand), "general.addComment")]
    [JsonDerivedType(typeof(PlaySoundEffectCommand), "media.playSound")]
    [JsonDerivedType(typeof(StopSoundEffectCommand), "media.stopSound")]
    [JsonDerivedType(typeof(PlayerSetNameCommand), "player.setName")]
    [JsonDerivedType(typeof(PlayerSetDescriptionCommand), "player.setDescription")]
    [JsonDerivedType(typeof(PlayerSetGenderCommand), "player.setGender")]
    [JsonDerivedType(typeof(CharacterSetGenderCommand), "char.setGender")]
    [JsonDerivedType(typeof(SetNumericRandomlyCommand), "var.setRandom")]
    [JsonDerivedType(typeof(CharacterMoveToRoomCommand), "char.moveToRoom")]
    [JsonDerivedType(typeof(DisplayMultimediaCommand), "media.displayMultimedia")]
    [JsonDerivedType(typeof(CharacterDisplayPortraitCommand), "char.displayPortrait")]
    [JsonDerivedType(typeof(CharacterSetPortraitMediaCommand), "char.setPortraitMedia")]
    [JsonDerivedType(typeof(PlayerSetPortraitMediaCommand), "player.setPortraitMedia")]
    [JsonDerivedType(typeof(VariableIncrementCommand), "var.inc")]
    [JsonDerivedType(typeof(VariableDecrementCommand), "var.dec")]
    [JsonDerivedType(typeof(VariableSetToVariableCommand), "var.setToVar")]
    [JsonDerivedType(typeof(SetRoomExitCommand), "room.setExit")]
    [JsonDerivedType(typeof(DisableRoomExitCommand), "room.disableExit")]
    [JsonDerivedType(typeof(LockRoomExitCommand), "room.lockExit")]
    [JsonDerivedType(typeof(UnlockRoomExitCommand), "room.unlockExit")]
    [JsonDerivedType(typeof(DamageCharacterCommand), "char.damage")]
    [JsonDerivedType(typeof(SetCharacterStateCommand), "char.setState")]
    [JsonDerivedType(typeof(TriggerTurnTickCommand), "general.triggerTurnTick")]
    [JsonDerivedType(typeof(EndGameCommand), "general.endGame")]
    [JsonDerivedType(typeof(PromptPlayerInputCommand), "general.promptInput")]
    [JsonDerivedType(typeof(OpenContainerCommand), "general.openContainer")]
    [JsonDerivedType(typeof(CloseContainerCommand), "general.closeContainer")]
    [JsonDerivedType(typeof(CallFunctionCommand), "general.callFunction")]
    [JsonDerivedType(typeof(StartDialogueCommand), "general.startDialogue")]
    [JsonDerivedType(typeof(AddCustomChoiceCommand), "general.addCustomChoice")]
    [JsonDerivedType(typeof(ClearCustomChoiceCommand), "general.clearCustomChoice")]
    [JsonDerivedType(typeof(RemoveCustomChoiceCommand), "general.removeCustomChoice")]
    [JsonDerivedType(typeof(SetCharacterAttributeCommand), "char.setAttribute")]
    [JsonDerivedType(typeof(SetPlayerAttributeCommand), "player.setAttribute")]
    [JsonDerivedType(typeof(SetTimerAttributeCommand), "timer.setAttribute")]
    [JsonDerivedType(typeof(SetItemAttributeCommand), "item.setAttribute")]
    public abstract class ActionStep
    {
        public abstract ActionStepKind Kind { get; }
        // Optional user label common to both
        public string? Label { get; set; }
        public virtual string TypeName => GetType().Name;
        public double X { get; set; }
        public double Y { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
    }

    public abstract class Condition : ActionStep
    {
        public override ActionStepKind Kind => ActionStepKind.Condition;
        
        // These fields allow Conditions to hold nested Commands or other Conditions
        public ObservableCollection<ActionStep> TrueBranch { get; set; } = new();
        public ObservableCollection<ActionStep> FalseBranch { get; set; } = new();

        public abstract bool Evaluate(ActionContext ctx);
    }

    public abstract class GameCommand : ActionStep
    {
        public override ActionStepKind Kind => ActionStepKind.Command;
        public abstract void Execute(ActionContext ctx);
    }

    public sealed class VariableEqualsCondition : Condition
    {
        public string Name { get; set; } = string.Empty;
        public string? Value { get; set; }
        public bool CaseInsensitive { get; set; }
        public override string TypeName => "Variable: Equals String";
        public override bool Evaluate(ActionContext ctx)
        {
            var v = ctx.GetVariable(Name)?.Value;
            return CaseInsensitive
                ? string.Equals(v, Value, StringComparison.OrdinalIgnoreCase)
                : string.Equals(v, Value, StringComparison.Ordinal);
        }
    }

    public sealed class PlayerInRoomCondition : Condition
    {
        public string RoomId { get; set; } = string.Empty;
        public override string TypeName => "Player in room";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            return Guid.TryParse(resolved, out var g) && ctx.CurrentRoom?.Id == g;
        }
    }

    public sealed class CharacterAttributeCheckCondition : Condition
    {
        public string CharacterId { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string ExpectedValue { get; set; } = string.Empty;
        public override string TypeName => "Character: Attribute Check";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(ExpectedValue, ctx);
            var resolvedAttr = RagsCore.Services.TemplateResolver.Resolve(AttributeName, ctx);
            
            var character = ctx.Game.Characters.FirstOrDefault(c => string.Equals(c.Id.ToString(), resolvedChar, StringComparison.OrdinalIgnoreCase));
            if (character == null)
            {
                var obj = ctx.Game.Objects.FirstOrDefault(o => string.Equals(o.Id.ToString(), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (obj != null)
                {
                    return string.Equals(CustomAttribute.GetAttribute(resolvedAttr, obj.Attributes), resolvedVal, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            }
            return string.Equals(CustomAttribute.GetAttribute(resolvedAttr, character.Attributes), resolvedVal, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ItemAttributeCheckCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string ExpectedValue { get; set; } = string.Empty;
        public override string TypeName => "Item: Attribute Check";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedItem = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(ExpectedValue, ctx);
            var resolvedAttr = RagsCore.Services.TemplateResolver.Resolve(AttributeName, ctx);

            var obj = ctx.Game.Objects.FirstOrDefault(o => string.Equals(o.Id.ToString(), resolvedItem, StringComparison.OrdinalIgnoreCase));
            return obj != null && string.Equals(CustomAttribute.GetAttribute(resolvedAttr, obj.Attributes), resolvedVal, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class PlayerAttributeCheckCondition : Condition
    {
        public string AttributeName { get; set; } = string.Empty;
        public string ExpectedValue { get; set; } = string.Empty;
        public override string TypeName => "Player: Attribute Check";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(ExpectedValue, ctx);
            var resolvedAttr = RagsCore.Services.TemplateResolver.Resolve(AttributeName, ctx);
            return string.Equals(CustomAttribute.GetAttribute(resolvedAttr, ctx.Player.Attributes), resolvedVal, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class RoomAttributeCheckCondition : Condition
    {
        public string RoomId { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string ExpectedValue { get; set; } = string.Empty;
        public override string TypeName => "Room: Attribute Check";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(ExpectedValue, ctx);
            var resolvedAttr = RagsCore.Services.TemplateResolver.Resolve(AttributeName, ctx);

            var room = ctx.Game.Rooms.FirstOrDefault(r => string.Equals(r.Id.ToString(), resolvedRoom, StringComparison.OrdinalIgnoreCase));
            return room != null && string.Equals(CustomAttribute.GetAttribute(resolvedAttr, room.Attributes), resolvedVal, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class TimerActiveCondition : Condition
    {
        public string TimerId { get; set; } = string.Empty;
        public override string TypeName => "Timer: Is Active";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedTimer = RagsCore.Services.TemplateResolver.Resolve(TimerId, ctx);
            var timer = ctx.Game.Timers.FirstOrDefault(t => string.Equals(t.Name, resolvedTimer, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Id.ToString(), resolvedTimer, StringComparison.OrdinalIgnoreCase));
            return timer != null && timer.IsActive;
        }
    }

    public sealed class RoomHasObjectCondition : Condition
    {
        public string RoomId { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Room has object";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (!Guid.TryParse(resolvedRoom, out var rId) || !Guid.TryParse(resolvedObj, out var oId)) return false;
            
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId);
            return room is not null && room.ObjectIds.Contains(oId);
        }
    }

    public sealed class SetVariableCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public string? Value { get; set; }
        public override string TypeName => "Variable: Set";
        public override void Execute(ActionContext ctx) => ctx.SetVariable(Name, Value);
    }

    public sealed class MovePlayerToRoomCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public override string TypeName => "Move player to room";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            if (Guid.TryParse(resolved, out var g))
                ctx.SetVariable("player.currentRoomId", g.ToString());
            else if (!string.IsNullOrEmpty(resolved))
                ctx.SetVariable("player.currentRoomId", resolved);
        }
    }

    public sealed class AddObjectToRoomCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Object: Move to Room";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (!Guid.TryParse(resolvedRoom, out var rId) || !Guid.TryParse(resolvedObj, out var oId)) return;

            RemoveObjectFromEverywhere(ctx, oId);

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId);
            if (room is null) return;
            if (!room.ObjectIds.Contains(oId))
                room.ObjectIds.Add(oId);
        }

        public static void RemoveObjectFromEverywhere(ActionContext ctx, Guid oId)
        {
            foreach (var r in ctx.Game.Rooms)
            {
                r.ObjectIds.Remove(oId);
            }
            foreach (var c in ctx.Game.Characters)
            {
                var item = c.Inventory.FirstOrDefault(i => i.Id == oId);
                if (item != null) c.Inventory.Remove(item);
            }
            var pItem = ctx.Player.Inventory.FirstOrDefault(i => i.Id == oId);
            if (pItem != null) ctx.Player.Inventory.Remove(pItem);

            foreach (var o in ctx.Game.Objects)
            {
                if (o.ContainedObjectIds != null)
                {
                    o.ContainedObjectIds.Remove(oId);
                }
            }

            var targetObj = ctx.Game.Objects.FirstOrDefault(o => o.Id == oId);
            if (targetObj != null)
            {
                targetObj.Properties.Remove("ParentContainerId");
            }
        }
    }

    public sealed class RemoveObjectFromRoomCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Remove object from room";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (!Guid.TryParse(resolvedRoom, out var rId) || !Guid.TryParse(resolvedObj, out var oId)) return;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId);
            room?.ObjectIds.Remove(oId);
        }
    }

    public sealed class DisplayTextCommand : GameCommand
    {
        public string Text { get; set; } = string.Empty;
        public override string TypeName => "Display Text";
        public override void Execute(ActionContext ctx)
        {
            ctx.SetVariable("system.lastDisplayedText", RagsCore.Services.TemplateResolver.Resolve(Text, ctx));
        }
    }

    public sealed class AddCommentCommand : GameCommand
    {
        public string CommentText { get; set; } = string.Empty;
        public override string TypeName => "Add A Comment";
        public override void Execute(ActionContext ctx)
        {
            // Comments are for design-time documentation. No runtime action needed.
        }
    }

    public sealed class PlaySoundEffectCommand : GameCommand
    {
        public string SoundId { get; set; } = string.Empty;
        public double Volume { get; set; } = 100.0;
        public bool Loop { get; set; } = false;
        public override string TypeName => "Media: Play Sound Effect";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(SoundId, ctx);
            ctx.SetVariable("media.lastSoundId", resolved);
            ctx.SetVariable("media.lastSoundVolume", Volume.ToString());
            ctx.SetVariable("media.lastSoundLoop", Loop.ToString().ToLower());
        }
    }

    public sealed class StopSoundEffectCommand : GameCommand
    {
        public string SoundId { get; set; } = string.Empty;
        public bool StopAllLooping { get; set; } = false;
        public override string TypeName => "Media: Stop Sound Effect";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(SoundId, ctx);
            ctx.SetVariable("media.stopSoundId", resolved);
            ctx.SetVariable("media.stopAllLooping", StopAllLooping.ToString().ToLower());
        }
    }

    public sealed class PlayerSetNameCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public override string TypeName => "Player: Set Name";
        public override void Execute(ActionContext ctx)
        {
            ctx.Player.Name = Name;
        }
    }

    public sealed class PlayerSetDescriptionCommand : GameCommand
    {
        public string Description { get; set; } = string.Empty;
        public override string TypeName => "Player: Set Description";
        public override void Execute(ActionContext ctx)
        {
            ctx.Player.Description = Description;
        }
    }

    public sealed class PlayerSetGenderCommand : GameCommand
    {
        public string Gender { get; set; } = "Male";
        public override string TypeName => "Player: Set Gender";
        public override void Execute(ActionContext ctx)
        {
            ctx.Player.Gender = Gender;
        }
    }

    public sealed class CharacterSetGenderCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string Gender { get; set; } = "Male";
        public override string TypeName => "Character: Set Gender";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (Guid.TryParse(resolvedChar, out var cId))
            {
                var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId);
                if (character != null)
                {
                    character.Properties["Gender"] = Gender;
                }
            }
        }
    }

    public sealed class SetNumericRandomlyCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public override string TypeName => "Variable: Set Numeric Randomly";
        private static readonly Random _rnd = new();
        public override void Execute(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(Name)) return;
            var val = _rnd.Next((int)Minimum, (int)Maximum + 1);
            ctx.SetVariable(Name, val.ToString());
        }
    }

    public sealed class VariableIncrementCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Variable: Increment";
        public override void Execute(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(Name)) return;
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            var existing = ctx.GetVariable(Name)?.Value;
            
            if (double.TryParse(existing, out var existingNum) && double.TryParse(resolvedVal, out var addNum))
            {
                ctx.SetVariable(Name, (existingNum + addNum).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                ctx.SetVariable(Name, (existing ?? string.Empty) + resolvedVal);
            }
        }
    }

    public sealed class VariableDecrementCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Variable: Decrement";
        public override void Execute(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(Name)) return;
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            var existing = ctx.GetVariable(Name)?.Value;
            
            if (double.TryParse(existing, out var existingNum) && double.TryParse(resolvedVal, out var subNum))
            {
                ctx.SetVariable(Name, (existingNum - subNum).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    public sealed class VariableSetToVariableCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public override string TypeName => "Variable: Set to Variable";
        public override void Execute(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(SourceName)) return;
            var sourceVal = ctx.GetVariable(SourceName)?.Value;
            ctx.SetVariable(Name, sourceVal);
        }
    }

    public sealed class CharacterMoveToRoomCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public override string TypeName => "Character: Move To Room";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            if (!Guid.TryParse(resolvedChar, out var cId) || !Guid.TryParse(resolvedRoom, out var rId)) return;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId);
            if (character is not null)
            {
                ctx.SetVariable($"char.{cId}.currentRoomId", rId.ToString());
            }
        }
    }

    public sealed class PlayerInSameRoomAsCondition : Condition
    {
        public string CharacterId { get; set; } = string.Empty;
        public override string TypeName => "Player: In Same Room As";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var charRoomVar = ctx.GetVariable($"char.{resolvedChar}.currentRoomId")?.Value;
            var playerRoomId = ctx.CurrentRoom?.Id.ToString() ?? ctx.GetVariable("player.currentRoomId")?.Value;
            return charRoomVar != null && playerRoomId != null && string.Equals(charRoomVar, playerRoomId, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ItemHeldByPlayerCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public override string TypeName => "Item: Held By Player";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            if (!Guid.TryParse(resolved, out var itemId)) return false;
            return ctx.Player.Inventory.Any(item => item.Id == itemId);
        }
    }

    public sealed class VariableComparisonCondition : Condition
    {
        public string Name { get; set; } = string.Empty;
        public string Comparison { get; set; } = "=";
        public string? Value { get; set; }
        public override string TypeName => "Variable: Comparison";
        public override bool Evaluate(ActionContext ctx)
        {
            var varVal = ctx.GetVariable(Name)?.Value ?? string.Empty;
            var compVal = Value ?? string.Empty;

            if (double.TryParse(varVal, out double varNum) && double.TryParse(compVal, out double compNum))
            {
                return Comparison switch
                {
                    "=" => varNum == compNum,
                    "!=" => varNum != compNum,
                    ">" => varNum > compNum,
                    ">=" => varNum >= compNum,
                    "<" => varNum < compNum,
                    "<=" => varNum <= compNum,
                    _ => false
                };
            }

            return Comparison switch
            {
                "=" => string.Equals(varVal, compVal, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(varVal, compVal, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
    }

    public sealed class CharacterGenderCondition : Condition
    {
        public string CharacterId { get; set; } = string.Empty;
        public string Gender { get; set; } = "Male";
        public override string TypeName => "Character: Gender";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (!Guid.TryParse(resolved, out var cId)) return false;
            
            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId);
            if (character is null) return false;
            var charGender = character.Properties.TryGetValue("Gender", out var g) ? g : "Male";
            return string.Equals(charGender, Gender, StringComparison.OrdinalIgnoreCase);
        }
    }

    // NEW COMMANDS IMPLEMENTATION

    public sealed class DisplayMultimediaCommand : GameCommand
    {
        public string MediaId { get; set; } = string.Empty;
        public override string TypeName => "Media: Display Multimedia";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(MediaId, ctx);
            ctx.SetVariable("media.lastDisplayedMediaId", resolved);
        }
    }

    public sealed class CharacterDisplayPortraitCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string PortraitId { get; set; } = string.Empty;
        public override string TypeName => "Character: Display Portrait";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var resolvedPort = RagsCore.Services.TemplateResolver.Resolve(PortraitId, ctx);
            ctx.SetVariable($"char.{resolvedChar}.displayedPortraitId", resolvedPort);
        }
    }

    public sealed class CharacterSetPortraitMediaCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string MediaId { get; set; } = string.Empty;
        public override string TypeName => "Character: Set Portrait Media";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var resolvedMedia = RagsCore.Services.TemplateResolver.Resolve(MediaId, ctx);
            
            if (Guid.TryParse(resolvedChar, out var charId))
            {
                var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == charId);
                if (character is not null)
                {
                    if (Guid.TryParse(resolvedMedia, out var mediaGuid))
                    {
                        var mediaAsset = ctx.Game.MediaAssets.FirstOrDefault(m => m.Id == mediaGuid);
                        if (mediaAsset is not null)
                        {
                            character.PortraitImagePath = mediaAsset.RelativePath;
                        }
                    }
                    else
                    {
                        character.PortraitImagePath = resolvedMedia;
                    }
                }
            }
        }
    }

    public sealed class PlayerSetPortraitMediaCommand : GameCommand
    {
        public string MediaId { get; set; } = string.Empty;
        public override string TypeName => "Player: Set Portrait Media";
        public override void Execute(ActionContext ctx)
        {
            var resolvedMedia = RagsCore.Services.TemplateResolver.Resolve(MediaId, ctx);
            if (Guid.TryParse(resolvedMedia, out var mediaGuid))
            {
                var mediaAsset = ctx.Game.MediaAssets.FirstOrDefault(m => m.Id == mediaGuid);
                if (mediaAsset is not null)
                {
                    ctx.Player.PortraitImagePath = mediaAsset.RelativePath;
                }
            }
            else
            {
                ctx.Player.PortraitImagePath = resolvedMedia;
            }
        }
    }

    // NEW CONDITIONS IMPLEMENTATION

    public sealed class CharacterInRoomCondition : Condition
    {
        public string CharacterId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public override string TypeName => "Character: In Room";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var charRoomVar = ctx.GetVariable($"char.{resolvedChar}.currentRoomId")?.Value;
            return charRoomVar != null && string.Equals(charRoomVar, resolvedRoom, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ItemInRoomCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public override string TypeName => "Item: In Room";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedItem = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            if (!Guid.TryParse(resolvedItem, out var itemId) || !Guid.TryParse(resolvedRoom, out var roomId)) return false;
            
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == roomId);
            return room is not null && room.ObjectIds.Contains(itemId);
        }
    }

    public sealed class PlayerGenderCondition : Condition
    {
        public string Gender { get; set; } = "Male";
        public override string TypeName => "Player: Gender";
        public override bool Evaluate(ActionContext ctx)
        {
            return string.Equals(ctx.Player.Gender, Gender, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ItemHeldByCharacterCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public string CharacterId { get; set; } = string.Empty;
        public override string TypeName => "Item: Held By Character";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedItem = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (!Guid.TryParse(resolvedItem, out var itemId) || !Guid.TryParse(resolvedChar, out var charId)) return false;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == charId);
            return character is not null && character.Inventory.Any(i => i.Id == itemId);
        }
    }

    public sealed class ItemInObjectCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public string ContainerObjectId { get; set; } = string.Empty;
        public override string TypeName => "Item: In Object";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedItem = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var resolvedContainer = RagsCore.Services.TemplateResolver.Resolve(ContainerObjectId, ctx);
            if (!Guid.TryParse(resolvedItem, out var itemId)) return false;

            var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == itemId);
            if (obj == null) return false;
            obj.Properties.TryGetValue("ParentContainerId", out var pId);
            return string.Equals(pId, resolvedContainer, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ItemNotHeldByPlayerCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public override string TypeName => "Item: Not Held By Player";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            if (!Guid.TryParse(resolved, out var itemId)) return true;
            return !ctx.Player.Inventory.Any(item => item.Id == itemId);
        }
    }

    public sealed class ItemNotInObjectCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Item: Not In Object";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedItem = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (!Guid.TryParse(resolvedItem, out var itemId)) return true;

            var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == itemId);
            if (obj == null) return true;
            obj.Properties.TryGetValue("ParentContainerId", out var parentId);
            return !string.Equals(parentId, resolvedObj, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class VariableComparisonToVariableCondition : Condition
    {
        public string NameA { get; set; } = string.Empty;
        public string Comparison { get; set; } = "=";
        public string NameB { get; set; } = string.Empty;
        public override string TypeName => "Variable: Comparison To Variable";
        public override bool Evaluate(ActionContext ctx)
        {
            var valA = ctx.GetVariable(NameA)?.Value ?? string.Empty;
            var valB = ctx.GetVariable(NameB)?.Value ?? string.Empty;

            if (double.TryParse(valA, out double numA) && double.TryParse(valB, out double numB))
            {
                return Comparison switch
                {
                    "=" => numA == numB,
                    "!=" => numA != numB,
                    ">" => numA > numB,
                    ">=" => numA >= numB,
                    "<" => numA < numB,
                    "<=" => numA <= numB,
                    _ => false
                };
            }

            return Comparison switch
            {
                "=" => string.Equals(valA, valB, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(valA, valB, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
    }

    /// <summary>
    /// Sets (or clears) a specific directional exit on a room at runtime.
    /// Setting DestinationRoomId to empty/blank removes the exit.
    /// </summary>
    public sealed class SetRoomExitCommand : GameCommand
    {
        /// <summary>The room whose exit to modify. Leave blank to use the current room.</summary>
        public string RoomId { get; set; } = string.Empty;
        /// <summary>The direction key (e.g. "North", "South", "East", "West", "Up", "Down", "In", "Out").</summary>
        public string Direction { get; set; } = string.Empty;
        /// <summary>The destination room. Leave blank to remove the exit in this direction.</summary>
        public string DestinationRoomId { get; set; } = string.Empty;
        public override string TypeName => "Room: Set Exit";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedDest = RagsCore.Services.TemplateResolver.Resolve(DestinationRoomId, ctx);
            var resolvedDir = RagsCore.Services.TemplateResolver.Resolve(Direction, ctx);

            var rId = Guid.TryParse(resolvedRoom, out var g1) ? g1 : ctx.CurrentRoom?.Id;
            if (rId == null || rId == Guid.Empty || string.IsNullOrWhiteSpace(resolvedDir)) return;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId.Value);
            if (room is null) return;

            if (string.IsNullOrWhiteSpace(resolvedDest))
            {
                room.Exits.Remove(resolvedDir);
            }
            else if (Guid.TryParse(resolvedDest, out var destId) && destId != Guid.Empty)
            {
                room.Exits[resolvedDir] = destId;
            }
        }
    }

    /// <summary>
    /// Removes (disables) a specific directional exit from a room at runtime.
    /// </summary>
    public sealed class DisableRoomExitCommand : GameCommand
    {
        /// <summary>The room whose exit to disable. Leave blank to use the current room.</summary>
        public string RoomId { get; set; } = string.Empty;
        /// <summary>The direction key to remove (e.g. "North").</summary>
        public string Direction { get; set; } = string.Empty;
        public override string TypeName => "Room: Disable Exit";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedDir = RagsCore.Services.TemplateResolver.Resolve(Direction, ctx);

            var rId = Guid.TryParse(resolvedRoom, out var g) ? g : ctx.CurrentRoom?.Id;
            if (rId == null || rId == Guid.Empty || string.IsNullOrWhiteSpace(resolvedDir)) return;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId.Value);
            room?.Exits.Remove(resolvedDir);
        }
    }

    public enum PlayerInputType { Text, Objects, Characters, Custom }

    public sealed class EndGameCommand : GameCommand
    {
        public string FinalMessage { get; set; } = string.Empty;
        public override string TypeName => "General: End Game";
        public override void Execute(ActionContext ctx)
        {
            ctx.SetVariable("system.isGameOver", "true");
            ctx.SetVariable("system.endGameMessage", RagsCore.Services.TemplateResolver.Resolve(FinalMessage, ctx));
        }
    }

    public sealed class PromptPlayerInputCommand : GameCommand
    {
        public string PromptName { get; set; } = string.Empty;
        public string PromptText { get; set; } = string.Empty;
        public PlayerInputType InputType { get; set; } = PlayerInputType.Text;
        public string CustomOptions { get; set; } = string.Empty;
        public string StoreVariableName { get; set; } = string.Empty;
        public override string TypeName => "General: Prompt Player Input";
        public override void Execute(ActionContext ctx)
        {
            ctx.SetVariable("system.prompt.name", RagsCore.Services.TemplateResolver.Resolve(PromptName, ctx));
            ctx.SetVariable("system.prompt.text", RagsCore.Services.TemplateResolver.Resolve(PromptText, ctx));
            ctx.SetVariable("system.prompt.type", InputType.ToString());
            ctx.SetVariable("system.prompt.options", CustomOptions);
            ctx.SetVariable("system.prompt.targetVar", StoreVariableName);
            ctx.SetVariable("system.prompt.active", "true");
        }
    }

    public sealed class OpenContainerCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "General: Open Container";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            ctx.SetVariable($"obj.{resolved}.containerOpen", "true");
        }
    }

    public sealed class CloseContainerCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "General: Close Container";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            ctx.SetVariable($"obj.{resolved}.containerOpen", "false");
        }
    }

    public sealed class CallFunctionCommand : GameCommand
    {
        public string FunctionId { get; set; } = string.Empty;
        public override string TypeName => "General: Call Function";
        public override void Execute(ActionContext ctx)
        {
            var target = ctx.Game?.Functions.FirstOrDefault(f => f.Id.ToString() == FunctionId || string.Equals(f.Name, FunctionId, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                ExecuteSteps(target.Nodes, ctx);
            }
        }

        private void ExecuteSteps(System.Collections.Generic.IEnumerable<ActionStep> steps, ActionContext ctx)
        {
            foreach (var step in steps)
            {
                if (step is GameCommand cmd)
                {
                    cmd.Execute(ctx);
                }
                else if (step is Condition cond)
                {
                    bool result = cond.Evaluate(ctx);
                    ExecuteSteps(result ? cond.TrueBranch : cond.FalseBranch, ctx);
                }
            }
        }
    }

    public sealed class IsRoomExitLockedCondition : Condition
    {
        public string RoomId { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public override string TypeName => "Room: Exit Is Locked";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedDir = RagsCore.Services.TemplateResolver.Resolve(Direction, ctx);

            var rId = Guid.TryParse(resolvedRoom, out var g) ? g : ctx.CurrentRoom?.Id;
            if (rId == null || rId == Guid.Empty || string.IsNullOrWhiteSpace(resolvedDir)) return false;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId.Value);
            return room != null && room.LockedExits.TryGetValue(resolvedDir, out var isLocked) && isLocked;
        }
    }

    public sealed class LockRoomExitCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public override string TypeName => "Room: Lock Exit";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedDir = RagsCore.Services.TemplateResolver.Resolve(Direction, ctx);

            var rId = Guid.TryParse(resolvedRoom, out var g) ? g : ctx.CurrentRoom?.Id;
            if (rId == null || rId == Guid.Empty || string.IsNullOrWhiteSpace(resolvedDir)) return;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId.Value);
            if (room is not null)
            {
                room.LockedExits[resolvedDir] = true;
            }
        }
    }

    public sealed class UnlockRoomExitCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public override string TypeName => "Room: Unlock Exit";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedDir = RagsCore.Services.TemplateResolver.Resolve(Direction, ctx);

            var rId = Guid.TryParse(resolvedRoom, out var g) ? g : ctx.CurrentRoom?.Id;
            if (rId == null || rId == Guid.Empty || string.IsNullOrWhiteSpace(resolvedDir)) return;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId.Value);
            if (room is not null)
            {
                room.LockedExits[resolvedDir] = false;
            }
        }
    }

    public sealed class DamageCharacterCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public int Amount { get; set; } = 0;
        public override string TypeName => "Character: Damage / Heal";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (!Guid.TryParse(resolvedChar, out var charId)) return;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == charId);
            if (character is not null)
            {
                character.Properties.TryGetValue("Health", out var hpStr);
                int hp = int.TryParse(hpStr, out var val) ? val : 100;
                hp += Amount;
                character.Properties["Health"] = hp.ToString();

                if (hp <= 0)
                {
                    character.Properties["State"] = "Dead";
                }
            }
        }
    }

    public sealed class SetCharacterStateCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string State { get; set; } = "Alive";
        public override string TypeName => "Character: Set State";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (!Guid.TryParse(resolvedChar, out var charId)) return;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == charId);
            if (character is not null)
            {
                character.Properties["State"] = State;
            }
        }
    }

    public sealed class SetCharacterAttributeCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Character: Set Attribute";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            if (!Guid.TryParse(resolvedChar, out var charId)) return;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == charId);
            if (character is not null)
            {
                CustomAttribute.SetAttribute(AttributeName, resolvedVal, character.Attributes);
            }
        }
    }

    public sealed class SetPlayerAttributeCommand : GameCommand
    {
        public string AttributeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Player: Set Attribute";
        public override void Execute(ActionContext ctx)
        {
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            CustomAttribute.SetAttribute(AttributeName, resolvedVal, ctx.Player.Attributes);
        }
    }

    public sealed class SetTimerAttributeCommand : GameCommand
    {
        public string TimerId { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Timer: Set Attribute";
        public override void Execute(ActionContext ctx)
        {
            var resolvedTimer = RagsCore.Services.TemplateResolver.Resolve(TimerId, ctx);
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            var timer = ctx.Game.Timers.FirstOrDefault(t => string.Equals(t.Name, resolvedTimer, StringComparison.OrdinalIgnoreCase));
            if (timer is not null)
            {
                CustomAttribute.SetAttribute(AttributeName, resolvedVal, timer.Attributes);
            }
        }
    }

    public sealed class SetItemAttributeCommand : GameCommand
    {
        public string ItemId { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Item: Set Attribute";
        public override void Execute(ActionContext ctx)
        {
            var resolvedItem = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            if (!Guid.TryParse(resolvedItem, out var itemId)) return;

            var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == itemId);
            if (obj is not null)
            {
                CustomAttribute.SetAttribute(AttributeName, resolvedVal, obj.Attributes);
            }
        }
    }

    public sealed class TriggerTurnTickCommand : GameCommand
    {
        public override string TypeName => "Game: Trigger Turn Tick";
        public override void Execute(ActionContext ctx)
        {
            // Handled inside target environment runtime context updates.
        }
    }

    public sealed class DialogueChoice
    {
        public string Text { get; set; } = string.Empty;
        public string DestinationNodeId { get; set; } = string.Empty;
        public ObservableCollection<ActionStep> Commands { get; set; } = new();
    }

    public sealed class StartDialogueCommand : GameCommand
    {
        public string DialogueId { get; set; } = string.Empty;
        public string CharacterLines { get; set; } = string.Empty;
        public ObservableCollection<DialogueChoice> Choices { get; set; } = new();
        public override string TypeName => "Start Dialogue";
        public override void Execute(ActionContext ctx)
        {
            // Handled by custom visual game dialogues system
        }
    }

    public sealed class AddCustomChoiceCommand : GameCommand
    {
        public string PromptName { get; set; } = string.Empty;
        public string ChoiceText { get; set; } = string.Empty;
        public string VariableName { get; set; } = string.Empty;
        public override string TypeName => "Action: Add Custom Choice";
        public override void Execute(ActionContext ctx)
        {
            // Handled inside target environment runtime
        }
    }

    public sealed class ClearCustomChoiceCommand : GameCommand
    {
        public string PromptName { get; set; } = string.Empty;
        public override string TypeName => "Action: Clear Custom Choice";
        public override void Execute(ActionContext ctx)
        {
            // Handled inside target environment runtime
        }
    }

    public sealed class RemoveCustomChoiceCommand : GameCommand
    {
        public string PromptName { get; set; } = string.Empty;
        public string ChoiceText { get; set; } = string.Empty;
        public override string TypeName => "Action: Remove Custom Choice";
        public override void Execute(ActionContext ctx)
        {
            // Handled inside target environment runtime
        }
    }

    public sealed class ObjectDisplayDescriptionCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Object: Display Description";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (Guid.TryParse(resolved, out var oId))
            {
                var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == oId);
                if (obj != null)
                {
                    ctx.SetVariable("system.lastDisplayedText", RagsCore.Services.TemplateResolver.Resolve(obj.Description, ctx));
                }
            }
        }
    }

    public sealed class ObjectMoveToCharacterCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public string CharacterId { get; set; } = string.Empty;
        public override string TypeName => "Object: Move to Character";
        public override void Execute(ActionContext ctx)
        {
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (Guid.TryParse(resolvedObj, out var oId) && Guid.TryParse(resolvedChar, out var cId))
            {
                AddObjectToRoomCommand.RemoveObjectFromEverywhere(ctx, oId);
                var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId);
                if (character != null && !character.Inventory.Any(i => i.Id == oId))
                {
                    var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == oId);
                    if (obj != null)
                        character.Inventory.Add(obj);
                }
            }
        }
    }

    public sealed class ObjectMoveToInventoryCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Object: Move to Inventory";
        public override void Execute(ActionContext ctx)
        {
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (Guid.TryParse(resolvedObj, out var oId))
            {
                AddObjectToRoomCommand.RemoveObjectFromEverywhere(ctx, oId);
                if (!ctx.Player.Inventory.Any(i => i.Id == oId))
                {
                    var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == oId);
                    if (obj != null)
                        ctx.Player.Inventory.Add(obj);
                }
            }
        }
    }

    public sealed class ObjectMoveInsideObjectCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public string ContainerObjectId { get; set; } = string.Empty;
        public override string TypeName => "Object: Move Inside Object";
        public override void Execute(ActionContext ctx)
        {
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            var resolvedContainer = RagsCore.Services.TemplateResolver.Resolve(ContainerObjectId, ctx);
            if (Guid.TryParse(resolvedObj, out var oId) && Guid.TryParse(resolvedContainer, out var containerId))
            {
                AddObjectToRoomCommand.RemoveObjectFromEverywhere(ctx, oId);
                var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == oId);
                if (obj != null)
                {
                    obj.Properties["ParentContainerId"] = containerId.ToString();
                }
            }
        }
    }
}