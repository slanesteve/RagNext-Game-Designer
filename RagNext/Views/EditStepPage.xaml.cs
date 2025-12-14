using Microsoft.Maui.Controls;
using RagsCore.Actions;
using RagNext.ViewModels;
using System;
using System.Linq;

namespace RagNext.Views
{
    public partial class EditStepPage : ContentPage
    {
        public EditStepPage(StepDefinitionBase step)
        {
            InitializeComponent();
            BindingContext = new EditStepViewModel(step, CloseAsync);
        }

        private async Task CloseAsync() => await Navigation.PopModalAsync();

        private static void ConfigurePickerForInput(Picker picker, InputDefinition input)
        {
            picker.ItemsSource = input.PickerSource?.ToList();

            // Display each item's Name
            picker.ItemDisplayBinding = new Binding("Name");

            // Try to select the previously saved item
            var savedName = TryGetName(input.Value);
            if (!string.IsNullOrWhiteSpace(savedName) && picker.ItemsSource is IList<object> items)
            {
                var selected = items.FirstOrDefault(o => string.Equals(TryGetName(o), savedName, StringComparison.Ordinal));
                if (selected != null)
                    picker.SelectedItem = selected;
            }

            // Keep InputDefinition.Value in sync when the user changes selection
            picker.SelectedIndexChanged += (s, e) =>
            {
                input.Value = picker.SelectedItem;
            };
        }

        private static string? TryGetName(object? value)
        {
            if (value is null) return null;
            if (value is string s) return s;

            // JsonElement support
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

            // Dictionary support
            if (value is System.Collections.IDictionary dict)
            {
                if (dict.Contains("name")) return dict["name"]?.ToString();
                if (dict.Contains("Name")) return dict["Name"]?.ToString();
            }

            // Reflection fallback
            var prop = value.GetType().GetProperty("Name", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
            if (prop?.GetValue(value) is string pn && !string.IsNullOrWhiteSpace(pn)) return pn;

            return value.ToString();
        }

        private void OnInputPickerLoaded(object? sender, EventArgs e)
        {
            if (sender is Picker picker && picker.BindingContext is InputDefinition input)
            {
                ConfigurePickerForInput(picker, input);
            }
        }
    }
}