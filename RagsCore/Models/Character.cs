using System;
using System.Collections.ObjectModel;

namespace RagsCore.Models
{
    /// <summary>
    /// Character in the world. Inherits from GameObject so it can be placed or carried similarly.
    /// </summary>
    public class Character : GameObject
    {
        private bool _isHostile;
        public bool IsHostile { get => _isHostile; set => SetProperty(ref _isHostile, value); }

        private int _health = 100;
        public int Health { get => _health; set => SetProperty(ref _health, value); }

        private Room? _startingRoom = null;
        public Room? StartingRoom { get => _startingRoom; set => SetProperty(ref _startingRoom, value); }

        public ObservableCollection<GameObject> Inventory { get; set; } = new();

    }
}