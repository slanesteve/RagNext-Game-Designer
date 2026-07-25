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
            if (!string.IsNullOrWhiteSpace(value))
            {
                // Try evaluating math expressions (e.g. "10 + 1", "{player.attribute.Strength} + 1")
                // If it's a numeric formula, update value to result string. If it's literal non-numeric text, catch and keep original text.
                if (!double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _))
                {
                    try
                    {
                        var tokens = value.Split(new[] { '+', '-', '*', '/', '%', '^' }, StringSplitOptions.RemoveEmptyEntries);
                        if (tokens.Length > 1 && tokens.All(t => double.TryParse(t.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out _)))
                        {
                            double numVal = RagsCore.Services.MathEvaluator.Evaluate(value);
                            value = numVal.ToString(System.Globalization.CultureInfo.InvariantCulture);
                        }
                    }
                    catch
                    {
                        // Fallback to original string if not a valid mathematical formula
                    }
                }
            }

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