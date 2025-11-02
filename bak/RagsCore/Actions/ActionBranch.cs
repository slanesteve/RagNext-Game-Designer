using System.Collections.ObjectModel;

namespace RagsCore.Actions
{
    // A branch (action) = an optional condition with commands and nested branches.
    public sealed class ActionBranch
    {
        // Optional friendly name for UI
        public string? Name { get; set; }

        // null => no condition (runs by default)
        public Condition? Condition { get; set; }

        public ObservableCollection<GameCommand> Commands { get; set; } = new();

        // Child actions to evaluate after this action's Commands run
        public ObservableCollection<ActionBranch> Children { get; set; } = new();

        // UI helper
        public string DisplayName => Name ?? (Condition?.TypeName ?? "Action");
    }
}