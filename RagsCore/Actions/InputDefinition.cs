using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace RagsCore.Actions
{
    [JsonConverter(typeof(JsonStringEnumConverter<InputControlType>))]
    public enum InputControlType { Text, TextArea, ComboBox, Radio, Checkbox, Number }

    [JsonConverter(typeof(JsonStringEnumConverter<InputDataType>))]
    public enum InputDataType
    {
        String, Bool, Number, Variable, GameObject, Room, Media, Character,
        Item, RoomGroup, Group, Timer, Operator, Direction, Function, None,
        PromptName
    }

    public class InputDefinition : INotifyPropertyChanged
    {
        private string _label = string.Empty;
        private InputControlType _controlType;
        private InputDataType _dataType;
        private object? _value;
        private IEnumerable<object>? _pickerSource;
        private bool _isManualMode;

        public string Label
        {
            get => _label;
            set { if (_label != value) { _label = value; OnPropertyChanged(); } }
        }

        public InputControlType ControlType
        {
            get => _controlType;
            set { if (_controlType != value) { _controlType = value; OnPropertyChanged(); } }
        }

        public InputDataType DataType
        {
            get => _dataType;
            set { if (_dataType != value) { _dataType = value; OnPropertyChanged(); } }
        }

        public object? Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsManualMode));
                    OnPropertyChanged(nameof(IsNormalMode));
                    OnPropertyChanged(nameof(ToggleButtonText));
                }
            }
        }

        public IEnumerable<object>? PickerSource
        {
            get => _pickerSource;
            set { if (_pickerSource != value) { _pickerSource = value; OnPropertyChanged(); } }
        }

        [JsonIgnore]
        public bool IsManualMode
        {
            get
            {
                if (_isManualMode) return true;
                if (Value is string str && str.StartsWith("{") && str.EndsWith("}")) return true;
                return false;
            }
            set
            {
                if (_isManualMode != value)
                {
                    _isManualMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsNormalMode));
                    OnPropertyChanged(nameof(ToggleButtonText));
                }
            }
        }

        [JsonIgnore]
        public bool IsNormalMode => !IsManualMode;

        [JsonIgnore]
        public string ToggleButtonText => IsManualMode ? "Pick" : "Text";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
