using Microsoft.Maui.Controls;
using RagNext.Models;
using RagNext.Services;
using System;


namespace RagNext.Views
{
    public partial class GeneralSettingsPage : ContentPage
    {
        private readonly IGeneralSettingsService _service;
        private GeneralSettings _settings = new();
        private bool _isApplyingSelection = false;

        public GeneralSettingsPage()
        {
            InitializeComponent();
            var combinedThemePicker = this.FindByName<Picker>("CombinedThemePicker");
            if (combinedThemePicker == null)
                throw new InvalidOperationException("CombinedThemePicker not found in XAML.");

            _service = MauiProgram.Services.GetService(typeof(IGeneralSettingsService)) as IGeneralSettingsService
                       ?? throw new InvalidOperationException("GeneralSettingsService not registered.");
            _settings = _service.Load();
            BindSettingsToUI(combinedThemePicker);

            combinedThemePicker.SelectedIndexChanged += (_, __) =>
            {
                int currentIdx = combinedThemePicker.SelectedIndex;
                System.Diagnostics.Debug.WriteLine($"[Picker] SelectedIndexChanged: {currentIdx}, _isApplyingSelection={_isApplyingSelection}");
                if (_isApplyingSelection) return;
                
                int selectedIndex = combinedThemePicker.SelectedIndex;
                if (selectedIndex < 0) return;

                // Postpone selection application asynchronously to let the picker dropdown fully close
                // before we trigger a theme/layout redraw.
                Dispatcher.Dispatch(async () =>
                {
                    await Task.Delay(100); // Wait for the native dropdown popover to fully dismiss
                    if (_isApplyingSelection) return;
                    try
                    {
                        _isApplyingSelection = true;
                        ApplySelection(selectedIndex);

                        // Asynchronously restore/confirm the selected index to keep UI in perfect sync
                        if (combinedThemePicker.SelectedIndex != selectedIndex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Picker] Restoring SelectedIndex to {selectedIndex}");
                            combinedThemePicker.SelectedIndex = selectedIndex;
                        }
                    }
                    finally
                    {
                        _isApplyingSelection = false;
                    }
                });
            };
        }

        private void BindSettingsToUI(Picker combinedThemePicker)
        {
            // If a non-default palette is selected, map directly to that index.
            if (_settings.Palette != PaletteTheme.Default)
            {
                combinedThemePicker.SelectedIndex = _settings.Palette switch
                {
                    PaletteTheme.Nord => 3,
                    PaletteTheme.Dracula => 4,
                    PaletteTheme.OneDark => 5,
                    PaletteTheme.Sepia => 6,
                    _ => 0
                };
            }
            else
            {
                combinedThemePicker.SelectedIndex = _settings.DesignerTheme switch
                {
                    DesignerTheme.Light => 1,
                    DesignerTheme.Dark => 2,
                    _ => 0
                };
            }
        }

        private void ApplySelection(int index)
        {
            if (index < 0) return;
            var (designerTheme, palette) = IndexToSelection(index);
            System.Diagnostics.Debug.WriteLine($"[ApplySelection] index={index}, targetTheme={designerTheme}, targetPalette={palette}");

            // 1. Always apply the palette colors in real-time for instant, safe preview!
            ApplyPalette(palette);

            // 2. Only apply theme changes immediately if it matches the current theme (no-op)
            // to prevent WinUI 3 modal layout sweep deadlocks.
            if (Application.Current is Application app)
            {
                var targetAppTheme = designerTheme switch
                {
                    DesignerTheme.Light => AppTheme.Light,
                    DesignerTheme.Dark => AppTheme.Dark,
                    _ => AppTheme.Unspecified
                };

                if (app.UserAppTheme == targetAppTheme)
                {
                    ApplyTheme(designerTheme);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ApplySelection] Postponing cross-theme change ({app.UserAppTheme} -> {targetAppTheme}) until modal closes.");
                }
            }
        }

        private static (DesignerTheme theme, PaletteTheme palette) IndexToSelection(int index) => index switch
        {
            0 => (DesignerTheme.System,     PaletteTheme.Default),
            1 => (DesignerTheme.Light,      PaletteTheme.Default),
            2 => (DesignerTheme.Dark,       PaletteTheme.Default),
            3 => (DesignerTheme.Dark,       PaletteTheme.Nord),
            4 => (DesignerTheme.Dark,       PaletteTheme.Dracula),
            5 => (DesignerTheme.Dark,       PaletteTheme.OneDark),
            6 => (DesignerTheme.Light,      PaletteTheme.Sepia),
            _ => (DesignerTheme.System,     PaletteTheme.Default)
        };

        private static void ApplyTheme(DesignerTheme theme)
        {
            System.Diagnostics.Debug.WriteLine($"[ApplyTheme] theme={theme}");
            if (Application.Current is Application app)
            {
                var appTheme = theme switch
                {
                    DesignerTheme.Light => AppTheme.Light,
                    DesignerTheme.Dark => AppTheme.Dark,
                    _ => AppTheme.Unspecified
                };

#if WINDOWS
                try
                {
                    var window = app.Windows.FirstOrDefault()?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;
                    if (window?.Content is Microsoft.UI.Xaml.FrameworkElement element)
                    {
                        var targetElementTheme = appTheme switch
                        {
                            AppTheme.Light => Microsoft.UI.Xaml.ElementTheme.Light,
                            AppTheme.Dark => Microsoft.UI.Xaml.ElementTheme.Dark,
                            _ => Microsoft.UI.Xaml.ElementTheme.Default
                        };
                        System.Diagnostics.Debug.WriteLine($"[ApplyTheme] Setting native WinUI 3 RequestedTheme to {targetElementTheme}");
                        element.RequestedTheme = targetElementTheme;
                    }
                    ThemeService.UpdateWindowTitleBarColors(appTheme);
                    
                    // Set app.UserAppTheme safely so that AppThemeBindings on all pages redraw correctly
                    if (app.UserAppTheme != appTheme)
                    {
                        app.UserAppTheme = appTheme;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ApplyTheme] Native error: {ex.Message}");
                }
#else
                System.Diagnostics.Debug.WriteLine($"[ApplyTheme] app.UserAppTheme={app.UserAppTheme} -> setting to {appTheme}");
                app.UserAppTheme = appTheme;
#endif
            }
        }

        private static void ApplyPalette(PaletteTheme palette)
        {
            System.Diagnostics.Debug.WriteLine($"[ApplyPalette] palette={palette}");
            ThemeService.ApplyPalette(palette);
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var (designerTheme, palette) = IndexToSelection(CombinedThemePicker.SelectedIndex);
            _settings.DesignerTheme = designerTheme;
            _settings.Palette = palette;
            _service.Save(_settings);
            
            // Pop the modal first so the WinUI modal host is removed from the visual tree
            await Navigation.PopModalAsync();

            // Safely apply the main application theme shift on the next UI tick
            Dispatcher.Dispatch(() =>
            {
                ApplyTheme(designerTheme);
            });
        }

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            var originalSettings = _service.Load();
            ApplyPalette(originalSettings.Palette);
            
            await Navigation.PopModalAsync();
            
            Dispatcher.Dispatch(() =>
            {
                ApplyTheme(originalSettings.DesignerTheme);
            });
        }
    }
}