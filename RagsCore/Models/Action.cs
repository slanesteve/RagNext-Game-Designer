using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RagsCore.Actions;

namespace RagsCore.Models
{
    internal class Action : BaseModel
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private string _type = "string";
        /// <summary>
        /// Friendly type name, e.g. "int", "bool", "string" — used for parsing/rehydration.
        /// </summary>
        public string Type { get => _type; set => SetProperty(ref _type, value); }

        // Steps can be any IActionNode (commands, conditions, or condition blocks).
        public ObservableCollection<ActionNode> Nodes { get; set; } = new();
    }
}
