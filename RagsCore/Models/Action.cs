using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using RagsCore.Actions;

namespace RagsCore.Models
{
    // Made public so UI layer can bind.
    public class Action : BaseModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }
        private bool _initiallyactive = true;
        public bool InitallyActive { get => _initiallyactive; set => SetProperty(ref _initiallyactive, value); }

        private ActionTrigger _trigger = ActionTrigger.UserClicked;
        public ActionTrigger Trigger { get => _trigger; set => SetProperty(ref _trigger, value); }

        private string _type = "string";
        /// <summary>Friendly type name, e.g. "int", "bool", "string".</summary>
        public string Type { get => _type; set => SetProperty(ref _type, value); }

        // Steps can be any ActionStep (commands, or conditions that hold other ActionSteps).
        private ObservableCollection<ActionStep> _nodes = new();
        public ObservableCollection<ActionStep> Nodes { get => _nodes; set => SetProperty(ref _nodes, value); }

        // Backward compatibility fallback for older games storing actions in 'Steps'
        public ObservableCollection<ActionStep>? Steps
        {
            get => Nodes;
            set
            {
                if (value != null)
                {
                    Nodes = value;
                }
            }
        }
    }
}