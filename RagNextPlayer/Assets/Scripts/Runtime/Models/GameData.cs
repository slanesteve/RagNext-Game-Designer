using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace RagNextPlayer.Runtime.Models
{
    // ── Top-level game package ────────────────────────────────────────────────
    public class GameData
    {
        public string Title       { get; set; } = string.Empty;
        public string Author      { get; set; } = string.Empty;
        public string Version     { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public PlayerData                 Player     { get; set; } = new();
        public List<RoomData>             Rooms      { get; set; } = new();
        public List<GameObjectData>       Objects    { get; set; } = new();
        public List<GameObjectData>       Characters { get; set; } = new();
        public List<GameVariableData>     Variables  { get; set; } = new();
        public List<MediaAssetData>       MediaAssets{ get; set; } = new();
    }

    // ── Player ────────────────────────────────────────────────────────────────
    public class PlayerData
    {
        public string Id                { get; set; } = string.Empty;
        public string Name              { get; set; } = "Player";
        public string Description       { get; set; } = string.Empty;
        public string Gender            { get; set; } = "Male";
        public string? PortraitImagePath{ get; set; }
        public RoomData? StartingRoom   { get; set; }
        public List<GameObjectData>  Inventory  { get; set; } = new();
        public List<ActionData>      Actions    { get; set; } = new();
    }

    // ── Room ──────────────────────────────────────────────────────────────────
    public class RoomData
    {
        public string Id                   { get; set; } = string.Empty;
        public string Name                 { get; set; } = string.Empty;
        public string Description          { get; set; } = string.Empty;
        public string? PortraitImagePath   { get; set; }
        public Dictionary<string, string>  Exits     { get; set; } = new();
        public List<string>                ObjectIds { get; set; } = new();
        public List<ActionData>            Actions   { get; set; } = new();
    }

    // ── Game Object / Character ───────────────────────────────────────────────
    public class GameObjectData
    {
        public string Id                   { get; set; } = string.Empty;
        public string Name                 { get; set; } = string.Empty;
        public string Description          { get; set; } = string.Empty;
        public string? PortraitImagePath   { get; set; }
        public bool   IsCollectible        { get; set; }
        public bool   IsCharacter          { get; set; }
        public List<ActionData>            Actions    { get; set; } = new();
        public List<GameObjectData>        Inventory  { get; set; } = new();
        public Dictionary<string, string>  Properties { get; set; } = new();
    }

    // ── Actions ───────────────────────────────────────────────────────────────
    public class ActionData
    {
        public string Id             { get; set; } = string.Empty;
        public string Name           { get; set; } = string.Empty;
        public bool   InitallyActive { get; set; } = true;
        public List<ActionStepData>  Nodes { get; set; } = new();
    }

    // ── Action Steps (Commands + Conditions) ──────────────────────────────────
    // Polymorphic — deserialized by ActionStepConverter using the "$type" field.
    [JsonConverter(typeof(ActionStepConverter))]
    public abstract class ActionStepData
    {
        [JsonPropertyName("$type")]
        public string Type        { get; set; } = string.Empty;
        public string? Label      { get; set; }
    }

    // ── Condition base (holds TrueBranch / FalseBranch) ───────────────────────
    public abstract class ConditionData : ActionStepData
    {
        public List<ActionStepData> TrueBranch  { get; set; } = new();
        public List<ActionStepData> FalseBranch { get; set; } = new();
    }

    // ── Command base ──────────────────────────────────────────────────────────
    public abstract class CommandData : ActionStepData { }

    // ── Concrete Conditions ───────────────────────────────────────────────────
    public class VariableEqualsConditionData : ConditionData
    {
        public string  Name            { get; set; } = string.Empty;
        public string? Value           { get; set; }
        public bool    CaseInsensitive { get; set; }
    }
    public class VariableComparisonConditionData : ConditionData
    {
        public string  Name       { get; set; } = string.Empty;
        public string  Comparison { get; set; } = "=";
        public string? Value      { get; set; }
    }
    public class VariableComparisonToVariableConditionData : ConditionData
    {
        public string NameA       { get; set; } = string.Empty;
        public string Comparison  { get; set; } = "=";
        public string NameB       { get; set; } = string.Empty;
    }
    public class PlayerInRoomConditionData        : ConditionData { public string RoomId { get; set; } = string.Empty; }
    public class RoomHasObjectConditionData       : ConditionData { public string RoomId { get; set; } = string.Empty; public string ObjectId { get; set; } = string.Empty; }
    public class ItemInRoomConditionData          : ConditionData { public string ItemId { get; set; } = string.Empty; public string RoomId   { get; set; } = string.Empty; }
    public class ItemHeldByPlayerConditionData    : ConditionData { public string ItemId { get; set; } = string.Empty; }
    public class ItemNotHeldByPlayerConditionData : ConditionData { public string ItemId { get; set; } = string.Empty; }
    public class ItemHeldByCharacterConditionData : ConditionData { public string ItemId { get; set; } = string.Empty; public string CharacterId { get; set; } = string.Empty; }
    public class ItemInObjectConditionData        : ConditionData { public string ItemId { get; set; } = string.Empty; public string ContainerObjectId { get; set; } = string.Empty; }
    public class ItemNotInObjectConditionData     : ConditionData { public string ItemId { get; set; } = string.Empty; public string ObjectId { get; set; } = string.Empty; }
    public class PlayerInSameRoomAsConditionData  : ConditionData { public string CharacterId { get; set; } = string.Empty; }
    public class CharacterInRoomConditionData     : ConditionData { public string CharacterId { get; set; } = string.Empty; public string RoomId { get; set; } = string.Empty; }
    public class CharacterGenderConditionData     : ConditionData { public string CharacterId { get; set; } = string.Empty; public string Gender { get; set; } = "Male"; }
    public class PlayerGenderConditionData        : ConditionData { public string Gender { get; set; } = "Male"; }

    // ── Concrete Commands ─────────────────────────────────────────────────────
    public class DisplayTextCommandData         : CommandData { public string Text { get; set; } = string.Empty; }
    public class AddCommentCommandData          : CommandData { public string CommentText { get; set; } = string.Empty; }
    public class SetVariableCommandData         : CommandData { public string Name { get; set; } = string.Empty; public string? Value { get; set; } }
    public class VariableIncrementCommandData   : CommandData { public string Name { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }
    public class VariableDecrementCommandData   : CommandData { public string Name { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }
    public class VariableSetToVariableCommandData : CommandData { public string Name { get; set; } = string.Empty; public string SourceName { get; set; } = string.Empty; }
    public class SetNumericRandomlyCommandData  : CommandData { public string Name { get; set; } = string.Empty; public double Minimum { get; set; } public double Maximum { get; set; } }
    public class MovePlayerToRoomCommandData    : CommandData { public string RoomId { get; set; } = string.Empty; }
    public class AddObjectToRoomCommandData     : CommandData { public string RoomId { get; set; } = string.Empty; public string ObjectId { get; set; } = string.Empty; }
    public class RemoveObjectFromRoomCommandData: CommandData { public string RoomId { get; set; } = string.Empty; public string ObjectId { get; set; } = string.Empty; }
    public class SetRoomExitCommandData         : CommandData { public string RoomId { get; set; } = string.Empty; public string Direction { get; set; } = string.Empty; public string DestinationRoomId { get; set; } = string.Empty; }
    public class DisableRoomExitCommandData     : CommandData { public string RoomId { get; set; } = string.Empty; public string Direction { get; set; } = string.Empty; }
    public class PlayerSetNameCommandData       : CommandData { public string Name { get; set; } = string.Empty; }
    public class PlayerSetDescriptionCommandData: CommandData { public string Description { get; set; } = string.Empty; }
    public class PlayerSetGenderCommandData     : CommandData { public string Gender { get; set; } = "Male"; }
    public class PlayerSetPortraitMediaCommandData : CommandData { public string MediaId { get; set; } = string.Empty; }
    public class CharacterMoveToRoomCommandData : CommandData { public string CharacterId { get; set; } = string.Empty; public string RoomId { get; set; } = string.Empty; }
    public class CharacterDisplayPortraitCommandData : CommandData { public string CharacterId { get; set; } = string.Empty; public string PortraitId { get; set; } = string.Empty; }
    public class CharacterSetPortraitMediaCommandData : CommandData { public string CharacterId { get; set; } = string.Empty; public string MediaId { get; set; } = string.Empty; }
    public class PlaySoundEffectCommandData     : CommandData { public string SoundId { get; set; } = string.Empty; public double Volume { get; set; } = 100.0; }
    public class DisplayMultimediaCommandData   : CommandData { public string MediaId { get; set; } = string.Empty; }

    // ── Supporting ────────────────────────────────────────────────────────────
    public class GameVariableData
    {
        public string  Name  { get; set; } = string.Empty;
        public string? Value { get; set; }
    }

    public class MediaAssetData
    {
        public string Id           { get; set; } = string.Empty;
        public string Name         { get; set; } = string.Empty;
        public string RelativePath { get; set; } = string.Empty;
        public string MediaType    { get; set; } = string.Empty;
    }
}
