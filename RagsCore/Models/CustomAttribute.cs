using System;
using System.Collections.ObjectModel;
using System.Xml.Linq;

namespace RagsCore.Models
{
    // Bindable, serializable name/value pair
    public class CustomAttribute : BaseModel
    {
        public static string? GetAttribute(string name, ObservableCollection<CustomAttribute> Attributes) =>
           Attributes.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;

        public static void SetAttribute(string name, string? value, ObservableCollection<CustomAttribute> Attributes)
        {
            var attr = Attributes.FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
            if (attr is null) Attributes.Add(new CustomAttribute { Name = name, Value = value });
            else attr.Value = value;
        }
        private string _name = string.Empty;
        public string Name { get => _name; set => SetProperty(ref _name, value); }

        private string? _value;
        public string? Value { get => _value; set => SetProperty(ref _value, value); }
    }
}