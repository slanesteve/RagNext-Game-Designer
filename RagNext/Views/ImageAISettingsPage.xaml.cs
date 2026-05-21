using Microsoft.Maui.Controls;
using RagNext.Models;
using RagNext.Services;
using System;

namespace RagNext.Views
{
    public partial class ImageAISettingsPage : ContentPage
    {
        private AISettings _settings;
        private string? _helpUrl;

        // UI-only provider list distinct from text AI providers
        private enum ImageProviderUi
        {
            PollinationsAI,
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
            if ((_settings.ImageHost?.Contains("pollinations", StringComparison.OrdinalIgnoreCase) ?? false))
                ProviderPicker.SelectedIndex = (int)ImageProviderUi.PollinationsAI;
            else if ((_settings.ImageHost?.Contains("openai", StringComparison.OrdinalIgnoreCase) ?? false))
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

            HostEntry.Text = _settings.ImageHost ?? "https://image.pollinations.ai";
            PortEntry.Text = _settings.ImagePort.ToString();
            ModelEntry.Text = _settings.ImageModel ?? "flux";
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

            // Trigger initial help box guidance load
            UpdateHelpGuidance((ImageProviderUi)ProviderPicker.SelectedIndex);
        }

        private void OnProviderChanged(object? sender, EventArgs e)
        {
            var idx = ProviderPicker.SelectedIndex;
            if (idx < 0) return;
            var choice = (ImageProviderUi)idx;

            // Apply reasonable defaults
            switch (choice)
            {
                case ImageProviderUi.PollinationsAI:
                    HostEntry.Text = "https://image.pollinations.ai";
                    PortEntry.Text = "0";
                    ModelEntry.Text = "flux";
                    ApiKeyEntry.Text = null;
                    break;
                case ImageProviderUi.OpenAI:
                    HostEntry.Text = "https://api.openai.com";
                    PortEntry.Text = "0";
                    ModelEntry.Text = "dall-e-3";
                    break;
                case ImageProviderUi.AzureOpenAI:
                    HostEntry.Text = "https://YOUR-RESOURCE.openai.azure.com";
                    PortEntry.Text = "0";
                    ModelEntry.Text = "dall-e-3";
                    break;
                case ImageProviderUi.GoogleVertexAI:
                    HostEntry.Text = "https://generativelanguage.googleapis.com";
                    PortEntry.Text = "0";
                    ModelEntry.Text = "imagen-3";
                    break;
                case ImageProviderUi.LocalStableDiffusion:
                    HostEntry.Text = "http://localhost";
                    PortEntry.Text = "7860"; // Automatic1111 default web UI port
                    ModelEntry.Text = "stable-diffusion";
                    ApiKeyEntry.Text = null;
                    break;
                case ImageProviderUi.LocalComfyUI:
                    HostEntry.Text = "http://localhost";
                    PortEntry.Text = "8000"; // ComfyUI default port
                    ModelEntry.Text = "comfy-ui";
                    ApiKeyEntry.Text = null;
                    break;
                case ImageProviderUi.Other:
                    // leave as user-entered
                    break;
            }

            SetApiKeyVisibility(idx);

            // Show ComfyUI fields only for Local ComfyUI
            SetComfyUiVisibility(choice == ImageProviderUi.LocalComfyUI);

            // Update instruction card
            UpdateHelpGuidance(choice);
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

        private void UpdateHelpGuidance(ImageProviderUi provider)
        {
            HelpBox.IsVisible = true;
            switch (provider)
            {
                case ImageProviderUi.PollinationsAI:
                    HelpBoxTitle.Text = "Pollinations.ai Setup (Free & Cloud-Based):";
                    HelpBoxStep1.Text = "1. Uses state-of-the-art open-source creative models (like FLUX) hosted by the community.";
                    HelpBoxStep2.Text = "2. Completely free online service requiring no API keys, tokens, or local installation.";
                    HelpBoxStep3.Text = "3. Perfect out-of-the-box experience for immediate testing and portrait generation!";
                    HelpActionButton.Text = "Open Pollinations.ai Website ↗";
                    _helpUrl = "https://pollinations.ai";
                    break;

                case ImageProviderUi.OpenAI:
                    HelpBoxTitle.Text = "ChatGPT / OpenAI Setup (Paid Cloud):";
                    HelpBoxStep1.Text = "1. Requires an active OpenAI Developer account with paid API billing.";
                    HelpBoxStep2.Text = "2. Go to the API Keys tab in your dashboard, generate a key, and copy it.";
                    HelpBoxStep3.Text = "3. Paste the key in the API Key box below and use DALL-E 3 (default).";
                    HelpActionButton.Text = "Get OpenAI API Key ↗";
                    _helpUrl = "https://platform.openai.com/api-keys";
                    break;

                case ImageProviderUi.AzureOpenAI:
                    HelpBoxTitle.Text = "Azure OpenAI Setup (Enterprise Cloud):";
                    HelpBoxStep1.Text = "1. Deploy an Azure OpenAI instance in your Microsoft Azure Portal.";
                    HelpBoxStep2.Text = "2. Copy your deployed endpoint (e.g. https://NAME.openai.azure.com/) into the Host entry.";
                    HelpBoxStep3.Text = "3. Enter your resource-specific auth key in the API Key field below.";
                    HelpActionButton.Text = "Open Azure Portal ↗";
                    _helpUrl = "https://portal.azure.com";
                    break;

                case ImageProviderUi.GoogleVertexAI:
                    HelpBoxTitle.Text = "Google Gemini / Vertex Setup (Cloud):";
                    HelpBoxStep1.Text = "1. Enable the Vertex AI API inside your Google Cloud Console.";
                    HelpBoxStep2.Text = "2. Setup your billing project and copy your project endpoints.";
                    HelpBoxStep3.Text = "3. Configure your API token or service key credentials in the fields below.";
                    HelpActionButton.Text = "Google Cloud Console ↗";
                    _helpUrl = "https://console.cloud.google.com";
                    break;

                case ImageProviderUi.LocalStableDiffusion:
                    HelpBoxTitle.Text = "Stable Diffusion (Automatic1111 - Local & Free):";
                    HelpBoxStep1.Text = "1. Ensure Automatic1111's WebUI is installed and running on your local computer.";
                    HelpBoxStep2.Text = "2. Crucial: Launch the WebUI with the '--api' flag enabled to allow external API connections.";
                    HelpBoxStep3.Text = "3. Keep your local instance active at http://localhost:7860 while generating.";
                    HelpActionButton.Text = "View Automatic1111 Guide ↗";
                    _helpUrl = "https://github.com/AUTOMATIC1111/stable-diffusion-webui";
                    break;

                case ImageProviderUi.LocalComfyUI:
                    HelpBoxTitle.Text = "ComfyUI (Local & Free):";
                    HelpBoxStep1.Text = "1. Install ComfyUI and run it locally (uses port 8000 or 8188 by default).";
                    HelpBoxStep2.Text = "2. Setup a custom image-generation workflow JSON and save it to your disk.";
                    HelpBoxStep3.Text = "3. Enter the absolute path to your JSON workflow file in the settings below.";
                    HelpActionButton.Text = "Download ComfyUI ↗";
                    _helpUrl = "https://github.com/comfyanonymous/ComfyUI";
                    break;

                default:
                    HelpBox.IsVisible = false;
                    _helpUrl = null;
                    break;
            }
        }

        private async void OnHelpActionButtonClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_helpUrl))
            {
                await Browser.Default.OpenAsync(_helpUrl, BrowserLaunchMode.SystemPreferred);
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var service = MauiProgram.Services.GetService(typeof(IAISettingsService)) as IAISettingsService
                         ?? throw new InvalidOperationException("AISettingsService not registered.");

            // Map UI selection to storage enum
            _settings.ImageProvider = ProviderPicker.SelectedIndex switch
            {
                (int)ImageProviderUi.PollinationsAI => AIProviderKind.OpenAICompatible, // custom pollinations router
                (int)ImageProviderUi.OpenAI => AIProviderKind.OpenAICompatible,
                (int)ImageProviderUi.AzureOpenAI => AIProviderKind.OpenAICompatible,
                (int)ImageProviderUi.GoogleVertexAI => AIProviderKind.OpenAICompatible,
                (int)ImageProviderUi.LocalStableDiffusion => AIProviderKind.Ollama,
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
