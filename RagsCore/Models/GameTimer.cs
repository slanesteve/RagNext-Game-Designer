using System;

namespace RagsCore.Models
{
    public class GameTimer : Action
    {
        // Inherits Id, Name, and Nodes from Action.
        // This makes it 100% compatible with ActionTreeView in the designer!

        private double _intervalSeconds = 60.0;
        public double IntervalSeconds { get => _intervalSeconds; set => SetProperty(ref _intervalSeconds, value); }

        private bool _isActive = true;
        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }

        private bool _isRepeating = true;
        public bool IsRepeating { get => _isRepeating; set => SetProperty(ref _isRepeating, value); }
    }
}
