using Microsoft.Maui.Controls;
using System;
using System.Linq;
using RagsCore.Actions;

namespace RagNext.Views.Controls
{
    public partial class EditStepView : ContentView
    {

        // Call SaveAsync whenever a user changes an input.
        private RagNext.ViewModels.EditStepViewModel? Vm => BindingContext as RagNext.ViewModels.EditStepViewModel;
        
        private void SaveNowAsync()
        {
            var vmToSave = Vm;
            if (vmToSave == null) return;

            Dispatcher.Dispatch(async () =>
            {
                await vmToSave.SaveAsync();
            });
        }

        private void OnEntryUnfocused(object? sender, FocusEventArgs e) => SaveNowAsync();
        private void OnEditorUnfocused(object? sender, FocusEventArgs e) => SaveNowAsync();
        private void OnCheckChanged(object? sender, CheckedChangedEventArgs e) => SaveNowAsync();

        private void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (sender is BindableObject bindable && bindable.BindingContext is InputDefinition input)
            {
                // Only handle text, textarea, or ComboBox in manual mode to avoid overwriting Guids or other types with strings
                if (input.ControlType != InputControlType.Text && 
                    input.ControlType != InputControlType.TextArea &&
                    !(input.ControlType == InputControlType.ComboBox && input.IsManualMode))
                {
                    return;
                }

                var currentStr = input.Value?.ToString() ?? string.Empty;
                var newStr = e.NewTextValue ?? string.Empty;
                if (currentStr != newStr)
                {
                    input.Value = e.NewTextValue;
                    SaveNowAsync();
                }
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
                SaveNowAsync();
            }
        }

        private Page? FindParentPage()
        {
            Element? parent = this;
            while (parent != null)
            {
                if (parent is Page page)
                    return page;
                parent = parent.Parent;
            }
            return null;
        }

        private async void OnAskAIClicked(object? sender, EventArgs e)
        {
            if (sender is not Button btn) return;
            var parentPage = FindParentPage();
            if (parentPage == null) return;

            var ai = MauiProgram.Services.GetService(typeof(RagNext.Services.IAIChatService)) as RagNext.Services.IAIChatService;
            if (ai == null) return;

            await RagNext.Services.AIAssistHelper.HandleAskAIAsync(parentPage, btn, btn.CommandParameter, ai);
        }

        public EditStepView()
        {
            InitializeComponent();
        }

        private void OnInputPickerLoaded(object? sender, EventArgs e)
        {
            if (sender is not Picker picker || picker.BindingContext is not InputDefinition input)
                return;

            // Ensure display binding if not set in XAML
            if (picker.ItemDisplayBinding is null)
                picker.ItemDisplayBinding = new Binding("Name");

            // Setup our guarded selection changed event listener cleanly
            picker.SelectedIndexChanged -= OnInputPickerSelectedIndexChanged;
            picker.SelectedIndexChanged += OnInputPickerSelectedIndexChanged;
        }

        private void OnInputPickerSelectedIndexChanged(object? sender, EventArgs e)
        {
            if (sender is not Picker picker || picker.BindingContext is not InputDefinition input)
                return;

            if (picker.SelectedItem is not null)
            {
                // Only save if picker's selected item differs from current input value (reference-level or name-level)
                var isSame = Equals(input.Value, picker.SelectedItem) ||
                             string.Equals(TryGetName(input.Value), TryGetName(picker.SelectedItem), StringComparison.Ordinal);

                if (!isSame)
                {
                    input.Value = picker.SelectedItem;
                    SaveNowAsync();
                }
            }
        }

        private static string? TryGetName(object? value)
        {
            if (value is null) return null;
            if (value is string s) return s;
            if (value is RagNext.ViewModels.NamedOption no) return no.Name;
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