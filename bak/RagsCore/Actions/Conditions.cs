using System;
using System.Linq;
using System.Text.Json.Serialization;
using RagsCore.Models;

namespace RagsCore.Actions
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(VariableEqualsCondition), "var.equals")]
    [JsonDerivedType(typeof(PlayerInRoomCondition), "player.inRoom")]
    [JsonDerivedType(typeof(RoomHasObjectCondition), "room.hasObject")]
    public abstract class Condition
    {
        // Friendly display name for UI
        public string TypeName => this switch
        {
            VariableEqualsCondition => "Variable equals",
            PlayerInRoomCondition => "Player in room",
            RoomHasObjectCondition => "Room has object",
            _ => GetType().Name
        };

        public abstract bool Evaluate(ActionContext ctx);
    }

    public sealed class VariableEqualsCondition : Condition
    {
        public string Name { get; set; } = string.Empty;
        public string? Value { get; set; }
        public bool CaseInsensitive { get; set; }

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

        public override bool Evaluate(ActionContext ctx)
        {
            // If you later add Player.CurrentRoomId, update this accordingly.
            return ctx.CurrentRoom?.Id == RoomId;
        }
    }

    public sealed class RoomHasObjectCondition : Condition
    {
        public Guid RoomId { get; set; }
        public Guid ObjectId { get; set; }

        public override bool Evaluate(ActionContext ctx)
        {
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == RoomId);
            return room is not null && room.ObjectIds.Contains(ObjectId);
        }
    }
}