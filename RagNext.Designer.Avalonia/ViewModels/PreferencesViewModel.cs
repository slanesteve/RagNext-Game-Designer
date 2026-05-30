using System;
using System.IO;
using System.Text.Json;
using Avalonia;
using Avalonia.Styling;
using RagNext.Designer.Avalonia.Models;

namespace RagNext.Designer.Avalonia.ViewModels
{
    public class PreferencesViewModel : ViewModelBase
    {
        private AppSettings _settings = new();

        public PreferencesViewModel()
        {
            LoadSettings();
        }

        private string SettingsFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RagNext",
            "app_settings.json");

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
                    OnPropertyChanged(nameof(ShowCoAuthorHostPort));
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
                    OnPropertyChanged(nameof(ShowImageGenHostPort));
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

        private void ApplyTheme(string themeName)
        {
            if (Application.Current == null) return;

            if (themeName.Equals("Light", StringComparison.OrdinalIgnoreCase))
            {
                Application.Current.RequestedThemeVariant = ThemeVariant.Light;
            }
            else
            {
                // Default to Dark/OneDark using dark base
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
                        OnPropertyChanged(nameof(ShowImageGenHostPort));
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
