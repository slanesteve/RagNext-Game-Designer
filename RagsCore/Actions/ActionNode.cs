using System.Collections.Generic;

namespace RagsCore.Actions
{
    // Public so UI can read it.
    public class ActionNode
    {
        // User-facing label.
        public string Title { get; set; } = string.Empty;
        public List<StepDefinitionBase> Steps { get; set; } = new();
    }
}