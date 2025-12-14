using Microsoft.Maui.Controls;
using RagsCore.Actions;
using System;
using System.Linq;

namespace RagNext.Behaviors
{
    public sealed class SyncPickerSelectionBehavior : Behavior<Picker>
    {
        protected override void OnAttachedTo(Picker bindable)
        {
            base.OnAttachedTo(bindable);
            bindable.BindingContextChanged += OnBindingContextChanged;
            bindable.HandlerChanged += OnHandlerChanged;
        }

        protected override void OnDetachingFrom(Picker bindable)
        {
            bindable.BindingContextChanged -= OnBindingContextChanged;
            bindable.HandlerChanged -= OnHandlerChanged;
            base.OnDetachingFrom(bindable);
        }

        private void OnHandlerChanged(object? sender, EventArgs e)
        {
            if (sender is Picker picker)
                TrySyncSelection(picker);
        }

        private void OnBindingContextChanged(object? sender, EventArgs e)
        {
            if (sender is Picker picker)
                TrySyncSelection(picker);
        }

        private static void TrySyncSelection(Picker picker)
        {
            if (picker.BindingContext is not InputDefinition input)
                return;

            picker.ItemsSource = input.PickerSource?.ToList();
            picker.ItemDisplayBinding = new Binding("Name");

            // First, if Value already holds a reference present in ItemsSource, use it.
            if (picker.ItemsSource is System.Collections.IList items && input.Value is not null)
            {
                if (items.Contains(input.Value))
                {
                    picker.SelectedItem = input.Value;
                }
                else
                {
                    // Fallback to name-based matching when instances differ.
                    var savedName = TryGetName(input.Value);
                    if (!string.IsNullOrWhiteSpace(savedName))
                    {
                        var selected = items.Cast<object?>().FirstOrDefault(o => string.Equals(TryGetName(o), savedName, StringComparison.Ordinal));
                        if (selected != null)
                            picker.SelectedItem = selected;
                    }
                }
            }

            picker.SelectedIndexChanged -= SelectedIndexChanged;
            picker.SelectedIndexChanged += SelectedIndexChanged;

            void SelectedIndexChanged(object? s, EventArgs _)
            {
                // Avoid pushing null into Value; only update when a selection exists.
                if (picker.SelectedItem is not null)
                    input.Value = picker.SelectedItem;
            }
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
