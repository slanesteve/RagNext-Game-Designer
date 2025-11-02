namespace RagsCore.Actions
{
    // Marker for items that can live in an action's step list.
    public class ActionNode
    {
        // Short, user-facing label for UI lists.
        string Title { get; set; }
        public List<StepDefinitionBase> Steps { get; set; } = new();

    }
}