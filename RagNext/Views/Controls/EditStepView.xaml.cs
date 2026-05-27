using Microsoft.Maui.Controls;
using System;
using System.Linq;
using RagsCore.Actions;
using RagsCore.Models;
using RagNext.ViewModels;

namespace RagNext.Views.Controls
{
    public partial class EditStepView : ContentView
    {

        private int _bindingContextVersion = 0;
        private bool _isBindingContextChanging = false;

        protected override void OnBindingContextChanged()
        {
            var currentVersion = ++_bindingContextVersion;
            _isBindingContextChanging = true;
            try
            {
                base.OnBindingContextChanged();
            }
            finally
            {
                Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(100), () =>
                {
                    if (currentVersion == _bindingContextVersion)
                    {
                        _isBindingContextChanging = false;
                    }
                });
            }
        }

        // Call SaveAsync whenever a user changes an input.
        private RagNext.ViewModels.EditStepViewModel? Vm => BindingContext as RagNext.ViewModels.EditStepViewModel;

        private void OnDefinitionPickerSelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_isBindingContextChanging) return;
            if (Vm == null || DefinitionPicker.SelectedItem == null) return;

            if (DefinitionPicker.SelectedItem is StepTypeWrapper selectedDef)
            {
                if (Vm.SelectedDefinition != selectedDef)
                {
                    Vm.SelectedDefinition = selectedDef;
                }
            }
        }
        
        private void SaveNowAsync()
        {
            var vmToSave = Vm;
            if (vmToSave == null) return;

            Dispatcher.Dispatch(async () =>
            {
                await vmToSave.SaveAsync();
            });
        }

        private void OnEntryUnfocused(object? sender, FocusEventArgs e)
        {
            if (_isBindingContextChanging) return;
            if (sender is BindableObject bindable && bindable.BindingContext is InputDefinition input)
            {
                if (Vm == null || !Vm.EditableInputs.Contains(input))
                    return;
                SaveNowAsync();
            }
        }

        private void OnEditorUnfocused(object? sender, FocusEventArgs e)
        {
            if (_isBindingContextChanging) return;
            if (sender is BindableObject bindable && bindable.BindingContext is InputDefinition input)
            {
                if (Vm == null || !Vm.EditableInputs.Contains(input))
                    return;
                SaveNowAsync();
            }
        }

        private void OnCheckChanged(object? sender, CheckedChangedEventArgs e)
        {
            if (_isBindingContextChanging) return;
            if (sender is BindableObject bindable && bindable.BindingContext is InputDefinition input)
            {
                if (Vm == null || !Vm.EditableInputs.Contains(input))
                    return;

                input.Value = e.Value;
                Vm.SetModelValue(input.Label, e.Value);
                SaveNowAsync();
            }
        }

        private void OnTextChanged(object? sender, TextChangedEventArgs e)
        {
            if (_isBindingContextChanging) return;
            if (sender is BindableObject bindable && bindable.BindingContext is InputDefinition input)
            {
                if (Vm == null || !Vm.EditableInputs.Contains(input))
                    return;

                // Only handle text, textarea, number, or ComboBox in manual mode to avoid overwriting Guids or other types with strings
                if (input.ControlType != InputControlType.Text && 
                    input.ControlType != InputControlType.TextArea &&
                    input.ControlType != InputControlType.Number &&
                    !(input.ControlType == InputControlType.ComboBox && input.IsManualMode))
                {
                    return;
                }

                var modelVal = Vm.GetModelValue(input.Label)?.ToString() ?? string.Empty;
                var newStr = e.NewTextValue ?? string.Empty;
                if (modelVal != newStr)
                {
                    input.Value = e.NewTextValue;
                    Vm.SetModelValue(input.Label, e.NewTextValue);
                    SaveNowAsync();
                }
            }
        }

        private void OnToggleManualMode(object? sender, EventArgs e)
        {
            if (_isBindingContextChanging) return;
            if (sender is BindableObject bindable && bindable.BindingContext is InputDefinition input)
            {
                if (Vm == null || !Vm.EditableInputs.Contains(input))
                    return;

                input.IsManualMode = !input.IsManualMode;
                if (!input.IsManualMode)
                {
                    input.Value = null;
                }
                Vm.SetModelValue(input.Label, input.Value);
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

        private void OnAddCustomOptionClicked(object? sender, EventArgs e)
        {
            if (_isBindingContextChanging) return;
            if (Vm == null) return;

            if (sender is Button btn)
            {
                var input = Vm.EditableInputs.FirstOrDefault(x => x.Label == "CustomOptions");
                if (input == null) return;

                Entry? entry = null;
                if (btn.Parent is Grid grid)
                {
                    entry = grid.Children.OfType<Entry>().FirstOrDefault();
                }

                if (entry != null)
                {
                    var text = entry.Text?.Trim();
                    if (string.IsNullOrEmpty(text)) return;

                    var currentVal = input.Value?.ToString() ?? string.Empty;
                    var items = currentVal.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                          .Select(x => x.Trim())
                                          .ToList();

                    if (!items.Contains(text))
                    {
                        items.Add(text);
                        var newVal = string.Join(",", items);
                        input.Value = newVal; // Automatically triggers OnInputPropertyChanged -> SetModelValue -> SaveAsync()
                        entry.Text = string.Empty;
                    }
                }
            }
        }

        private void OnDeleteCustomOptionClicked(object? sender, EventArgs e)
        {
            if (_isBindingContextChanging) return;
            if (Vm == null) return;

            if (sender is Button btn && btn.CommandParameter is string itemToDelete)
            {
                var input = Vm.EditableInputs.FirstOrDefault(x => x.Label == "CustomOptions");
                if (input == null) return;

                var currentVal = input.Value?.ToString() ?? string.Empty;
                var items = currentVal.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                      .Select(x => x.Trim())
                                      .ToList();

                if (items.Remove(itemToDelete))
                {
                    var newVal = string.Join(",", items);
                    input.Value = newVal; // Automatically triggers OnInputPropertyChanged -> SetModelValue -> SaveAsync()
                }
            }
        }

        private void OnMediaHelpClicked(object? sender, EventArgs e)
        {
            var parentPage = FindParentPage();
            if (parentPage != null)
            {
                parentPage.DisplayAlert(
                    "Recommended Unity Media Formats",
                    "For optimal rendering in Unity and cross-platform RAGS clients, please use the following formats:\n\n" +
                    "🖼️ IMAGES (Room Pictures, Portraits):\n" +
                    "• .png (lossless, supports transparency - Recommended)\n" +
                    "• .jpg / .jpeg (efficient for photographs)\n\n" +
                    "🔊 AUDIO (Sound Effects, Background Music):\n" +
                    "• .mp3 (high compatibility - Recommended for music)\n" +
                    "• .wav (uncompressed PCM - Recommended for short SFX)\n" +
                    "• .ogg (highly efficient open audio format)\n\n" +
                    "🎬 VIDEO (Cutscenes, Multimedia):\n" +
                    "• .mp4 (highly recommended, standard H.264)\n" +
                    "• .webm (efficient open web format)\n\n" +
                    "⚠️ Note: Avoid legacy Windows-only formats like .bmp, .wma, or .wmv, which may fail to play on non-Windows players.",
                    "OK"
                );
            }
        }

        public EditStepView()
        {
            InitializeComponent();
        }
    }
}