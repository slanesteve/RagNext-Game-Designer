using System;
using System.Linq;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using RagNext.Models; // for Color

namespace RagNext.Services
{
    // Add this enum if it does not exist elsewhere in your project
 

    public static class ThemeService
    {
        // Loads a themed ResourceDictionary and pushes its values into Application resources
        public static void ApplyThemeDictionary(string themeName)
        {
            if (Application.Current is not Application app) return;

            RemoveCustomDictionaries(app);

            var typeName = $"RagNext.Resources.Styles.Theme.{themeName}Theme";
            var assembly = typeof(ThemeService).Assembly;
            var type = assembly.GetType(typeName) ?? throw new InvalidOperationException($"Theme type not found: {typeName}");

            var dict = (ResourceDictionary)Activator.CreateInstance(type)!;

            // Push values so DynamicResource updates everywhere immediately
            ApplyResourceOverrides(app, dict);

            // Keep merged for any extra resources not in the base palette
            app.Resources.MergedDictionaries.Add(dict);
        }

        private static void ApplyResourceOverrides(Application app, ResourceDictionary dict)
        {
            foreach (var key in dict.Keys)
            {
                var newVal = dict[key];

                if (app.Resources.TryGetValue(key, out var existing))
                {
                    // Preserve existing brush references by updating Color in-place
                    if (existing is SolidColorBrush existingBrush)
                    {
                        if (newVal is Color c) existingBrush.Color = c;
                        else if (newVal is SolidColorBrush b) existingBrush.Color = b.Color;
                        else app.Resources[key] = newVal;
                    }
                    else
                    {
                        app.Resources[key] = newVal;
                    }
                }
                else
                {
                    app.Resources.Add(key, newVal);
                }
            }
        }

        private static void RemoveCustomDictionaries(Application app)
        {
            var merged = app.Resources.MergedDictionaries;
            for (int i = merged.Count - 1; i >= 0; i--)
            {
                var d = merged.ElementAt(i);
                if (d.GetType().Namespace == "RagNext.Resources.Styles.Theme")
                    merged.Remove(d);
            }
        }

        public static void ApplyPalette(PaletteTheme palette)
        {
            if (Application.Current is not Application app) return;

            RemoveCustomDictionaries(app);

            if (palette == PaletteTheme.Default)
            {
                ApplyDefaultPaletteOverrides(app);
                return;
            }

            ApplyThemeDictionary(palette.ToString());
        }

        private static void ApplyDefaultPaletteOverrides(Application app)
        {
            var defaultDict = new ResourceDictionary
            {
                { "Primary", Color.FromArgb("#512BD4") },
                { "PrimaryDark", Color.FromArgb("#ac99ea") },
                { "PrimaryDarkText", Color.FromArgb("#242424") },
                { "Secondary", Color.FromArgb("#DFD8F7") },
                { "SecondaryDarkText", Color.FromArgb("#9880e5") },
                { "Tertiary", Color.FromArgb("#2B0B98") },
                { "White", Colors.White },
                { "Black", Colors.Black },
                { "OffBlack", Color.FromArgb("#1f1f1f") },
                { "Gray100", Color.FromArgb("#E1E1E1") },
                { "Gray200", Color.FromArgb("#C8C8C8") },
                { "Gray300", Color.FromArgb("#ACACAC") },
                { "Gray400", Color.FromArgb("#919191") },
                { "Gray500", Color.FromArgb("#6E6E6E") },
                { "Gray600", Color.FromArgb("#404040") },
                { "Gray900", Color.FromArgb("#212121") },
                { "Gray950", Color.FromArgb("#141414") },
                { "PrimaryBrush", new SolidColorBrush(Color.FromArgb("#512BD4")) },
                { "SecondaryBrush", new SolidColorBrush(Color.FromArgb("#DFD8F7")) },
                { "TertiaryBrush", new SolidColorBrush(Color.FromArgb("#2B0B98")) },
                { "WhiteBrush", new SolidColorBrush(Colors.White) },
                { "BlackBrush", new SolidColorBrush(Colors.Black) },
                { "Gray100Brush", new SolidColorBrush(Color.FromArgb("#E1E1E1")) },
                { "Gray200Brush", new SolidColorBrush(Color.FromArgb("#C8C8C8")) },
                { "Gray300Brush", new SolidColorBrush(Color.FromArgb("#ACACAC")) },
                { "Gray400Brush", new SolidColorBrush(Color.FromArgb("#919191")) },
                { "Gray500Brush", new SolidColorBrush(Color.FromArgb("#6E6E6E")) },
                { "Gray600Brush", new SolidColorBrush(Color.FromArgb("#404040")) },
                { "Gray900Brush", new SolidColorBrush(Color.FromArgb("#212121")) },
                { "Gray950Brush", new SolidColorBrush(Color.FromArgb("#141414")) }
            };
            ApplyResourceOverrides(app, defaultDict);
        }

        public static Color GetPrimaryColor()
        {
            if (Application.Current?.Resources.TryGetValue("Primary", out var val) == true)
            {
                if (val is Color c) return c;
                if (val is SolidColorBrush b) return b.Color;
            }
            return Color.FromArgb("#512BD4");
        }

        public static void UpdateWindowTitleBarColors(AppTheme theme)
        {
#if WINDOWS
            try
            {
                if (Application.Current is not Application app) return;
                var window = app.Windows.FirstOrDefault();
                if (window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow) return;

                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                var id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);

                if (appWindow?.TitleBar != null && Microsoft.UI.Windowing.AppWindowTitleBar.IsCustomizationSupported())
                {
                    var titleBar = appWindow.TitleBar;
                    bool isDark = theme == AppTheme.Dark || (theme == AppTheme.Unspecified && app.RequestedTheme == AppTheme.Dark);

                    if (isDark)
                    {
                        titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 240, 240, 245);
                        titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                        titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(40, 255, 255, 255);
                        titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255);
                        titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(60, 255, 255, 255);
                        titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 120, 120, 130);
                    }
                    else
                    {
                        titleBar.ButtonForegroundColor = Windows.UI.Color.FromArgb(255, 30, 30, 35);
                        titleBar.ButtonHoverForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
                        titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(15, 0, 0, 0);
                        titleBar.ButtonPressedForegroundColor = Windows.UI.Color.FromArgb(255, 0, 0, 0);
                        titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(30, 0, 0, 0);
                        titleBar.ButtonInactiveForegroundColor = Windows.UI.Color.FromArgb(255, 150, 150, 160);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to update window title bar colors: {ex.Message}");
            }
#endif
        }
    }
}