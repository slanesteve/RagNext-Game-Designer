using RagsCore.Models;
using System.Collections.ObjectModel;
using System.Linq;
namespace RagsCore.Actions
{
    public class GameAction
    {
        public string Name { get; set; } = string.Empty;
        // Unified ordered list: conditions and commands interleaved.
        public ObservableCollection<ActionStep> Steps { get; } = new();

        // Convenience views (optional).
        public IEnumerable<Condition> Conditions => Steps.OfType<Condition>();
        public IEnumerable<GameCommand> Commands => Steps.OfType<GameCommand>();
    }
}