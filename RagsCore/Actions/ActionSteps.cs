using System;
using System.Text.Json.Serialization;
using RagsCore.Models;

namespace RagsCore.Actions
{
    public enum ActionStepKind { Condition, Command }
    // Polymorphic base for both conditions and commands.
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    // Conditions
    [JsonDerivedType(typeof(VariableEqualsCondition), "var.equals")]
    [JsonDerivedType(typeof(PlayerInRoomCondition), "player.inRoom")]
    [JsonDerivedType(typeof(RoomHasObjectCondition), "room.hasObject")]
    // Commands
    [JsonDerivedType(typeof(SetVariableCommand), "var.set")]
    [JsonDerivedType(typeof(MovePlayerToRoomCommand), "player.moveTo")]
    [JsonDerivedType(typeof(AddObjectToRoomCommand), "room.addObject")]
    [JsonDerivedType(typeof(RemoveObjectFromRoomCommand), "room.removeObject")]
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
}