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

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class PreferencesViewModel : ViewModelBase
    {
        private AppSettings _settings = new();
        private ObservableCollection<string> _availableModels = new();
        private bool _isFetchingModels;

        public PreferencesViewModel()
        {
            FetchModelsCommand = new Command(async () => await FetchModelsAsync());
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
                                          string.Equals(AiCoAuthorProvider, "OpenRouter", StringComparison.OrdinalIgnoreCase);

        public bool ShowImageGenApiKey
        {
            get
            {
                var provider = AiImageGenProvider;
                return string.Equals(provider, "ChatGPT", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(provider, "Azure OpenAI Images", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(provider, "Google Gemini", StringComparison.OrdinalIgnoreCase);
            }
        }

        // Help Instructions Support (Co-Author)
        public string CoAuthorSetupStepsTitle => AiCoAuthorProvider switch
        {
            "Ollama" => "Ollama Setup (Local & Free):",
            "LMStudio" => "LM Studio Setup (Local & Free):",
            "OpenRouter" => "OpenRouter Setup (Cloud & Free Tier):",
            _ => "AI Provider Connection Details:"
        };

        public string CoAuthorStep1 => AiCoAuthorProvider switch
        {
            "Ollama" => "1. Make sure Ollama is installed and running on your computer.",
            "LMStudio" => "1. Start LM Studio and download a model from the Search tab.",
            "OpenRouter" => "1. Create an account at OpenRouter (no credit card required).",
            _ => "1. Set up your custom API gateway endpoint URL."
        };

        public string CoAuthorStep2 => AiCoAuthorProvider switch
        {
            "Ollama" => "2. Pull a model using the command prompt (e.g. 'ollama run gemma2' or 'ollama run llama3').",
            "LMStudio" => "2. Go to the Developer tab (Server icon) and click 'Start Server'.",
            "OpenRouter" => "2. Create a new API Key, copy it, and paste it into the 'API Key' field below.",
            _ => "2. Input your user credentials / API Key in the field below."
        };

        public string CoAuthorStep3 => AiCoAuthorProvider switch
        {
            "Ollama" => "3. Click Refresh (↻) below to pick a pulled model, or type one in.",
            "LMStudio" => "3. Keep LM Studio running in the background while designing.",
            "OpenRouter" => "3. Select free creative writing models like Gemma 2!",
            _ => "3. Enter the exact model identifier code required by the provider."
        };

        public string CoAuthorHelpButtonText => AiCoAuthorProvider switch
        {
            "Ollama" => "Download Ollama ↗",
            "LMStudio" => "Download LM Studio ↗",
            "OpenRouter" => "Get Free API Key ↗",
            _ => "Open Dashboard ↗"
        };

        public string CoAuthorHelpUrl => AiCoAuthorProvider switch
        {
            "Ollama" => "https://ollama.com",
            "LMStudio" => "https://lmstudio.ai",
            "OpenRouter" => "https://openrouter.ai/keys",
            _ => "https://platform.openai.com"
        };

        public bool IsCoAuthorHelpVisible => true;

        // Help Instructions Support (Image Gen)
        public string ImageGenSetupStepsTitle => AiImageGenProvider switch
        {
            "Pollinations.ai" => "Pollinations.ai Setup (Free & Cloud-Based):",
            "ChatGPT" => "ChatGPT / OpenAI Setup (Paid Cloud):",
            "Azure OpenAI Images" => "Azure OpenAI Setup (Enterprise Cloud):",
            "Google Gemini" => "Google Gemini / Vertex Setup (Cloud):",
            "Local Stable Diffusion" => "Stable Diffusion (Automatic1111 - Local & Free):",
            "Local ComfyUI" => "ComfyUI (Local & Free):",
            _ => "AI Image Provider Connection Details:"
        };

        public string ImageGenStep1 => AiImageGenProvider switch
        {
            "Pollinations.ai" => "1. Uses state-of-the-art open-source creative models (like FLUX) hosted by the community.",
            "ChatGPT" => "1. Requires an active OpenAI Developer account with paid API billing.",
            "Azure OpenAI Images" => "1. Deploy an Azure OpenAI instance in your Microsoft Azure Portal.",
            "Google Gemini" => "1. Enable the Vertex AI API inside your Google Cloud Console.",
            "Local Stable Diffusion" => "1. Ensure Automatic1111's WebUI is installed and running on your local computer.",
            "Local ComfyUI" => "1. Install ComfyUI and run it locally (uses port 8000 or 8188 by default).",
            _ => "1. Configure the API endpoint host for the chosen provider."
        };

        public string ImageGenStep2 => AiImageGenProvider switch
        {
            "Pollinations.ai" => "2. Completely free online service requiring no API keys, tokens, or local installation.",
            "ChatGPT" => "2. Go to the API Keys tab in your dashboard, generate a key, and copy it.",
            "Azure OpenAI Images" => "2. Copy your deployed endpoint (e.g. https://YOUR-RESOURCE.openai.azure.com/) into Host.",
            "Google Gemini" => "2. Setup your billing project and copy your project endpoints.",
            "Local Stable Diffusion" => "2. Crucial: Launch the WebUI with the '--api' flag enabled to allow external connections.",
            "Local ComfyUI" => "2. Setup a custom image-generation workflow JSON and save it to your disk.",
            _ => "2. Set up your authentication keys / project credentials."
        };

        public string ImageGenStep3 => AiImageGenProvider switch
        {
            "Pollinations.ai" => "3. Perfect out-of-the-box experience for immediate testing and portrait generation!",
            "ChatGPT" => "3. Paste the key in the API Key box below and use DALL-E 3 (default).",
            "Azure OpenAI Images" => "3. Enter your resource-specific auth key in the API Key field below.",
            "Google Gemini" => "3. Configure your API token or service key credentials in the fields below.",
            "Local Stable Diffusion" => "3. Keep your local instance active at http://localhost:7860 while generating.",
            "Local ComfyUI" => "3. Keep ComfyUI running in the background and configure workflow path.",
            _ => "3. Specify the target model / checkpoint variant name."
        };

        public string ImageGenHelpButtonText => AiImageGenProvider switch
        {
            "Pollinations.ai" => "Open Pollinations.ai ↗",
            "ChatGPT" => "Get OpenAI API Key ↗",
            "Azure OpenAI Images" => "Open Azure Portal ↗",
            "Google Gemini" => "Google Cloud Console ↗",
            "Local Stable Diffusion" => "View Automatic1111 Guide ↗",
            "Local ComfyUI" => "Download ComfyUI ↗",
            _ => "Open Dashboard ↗"
        };

        public string ImageGenHelpUrl => AiImageGenProvider switch
        {
            "Pollinations.ai" => "https://pollinations.ai",
            "ChatGPT" => "https://platform.openai.com/api-keys",
            "Azure OpenAI Images" => "https://portal.azure.com",
            "Google Gemini" => "https://console.cloud.google.com",
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
                    AiImageGenModel = "imagen-3";
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

        public async Task FetchModelsAsync()
        {
            if (IsFetchingModels) return;

            IsFetchingModels = true;
            AvailableModels.Clear();
            AvailableModels.Add("Fetching...");

            try
            {
                using var http = new System.Net.Http.HttpClient();
                http.Timeout = TimeSpan.FromSeconds(10);

                string baseUri = "";
                if (AiCoAuthorProvider == "Ollama")
                {
                    var portStr = string.IsNullOrEmpty(AiCoAuthorPort) ? "11434" : AiCoAuthorPort;
                    baseUri = $"http://localhost:{portStr}/";
                    var resp = await http.GetAsync(new Uri(new Uri(baseUri), "api/tags"));
                    if (resp.IsSuccessStatusCode)
                    {
                        var txt = await resp.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(txt);
                        if (doc.RootElement.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
                        {
                            var list = new List<string>();
                            foreach (var m in models.EnumerateArray())
                            {
                                if (m.TryGetProperty("name", out var name))
                                    list.Add(name.GetString() ?? "");
                            }
                            AvailableModels.Clear();
                            foreach (var m in list.Distinct().OrderBy(x => x))
                                AvailableModels.Add(m);
                        }
                    }
                }
                else
                {
                    // LMStudio / OpenAICompatible / OpenRouter
                    var portStr = string.IsNullOrEmpty(AiCoAuthorPort) ? "1234" : AiCoAuthorPort;
                    if (AiCoAuthorProvider == "OpenAICompatible")
                    {
                        baseUri = AiCoAuthorEndpoint; 
                    }
                    else if (AiCoAuthorProvider == "OpenRouter")
                    {
                        baseUri = "https://openrouter.ai/api/";
                    }
                    else if (AiCoAuthorProvider == "LMStudio")
                    {
                        baseUri = $"http://localhost:{portStr}/";
                    }

                    if (string.IsNullOrEmpty(baseUri)) return;
                    if (!baseUri.EndsWith("/")) baseUri += "/";

                    using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, new Uri(new Uri(baseUri), "v1/models"));
                    if (!string.IsNullOrEmpty(AiCoAuthorKey))
                    {
                        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AiCoAuthorKey);
                    }

                    var resp = await http.SendAsync(req);
                    if (resp.IsSuccessStatusCode)
                    {
                        var txt = await resp.Content.ReadAsStringAsync();
                        using var doc = JsonDocument.Parse(txt);
                        var list = new List<string>();
                        if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var m in data.EnumerateArray())
                            {
                                if (m.TryGetProperty("id", out var idEl))
                                    list.Add(idEl.GetString() ?? "");
                            }
                        }
                        
                        AvailableModels.Clear();
                        if (AiCoAuthorProvider == "OpenRouter")
                        {
                            foreach (var m in list.Where(x => x.Contains(":free") || x == "gryphe/mythomax-l2-13b").Distinct().OrderBy(x => x))
                                AvailableModels.Add(m);
                        }
                        else
                        {
                            foreach (var m in list.Distinct().OrderBy(x => x))
                                AvailableModels.Add(m);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                AvailableModels.Clear();
                AvailableModels.Add($"Error: {ex.Message}");
            }
            finally
            {
                IsFetchingModels = false;
                if (AvailableModels.Count == 0)
                {
                    AvailableModels.Add("No models found. Check provider connection.");
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
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json);
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
                        OnPropertyChanged(nameof(ShowCoAuthorHostPort));
                        OnPropertyChanged(nameof(ShowCoAuthorEndpoint));
                        OnPropertyChanged(nameof(ShowCoAuthorApiKey));
                        OnPropertyChanged(nameof(ShowImageGenHostPort));
                        OnPropertyChanged(nameof(ShowImageGenEndpoint));
                        OnPropertyChanged(nameof(ShowImageGenApiKey));
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
                var json = JsonSerializer.Serialize(_settings);
                var path = SettingsFilePath;
                var dir = Path.GetDirectoryName(path);
                if (dir != null) Directory.CreateDirectory(dir);
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}
