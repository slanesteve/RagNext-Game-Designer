using System.Collections.ObjectModel;

namespace RagsCore.Actions
{
    // A single action: when all conditions pass, execute each command in order.
    public class GameAction
    {
        public string Name { get; set; } = string.Empty;
        public ObservableCollection<Condition> Conditions { get; set; } = new();
        public ObservableCollection<GameCommand> Commands { get; set; } = new();
    }
}