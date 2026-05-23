using Microsoft.Maui.Controls;
using RagsCore.Actions;
using System;
using System.Linq;

namespace RagNext.Behaviors
{
    public sealed class SyncPickerSelectionBehavior : Behavior<Picker>
    {
        private Picker? _associatedPicker;
        private InputDefinition? _currentInput;
        private int _syncVersion = 0;
        private bool _isSyncing = false;

        protected override void OnAttachedTo(Picker bindable)
        {
            base.OnAttachedTo(bindable);
            _associatedPicker = bindable;
            bindable.BindingContextChanged += OnBindingContextChanged;
            bindable.HandlerChanged += OnHandlerChanged;
            Sync();
        }

        protected override void OnDetachingFrom(Picker bindable)
        {
            bindable.BindingContextChanged -= OnBindingContextChanged;
            bindable.HandlerChanged -= OnHandlerChanged;
            bindable.SelectedIndexChanged -= OnSelectedIndexChanged;
            _associatedPicker = null;
            _currentInput = null;
            base.OnDetachingFrom(bindable);
        }

        private void OnHandlerChanged(object? sender, EventArgs e)
        {
            Sync();
        }

        private void OnBindingContextChanged(object? sender, EventArgs e)
        {
            Sync();
        }

        private void Subscribe()
        {
            if (_associatedPicker != null)
            {
                _associatedPicker.SelectedIndexChanged -= OnSelectedIndexChanged;
                _associatedPicker.SelectedIndexChanged += OnSelectedIndexChanged;
            }
        }

        private void Unsubscribe()
        {
            if (_associatedPicker != null)
            {
                _associatedPicker.SelectedIndexChanged -= OnSelectedIndexChanged;
            }
        }

        private void Sync()
        {
            if (_associatedPicker == null) return;
            if (_isSyncing) return;

            _isSyncing = true;
            Unsubscribe();

            try
            {
                if (_associatedPicker.BindingContext is not InputDefinition input)
                {
                    _currentInput = null;
                    return;
                }

                _currentInput = input;
                var currentVersion = ++_syncVersion;

                try
                {
                    // Swap order: set DisplayBinding FIRST, then ItemsSource to avoid WinUI text blanking bug
                    _associatedPicker.ItemDisplayBinding = new Binding("Name");
                    _associatedPicker.ItemsSource = input.PickerSource?.ToList();

                    ApplySelection(input);
                }
                finally
                {
                    var picker = _associatedPicker;
                    picker.Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(150), () =>
                    {
                        if (currentVersion == _syncVersion && picker == _associatedPicker)
                        {
                            _isSyncing = true;
                            try
                            {
                                ApplySelection(input);
                            }
                            finally
                            {
                                _isSyncing = false;
                                Subscribe();
                            }
                        }
                    });
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void ApplySelection(InputDefinition input)
        {
            if (_associatedPicker == null) return;

            if (_associatedPicker.ItemsSource is System.Collections.IList items && input.Value is not null)
            {
                int targetIndex = -1;

                // 1. Try exact reference match
                targetIndex = items.IndexOf(input.Value);

                // 2. Try matching by Name
                if (targetIndex < 0)
                {
                    var savedName = TryGetName(input.Value);
                    if (!string.IsNullOrWhiteSpace(savedName))
                    {
                        for (int i = 0; i < items.Count; i++)
                        {
                            if (string.Equals(TryGetName(items[i]), savedName, StringComparison.Ordinal))
                            {
                                targetIndex = i;
                                break;
                            }
                        }
                    }
                }

                // 3. Set SelectedIndex directly for robust selection rendering
                if (targetIndex >= 0 && targetIndex < items.Count)
                {
                    _associatedPicker.SelectedIndex = targetIndex;
                }
                else
                {
                    _associatedPicker.SelectedIndex = -1;
                }
            }
            else
            {
                _associatedPicker.SelectedIndex = -1;
            }
        }

        private void OnSelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_associatedPicker == null || _currentInput == null || _isSyncing)
                return;

            if (_associatedPicker.SelectedIndex >= 0 && _associatedPicker.ItemsSource is System.Collections.IList items)
            {
                if (_associatedPicker.SelectedIndex < items.Count)
                {
                    _currentInput.Value = items[_associatedPicker.SelectedIndex];
                }
            }
            else
            {
                _currentInput.Value = null;
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
