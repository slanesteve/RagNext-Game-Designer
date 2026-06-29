using System;
using System.Collections.Generic;

namespace RagNextPlayer.Runtime.Models
{
    [Serializable]
    public class SaveStateData
    {
        public string Version { get; set; } = "1.0.0";
        public string PlayerCurrentRoomId { get; set; } = string.Empty;
        public PlayerStateSaveData PlayerState { get; set; } = new PlayerStateSaveData();
        public List<VariableSaveData> Variables { get; set; } = new List<VariableSaveData>();
        public List<ItemStateSaveData> ItemStates { get; set; } = new List<ItemStateSaveData>();
        public List<RoomStateSaveData> RoomStates { get; set; } = new List<RoomStateSaveData>();
        public List<CharacterStateSaveData> CharacterStates { get; set; } = new List<CharacterStateSaveData>();
        public List<ActionActiveStateSaveData> ActionActiveStates { get; set; } = new List<ActionActiveStateSaveData>();
        public List<TimerStateSaveData> TimerStates { get; set; } = new List<TimerStateSaveData>();
    }

    [Serializable]
    public class PlayerStateSaveData
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Gender { get; set; } = string.Empty;
        public string PortraitImagePath { get; set; } = string.Empty;
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    [Serializable]
    public class RoomStateSaveData
    {
        public string Id { get; set; } = string.Empty;
        public Dictionary<string, bool> LockedExits { get; set; } = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    [Serializable]
    public class VariableSaveData
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public List<List<string>> Rows { get; set; } = new List<List<string>>();
    }

    [Serializable]
    public class ItemStateSaveData
    {
        public string Id { get; set; } = string.Empty;
        public bool IsWorn { get; set; }
        public string LocationType { get; set; } = "None"; // Room, PlayerInventory, CharacterInventory, None
        public string LocationId { get; set; } = string.Empty;
        public bool ContainerOpen { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    [Serializable]
    public class CharacterStateSaveData
    {
        public string Id { get; set; } = string.Empty;
        public string PortraitImagePath { get; set; } = string.Empty;
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }

    [Serializable]
    public class ActionActiveStateSaveData
    {
        public string OwnerType { get; set; } = string.Empty; // Game, Item, Character, Room
        public string OwnerId { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    [Serializable]
    public class TimerStateSaveData
    {
        public string Id { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public float ElapsedSeconds { get; set; }
    }
}
