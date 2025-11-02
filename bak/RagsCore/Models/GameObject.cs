using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RagsCore.Actions;

namespace RagsCore.Models
{
    /// <summary>
    /// Base object that can be placed in rooms or carried by players.
    /// </summary>
    public class GameObject : BaseModel
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private string _description = string.Empty;
        public string Description { get => _description; set => SetProperty(ref _description, value); }

        private bool _isCollectible = true;
        public bool IsCollectible { get => _isCollectible; set => SetProperty(ref _isCollectible, value); }

        // Arbitrary properties for flexible behavior (serialized as string-to-string)
        public Dictionary<string, string> Properties { get; } = new();

        // Legacy flat actions (kept for backward-compatibility)
        public ObservableCollection<GameAction> Actions { get; set; } = new();

        // New nested action trees
        public ObservableCollection<ActionTree> ActionTrees { get; set; } = new();
    }
}