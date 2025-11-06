using Microsoft.Maui.Controls;
using RagsCore.Models;
using System;
using Microsoft.Maui.ApplicationModel;
using RagNext.Services;
using RagNext.Models;
using System.Diagnostics;
using System.Threading.Tasks;

namespace RagNext
{
    public partial class App : Application
    {
        private static Game? _currentGame;
        public static event Action<Game?>? GameChanged;
        public static Game? CurrentGame
        {
            get => _currentGame;
            set
            {
                if (ReferenceEquals(_currentGame, value))
                    return;

                _currentGame = value;
                GameChanged?.Invoke(_currentGame);

                // Reset each tab to its root page (RoomsPage, GameObjectsPage, etc.)
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    var shell = Shell.Current;
                    if (shell is null) return;

                    var original = shell.CurrentItem;

                    try
                    {
                        await shell.GoToAsync("//rooms", false);
                        await shell.Navigation.PopToRootAsync(false);

                        await shell.GoToAsync("//gameobjects", false);
                        await shell.Navigation.PopToRootAsync(false);

                        await shell.GoToAsync("//main", false);
                        await shell.Navigation.PopToRootAsync(false);

                        // Restore previously selected tab (now reset), or stay on Rooms
                        if (original is not null)
                            shell.CurrentItem = original;
                        else
                            await shell.GoToAsync("//rooms", false);
                    }
                    catch
                    {
                        // Ignore navigation errors
                    }
                });
            }
        }

        public static AISettings? CurrentAISettings { get; set; }

        public App()
        {
            InitializeComponent();

            // Global diagnostics to catch “pink screen” root causes in Release
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                Trace.WriteLine($"[FATAL] {e.ExceptionObject}");
                MainThread.BeginInvokeOnMainThread(async () =>
                    await Current?.MainPage?.DisplayAlert("UnhandledException", e.ExceptionObject?.ToString() ?? "Unknown", "OK"));
            };

            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                e.SetObserved();
                Trace.WriteLine($"[TASK] {e.Exception}");
            };

            // Initialize packaged catalogs (fire-and-forget)
            _ = CommandCatalogLoader.InitializeAsync();
            _ = ConditionCatalogLoader.InitializeAsync();

            MainPage = new SplashPage();

            var aiService = MauiProgram.Services.GetService(typeof(IAISettingsService)) as IAISettingsService;
            if (aiService is not null)
                CurrentAISettings = aiService.Load();
        }
    }
}