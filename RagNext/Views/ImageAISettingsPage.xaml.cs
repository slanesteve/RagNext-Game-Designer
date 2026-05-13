using Microsoft.Maui.Controls;
using RagNext.Models;
using RagNext.Services;
using System;

namespace RagNext.Views
{
    public partial class ImageAISettingsPage : ContentPage
    {
        private AISettings _settings;
        // UI-only provider list distinct from text AI providers
        private enum ImageProviderUi
        {
            OpenAI,
            AzureOpenAI,
            GoogleVertexAI,
            LocalStableDiffusion,
            LocalComfyUI,
            Other
        }

        public ImageAISettingsPage()
        {
            InitializeComponent();
            var service = MauiProgram.Services.GetService(typeof(IAISettingsService)) as IAISettingsService
                         ?? throw new InvalidOperationException("AISettingsService not registered.");
            _settings = service.Load();

            // Map settings to picker default
            // Prefer exact match via saved host/port hints when using local providers
            if ((_settings.ImageHost?.Contains("openai", StringComparison.OrdinalIgnoreCase) ?? false))
                ProviderPicker.SelectedIndex = (int)ImageProviderUi.OpenAI;
            else if ((_settings.ImageHost?.Contains("azure", StringComparison.OrdinalIgnoreCase) ?? false))
                ProviderPicker.SelectedIndex = (int)ImageProviderUi.AzureOpenAI;
            else if ((_settings.ImageHost?.Contains("googleapis", StringComparison.OrdinalIgnoreCase) ?? false))
                ProviderPicker.SelectedIndex = (int)ImageProviderUi.GoogleVertexAI;
            else if (_settings.ImagePort == 7860)
                ProviderPicker.SelectedIndex = (int)ImageProviderUi.LocalStableDiffusion;
            else if (_settings.ImagePort == 8188 || _settings.ImagePort == 8000)
                ProviderPicker.SelectedIndex = (int)ImageProviderUi.LocalComfyUI;
            else
                ProviderPicker.SelectedIndex = (int)ImageProviderUi.Other;
            HostEntry.Text = _settings.ImageHost ?? "https://api.openai.com";
            PortEntry.Text = _settings.ImagePort.ToString();
            ModelEntry.Text = _settings.ImageModel ?? "dall-e-3";
            ApiKeyEntry.Text = _settings.ImageApiKey;

            // Set API key visibility based on provider inference
            SetApiKeyVisibility(ProviderPicker.SelectedIndex);

            // Populate ComfyUI settings
            ComfyWorkflowEntry.Text = _settings.ComfyWorkflowPath;
            ComfyPositiveNodeEntry.Text = _settings.ComfyPositivePromptNode;
            ComfyNegativeNodeEntry.Text = _settings.ComfyNegativePromptNode;
            ComfySizeNodeEntry.Text = _settings.ComfySizeNode;

            // Ensure ComfyUI fields visibility matches initial selection
            SetComfyUiVisibility(ProviderPicker.SelectedIndex == (int)ImageProviderUi.LocalComfyUI);
        }

        private void OnProviderChanged(object? sender, EventArgs e)
        {
            var idx = ProviderPicker.SelectedIndex;
            if (idx < 0) return;
            var choice = (ImageProviderUi)idx;

            // Apply reasonable defaults as of today
            switch (choice)
            {
                case ImageProviderUi.OpenAI:
                    HostEntry.Text = "https://api.openai.com";
                    PortEntry.Text = "0";
                    ModelEntry.Text = "dall-e-3";
                    // API key required
                    break;
                case ImageProviderUi.AzureOpenAI:
                    HostEntry.Text = "https://YOUR-RESOURCE.openai.azure.com";
                    PortEntry.Text = "0";
                    ModelEntry.Text = "dall-e-3"; // or your deployed model name
                    // API key required; use Azure key
                    break;
                case ImageProviderUi.GoogleVertexAI:
                    HostEntry.Text = "https://generativelanguage.googleapis.com"; // general Gemini endpoint (note: image via Vertex/Imagen)
                    PortEntry.Text = "0";
                    ModelEntry.Text = "imagen-3"; // example; actual model via Vertex AI
                    // API key or service account needed
                    break;
                case ImageProviderUi.LocalStableDiffusion:
                    HostEntry.Text = "http://localhost";
                    PortEntry.Text = "7860"; // Automatic1111 default web UI port
                    ModelEntry.Text = "stable-diffusion";
                    ApiKeyEntry.Text = null; // no key needed
                    break;
                case ImageProviderUi.LocalComfyUI:
                    HostEntry.Text = "http://localhost";
                    //PortEntry.Text = "8188"; // ComfyUI default port
                    PortEntry.Text = "8000"; // updated default port as of v1.6.0
                    ModelEntry.Text = "comfy-ui";
                    ApiKeyEntry.Text = null; // no key needed
                    break;
                case ImageProviderUi.Other:
                    // leave as user-entered
                    break;
            }

            SetApiKeyVisibility(idx);

            // Show ComfyUI fields only for Local ComfyUI
            SetComfyUiVisibility(choice == ImageProviderUi.LocalComfyUI);
        }

