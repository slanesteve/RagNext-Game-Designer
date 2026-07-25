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
        public string IdString => Id.ToString();

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }
        private bool _initiallyactive = true;
        public bool InitallyActive { get => _initiallyactive; set => SetProperty(ref _initiallyactive, value); }

        public override string ToString()
        {
            return Name;
        }

        private ActionTrigger _trigger = ActionTrigger.UserClicked;
        public ActionTrigger Trigger 
        { 
            get => _trigger; 
            set 
            { 
                if (SetProperty(ref _trigger, value))
                {
                    OnPropertyChanged(nameof(IsDirectionFilterVisible));
                }
            } 
        }

        private string _directionFilter = "All";
        public string DirectionFilter
        {
            get => _directionFilter;
            set
            {
                if (string.IsNullOrEmpty(value)) return;
                var norm = NormalizeDirection(value);
                if (_directionFilter != norm)
                {
                    System.Diagnostics.Debug.WriteLine($"[Action '{Name}' ({Id})] DirectionFilter changed from '{_directionFilter}' to '{norm}' (input='{value}')");
                    SetProperty(ref _directionFilter, norm);
                }
            }
        }

        public static string NormalizeDirection(string? dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return "All";
            dir = dir.Trim();
            if (string.Equals(dir, "North", StringComparison.OrdinalIgnoreCase) || string.Equals(dir, "N", StringComparison.OrdinalIgnoreCase)) return "N";
            if (string.Equals(dir, "South", StringComparison.OrdinalIgnoreCase) || string.Equals(dir, "S", StringComparison.OrdinalIgnoreCase)) return "S";
            if (string.Equals(dir, "East", StringComparison.OrdinalIgnoreCase) || string.Equals(dir, "E", StringComparison.OrdinalIgnoreCase)) return "E";
            if (string.Equals(dir, "West", StringComparison.OrdinalIgnoreCase) || string.Equals(dir, "W", StringComparison.OrdinalIgnoreCase)) return "W";
            if (string.Equals(dir, "NorthWest", StringComparison.OrdinalIgnoreCase) || string.Equals(dir, "NW", StringComparison.OrdinalIgnoreCase)) return "NW";
            if (string.Equals(dir, "NorthEast", StringComparison.OrdinalIgnoreCase) || string.Equals(dir, "NE", StringComparison.OrdinalIgnoreCase)) return "NE";
            if (string.Equals(dir, "SouthWest", StringComparison.OrdinalIgnoreCase) || string.Equals(dir, "SW", StringComparison.OrdinalIgnoreCase)) return "SW";
            if (string.Equals(dir, "SouthEast", StringComparison.OrdinalIgnoreCase) || string.Equals(dir, "SE", StringComparison.OrdinalIgnoreCase)) return "SE";
            if (string.Equals(dir, "Up", StringComparison.OrdinalIgnoreCase)) return "Up";
            if (string.Equals(dir, "Down", StringComparison.OrdinalIgnoreCase)) return "Down";
            if (string.Equals(dir, "In", StringComparison.OrdinalIgnoreCase)) return "In";
            if (string.Equals(dir, "Out", StringComparison.OrdinalIgnoreCase)) return "Out";
            return dir;
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsDirectionFilterVisible => 
            Trigger == ActionTrigger.OnPlayerEnter || 
            Trigger == ActionTrigger.OnPlayerExit || 
            Trigger == ActionTrigger.OnCharacterEnter || 
            Trigger == ActionTrigger.OnCharacterExit;

        private string _type = "string";
        /// <summary>Friendly type name, e.g. "int", "bool", "string".</summary>
        public string Type { get => _type; set => SetProperty(ref _type, value); }

        private bool _applyToRooms;
        public bool ApplyToRooms { get => _applyToRooms; set => SetProperty(ref _applyToRooms, value); }

        private bool _applyToPlayer;
        public bool ApplyToPlayer { get => _applyToPlayer; set => SetProperty(ref _applyToPlayer, value); }

        private bool _applyToCharacters;
        public bool ApplyToCharacters { get => _applyToCharacters; set => SetProperty(ref _applyToCharacters, value); }

        private bool _applyToGrabableObjects;
        public bool ApplyToGrabableObjects { get => _applyToGrabableObjects; set => SetProperty(ref _applyToGrabableObjects, value); }

        private bool _applyToWearableObjects;
        public bool ApplyToWearableObjects { get => _applyToWearableObjects; set => SetProperty(ref _applyToWearableObjects, value); }

        private bool _applyToStaticObjects;
        public bool ApplyToStaticObjects { get => _applyToStaticObjects; set => SetProperty(ref _applyToStaticObjects, value); }

        private bool _applyToContainerObjects;
        public bool ApplyToContainerObjects { get => _applyToContainerObjects; set => SetProperty(ref _applyToContainerObjects, value); }

        private bool _applyToTimers;
        public bool ApplyToTimers { get => _applyToTimers; set => SetProperty(ref _applyToTimers, value); }

        private bool _applyToFunctions;
        public bool ApplyToFunctions { get => _applyToFunctions; set => SetProperty(ref _applyToFunctions, value); }

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