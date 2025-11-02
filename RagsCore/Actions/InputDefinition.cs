using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace RagsCore.Actions
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum InputControlType
    {
        Text,
        TextArea,
        ComboBox,
        Radio,
        Checkbox,
        Number
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum InputDataType
    {
        String,
        Bool,
        Number,
        Variable,
        GameObject,
        Room,
        Media,
        Character,
        Item,
        RoomGroup,
        Group,
        Timer,
        Operator,
        None
    }

    public class InputDefinition
    {
        public string Label { get; set; }            // e.g., "Target Room"
        public InputControlType ControlType { get; set; }  // TextBox, ComboBox, Toggle
        public InputDataType DataType { get; set; }         // String, Bool, Room, GameObject, Variable
        public object? Value { get; set; }                 // Holds the user’s selection
    }
}
