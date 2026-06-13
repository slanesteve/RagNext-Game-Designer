using System;
using System.Collections.ObjectModel;

namespace RagsCore.Models
{
    /// <summary>
    /// Generic name/value variable for game state. Value is stored as string to keep serialization simple.
    /// </summary>
    public class GameVariable : BaseModel
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private string? _value;
        public string? Value 
        { 
            get => _value; 
            set 
            {
                var cleanValue = value;
                if (Type == "bool" && value != null)
                {
                    if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
                        cleanValue = "true";
                    else if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                        cleanValue = "false";
                }
                if (SetProperty(ref _value, cleanValue))
                {
                    OnPropertyChanged(nameof(ValidationWarning));
                }
            } 
        }

        private string _type = "string";
        /// <summary>
        /// Friendly type name, e.g. "int", "bool", "string" — used for parsing/rehydration.
        /// </summary>
        public string Type 
        { 
            get => _type; 
            set 
            {
                if (SetProperty(ref _type, value))
                {
                    if (_type == "datetime")
                    {
                        if (string.IsNullOrEmpty(Value) || Value == "0" || !DateTime.TryParse(Value, out _))
                        {
                            Value = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                        }
                    }
                    else if (_type == "bool")
                    {
                        if (Value != "true" && Value != "false")
                        {
                            Value = "false";
                        }
                    }
                    else if (_type == "int" || _type == "number")
                    {
                        if (!double.TryParse(Value, out _))
                        {
                            Value = "0";
                        }
                    }
                    OnPropertyChanged(nameof(ValidationWarning));
                }
            } 
        }

        [System.Text.Json.Serialization.JsonIgnore]
        public string? ValidationWarning
        {
            get
            {
                if (string.IsNullOrEmpty(Value)) return null;
                if (Type == "bool")
                {
                    if (!string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase) && 
                        !string.Equals(Value, "false", StringComparison.OrdinalIgnoreCase))
                    {
                        return "⚠️ Value must be 'true' or 'false' (no quotes).";
                    }
                }
                else if (Type == "int" || Type == "number")
                {
                    if (!double.TryParse(Value, out _))
                    {
                        return "⚠️ Value must be a valid number.";
                    }
                }
                else if (Type == "datetime")
                {
                    if (!DateTime.TryParse(Value, out _))
                    {
                        return "⚠️ Invalid format. Expected: YYYY-MM-DDTHH:MM:SS (e.g. 2026-06-04T12:00:00).";
                    }
                }
                return null;
            }
        }

        public ObservableCollection<string> Columns { get; set; } = new();
        public ObservableCollection<ObservableCollection<string>> Rows { get; set; } = new();

        public ObservableCollection<CustomAttribute> Attributes { get; set; } = new();
        internal ObservableCollection<Action> Actions { get; set; } = new();
    }
}