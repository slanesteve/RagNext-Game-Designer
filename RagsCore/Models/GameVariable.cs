using System;
using System.Collections.ObjectModel;

namespace RagsCore.Models
{
    /// <summary>
    /// Generic name/value variable for game state. Value is stored as string to keep serialization simple.
    /// </summary>
    public class GameVariable : BaseModel
    {
        public Guid Id { get; init; } = Guid.NewGuid();

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private string? _value;
        public string? Value { get => _value; set => SetProperty(ref _value, value); }

        private string _type = "string";
        /// <summary>
        /// Friendly type name, e.g. "int", "bool", "string" — used for parsing/rehydration.
        /// </summary>
        public string Type { get => _type; set => SetProperty(ref _type, value); }
        public ObservableCollection<CustomAttribute> Attributes { get; set; } = new();
        internal ObservableCollection<Action> Actions { get; set; } = new();
    }
}