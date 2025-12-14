using Microsoft.Maui.Controls;
using System;
using System.Linq;
using RagsCore.Actions;

namespace RagNext.Views.Controls
{
    public partial class EditStepView : ContentView
    {
        public EditStepView()
        {
            InitializeComponent();
        }

        private void OnInputPickerLoaded(object? sender, EventArgs e)
        {
            if (sender is not Picker picker)
                return;

            if (picker.BindingContext is not InputDefinition input)
                return;

            // Ensure display binding if not set in XAML
            if (picker.ItemDisplayBinding is null)
                picker.ItemDisplayBinding = new Binding("Name");

            // If Value is already set to an instance from PickerSource, set SelectedItem directly.
            if (input.Value is not null && picker.ItemsSource is System.Collections.IList list)
            {
                if (list.Contains(input.Value))
                {
                    picker.SelectedItem = input.Value;
                }
                else
                {
                    // Fallback to matching by name-like value.
                    var savedName = TryGetName(input.Value);
                    if (!string.IsNullOrWhiteSpace(savedName))
                    {
                        var selected = list.Cast<object?>().FirstOrDefault(o => string.Equals(TryGetName(o), savedName, StringComparison.Ordinal));
                        if (selected != null)
                            picker.SelectedItem = selected;
                    }
                }
            }
            else if (picker.ItemsSource is System.Collections.IList list2)
            {
                // Defensive fallback: if clone.Value is null, try to restore from the parent view model's target by label.
                if (this.BindingContext is RagNext.ViewModels.EditStepViewModel vm)
                {
                    var index = vm.EditableInputs.IndexOf(input);
                    if (index >= 0 && vm.SelectedDefinition != null)
                    {
                        // Try to find the intended value from the underlying target step.
                        var intended = vm.EditableInputs.ElementAtOrDefault(index)?.Value;
                        var intendedName = TryGetName(intended);
                        if (intended is not null && list2.Contains(intended))
                        {
                            picker.SelectedItem = intended;
                            input.Value = intended;
                        }
                        else if (!string.IsNullOrWhiteSpace(intendedName))
                        {
                            var selected = list2.Cast<object?>().FirstOrDefault(o => string.Equals(TryGetName(o), intendedName, StringComparison.Ordinal));
                            if (selected != null)
                            {
                                picker.SelectedItem = selected;
                                input.Value = selected;
                            }
                        }
                    }
                }
            }

            // Keep Value in sync if the user changes selection.
            picker.SelectedIndexChanged += (s, _) =>
            {
                // Avoid pushing null into Value; only update when selection exists.
                if (picker.SelectedItem is not null)
                    input.Value = picker.SelectedItem;
            };

            // If needed, you can access the view model and its EditableInputs like this:
            // var vm = this.BindingContext as RagNext.ViewModels.EditStepViewModel;
            // var inputs = vm?.EditableInputs; // contains the clones used in the template
        }

        private static string? TryGetName(object? value)
        {
            if (value is null) return null;
            if (value is string s) return s;
            if (value is Enum e) return e.ToString();
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
    }
}