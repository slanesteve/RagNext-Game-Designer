namespace RagsCore.Actions
{
    public enum StepKind { Command, Condition }

    // Common base for palette/catalog entries (not runtime nodes)
    public abstract class StepDefinitionBase
    {
        protected StepDefinitionBase(StepKind kind) => Kind = kind;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public List<InputDefinition> Inputs { get; set; } = new();
        public StepKind Kind { get; }
    }
}