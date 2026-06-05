using System;
using System.IO;
using System.Text.Json;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Styling;
using RagNext.Designer.Avalonia.Models;
using RagNext.Designer.Avalonia.Services;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class PreferencesViewModel : ViewModelBase
    {
        private AppSettings _settings = new();
        private ObservableCollection<string> _availableModels = new();
        private ObservableCollection<string> _availableCoAuthorModels = new();
        private ObservableCollection<string> _availableNodeAssistantModels = new();
        private ObservableCollection<string> _availableImageGenModels = new();
        private bool _isFetchingModels;

        public PreferencesViewModel()
        {
            FetchModelsCommand = new Command<string>(async (mode) => await FetchModelsAsync(mode));
            OpenHelpCommand = new Command<string>(OpenHelpUrl);
            LoadSettings();
        }

        private string SettingsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RagNext",
            "app_settings.json");

        public ObservableCollection<string> AvailableModels
        {
            get => _availableModels;
            set => SetProperty(ref _availableModels, value);
        }

        public ObservableCollection<string> AvailableCoAuthorModels
        {
            get => _availableCoAuthorModels;
            set => SetProperty(ref _availableCoAuthorModels, value);
        }

        public ObservableCollection<string> AvailableNodeAssistantModels
        {
            get => _availableNodeAssistantModels;
            set => SetProperty(ref _availableNodeAssistantModels, value);
        }

        public ObservableCollection<string> AvailableImageGenModels
        {
            get => _availableImageGenModels;
            set => SetProperty(ref _availableImageGenModels, value);
        }

        public bool IsFetchingModels
        {
            get => _isFetchingModels;
            set => SetProperty(ref _isFetchingModels, value);
        }

        public ICommand FetchModelsCommand { get; }
        public ICommand OpenHelpCommand { get; }

        public string ThemeName
        {
            get => _settings.ThemeName;
            set
            {
                if (_settings.ThemeName != value)
                {
                    _settings.ThemeName = value;
                    OnPropertyChanged();
                    ApplyTheme(value);
                    SaveSettings();
                }
            }
        }

        public string AiCoAuthorProvider
        {
            get => _settings.AiCoAuthorProvider;
            set
            {
                if (_settings.AiCoAuthorProvider != value)
                {
                    _settings.AiCoAuthorProvider = value;
                    OnPropertyChanged();
                    ApplyCoAuthorProviderDefaults(value);
                    OnPropertyChanged(nameof(ShowCoAuthorHostPort));
                    OnPropertyChanged(nameof(ShowCoAuthorEndpoint));
                    OnPropertyChanged(nameof(ShowCoAuthorApiKey));
                    SaveSettings();
                }
            }
        }

        public string AiCoAuthorEndpoint
        {
            get => _settings.AiCoAuthorEndpoint;
            set
            {
                if (_settings.AiCoAuthorEndpoint != value)
                {
                    _settings.AiCoAuthorEndpoint = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiCoAuthorKey
        {
            get => _settings.AiCoAuthorKey;
            set
            {
                if (_settings.AiCoAuthorKey != value)
                {
                    _settings.AiCoAuthorKey = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiCoAuthorModel
        {
            get => _settings.AiCoAuthorModel;
            set
            {
                if (_settings.AiCoAuthorModel != value)
                {
                    _settings.AiCoAuthorModel = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiCoAuthorHost
        {
            get => _settings.AiCoAuthorHost;
            set
            {
                if (_settings.AiCoAuthorHost != value)
                {
                    _settings.AiCoAuthorHost = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiCoAuthorPort
        {
            get => _settings.AiCoAuthorPort;
            set
            {
                if (_settings.AiCoAuthorPort != value)
                {
                    _settings.AiCoAuthorPort = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public double AiCoAuthorTemperature
        {
            get => _settings.AiCoAuthorTemperature;
            set
            {
                if (_settings.AiCoAuthorTemperature != value)
                {
                    _settings.AiCoAuthorTemperature = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public int AiCoAuthorMaxTokens
        {
            get => _settings.AiCoAuthorMaxTokens;
            set
            {
                if (_settings.AiCoAuthorMaxTokens != value)
                {
                    _settings.AiCoAuthorMaxTokens = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public bool AiCoAuthorEnableHelper
        {
            get => _settings.AiCoAuthorEnableHelper;
            set
            {
                if (_settings.AiCoAuthorEnableHelper != value)
                {
                    _settings.AiCoAuthorEnableHelper = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiCoAuthorAssistantPrompt
        {
            get => _settings.AiCoAuthorAssistantPrompt;
            set
            {
                if (_settings.AiCoAuthorAssistantPrompt != value)
                {
                    _settings.AiCoAuthorAssistantPrompt = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        // AI Image Generator Settings
        public string AiImageGenProvider
        {
            get => _settings.AiImageGenProvider;
            set
            {
                if (_settings.AiImageGenProvider != value)
                {
                    _settings.AiImageGenProvider = value;
                    OnPropertyChanged();
                    ApplyImageGenProviderDefaults(value);
                    OnPropertyChanged(nameof(ShowImageGenHostPort));
                    OnPropertyChanged(nameof(ShowImageGenEndpoint));
                    OnPropertyChanged(nameof(ShowImageGenApiKey));
                    SaveSettings();
                }
            }
        }

        public string AiImageGenEndpoint
        {
            get => _settings.AiImageGenEndpoint;
            set
            {
                if (_settings.AiImageGenEndpoint != value)
                {
                    _settings.AiImageGenEndpoint = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiImageGenKey
        {
            get => _settings.AiImageGenKey;
            set
            {
                if (_settings.AiImageGenKey != value)
                {
                    _settings.AiImageGenKey = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiImageGenModel
        {
            get => _settings.AiImageGenModel;
            set
            {
                if (_settings.AiImageGenModel != value)
                {
                    _settings.AiImageGenModel = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiImageGenHost
        {
            get => _settings.AiImageGenHost;
            set
            {
                if (_settings.AiImageGenHost != value)
                {
                    _settings.AiImageGenHost = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiImageGenPort
        {
            get => _settings.AiImageGenPort;
            set
            {
                if (_settings.AiImageGenPort != value)
                {
                    _settings.AiImageGenPort = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public bool AiImageGenEnableHelper
        {
            get => _settings.AiImageGenEnableHelper;
            set
            {
                if (_settings.AiImageGenEnableHelper != value)
                {
                    _settings.AiImageGenEnableHelper = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        // AI Node Assistant Settings
        public bool AiNodeAssistantUseCustom
        {
            get => _settings.AiNodeAssistantUseCustom;
            set
            {
                if (_settings.AiNodeAssistantUseCustom != value)
                {
                    _settings.AiNodeAssistantUseCustom = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiNodeAssistantProvider
        {
            get => _settings.AiNodeAssistantProvider;
            set
            {
                if (_settings.AiNodeAssistantProvider != value)
                {
                    _settings.AiNodeAssistantProvider = value;
                    OnPropertyChanged();
                    ApplyNodeAssistantProviderDefaults(value);
                    OnPropertyChanged(nameof(ShowNodeAssistantHostPort));
                    OnPropertyChanged(nameof(ShowNodeAssistantEndpoint));
                    OnPropertyChanged(nameof(ShowNodeAssistantApiKey));
                    SaveSettings();
                }
            }
        }

        public string AiNodeAssistantEndpoint
        {
            get => _settings.AiNodeAssistantEndpoint;
            set
            {
                if (_settings.AiNodeAssistantEndpoint != value)
                {
                    _settings.AiNodeAssistantEndpoint = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiNodeAssistantKey
        {
            get => _settings.AiNodeAssistantKey;
            set
            {
                if (_settings.AiNodeAssistantKey != value)
                {
                    _settings.AiNodeAssistantKey = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiNodeAssistantModel
        {
            get => _settings.AiNodeAssistantModel;
            set
            {
                if (_settings.AiNodeAssistantModel != value)
                {
                    _settings.AiNodeAssistantModel = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiNodeAssistantHost
        {
            get => _settings.AiNodeAssistantHost;
            set
            {
                if (_settings.AiNodeAssistantHost != value)
                {
                    _settings.AiNodeAssistantHost = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public string AiNodeAssistantPort
        {
            get => _settings.AiNodeAssistantPort;
            set
            {
                if (_settings.AiNodeAssistantPort != value)
                {
                    _settings.AiNodeAssistantPort = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public double AiNodeAssistantTemperature
        {
            get => _settings.AiNodeAssistantTemperature;
            set
            {
                if (_settings.AiNodeAssistantTemperature != value)
                {
                    _settings.AiNodeAssistantTemperature = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        public int AiNodeAssistantMaxTokens
        {
            get => _settings.AiNodeAssistantMaxTokens;
            set
            {
                if (_settings.AiNodeAssistantMaxTokens != value)
                {
                    _settings.AiNodeAssistantMaxTokens = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        // Last Published Directory
        public string LastPublishDirectory
        {
            get => _settings.LastPublishDirectory;
            set
            {
                if (_settings.LastPublishDirectory != value)
                {
                    _settings.LastPublishDirectory = value;
                    OnPropertyChanged();
                    SaveSettings();
                }
            }
        }

        // Helper visibility booleans for local server endpoints
        public bool ShowCoAuthorHostPort
        {
            get
            {
                var provider = AiCoAuthorProvider;
                return string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(provider, "LMStudio", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool ShowImageGenHostPort
        {
            get
            {
                var provider = AiImageGenProvider;
                return string.Equals(provider, "Local Stable Diffusion", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(provider, "Local ComfyUI", StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool ShowCoAuthorEndpoint => !ShowCoAuthorHostPort;
        public bool ShowImageGenEndpoint => !ShowImageGenHostPort;

        public bool ShowCoAuthorApiKey => string.Equals(AiCoAuthorProvider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(AiCoAuthorProvider, "OpenRouter", StringComparison.OrdinalIgnoreCase) ||
                                          string.Equals(AiCoAuthorProvider, "Google Gemini", StringComparison.OrdinalIgnoreCase);

        public bool ShowImageGenApiKey
        {
            get
            {
                var provider = AiImageGenProvider;
                return string.Equals(provider, "Pollinations.ai", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(provider, "ChatGPT", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(provider, "Azure OpenAI Images", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(provider, "Google Gemini", StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool ShowNodeAssistantHostPort
        {
            get
            {
                var provider = AiNodeAssistantProvider;
                return string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(provider, "LMStudio", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(provider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool ShowNodeAssistantEndpoint => !ShowNodeAssistantHostPort;

        public bool ShowNodeAssistantApiKey => string.Equals(AiNodeAssistantProvider, "OpenAICompatible", StringComparison.OrdinalIgnoreCase) ||
                                              string.Equals(AiNodeAssistantProvider, "OpenRouter", StringComparison.OrdinalIgnoreCase) ||
                                              string.Equals(AiNodeAssistantProvider, "Google Gemini", StringComparison.OrdinalIgnoreCase);


        // Help Instructions Support (Co-Author)
        public string CoAuthorSetupStepsTitle => AiCoAuthorProvider switch
        {
            "Ollama" => "Ollama Setup (Local & Free):",
            "LMStudio" => "LM Studio Setup (Local & Free):",
            "OpenRouter" => "OpenRouter Setup (Cloud & Free Tier):",
            "Google Gemini" => "Google Gemini Setup (Cloud & Free/Paid Tier):",
            _ => "AI Provider Connection Details:"
        };

        public string CoAuthorStep1 => AiCoAuthorProvider switch
        {
            "Ollama" => "1. Make sure Ollama is installed and running on your computer.",
            "LMStudio" => "1. Start LM Studio and download a model from the Search tab.",
            "OpenRouter" => "1. Create an account at OpenRouter (no credit card required).",
            "Google Gemini" => "1. Get a Gemini API Key from Google AI Studio (free for hobbyist use, subject to rate limits).",
            _ => "1. Set up your custom API gateway endpoint URL."
        };

        public string CoAuthorStep2 => AiCoAuthorProvider switch
        {
            "Ollama" => "2. Pull a model using the command prompt (e.g. 'ollama run gemma2' or 'ollama run llama3').",
            "LMStudio" => "2. Go to the Developer tab (Server icon) and click 'Start Server'.",
            "OpenRouter" => "2. Create a new API Key, copy it, and paste it into the 'API Key' field below.",
            "Google Gemini" => "2. Paste the API Key in the box below. The API Endpoint should remain at the default Google URL.",
            _ => "2. Input your user credentials / API Key in the field below."
        };

        public string CoAuthorStep3 => AiCoAuthorProvider switch
        {
            "Ollama" => "3. Click Refresh (↻) below to pick a pulled model, or type one in.",
            "LMStudio" => "3. Keep LM Studio running in the background while designing.",
            "OpenRouter" => "3. Select free creative writing models like Gemma 2!",
            "Google Gemini" => "3. Click Refresh (↻) below to pull available models, and select the latest Flash variant (e.g., 'gemini-3.5-flash').",
            _ => "3. Enter the exact model identifier code required by the provider."
        };

        public string CoAuthorHelpButtonText => AiCoAuthorProvider switch
        {
            "Ollama" => "Download Ollama ↗",
            "LMStudio" => "Download LM Studio ↗",
            "OpenRouter" => "Get Free API Key ↗",
            "Google Gemini" => "Get Gemini API Key ↗",
            _ => "Open Dashboard ↗"
        };

        public string CoAuthorHelpUrl => AiCoAuthorProvider switch
        {
            "Ollama" => "https://ollama.com",
            "LMStudio" => "https://lmstudio.ai",
            "OpenRouter" => "https://openrouter.ai/keys",
            "Google Gemini" => "https://aistudio.google.com/",
            _ => "https://platform.openai.com"
        };

        public bool IsCoAuthorHelpVisible => true;

        // Help Instructions Support (Image Gen)
        public string ImageGenSetupStepsTitle => AiImageGenProvider switch
        {
            "Pollinations.ai" => "Pollinations.ai Setup (Free & Cloud-Based):",
            "ChatGPT" => "ChatGPT / OpenAI Setup (Paid Cloud):",
            "Azure OpenAI Images" => "Azure OpenAI Setup (Enterprise Cloud):",
            "Google Gemini" => "Google Gemini Setup (Cloud - Paid/Billing Plan Required):",
            "Local Stable Diffusion" => "Stable Diffusion (Automatic1111 - Local & Free):",
            "Local ComfyUI" => "ComfyUI (Local & Free):",
            _ => "AI Image Provider Connection Details:"
        };

        public string ImageGenStep1 => AiImageGenProvider switch
        {
            "Pollinations.ai" => "1. Completely free and anonymous by default (leave API Key empty to use your local IP's quota).",
            "ChatGPT" => "1. Requires an active OpenAI Developer account with paid API billing.",
            "Azure OpenAI Images" => "1. Deploy an Azure OpenAI instance in your Microsoft Azure Portal.",
            "Google Gemini" => "1. Get a Gemini API Key from Google AI Studio (NOTE: Imagen API requires billing/paid plan enabled).",
            "Local Stable Diffusion" => "1. Ensure Automatic1111's WebUI is installed and running on your local computer.",
            "Local ComfyUI" => "1. Install ComfyUI and run it locally (uses port 8000 or 8188 by default).",
            _ => "1. Configure the API endpoint host for the chosen provider."
        };

        public string ImageGenStep2 => AiImageGenProvider switch
        {
            "Pollinations.ai" => "2. Optional: If you hit IP rate limits, log in at enter.pollinations.ai with GitHub to get a key.",
            "ChatGPT" => "2. Go to the API Keys tab in your dashboard, generate a key, and copy it.",
            "Azure OpenAI Images" => "2. Copy your deployed endpoint (e.g. https://YOUR-RESOURCE.openai.azure.com/) into Host.",
            "Google Gemini" => "2. Paste the API Key in the box below. The API Endpoint should remain at the default Google URL.",
            "Local Stable Diffusion" => "2. Crucial: Launch the WebUI with the '--api' flag enabled to allow external connections.",
            "Local ComfyUI" => "2. Setup a custom image-generation workflow JSON and save it to your disk.",
            _ => "2. Set up your authentication keys / project credentials."
        };

        public string ImageGenStep3 => AiImageGenProvider switch
        {
            "Pollinations.ai" => "3. Note: Generated Secret Keys (sk_...) require a funded balance, so anonymous mode is recommended.",
            "ChatGPT" => "3. Paste the key in the API Key box below and use DALL-E 3 (default).",
            "Azure OpenAI Images" => "3. Enter your resource-specific auth key in the API Key field below.",
            "Google Gemini" => "3. Click Refresh (↻) to pull models (e.g. 'imagen-4.0-generate-001'). Billing must be active in Google Cloud.",
            "Local Stable Diffusion" => "3. Keep your local instance active at http://localhost:7860 while generating.",
            "Local ComfyUI" => "3. Keep ComfyUI running in the background and configure workflow path.",
            _ => "3. Specify the target model / checkpoint variant name."
        };

        public string ImageGenHelpButtonText => AiImageGenProvider switch
        {
            "Pollinations.ai" => "Open Pollinations.ai ↗",
            "ChatGPT" => "Get OpenAI API Key ↗",
            "Azure OpenAI Images" => "Open Azure Portal ↗",
            "Google Gemini" => "Get Gemini API Key ↗",
            "Local Stable Diffusion" => "View Automatic1111 Guide ↗",
            "Local ComfyUI" => "Download ComfyUI ↗",
            _ => "Open Dashboard ↗"
        };

        public string ImageGenHelpUrl => AiImageGenProvider switch
        {
            "Pollinations.ai" => "https://pollinations.ai",
            "ChatGPT" => "https://platform.openai.com/api-keys",
            "Azure OpenAI Images" => "https://portal.azure.com",
            "Google Gemini" => "https://aistudio.google.com/",
            "Local Stable Diffusion" => "https://github.com/AUTOMATIC1111/stable-diffusion-webui",
            "Local ComfyUI" => "https://github.com/comfyanonymous/ComfyUI",
            _ => "https://platform.openai.com"
        };

        public bool IsImageGenHelpVisible => true;
        public bool IsComfyUiVisible => string.Equals(AiImageGenProvider, "Local ComfyUI", StringComparison.OrdinalIgnoreCase);

        public void ApplyCoAuthorProviderDefaults(string provider)
        {
            switch (provider)
            {
                case "Ollama":
                    AiCoAuthorEndpoint = "http://localhost";
                    AiCoAuthorPort = "11434";
                    AiCoAuthorModel = "llama3";
                    break;
                case "LMStudio":
                    AiCoAuthorEndpoint = "http://localhost";
                    AiCoAuthorPort = "1234";
                    AiCoAuthorModel = "llama3";
                    break;
                case "OpenAICompatible":
                    AiCoAuthorEndpoint = "https://api.openai.com/v1";
                    AiCoAuthorPort = "0";
                    AiCoAuthorModel = "gpt-4o";
                    break;
                case "OpenRouter":
                    AiCoAuthorEndpoint = "https://openrouter.ai/api";
                    AiCoAuthorPort = "0";
                    AiCoAuthorModel = "google/gemma-2-9b-it:free";
                    break;
                case "Google Gemini":
                    AiCoAuthorEndpoint = "https://generativelanguage.googleapis.com";
                    AiCoAuthorPort = "0";
                    AiCoAuthorModel = "gemini-3.5-flash";
                    break;
            }
            OnPropertyChanged(nameof(CoAuthorSetupStepsTitle));
            OnPropertyChanged(nameof(CoAuthorStep1));
            OnPropertyChanged(nameof(CoAuthorStep2));
            OnPropertyChanged(nameof(CoAuthorStep3));
            OnPropertyChanged(nameof(CoAuthorHelpButtonText));
            OnPropertyChanged(nameof(CoAuthorHelpUrl));
            OnPropertyChanged(nameof(IsCoAuthorHelpVisible));
        }

        public void ApplyImageGenProviderDefaults(string provider)
        {
            switch (provider)
            {
                case "Pollinations.ai":
                    AiImageGenEndpoint = "https://image.pollinations.ai";
                    AiImageGenPort = "0";
                    AiImageGenModel = "flux";
                    break;
                case "ChatGPT":
                    AiImageGenEndpoint = "https://api.openai.com";
                    AiImageGenPort = "0";
                    AiImageGenModel = "dall-e-3";
                    break;
                case "Azure OpenAI Images":
                    AiImageGenEndpoint = "https://YOUR-RESOURCE.openai.azure.com";
                    AiImageGenPort = "0";
                    AiImageGenModel = "dall-e-3";
                    break;
                case "Google Gemini":
                    AiImageGenEndpoint = "https://generativelanguage.googleapis.com";
                    AiImageGenPort = "0";
                    AiImageGenModel = "imagen-3.0-generate-002";
                    break;
                case "Local Stable Diffusion":
                    AiImageGenEndpoint = "http://localhost";
                    AiImageGenPort = "7860";
                    AiImageGenModel = "stable-diffusion";
                    break;
                case "Local ComfyUI":
                    AiImageGenEndpoint = "http://localhost";
                    AiImageGenPort = "8000";
                    AiImageGenModel = "comfy-ui";
                    break;
            }
            OnPropertyChanged(nameof(ImageGenSetupStepsTitle));
            OnPropertyChanged(nameof(ImageGenStep1));
            OnPropertyChanged(nameof(ImageGenStep2));
            OnPropertyChanged(nameof(ImageGenStep3));
            OnPropertyChanged(nameof(ImageGenHelpButtonText));
            OnPropertyChanged(nameof(ImageGenHelpUrl));
            OnPropertyChanged(nameof(IsImageGenHelpVisible));
            OnPropertyChanged(nameof(IsComfyUiVisible));
            OnPropertyChanged(nameof(ShowImageGenApiKey));
        }

        public void ApplyNodeAssistantProviderDefaults(string provider)
        {
            switch (provider)
            {
                case "Ollama":
                    AiNodeAssistantEndpoint = "http://localhost";
                    AiNodeAssistantPort = "11434";
                    AiNodeAssistantModel = "llama3";
                    break;
                case "LMStudio":
                    AiNodeAssistantEndpoint = "http://localhost";
                    AiNodeAssistantPort = "1234";
                    AiNodeAssistantModel = "llama3";
                    break;
                case "OpenAICompatible":
                    AiNodeAssistantEndpoint = "https://api.openai.com/v1";
                    AiNodeAssistantPort = "0";
                    AiNodeAssistantModel = "gpt-4o";
                    break;
                case "OpenRouter":
                    AiNodeAssistantEndpoint = "https://openrouter.ai/api";
                    AiNodeAssistantPort = "0";
                    AiNodeAssistantModel = "google/gemma-2-9b-it:free";
                    break;
                case "Google Gemini":
                    AiNodeAssistantEndpoint = "https://generativelanguage.googleapis.com";
                    AiNodeAssistantPort = "0";
                    AiNodeAssistantModel = "gemini-3.5-flash";
                    break;
            }
        }

        public async Task FetchModelsAsync(string mode)
        {
            if (IsFetchingModels) return;

            IsFetchingModels = true;
            
            var targetList = mode switch
            {
                "NodeAssistant" => AvailableNodeAssistantModels,
                "ImageGen" => AvailableImageGenModels,
                _ => AvailableCoAuthorModels
            };

            targetList.Clear();
            targetList.Add("Fetching...");

            AvailableModels.Clear();
            AvailableModels.Add("Fetching...");

            try
            {
                using var http = new System.Net.Http.HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);

                string provider = "";
                string endpoint = "";
                string key = "";
                string portStr = "";

                if (mode == "NodeAssistant")
                {
                    provider = AiNodeAssistantProvider;
                    endpoint = AiNodeAssistantEndpoint;
                    key = AiNodeAssistantKey;
                    portStr = string.IsNullOrEmpty(AiNodeAssistantPort) ? "1234" : AiNodeAssistantPort;
                }
                else if (mode == "ImageGen")
                {
                    provider = AiImageGenProvider;
                    endpoint = AiImageGenEndpoint;
                    key = AiImageGenKey;
                    portStr = string.IsNullOrEmpty(AiImageGenPort) ? "7860" : AiImageGenPort;
                }
                else
                {
                    provider = AiCoAuthorProvider;
                    endpoint = AiCoAuthorEndpoint;
                    key = AiCoAuthorKey;
                    portStr = string.IsNullOrEmpty(AiCoAuthorPort) ? "1234" : AiCoAuthorPort;
                }

                var list = new List<string>();

                if (mode == "ImageGen")
                {
                    if (provider == "Google Gemini")
                    {
                        if (string.IsNullOrEmpty(key))
                        {
                            targetList.Clear();
                            targetList.Add("Enter API Key first.");
                            return;
                        }
                        try
                        {
                            var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={key}";
                            var resp = await http.GetAsync(url);
                            if (resp.IsSuccessStatusCode)
                            {
                                var txt = await resp.Content.ReadAsStringAsync();
                                using var doc = JsonDocument.Parse(txt);
                                if (doc.RootElement.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var m in models.EnumerateArray())
                                    {
                                        if (m.TryGetProperty("name", out var nameEl))
                                        {
                                            var name = nameEl.GetString() ?? "";
                                            if (name.StartsWith("models/"))
                                            {
                                                name = name.Substring("models/".Length);
                                            }
                                            if (name.Contains("imagen", StringComparison.OrdinalIgnoreCase))
                                            {
                                                list.Add(name);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        catch {}

                        if (list.Count == 0)
                        {
                            list.Add("imagen-3.0-generate-002");
                            list.Add("imagen-3.0-fast-generate-001");
                            list.Add("imagen-4.0-generate-001");
                            list.Add("imagen-4.0-fast-generate-001");
                        }
                    }
                    else if (provider == "Pollinations.ai")
                    {
                        list.Add("flux");
                        list.Add("flux-realism");
                        list.Add("flux-anime");
                        list.Add("flux-3d");
                    }
                    else if (provider == "ChatGPT")
                    {
                        list.Add("dall-e-3");
                        list.Add("dall-e-2");
                    }
                    else if (provider == "Local Stable Diffusion")
                    {
                        var localPort = string.Equals(portStr, "0") || string.IsNullOrEmpty(portStr) ? "7860" : portStr;
                        var baseUri = $"http://localhost:{localPort}/";
                        var resp = await http.GetAsync(new Uri(new Uri(baseUri), "sdapi/v1/sd-models"));
                        if (resp.IsSuccessStatusCode)
                        {
                            var txt = await resp.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(txt);
                            foreach (var m in doc.RootElement.EnumerateArray())
                            {
                                if (m.TryGetProperty("title", out var title))
                                    list.Add(title.GetString() ?? "");
                            }
                        }
                    }
                }
                else
                {
                    // Text models: Ollama / LMStudio / OpenAICompatible / OpenRouter / Google Gemini
                    if (provider == "Google Gemini")
                    {
                        if (string.IsNullOrEmpty(key))
                        {
                            targetList.Clear();
                            targetList.Add("Enter API Key first.");
                            return;
                        }
                        var url = $"https://generativelanguage.googleapis.com/v1beta/models?key={key}";
                        var resp = await http.GetAsync(url);
                        if (resp.IsSuccessStatusCode)
                        {
                            var txt = await resp.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(txt);
                            if (doc.RootElement.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var m in models.EnumerateArray())
                                {
                                    if (m.TryGetProperty("name", out var nameEl))
                                    {
                                        var name = nameEl.GetString() ?? "";
                                        if (name.StartsWith("models/"))
                                        {
                                            name = name.Substring("models/".Length);
                                        }
                                        if (name.Contains("gemini"))
                                        {
                                            list.Add(name);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (provider == "Ollama")
                    {
                        var localPort = string.Equals(portStr, "0") || string.IsNullOrEmpty(portStr) ? "11434" : portStr;
                        var baseUri = $"http://localhost:{localPort}/";
                        var resp = await http.GetAsync(new Uri(new Uri(baseUri), "api/tags"));
                        if (resp.IsSuccessStatusCode)
                        {
                            var txt = await resp.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(txt);
                            if (doc.RootElement.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var m in models.EnumerateArray())
                                {
                                    if (m.TryGetProperty("name", out var name))
                                        list.Add(name.GetString() ?? "");
                                }
                            }
                        }
                    }
                    else
                    {
                        string baseUri = "";
                        if (provider == "OpenAICompatible")
                        {
                            baseUri = endpoint;
                        }
                        else if (provider == "OpenRouter")
                        {
                            baseUri = "https://openrouter.ai/api/";
                        }
                        else if (provider == "LMStudio")
                        {
                            baseUri = $"http://localhost:{portStr}/";
                        }

                        if (!string.IsNullOrEmpty(baseUri))
                        {
                            if (!baseUri.EndsWith("/")) baseUri += "/";
                            using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, new Uri(new Uri(baseUri), "v1/models"));
                            if (!string.IsNullOrEmpty(key))
                            {
                                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", key);
                            }

                            var resp = await http.SendAsync(req);
                            if (resp.IsSuccessStatusCode)
                            {
                                var txt = await resp.Content.ReadAsStringAsync();
                                using var doc = JsonDocument.Parse(txt);
                                if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var m in data.EnumerateArray())
                                    {
                                        if (m.TryGetProperty("id", out var idEl))
                                            list.Add(idEl.GetString() ?? "");
                                    }
                                }
                            }
                        }
                    }
                }

                targetList.Clear();
                AvailableModels.Clear();
                var sorted = list.Distinct().OrderBy(x => x).ToList();
                if (provider == "OpenRouter" && mode != "ImageGen")
                {
                    sorted = list.Where(x => x.Contains(":free") || x == "gryphe/mythomax-l2-13b").Distinct().OrderBy(x => x).ToList();
                }

                foreach (var m in sorted)
                {
                    targetList.Add(m);
                    AvailableModels.Add(m);
                }
            }
            catch (Exception ex)
            {
                targetList.Clear();
                targetList.Add($"Error: {ex.Message}");
                AvailableModels.Clear();
                AvailableModels.Add($"Error: {ex.Message}");
            }
            finally
            {
                IsFetchingModels = false;
                if (targetList.Count == 0)
                {
                    targetList.Add("No models found. Check provider connection.");
                }
            }
        }

        public void OpenHelpUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }

        private void ApplyTheme(string themeName)
        {
            if (Application.Current == null) return;

            if (themeName.Equals("Light", StringComparison.OrdinalIgnoreCase))
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Light;
            }
            else
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Dark;
            }
        }

        public void LoadSettings()
        {
            try
            {
                var path = SettingsFilePath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var loaded = JsonSerializer.Deserialize(json, DesignerJsonContext.Default.AppSettings);
                    if (loaded != null)
                    {
                        _settings = loaded;
                        ApplyTheme(_settings.ThemeName);
                        // Notify all properties
                        OnPropertyChanged(nameof(ThemeName));
                        OnPropertyChanged(nameof(AiCoAuthorProvider));
                        OnPropertyChanged(nameof(AiCoAuthorEndpoint));
                        OnPropertyChanged(nameof(AiCoAuthorKey));
                        OnPropertyChanged(nameof(AiCoAuthorModel));
                        OnPropertyChanged(nameof(AiCoAuthorHost));
                        OnPropertyChanged(nameof(AiCoAuthorPort));
                        OnPropertyChanged(nameof(AiCoAuthorTemperature));
                        OnPropertyChanged(nameof(AiCoAuthorMaxTokens));
                        OnPropertyChanged(nameof(AiCoAuthorEnableHelper));
                        OnPropertyChanged(nameof(AiCoAuthorAssistantPrompt));
                        OnPropertyChanged(nameof(AiImageGenProvider));
                        OnPropertyChanged(nameof(AiImageGenEndpoint));
                        OnPropertyChanged(nameof(AiImageGenKey));
                        OnPropertyChanged(nameof(AiImageGenModel));
                        OnPropertyChanged(nameof(AiImageGenHost));
                        OnPropertyChanged(nameof(AiImageGenPort));
                        OnPropertyChanged(nameof(AiImageGenEnableHelper));
                        OnPropertyChanged(nameof(AiNodeAssistantUseCustom));
                        OnPropertyChanged(nameof(AiNodeAssistantProvider));
                        OnPropertyChanged(nameof(AiNodeAssistantEndpoint));
                        OnPropertyChanged(nameof(AiNodeAssistantKey));
                        OnPropertyChanged(nameof(AiNodeAssistantModel));
                        OnPropertyChanged(nameof(AiNodeAssistantHost));
                        OnPropertyChanged(nameof(AiNodeAssistantPort));
                        OnPropertyChanged(nameof(AiNodeAssistantTemperature));
                        OnPropertyChanged(nameof(AiNodeAssistantMaxTokens));
                        OnPropertyChanged(nameof(ShowCoAuthorHostPort));
                        OnPropertyChanged(nameof(ShowCoAuthorEndpoint));
                        OnPropertyChanged(nameof(ShowCoAuthorApiKey));
                        OnPropertyChanged(nameof(ShowImageGenHostPort));
                        OnPropertyChanged(nameof(ShowImageGenEndpoint));
                        OnPropertyChanged(nameof(ShowImageGenApiKey));
                        OnPropertyChanged(nameof(ShowNodeAssistantHostPort));
                        OnPropertyChanged(nameof(ShowNodeAssistantEndpoint));
                        OnPropertyChanged(nameof(ShowNodeAssistantApiKey));
                        OnPropertyChanged(nameof(LastPublishDirectory));

                        OnPropertyChanged(nameof(CoAuthorSetupStepsTitle));
                        OnPropertyChanged(nameof(CoAuthorStep1));
                        OnPropertyChanged(nameof(CoAuthorStep2));
                        OnPropertyChanged(nameof(CoAuthorStep3));
                        OnPropertyChanged(nameof(CoAuthorHelpButtonText));
                        OnPropertyChanged(nameof(CoAuthorHelpUrl));
                        OnPropertyChanged(nameof(IsCoAuthorHelpVisible));

                        OnPropertyChanged(nameof(ImageGenSetupStepsTitle));
                        OnPropertyChanged(nameof(ImageGenStep1));
                        OnPropertyChanged(nameof(ImageGenStep2));
                        OnPropertyChanged(nameof(ImageGenStep3));
                        OnPropertyChanged(nameof(ImageGenHelpButtonText));
                        OnPropertyChanged(nameof(ImageGenHelpUrl));
                        OnPropertyChanged(nameof(IsImageGenHelpVisible));
                        OnPropertyChanged(nameof(IsComfyUiVisible));
                        OnPropertyChanged(nameof(ShowImageGenApiKey));
                        OnPropertyChanged(nameof(AvailableModels));
                        OnPropertyChanged(nameof(AvailableCoAuthorModels));
                        OnPropertyChanged(nameof(AvailableNodeAssistantModels));
                        OnPropertyChanged(nameof(AvailableImageGenModels));
                        OnPropertyChanged(nameof(LastPublishDirectory));
                    }
                }
            }
            catch { }
        }

        public void SaveSettings()
        {
            try
            {
                var json = JsonSerializer.Serialize(_settings, DesignerJsonContext.Default.AppSettings);
                var path = SettingsFilePath;
                var dir = Path.GetDirectoryName(path);
                if (dir != null) Directory.CreateDirectory(dir);
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}
