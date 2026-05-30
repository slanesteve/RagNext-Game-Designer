using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using RagNext.Models;
using RagNext.Views;
using RagNext.Services;

namespace RagNext
{
    public partial class AppShell : Shell
    {
        public static readonly BindableProperty IsFlyoutEnabledProperty =
            BindableProperty.Create(nameof(IsFlyoutEnabled), typeof(bool), typeof(AppShell), true);

        public bool IsFlyoutEnabled
        {
            get => (bool)GetValue(IsFlyoutEnabledProperty);
            set => SetValue(IsFlyoutEnabledProperty, value);
        }

        private readonly IAISettingsService? _settingsService;
        private bool _isUpdating;

        public AppShell()
        {
            InitializeComponent();
            RegisterRoutes();

            _settingsService = MauiProgram.Services.GetService(typeof(IAISettingsService)) as IAISettingsService;
            
            App.AISettingsChanged += OnAISettingsChanged;
            
            // Initial load of the model picker
            if (App.CurrentAISettings is not null)
            {
                OnAISettingsChanged(App.CurrentAISettings);
            }
        }

        private static void RegisterRoutes()
        {
            Routing.RegisterRoute("RoomEdit", typeof(RagNext.Views.RoomEditPage));
            Routing.RegisterRoute("GameObjectEdit", typeof(RagNext.Views.GameObjectEditPage));
            Routing.RegisterRoute("GameVariableEdit", typeof(RagNext.Views.GameVariableEditPage));
            Routing.RegisterRoute("CharacterEdit", typeof(RagNext.Views.CharacterEditPage));
            Routing.RegisterRoute("PlayerEdit", typeof(RagNext.Views.PlayerEditPage));
            Routing.RegisterRoute("GlobalFunctionEdit", typeof(RagNext.Views.GlobalFunctionEditPage));
            Routing.RegisterRoute("GameTimerEdit", typeof(RagNext.Views.GameTimerEditPage));
            Routing.RegisterRoute(nameof(AISettingsPage), typeof(AISettingsPage));
        }

        private void OnAISettingsChanged(AISettings? settings)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (settings is null)
                {
                    ProviderLabel.Text = "None";
                    ModelPicker.ItemsSource = null;
                    ModelPicker.IsEnabled = false;
                    return;
                }

                ProviderLabel.Text = settings.Provider.ToString();
                ModelPicker.IsEnabled = true;
                LoadAvailableModels(settings);
            });
        }

        private void LoadAvailableModels(AISettings settings)
        {
            if (_isUpdating) return;
            _isUpdating = true;

            try
            {
                var providerKey = "AISettings.CachedModels." + settings.Provider;
                var saved = Preferences.Get(providerKey, "");
                var list = new List<string>();

                if (!string.IsNullOrWhiteSpace(saved))
                {
                    list = saved.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
                    
                    // Keep the OpenRouter dropdown clean (only show free models and MythoMax)
                    if (settings.Provider == AIProviderKind.OpenRouter)
                    {
                        list = list.Where(m => m.Contains(":free") || m == "gryphe/mythomax-l2-13b").ToList();
                    }
                }

                // Fall back to sensible presets if there are no cached models
                if (list.Count == 0)
                {
                    list = settings.Provider switch
                    {
                        AIProviderKind.OpenRouter => new List<string>
                        {
                            "google/gemma-2-9b-it:free",
                            "gryphe/mythomax-l2-13b",
                            "meta-llama/llama-3-8b-instruct:free",
                            "openchat/openchat-7b:free"
                        },
                        AIProviderKind.Ollama => new List<string>
                        {
                            "llama3",
                            "mistral",
                            "gemma",
                            "phi3"
                        },
                        _ => new List<string> // LMStudio & OpenAICompatible
                        {
                            "local-model",
                            "gpt-3.5-turbo",
                            "gpt-4o"
                        }
                    };
                }

                // Safety: ensure current model is always in the dropdown list and selected properly
                var activeModel = settings.Model;
                if (!string.IsNullOrWhiteSpace(activeModel) && !list.Contains(activeModel, StringComparer.OrdinalIgnoreCase))
                {
                    list.Insert(0, activeModel);
                }

                ModelPicker.ItemsSource = list;
                ModelPicker.SelectedItem = activeModel;
            }
            finally
            {
                _isUpdating = false;
            }
        }

        private void OnModelPickerSelectedIndexChanged(object sender, EventArgs e)
        {
            if (_isUpdating || App.CurrentAISettings is null) return;

            var selectedModel = ModelPicker.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(selectedModel)) return;

            _isUpdating = true;
            try
            {
                App.CurrentAISettings.Model = selectedModel;
                _settingsService?.Save(App.CurrentAISettings);
                
                // Force a setter call to notify any other active views/services
                App.CurrentAISettings = App.CurrentAISettings;
            }
            finally
            {
                _isUpdating = false;
            }
        }
    }
}
