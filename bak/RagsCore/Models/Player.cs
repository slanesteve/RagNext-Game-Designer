using System;
using System.Collections.ObjectModel;
using RagsCore.Actions;

namespace RagsCore.Models
{
    /// <summary>
    /// Player model. Holds inventory and a reference to current room by Id to avoid circular object graphs.
    /// </summary>
    public class Player : BaseModel
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        private string _name = "Player";
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private int _health = 100;
        public int Health { get => _health; set => SetProperty(ref _health, value); }

        // Current room tracked by Id; resolves in the Game.Rooms collection.
        private Guid? _currentRoomId;
        public Guid? CurrentRoomId { get => _currentRoomId; set => SetProperty(ref _currentRoomId, value); }

        public ObservableCollection<GameObject> Inventory { get; } = new();

        // Actions attached to the player
        public ObservableCollection<GameAction> Actions { get; set; } = new();
    }
}