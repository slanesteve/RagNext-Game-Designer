using Microsoft.Maui.Controls;
using RagsCore.Actions;
using RagNext.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace RagNext.Views
{
    public partial class EditStepPage : ContentPage
    {
        public EditStepPage(ActionStep step)
        {
            InitializeComponent();
            BindingContext = new EditStepViewModel(step, CloseAsync);
        }

        private async Task CloseAsync(ActionStep? updatedStep)
        {
            await Navigation.PopModalAsync();
        }

        private static void ConfigurePickerForInput(Picker picker, InputDefinition input)
        {
            picker.ItemsSource = input.PickerSource?.ToList();
            picker.ItemDisplayBinding = new Binding("Name");

            var savedName = TryGetName(input.Value);
            if (!string.IsNullOrWhiteSpace(savedName) && picker.ItemsSource is IList<object> items)
            {
                var selected = items.FirstOrDefault(o => string.Equals(TryGetName(o), savedName, StringComparison.Ordinal));
                if (selected != null)
                    picker.SelectedItem = selected;
            }

            picker.SelectedIndexChanged += (s, e) =>
            {
                input.Value = picker.SelectedItem;
            };
        }

        private static string? TryGetName(object? value)
        {
            if (value is null) return null;
            if (value is string s) return s;

            if (value is System.Text.Json.JsonElement el)
            {
                if (el.ValueKind == System.Text.Json.JsonValueKind.String) return el.GetString();
                if (el.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    if (el.TryGetProperty("name", out var lower)) return lower.GetString();
                    if (el.TryGetProperty("Name", out var upper)) return upper.GetString();
                }
                return el.ToString();
            }

            if (value is System.Collections.IDictionary dict)
            {
                if (dict.Contains("name")) return dict["name"]?.ToString();
                if (dict.Contains("Name")) return dict["Name"]?.ToString();
            }

            var prop = value.GetType().GetProperty("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (prop?.GetValue(value) is string pn && !string.IsNullOrWhiteSpace(pn)) return pn;

            return value.ToString();
        }

        private void OnInputPickerLoaded(object? sender, EventArgs e)
        {
            if (sender is Picker picker && picker.BindingContext is InputDefinition input && input.PickerSource?.Any() == true)
            {
                ConfigurePickerForInput(picker, input);
            }
        }

        private void OnToggleManualMode(object? sender, EventArgs e)
        {
            if (sender is BindableObject bindable && bindable.BindingContext is InputDefinition input)
            {
                input.IsManualMode = !input.IsManualMode;
                if (!input.IsManualMode)
                {
                    input.Value = null;
                }
            }
        }
    }
}
