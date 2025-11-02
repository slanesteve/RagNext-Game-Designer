using System;

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

        // Optional simple dialogue or flavor text
        private string? _dialogue;
        public string? Dialogue { get => _dialogue; set => SetProperty(ref _dialogue, value); }
    }
}