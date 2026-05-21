using System;
using System.Text.Json.Serialization;
using RagsCore.Models;
using System.Collections.ObjectModel; // Added for ObservableCollection

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
    // Commands
    [JsonDerivedType(typeof(SetVariableCommand), "var.set")]
    [JsonDerivedType(typeof(MovePlayerToRoomCommand), "player.moveTo")]
    [JsonDerivedType(typeof(AddObjectToRoomCommand), "room.addObject")]
    [JsonDerivedType(typeof(RemoveObjectFromRoomCommand), "room.removeObject")]
    [JsonDerivedType(typeof(DisplayTextCommand), "general.displayText")]
    [JsonDerivedType(typeof(AddCommentCommand), "general.addComment")]
    [JsonDerivedType(typeof(PlaySoundEffectCommand), "media.playSound")]
    [JsonDerivedType(typeof(PlayerSetNameCommand), "player.setName")]
    [JsonDerivedType(typeof(PlayerSetDescriptionCommand), "player.setDescription")]
    [JsonDerivedType(typeof(PlayerSetGenderCommand), "player.setGender")]
    [JsonDerivedType(typeof(SetNumericRandomlyCommand), "var.setRandom")]
    [JsonDerivedType(typeof(CharacterMoveToRoomCommand), "char.moveToRoom")]
    public abstract class ActionStep
    {
        public abstract ActionStepKind Kind { get; }
        // Optional user label common to both
        public string? Label { get; set; }
        public virtual string TypeName => GetType().Name;
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
        public override string TypeName => "Variable equals";
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
        public Guid RoomId { get; set; }
        public override string TypeName => "Player in room";
        public override bool Evaluate(ActionContext ctx) => ctx.CurrentRoom?.Id == RoomId;
    }

    public sealed class RoomHasObjectCondition : Condition
    {
        public Guid RoomId { get; set; }
        public Guid ObjectId { get; set; }
        public override string TypeName => "Room has object";
        public override bool Evaluate(ActionContext ctx)
        {
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == RoomId);
            return room is not null && room.ObjectIds.Contains(ObjectId);
        }
    }

    public sealed class SetVariableCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public string? Value { get; set; }
        public override string TypeName => "Set variable";
        public override void Execute(ActionContext ctx) => ctx.SetVariable(Name, Value);
    }

    public sealed class MovePlayerToRoomCommand : GameCommand
    {
        public Guid RoomId { get; set; }
        public override string TypeName => "Move player to room";
        public override void Execute(ActionContext ctx)
        {
            ctx.SetVariable("player.currentRoomId", RoomId.ToString());
        }
    }

    public sealed class AddObjectToRoomCommand : GameCommand
    {
        public Guid RoomId { get; set; }
        public Guid ObjectId { get; set; }
        public override string TypeName => "Add object to room";
        public override void Execute(ActionContext ctx)
        {
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == RoomId);
            if (room is null) return;
            if (!room.ObjectIds.Contains(ObjectId))
                room.ObjectIds.Add(ObjectId);
        }
    }

    public sealed class RemoveObjectFromRoomCommand : GameCommand
    {
        public Guid RoomId { get; set; }
        public Guid ObjectId { get; set; }
        public override string TypeName => "Remove object from room";
        public override void Execute(ActionContext ctx)
        {
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == RoomId);
            room?.ObjectIds.Remove(ObjectId);
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
        public Guid SoundId { get; set; }
        public double Volume { get; set; } = 100.0;
        public override string TypeName => "Media: Play Sound Effect";
        public override void Execute(ActionContext ctx)
        {
            ctx.SetVariable("media.lastSoundId", SoundId.ToString());
            ctx.SetVariable("media.lastSoundVolume", Volume.ToString());
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

    public sealed class CharacterMoveToRoomCommand : GameCommand
    {
        public Guid CharacterId { get; set; }
        public Guid RoomId { get; set; }
        public override string TypeName => "Character: Move To Room";
        public override void Execute(ActionContext ctx)
        {
            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == CharacterId);
            if (character is not null)
            {
                ctx.SetVariable($"char.{CharacterId}.currentRoomId", RoomId.ToString());
            }
        }
    }

    public sealed class PlayerInSameRoomAsCondition : Condition
    {
        public Guid CharacterId { get; set; }
        public override string TypeName => "Player: In Same Room As";
        public override bool Evaluate(ActionContext ctx)
        {
            var charRoomVar = ctx.GetVariable($"char.{CharacterId}.currentRoomId")?.Value;
            var playerRoomId = ctx.CurrentRoom?.Id.ToString() ?? ctx.GetVariable("player.currentRoomId")?.Value;
            return charRoomVar != null && playerRoomId != null && string.Equals(charRoomVar, playerRoomId, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ItemHeldByPlayerCondition : Condition
    {
        public Guid ItemId { get; set; }
        public override string TypeName => "Item: Held By Player";
        public override bool Evaluate(ActionContext ctx)
        {
            return ctx.Player.Inventory.Any(item => item.Id == ItemId);
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
        public Guid CharacterId { get; set; }
        public string Gender { get; set; } = "Male";
        public override string TypeName => "Character: Gender";
        public override bool Evaluate(ActionContext ctx)
        {
            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == CharacterId);
            if (character is null) return false;
            var charGender = character.Properties.TryGetValue("Gender", out var g) ? g : "Male";
            return string.Equals(charGender, Gender, StringComparison.OrdinalIgnoreCase);
        }
    }
}