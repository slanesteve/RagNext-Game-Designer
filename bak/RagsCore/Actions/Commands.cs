using System;
using System.Linq;
using System.Text.Json.Serialization;
using RagsCore.Models;

namespace RagsCore.Actions
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(SetVariableCommand), "var.set")]
    [JsonDerivedType(typeof(MovePlayerToRoomCommand), "player.moveTo")]
    [JsonDerivedType(typeof(AddObjectToRoomCommand), "room.addObject")]
    [JsonDerivedType(typeof(RemoveObjectFromRoomCommand), "room.removeObject")]
    public abstract class GameCommand
    {
        // Optional user-defined label for UI
        public string? Label { get; set; }

        // Friendly type name for UI
        public string TypeName => this switch
        {
            SetVariableCommand => "Set variable",
            MovePlayerToRoomCommand => "Move player to room",
            AddObjectToRoomCommand => "Add object to room",
            RemoveObjectFromRoomCommand => "Remove object from room",
            _ => GetType().Name
        };

        public abstract void Execute(ActionContext ctx);
    }

    public sealed class SetVariableCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public string? Value { get; set; }

        public override void Execute(ActionContext ctx) => ctx.SetVariable(Name, Value);
    }

    public sealed class MovePlayerToRoomCommand : GameCommand
    {
        public Guid RoomId { get; set; }

        public override void Execute(ActionContext ctx)
        {
            // Minimal example: place a variable; update to real player state when available.
            ctx.SetVariable("player.currentRoomId", RoomId.ToString());
        }
    }

    public sealed class AddObjectToRoomCommand : GameCommand
    {
        public Guid RoomId { get; set; }
        public Guid ObjectId { get; set; }

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

        public override void Execute(ActionContext ctx)
        {
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == RoomId);
            if (room is null) return;
            room.ObjectIds.Remove(ObjectId);
        }
    }
}