using RagsCore.Models;

namespace RagsNextPlayer
{
    public partial class App : Application
    {
        public static Game? CurrentGame { get; set; }

        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new AppShell())
            {
                Width = 1100,
                Height = 800,
                MinimumWidth = 900,
                MinimumHeight = 600,
                Title = "RagsNext Player"
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
                catch { }
            };
#endif
            return window;
        }
    }
}