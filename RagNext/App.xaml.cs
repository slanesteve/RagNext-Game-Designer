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

        private static AISettings? _currentAISettings;
        public static event Action<AISettings?>? AISettingsChanged;
        public static AISettings? CurrentAISettings
        {
            get => _currentAISettings;
            set
            {
                if (ReferenceEquals(_currentAISettings, value))
                    return;
                _currentAISettings = value;
                AISettingsChanged?.Invoke(_currentAISettings);
            }
        }

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

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(MainPage ?? new SplashPage())
            {
                Width = 1280,
                Height = 800,
                MinimumWidth = 1024,
                MinimumHeight = 700,
                Title = "RagNext Designer"
            };

#if WINDOWS
            window.Created += (s, e) =>
            {
                try
                {
                    var native = window.Handler.PlatformView as Microsoft.UI.Xaml.Window;
                    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(native);
                    var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                    var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
                    var area = Microsoft.UI.Windowing.DisplayArea
                        .GetFromWindowId(id, Microsoft.UI.Windowing.DisplayAreaFallback.Nearest)
                        .WorkArea;

                    var w = (int)window.Width;
                    var h = (int)window.Height;
                    var x = area.X + (area.Width - w) / 2;
                    var y = area.Y + (area.Height - h) / 2;

                    appWindow.MoveAndResize(new Windows.Graphics.RectInt32(x, y, w, h));
                }
                catch { /* ignore centering errors */ }
            };
#endif
            return window;
        }
    }
}