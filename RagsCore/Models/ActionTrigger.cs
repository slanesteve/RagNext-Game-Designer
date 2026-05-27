using System;

namespace RagsCore.Models
{
    public enum ActionTrigger
    {
        UserClicked = 0,
        OnGameStart = 1,
        OnGameLoad = 2,
        OnTurnTick = 3,
        OnPlayerEnter = 4,
        OnPlayerExit = 5,
        OnCharacterEnter = 6,
        OnCharacterExit = 7,
        OnRoomTick = 8,
        OnInteract = 9,
        OnCharacterTick = 10,
        OnCharacterKilled = 11,
        OnObjectExamined = 12,
        OnObjectTaken = 13,
        OnObjectDropped = 14
    }
}
