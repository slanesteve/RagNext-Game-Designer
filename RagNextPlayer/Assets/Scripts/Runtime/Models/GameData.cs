#nullable enable
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RagNextPlayer.Runtime.Models
{
    // ── Top-level game package ────────────────────────────────────────────────
    public class GameData
    {
        public string Title       { get; set; } = string.Empty;
        public string Author      { get; set; } = string.Empty;
        public string Version     { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public PlayerData                 Player      { get; set; } = new PlayerData();
        public List<RoomData>             Rooms       { get; set; } = new List<RoomData>();
        public List<GameObjectData>       Objects     { get; set; } = new List<GameObjectData>();
        public List<GameObjectData>       Characters  { get; set; } = new List<GameObjectData>();
        public List<GameVariableData>     Variables   { get; set; } = new List<GameVariableData>();
        public List<MediaAssetData>       MediaAssets { get; set; } = new List<MediaAssetData>();
        public List<GlobalFunctionData>   Functions   { get; set; } = new List<GlobalFunctionData>();
        public List<GameTimerData>        Timers      { get; set; } = new List<GameTimerData>();
        public SplashScreenSettingsData   SplashScreen{ get; set; } = new SplashScreenSettingsData();

        [JsonIgnore]
        public List<RuntimeCustomChoice> CustomChoices { get; } = new List<RuntimeCustomChoice>();
    }

    public class SplashScreenSettingsData
    {
        public bool Enabled { get; set; } = false;
        public string Mode { get; set; } = "ImageAndText"; // "ImageAndText" or "Video"
        public string ImageAssetId { get; set; } = string.Empty;
        public string SoundAssetId { get; set; } = string.Empty;
        public string Text { get; set; } = "My Adventure";
        public string FontName { get; set; } = "Outfit";
        public double FontSize { get; set; } = 32;
        public string FontColor { get; set; } = "#FFFFFF";
        public double TextX { get; set; } = 50;
        public double TextY { get; set; } = 50;
        public double FadeInDuration { get; set; } = 1.5;
        public double DisplayDuration { get; set; } = 2.5;
        public double FadeOutDuration { get; set; } = 1.0;
        public string VideoAssetId { get; set; } = string.Empty;
        public string TransitionStyle { get; set; } = "Fade"; // Fade, Rise, Cinematic, Glitch, Exposure
        public double BorderWidth { get; set; } = 6;
        public string BorderColor { get; set; } = "#2A2A38";
        public double BorderRadius { get; set; } = 12;
    }

    // ── Player ────────────────────────────────────────────────────────────────
    public class PlayerData
    {
        public string  Id               { get; set; } = string.Empty;
        public string  Name             { get; set; } = "Player";
        public string  Description      { get; set; } = string.Empty;
        public string  Gender           { get; set; } = "Male";
        public string? PortraitImagePath{ get; set; }
        // StartingRoomId is exported as a string ID (not a nested Room object)
        // to break the circular reference that caused $id/$ref in the old exporter.
        public string? StartingRoomId   { get; set; }
        public List<GameObjectData>  Inventory  { get; set; } = new List<GameObjectData>();
        public List<ActionData>      Actions    { get; set; } = new List<ActionData>();
        [JsonConverter(typeof(AttributesConverter))]
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    }

    // ── Room ──────────────────────────────────────────────────────────────────
    public class RoomData
    {
        public string  Id                 { get; set; } = string.Empty;
        public string  Name               { get; set; } = string.Empty;
        public string  Description        { get; set; } = string.Empty;
        public string? PortraitImagePath  { get; set; }
        public Dictionary<string, string> Exits     { get; set; } = new Dictionary<string, string>();
        public Dictionary<string, bool>   LockedExits { get; set; } = new Dictionary<string, bool>();
        public List<string>               ObjectIds { get; set; } = new List<string>();
        public List<ActionData>           Actions   { get; set; } = new List<ActionData>();
        [JsonConverter(typeof(AttributesConverter))]
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    }

    // ── Game Object / Character ───────────────────────────────────────────────
    public class GameObjectData
    {
        public string  Id                 { get; set; } = string.Empty;
        public string  Name               { get; set; } = string.Empty;
        public string  Description        { get; set; } = string.Empty;
        public string? PortraitImagePath  { get; set; }
        public bool    IsCollectible      { get; set; }
        public bool    IsCharacter        { get; set; }
        public List<ActionData>            Actions            { get; set; } = new List<ActionData>();
        public List<GameObjectData>        Inventory          { get; set; } = new List<GameObjectData>();
        public Dictionary<string, string>  Properties         { get; set; } = new Dictionary<string, string>();
        public bool                        IsContainer        { get; set; }
        public bool                        ContainerOpen      { get; set; }
        public List<string>                ContainedObjectIds { get; set; } = new List<string>();
        public string?                     StartingRoomId     { get; set; }
        [JsonConverter(typeof(AttributesConverter))]
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    }

    // ── Actions ───────────────────────────────────────────────────────────────
    public class ActionData
    {
        public string Id             { get; set; } = string.Empty;
        public string Name           { get; set; } = string.Empty;
        public bool   InitallyActive { get; set; } = true;
        public string Trigger        { get; set; } = "UserClicked";
        public string DirectionFilter { get; set; } = "All";

        [JsonConverter(typeof(ActionStepListConverter))]
        public List<ActionStepData> Nodes { get; set; } = new List<ActionStepData>();
    }

    // ── Action Steps (Commands + Conditions) ──────────────────────────────────
    // Polymorphic — deserialized by ActionStepConverter using the "$type" field.
    public abstract class ActionStepData
    {
        [JsonProperty("$type")]
        public string Type   { get; set; } = string.Empty;
        public string? Label { get; set; }
    }

    // ── Condition base (holds TrueBranch / FalseBranch) ───────────────────────
    public abstract class ConditionData : ActionStepData
    {
        [JsonConverter(typeof(ActionStepListConverter))]
        public List<ActionStepData> TrueBranch  { get; set; } = new List<ActionStepData>();
        [JsonConverter(typeof(ActionStepListConverter))]
        public List<ActionStepData> FalseBranch { get; set; } = new List<ActionStepData>();
    }

    // ── Command base ──────────────────────────────────────────────────────────
    public abstract class CommandData : ActionStepData { }

    // ── Concrete Conditions ───────────────────────────────────────────────────
    public class VariableEqualsConditionData              : ConditionData { public string Name { get; set; } = string.Empty; public string? Value { get; set; } public bool CaseInsensitive { get; set; } }
    public class VariableComparisonConditionData          : ConditionData { public string Name { get; set; } = string.Empty; public string Comparison { get; set; } = "="; public string? Value { get; set; } }
    public class VariableComparisonToVariableConditionData: ConditionData { public string NameA { get; set; } = string.Empty; public string Comparison { get; set; } = "="; public string NameB { get; set; } = string.Empty; }
    public class PlayerInRoomConditionData                : ConditionData { public string RoomId { get; set; } = string.Empty; }
    public class RoomHasObjectConditionData               : ConditionData { public string RoomId { get; set; } = string.Empty; public string ObjectId { get; set; } = string.Empty; }
    public class ItemInRoomConditionData                  : ConditionData { public string ItemId { get; set; } = string.Empty; public string RoomId { get; set; } = string.Empty; }
    public class ItemHeldByPlayerConditionData            : ConditionData { public string ItemId { get; set; } = string.Empty; }
    public class ItemNotHeldByPlayerConditionData         : ConditionData { public string ItemId { get; set; } = string.Empty; }
    public class ItemHeldByCharacterConditionData         : ConditionData { public string ItemId { get; set; } = string.Empty; public string CharacterId { get; set; } = string.Empty; }
    public class ItemInObjectConditionData                : ConditionData { public string ItemId { get; set; } = string.Empty; public string ContainerObjectId { get; set; } = string.Empty; }
    public class ItemNotInObjectConditionData             : ConditionData { public string ItemId { get; set; } = string.Empty; public string ObjectId { get; set; } = string.Empty; }
    public class PlayerInSameRoomAsConditionData          : ConditionData { public string CharacterId { get; set; } = string.Empty; }
    public class CharacterInRoomConditionData             : ConditionData { public string CharacterId { get; set; } = string.Empty; public string RoomId { get; set; } = string.Empty; }
    public class CharacterGenderConditionData             : ConditionData { public string CharacterId { get; set; } = string.Empty; public string Gender { get; set; } = "Male"; }
    public class PlayerGenderConditionData                : ConditionData { public string Gender { get; set; } = "Male"; }
    public class IsRoomExitLockedConditionData            : ConditionData { public string RoomId { get; set; } = string.Empty; public string Direction { get; set; } = string.Empty; }
    public class CharacterAttributeCheckConditionData    : ConditionData { public string CharacterId { get; set; } = string.Empty; public string AttributeName { get; set; } = string.Empty; public string ExpectedValue { get; set; } = string.Empty; }
    public class ItemAttributeCheckConditionData         : ConditionData { public string ItemId { get; set; } = string.Empty; public string AttributeName { get; set; } = string.Empty; public string ExpectedValue { get; set; } = string.Empty; }
    public class PlayerAttributeCheckConditionData       : ConditionData { public string AttributeName { get; set; } = string.Empty; public string ExpectedValue { get; set; } = string.Empty; }
    public class RoomAttributeCheckConditionData         : ConditionData { public string RoomId { get; set; } = string.Empty; public string AttributeName { get; set; } = string.Empty; public string ExpectedValue { get; set; } = string.Empty; }
    public class TimerActiveConditionData                : ConditionData { public string TimerId { get; set; } = string.Empty; }
    public class DateTimePartComparisonConditionData     : ConditionData { public string VariableName { get; set; } = string.Empty; public string DateTimeComponent { get; set; } = "minute"; public string Comparison { get; set; } = "="; public double ExpectedValue { get; set; } }
    public class DateTimeIsPastConditionData             : ConditionData { public string VariableName { get; set; } = string.Empty; }
    public class DateTimeIsFutureConditionData           : ConditionData { public string VariableName { get; set; } = string.Empty; }
    public class DateTimeCompareVariablesConditionData   : ConditionData { public string VariableNameA { get; set; } = string.Empty; public string Comparison { get; set; } = "="; public string VariableNameB { get; set; } = string.Empty; }
    public class DateTimeCompareDifferenceConditionData  : ConditionData { public string VariableNameA { get; set; } = string.Empty; public string VariableNameB { get; set; } = string.Empty; public string Comparison { get; set; } = "="; public string Duration { get; set; } = string.Empty; }
    public class DateTimeCompareConstantConditionData    : ConditionData { public string VariableName { get; set; } = string.Empty; public string Comparison { get; set; } = "="; public string ConstantValue { get; set; } = string.Empty; }
    public class DateTimeIsValidConditionData            : ConditionData { public string VariableName { get; set; } = string.Empty; }

    // ── Concrete Commands ─────────────────────────────────────────────────────
    public class DisplayTextCommandData              : CommandData { public string Text { get; set; } = string.Empty; }
    public class AddCommentCommandData               : CommandData { public string CommentText { get; set; } = string.Empty; }
    public class SetVariableCommandData              : CommandData { public string Name { get; set; } = string.Empty; public string? Value { get; set; } }
    public class VariableIncrementCommandData        : CommandData { public string Name { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }
    public class VariableDecrementCommandData        : CommandData { public string Name { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }
    public class VariableSetToVariableCommandData    : CommandData { public string Name { get; set; } = string.Empty; public string SourceName { get; set; } = string.Empty; }
    public class SetNumericRandomlyCommandData       : CommandData { public string Name { get; set; } = string.Empty; public double Minimum { get; set; } public double Maximum { get; set; } }
    public class MovePlayerToRoomCommandData         : CommandData { public string RoomId { get; set; } = string.Empty; }
    public class AddObjectToRoomCommandData          : CommandData { public string RoomId { get; set; } = string.Empty; public string ObjectId { get; set; } = string.Empty; }
    public class RemoveObjectFromRoomCommandData     : CommandData { public string RoomId { get; set; } = string.Empty; public string ObjectId { get; set; } = string.Empty; }
    public class SetRoomExitCommandData              : CommandData { public string RoomId { get; set; } = string.Empty; public string Direction { get; set; } = string.Empty; public string DestinationRoomId { get; set; } = string.Empty; }
    public class DisableRoomExitCommandData          : CommandData { public string RoomId { get; set; } = string.Empty; public string Direction { get; set; } = string.Empty; }
    public class LockRoomExitCommandData             : CommandData { public string RoomId { get; set; } = string.Empty; public string Direction { get; set; } = string.Empty; }
    public class UnlockRoomExitCommandData           : CommandData { public string RoomId { get; set; } = string.Empty; public string Direction { get; set; } = string.Empty; }
    public class PlayerSetNameCommandData            : CommandData { public string Name { get; set; } = string.Empty; }
    public class PlayerSetDescriptionCommandData     : CommandData { public string Description { get; set; } = string.Empty; }
    public class PlayerSetGenderCommandData          : CommandData { public string Gender { get; set; } = "Male"; }
    public class PlayerSetPortraitMediaCommandData   : CommandData { public string MediaId { get; set; } = string.Empty; }
    public class CharacterMoveToRoomCommandData      : CommandData { public string CharacterId { get; set; } = string.Empty; public string RoomId { get; set; } = string.Empty; }
    public class CharacterDisplayPortraitCommandData : CommandData { public string CharacterId { get; set; } = string.Empty; public string PortraitId { get; set; } = string.Empty; }
    public class CharacterSetPortraitMediaCommandData: CommandData { public string CharacterId { get; set; } = string.Empty; public string MediaId { get; set; } = string.Empty; }
    public class PlaySoundEffectCommandData          : CommandData { public string SoundId { get; set; } = string.Empty; public double Volume { get; set; } = 100.0; public bool Loop { get; set; } = false; public double StartTime { get; set; } = 0.0; public double EndTime { get; set; } = 0.0; }
    public class PlayVideoCommandData                : CommandData { public string VideoId { get; set; } = string.Empty; public double Volume { get; set; } = 100.0; public bool Loop { get; set; } = false; public double StartTime { get; set; } = 0.0; public double EndTime { get; set; } = 0.0; }
    public class StopSoundEffectCommandData          : CommandData { public string SoundId { get; set; } = string.Empty; public bool StopAllLooping { get; set; } = false; }
    public class DisplayMultimediaCommandData        : CommandData { public string MediaId { get; set; } = string.Empty; }
    public class EndGameCommandData                  : CommandData { public string FinalMessage { get; set; } = string.Empty; }
    public class PromptPlayerInputCommandData        : CommandData { public string PromptName { get; set; } = string.Empty; public string PromptText { get; set; } = string.Empty; public string InputType { get; set; } = "Text"; public string CustomOptions { get; set; } = string.Empty; public string StoreVariableName { get; set; } = string.Empty; }
    public class OpenContainerCommandData            : CommandData { public string ObjectId { get; set; } = string.Empty; }
    public class CloseContainerCommandData           : CommandData { public string ObjectId { get; set; } = string.Empty; }
    public class CallFunctionCommandData            : CommandData { public string FunctionId { get; set; } = string.Empty; }
    public class DamageCharacterCommandData         : CommandData { public string CharacterId { get; set; } = string.Empty; public int Amount { get; set; } }
    public class SetCharacterStateCommandData        : CommandData { public string CharacterId { get; set; } = string.Empty; public string State { get; set; } = "Alive"; }
    public class TriggerTurnTickCommandData         : CommandData { }
    public class DebugTextCommandData               : CommandData { public string Message { get; set; } = string.Empty; }
    public class CharacterSetActionActiveCommandData : CommandData { public string CharacterId { get; set; } = string.Empty; public string ActionName { get; set; } = string.Empty; public bool Active { get; set; } = true; }
    // Bug #5: Scoped entity variants for set-action-active commands.
    public class ItemSetActionActiveCommandData     : CommandData { public string ItemId      { get; set; } = string.Empty; public string ActionName { get; set; } = string.Empty; public bool Active { get; set; } = true; }
    public class RoomSetActionActiveCommandData     : CommandData { public string RoomId      { get; set; } = string.Empty; public string ActionName { get; set; } = string.Empty; public bool Active { get; set; } = true; }
    public class PlayerSetActionActiveCommandData   : CommandData { public string ActionName { get; set; } = string.Empty; public bool Active { get; set; } = true; }
    public class SetTimerActiveCommandData          : CommandData { public string TimerId { get; set; } = string.Empty; public bool Active { get; set; } = true; }

    public class RuntimeCustomChoice
    {
        public string PromptName { get; set; } = string.Empty;
        public string ChoiceText { get; set; } = string.Empty;
        public string VariableName { get; set; } = string.Empty;
    }

    public class AddCustomChoiceCommandData : CommandData
    {
        public string PromptName { get; set; } = string.Empty;
        public string ChoiceText { get; set; } = string.Empty;
        public string VariableName { get; set; } = string.Empty;
    }

    public class ClearCustomChoiceCommandData : CommandData
    {
        public string PromptName { get; set; } = string.Empty;
    }

    public class RemoveCustomChoiceCommandData : CommandData
    {
        public string PromptName { get; set; } = string.Empty;
        public string ChoiceText { get; set; } = string.Empty;
    }
    
    public class ObjectDisplayDescriptionCommandData : CommandData { public string ObjectId { get; set; } = string.Empty; }
    public class PlayerDisplayDescriptionCommandData : CommandData { }
    public class CharacterDisplayDescriptionCommandData : CommandData { public string CharacterId { get; set; } = string.Empty; }
    public class RoomDisplayDescriptionCommandData : CommandData { public string RoomId { get; set; } = string.Empty; }
    public class ObjectMoveToCharacterCommandData    : CommandData { public string ObjectId { get; set; } = string.Empty; public string CharacterId { get; set; } = string.Empty; }
    public class ObjectMoveToInventoryCommandData    : CommandData { public string ObjectId { get; set; } = string.Empty; }
    public class ObjectMoveInsideObjectCommandData   : CommandData { public string ObjectId { get; set; } = string.Empty; public string ContainerObjectId { get; set; } = string.Empty; }
    
    public class SetCharacterAttributeCommandData    : CommandData { public string CharacterId { get; set; } = string.Empty; public string AttributeName { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }
    public class SetPlayerAttributeCommandData       : CommandData { public string AttributeName { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }
    public class SetTimerAttributeCommandData        : CommandData { public string TimerId { get; set; } = string.Empty; public string AttributeName { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }
    public class SetItemAttributeCommandData         : CommandData { public string ItemId { get; set; } = string.Empty; public string AttributeName { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }
    
    // Multi-Dimensional Array (MDA) Command and Condition Data structures
    public class ForEachLoopCommandData : ConditionData { public string ArrayVariableName { get; set; } = string.Empty; }
    public class BreakLoopCommandData : CommandData { }
    public class SetArrayElementCommandData : CommandData { public string ArrayVariableName { get; set; } = string.Empty; public string RowIndex { get; set; } = "0"; public string ColumnName { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }
    public class AddArrayRowCommandData : CommandData { public string ArrayVariableName { get; set; } = string.Empty; public string ValuesCommaSeparated { get; set; } = string.Empty; }
    public class RemoveArrayRowCommandData : CommandData { public string ArrayVariableName { get; set; } = string.Empty; public string RowIndex { get; set; } = "0"; }
    public class AppendTextCommandData : CommandData { public string VariableName { get; set; } = string.Empty; public string Text { get; set; } = string.Empty; }
    public class AppendLineCommandData : CommandData { public string VariableName { get; set; } = string.Empty; public string Text { get; set; } = string.Empty; }
    
    public class SwitchCommandData : ConditionData
    {
        public string Expression { get; set; } = string.Empty;

        [Newtonsoft.Json.JsonConverter(typeof(SwitchCasesConverter))]
        public Dictionary<string, List<ActionStepData>> Cases { get; set; } = new Dictionary<string, List<ActionStepData>>();

        [Newtonsoft.Json.JsonConverter(typeof(ActionStepListConverter))]
        public List<ActionStepData> DefaultBranch { get; set; } = new List<ActionStepData>();
    }
    
    public class DialogueChoiceData
    {
        public string Text { get; set; } = string.Empty;
        public string DestinationNodeId { get; set; } = string.Empty;
        [JsonConverter(typeof(ActionStepListConverter))]
        public List<ActionStepData> Commands { get; set; } = new List<ActionStepData>();
    }

    public class StartDialogueCommandData : CommandData
    {
        public string DialogueId { get; set; } = string.Empty;
        public string CharacterLines { get; set; } = string.Empty;
        public List<DialogueChoiceData> Choices { get; set; } = new List<DialogueChoiceData>();
    }

    public class GlobalFunctionData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;

        [JsonConverter(typeof(ActionStepListConverter))]
        public List<ActionStepData> Nodes { get; set; } = new List<ActionStepData>();
    }

    public class GameTimerData
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public double IntervalSeconds { get; set; } = 60.0;
        public bool IsActive { get; set; } = true;
        public bool IsRepeating { get; set; } = true;

        [JsonConverter(typeof(ActionStepListConverter))]
        public List<ActionStepData> Nodes { get; set; } = new List<ActionStepData>();

        [JsonIgnore]
        public float ElapsedSeconds { get; set; }
        [JsonConverter(typeof(AttributesConverter))]
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
    }

    // ── Supporting ────────────────────────────────────────────────────────────
    public class GameVariableData
    {
        public string  Name  { get; set; } = string.Empty;
        public string? Value { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
        public List<List<string>> Rows { get; set; } = new List<List<string>>();
    }

    public class MediaAssetData
    {
        public string Id               { get; set; } = string.Empty;
        public string Name             { get; set; } = string.Empty;
        public string RelativePath     { get; set; } = string.Empty;
        public string MediaType        { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
    }
}
