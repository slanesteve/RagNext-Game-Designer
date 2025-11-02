using System.Collections.Generic;

namespace RagsCore.Actions
{
    // Matches JSON root: { "commands": [ ... ] }
    public sealed class CommandCatalog
    {
        public List<CommandDefinition> Commands { get; set; } = new();
    }
}