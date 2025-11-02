using System.Collections.Generic;

namespace RagsCore.Actions
{
    // Matches JSON root: { "conditions": [ ... ] }
    public sealed class ConditionCatalog
    {
        public List<ConditionDefinition> Conditions { get; set; } = new();

    }
}