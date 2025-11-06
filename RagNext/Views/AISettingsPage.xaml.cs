using Microsoft.Maui.Controls;
using RagNext.Models;
using RagNext.Services;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RagNext.Views
{
    public partial class AISettingsPage : ContentPage
    {
        private readonly IAISettingsService _service;
        private readonly IAIChatService? _chat;
        private AISettings _settings;

        public AISettingsPage()
        {
            InitializeComponent();
            _service = MauiProgram.Services.GetService(typeof(IAISettingsService)) as IAISettingsService
                       ?? throw new InvalidOperationException("AISettingsService not registered.");
            _chat = MauiProgram.Services.GetService(typeof(IAIChatService)) as IAIChatService;
            _settings = _service.Load();
            BindSettingsToUI();
        }

        private void BindSettingsToUI()
        {
            ProviderPicker.SelectedIndex = (int)_settings.Provider;
            HostEntry.Text = _settings.Host;
            PortEntry.Text = _settings.Port.ToString();
            ModelEntry.Text = _settings.Model;
            TempSlider.Value = _settings.Temperature;
            TempValue.Text = _settings.Temperature.ToString("0.00");
            MaxTokensEntry.Text = _settings.MaxTokens.ToString();
            ApiKeyEntry.Text = _settings.ApiKey;
            EnableSwitch.IsToggled = _settings.EnableAIHelp;

            // Show effective prompt: default if user hasn't set one
            SystemPromptEditor.Text = string.IsNullOrWhiteSpace(_settings.SystemPrompt)
                ? AISettings.DefaultSystemPrompt
                : _settings.SystemPrompt;

            TempSlider.ValueChanged += (_, e) =>
            {
                TempValue.Text = e.NewValue.ToString("0.00");
            };
        }

        private async void OnProviderChanged(object? sender, EventArgs e)
        {
            var provider = (AIProviderKind)ProviderPicker.SelectedIndex;

            var hasUserValues =
                !string.IsNullOrWhiteSpace(HostEntry.Text) ||
                !string.IsNullOrWhiteSpace(PortEntry.Text) ||
                !string.IsNullOrWhiteSpace(ModelEntry.Text) ||
                !string.IsNullOrWhiteSpace(ApiKeyEntry.Text);

            var overwrite = false;
            if (hasUserValues)
            {
                var choice = await DisplayActionSheet(
                    "Apply provider defaults?",
                    "Cancel", null,
                    "Apply defaults", "Keep my values");
                if (choice == "Cancel") return;
                overwrite = choice == "Apply defaults";
            }

            ApplyProviderDefaults(provider, overwrite);
        }

        private void ApplyProviderDefaults(AIProviderKind provider, bool overwrite)
        {
            static void SetIf(bool condition, Action setter)
            {
                if (condition) setter();
            }

            switch (provider)
            {
                case AIProviderKind.Ollama:
                    SetIf(overwrite || string.IsNullOrWhiteSpace(HostEntry.Text), () => HostEntry.Text = "http://localhost");
                    SetIf(overwrite || string.IsNullOrWhiteSpace(PortEntry.Text), () => PortEntry.Text = "11434");
                    SetIf(overwrite || string.IsNullOrWhiteSpace(ModelEntry.Text), () => ModelEntry.Text = "llama3");
                    if (overwrite) ApiKeyEntry.Text = null;
                    break;

                case AIProviderKind.LMStudio:
                    SetIf(overwrite || string.IsNullOrWhiteSpace(HostEntry.Text), () => HostEntry.Text = "http://localhost");
                    SetIf(overwrite || string.IsNullOrWhiteSpace(PortEntry.Text), () => PortEntry.Text = "1234");
                    if (overwrite) ModelEntry.Text = string.Empty;
                    if (overwrite) ApiKeyEntry.Text = null;
                    break;

                case AIProviderKind.OpenAICompatible:
                    SetIf(overwrite || string.IsNullOrWhiteSpace(HostEntry.Text), () => HostEntry.Text = "https://api.openai.com");
                    SetIf(overwrite || string.IsNullOrWhiteSpace(PortEntry.Text), () => PortEntry.Text = "0");
                    if (overwrite) ModelEntry.Text = string.Empty;
                    break;
            }

            _settings.Provider = provider;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            _settings.Provider = (AIProviderKind)ProviderPicker.SelectedIndex;
            _settings.Host = HostEntry.Text?.Trim() ?? "http://localhost";
            _settings.Port = int.TryParse(PortEntry.Text, out var p) ? p : 11434;
            _settings.Model = ModelEntry.Text?.Trim() ?? "llama3";
            _settings.Temperature = TempSlider.Value;
            _settings.MaxTokens = int.TryParse(MaxTokensEntry.Text, out var mt) ? mt : 512;
            _settings.ApiKey = string.IsNullOrWhiteSpace(ApiKeyEntry.Text) ? null : ApiKeyEntry.Text.Trim();
            _settings.EnableAIHelp = EnableSwitch.IsToggled;

            var spText = SystemPromptEditor.Text?.Trim();
            // Store null when using the built-in default so future default changes propagate
            _settings.SystemPrompt = string.IsNullOrWhiteSpace(spText) || spText == AISettings.DefaultSystemPrompt ? null : spText;

            _service.Save(_settings);
            App.CurrentAISettings = _settings;
            await DisplayAlert("Saved", "AI settings saved.", "OK");
            await Navigation.PopModalAsync();
        }

        private sealed class DisposeAction : IDisposable
        {
            private readonly Action _a;
            public DisposeAction(Action a) => _a = a;
            public void Dispose() => _a();
        }

        private IDisposable StartSpinner(Button btn)
        {
            var original = btn.Text;
            btn.IsEnabled = false;
            btn.Text = "⟳";
            var anim = new Animation(v => btn.Rotation = v, 0, 360);
            anim.Commit(btn, "spin", length: 700, easing: Easing.Linear, repeat: () => true);
            return new DisposeAction(() =>
            {
                btn.AbortAnimation("spin");
                btn.Rotation = 0;
                btn.Text = original;
                btn.IsEnabled = true;
            });
        }

        private async void OnFetchModelsClicked(object sender, EventArgs e)
        {
            if (_chat is null)
            {
                await DisplayAlert("AI", "AI service is not available.", "OK");
                return;
            }

            var tempSettings = new AISettings
            {
                Provider = (AIProviderKind)ProviderPicker.SelectedIndex,
                Host = HostEntry.Text?.Trim() ?? "http://localhost",
                Port = int.TryParse(PortEntry.Text, out var p2) ? p2 : 11434,
                Model = ModelEntry.Text?.Trim() ?? _settings.Model,
                Temperature = TempSlider.Value,
                MaxTokens = int.TryParse(MaxTokensEntry.Text, out var mt2) ? mt2 : 512,
                ApiKey = string.IsNullOrWhiteSpace(ApiKeyEntry.Text) ? null : ApiKeyEntry.Text.Trim(),
                EnableAIHelp = EnableSwitch.IsToggled,
                SystemPrompt = _settings.SystemPrompt
            };
            App.CurrentAISettings = tempSettings;

            using var spin = StartSpinner((Button)sender);
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var models = await _chat.GetModelsAsync(cts.Token);
                if (models is null || models.Count == 0)
                {
                    await DisplayAlert("Models", "No models returned by the provider.", "OK");
                    return;
                }

                var choice = await DisplayActionSheet("Select a model", "Cancel", null, models.ToArray());
                if (!string.IsNullOrWhiteSpace(choice) && choice != "Cancel")
                {
                    ModelEntry.Text = choice;
                }
            }
            catch (TaskCanceledException)
            {
                await DisplayAlert("Models", "Timed out fetching models. Check your connection and settings.", "OK");
            }
            catch (Exception ex)
            {
                await DisplayAlert("Models Error", ex.Message, "OK");
            }
        }
    }
}