using System.Collections.ObjectModel;

namespace RagsCore.Actions
{
    // Non-breaking path: keep existing GameAction for flat actions,
    // and use ActionTree when you need nesting.
    public sealed class ActionTree
    {
        public string Name { get; set; } = string.Empty;

        // Always-executed commands at the root (your "Default" branch)
        public ObservableCollection<GameCommand> DefaultCommands { get; set; } = new();

        // Additional top-level conditional branches
        public ObservableCollection<ActionBranch> Branches { get; set; } = new();
    }
}