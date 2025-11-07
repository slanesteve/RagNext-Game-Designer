using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace RagsCore.Actions
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum InputControlType { Text, TextArea, ComboBox, Radio, Checkbox, Number }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum InputDataType
    {
        String, Bool, Number, Variable, GameObject, Room, Media, Character,
        Item, RoomGroup, Group, Timer, Operator, None
    }

    public class InputDefinition
    {
        public string Label { get; set; }
        public InputControlType ControlType { get; set; }
        public InputDataType DataType { get; set; }
        public object? Value { get; set; }

        // Added: source for Picker / ComboBox style inputs
        public IEnumerable<object>? PickerSource { get; set; }
    }
}
