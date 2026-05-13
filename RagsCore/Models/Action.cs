using RagsCore.Actions;
using System.Collections.ObjectModel;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RagsCore.Actions;

namespace RagsCore.Models
{
    // Made public so UI layer can bind.
    public class Action : BaseModel
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }
        private bool _initiallyactive = true;
        public bool InitallyActive { get => _initiallyactive; set => SetProperty(ref _initiallyactive, value); }

        private string _type = "string";
        /// <summary>Friendly type name, e.g. "int", "bool", "string".</summary>
        public string Type { get => _type; set => SetProperty(ref _type, value); }

        // Steps can be any ActionStep (commands, or conditions that hold other ActionSteps).
        public ObservableCollection<ActionStep> Nodes { get; set; } = new();
    }
}