        private void SetComfyUiVisibility(bool visible)
        {
            ComfyWorkflowLabel.IsVisible = visible;
            ComfyWorkflowEntry.IsVisible = visible;
            ComfyPositiveLabel.IsVisible = visible;
            ComfyPositiveNodeEntry.IsVisible = visible;
            ComfyNegativeLabel.IsVisible = visible;
            ComfyNegativeNodeEntry.IsVisible = visible;
            ComfySizeLabel.IsVisible = visible;
            ComfySizeNodeEntry.IsVisible = visible;
        }

        private void SetApiKeyVisibility(int selectedIndex)
        {
            var choice = (ImageProviderUi)(selectedIndex < 0 ? (int)ImageProviderUi.Other : selectedIndex);
            var needsKey = choice is ImageProviderUi.OpenAI or ImageProviderUi.AzureOpenAI or ImageProviderUi.GoogleVertexAI;
            ApiKeyLabel.IsVisible = needsKey;
            ApiKeyEntry.IsVisible = needsKey;
            if (!needsKey)
                ApiKeyEntry.Text = null;
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var service = MauiProgram.Services.GetService(typeof(IAISettingsService)) as IAISettingsService
                         ?? throw new InvalidOperationException("AISettingsService not registered.");
            // Map UI selection to storage enum
            _settings.ImageProvider = ProviderPicker.SelectedIndex switch
            {
                (int)ImageProviderUi.OpenAI => AIProviderKind.OpenAICompatible,
                (int)ImageProviderUi.AzureOpenAI => AIProviderKind.OpenAICompatible, // treat similarly; host differs
                (int)ImageProviderUi.GoogleVertexAI => AIProviderKind.OpenAICompatible, // custom impl will use different host
                (int)ImageProviderUi.LocalStableDiffusion => AIProviderKind.Ollama, // using local style
                (int)ImageProviderUi.LocalComfyUI => AIProviderKind.Ollama,
                _ => AIProviderKind.OpenAICompatible
            };
            _settings.ImageHost = HostEntry.Text?.Trim();
            _settings.ImagePort = int.TryParse(PortEntry.Text, out var p) ? p : 0;
            _settings.ImageModel = ModelEntry.Text?.Trim();
            _settings.ImageApiKey = string.IsNullOrWhiteSpace(ApiKeyEntry.Text) ? null : ApiKeyEntry.Text.Trim();
            // Save ComfyUI-specific settings
            _settings.ComfyWorkflowPath = string.IsNullOrWhiteSpace(ComfyWorkflowEntry.Text) ? null : ComfyWorkflowEntry.Text.Trim();
            _settings.ComfyPositivePromptNode = string.IsNullOrWhiteSpace(ComfyPositiveNodeEntry.Text) ? null : ComfyPositiveNodeEntry.Text.Trim();
            _settings.ComfyNegativePromptNode = string.IsNullOrWhiteSpace(ComfyNegativeNodeEntry.Text) ? null : ComfyNegativeNodeEntry.Text.Trim();
            _settings.ComfySizeNode = string.IsNullOrWhiteSpace(ComfySizeNodeEntry.Text) ? null : ComfySizeNodeEntry.Text.Trim();
            service.Save(_settings);
            App.CurrentAISettings = _settings;
            await DisplayAlert("Saved", "Image AI settings saved.", "OK");
            await Navigation.PopModalAsync();
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            await Navigation.PopModalAsync();
        }
    }
}
