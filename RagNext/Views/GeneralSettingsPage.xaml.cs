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
                ApplySelection(combinedThemePicker.SelectedIndex);
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
                    PaletteTheme.SolarizedDark => 5,
                    PaletteTheme.SolarizedLight => 6,
                    PaletteTheme.OneDark => 7,
                    PaletteTheme.Monokai => 8,
                    PaletteTheme.GruvboxDark => 9,
                    PaletteTheme.GruvboxLight => 10,
                    PaletteTheme.TrueBlack => 11,
                    PaletteTheme.HighContrast => 12,
                    PaletteTheme.Sepia => 13,
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
            var (designerTheme, palette) = IndexToSelection(index);
            ApplyTheme(designerTheme);
            ApplyPalette(palette);
        }

        private static (DesignerTheme theme, PaletteTheme palette) IndexToSelection(int index) => index switch
        {
            0 => (DesignerTheme.System,     PaletteTheme.Default),
            1 => (DesignerTheme.Light,      PaletteTheme.Default),
            2 => (DesignerTheme.Dark,       PaletteTheme.Default),
            3 => (DesignerTheme.Dark,       PaletteTheme.Nord),
            4 => (DesignerTheme.Dark,       PaletteTheme.Dracula),
            5 => (DesignerTheme.Dark,       PaletteTheme.SolarizedDark),
            6 => (DesignerTheme.Light,      PaletteTheme.SolarizedLight),
            7 => (DesignerTheme.Dark,       PaletteTheme.OneDark),
            8 => (DesignerTheme.Dark,       PaletteTheme.Monokai),
            9 => (DesignerTheme.Dark,       PaletteTheme.GruvboxDark),
            10 => (DesignerTheme.Light,     PaletteTheme.GruvboxLight),
            11 => (DesignerTheme.Dark,      PaletteTheme.TrueBlack),
            12 => (DesignerTheme.System,    PaletteTheme.HighContrast),
            13 => (DesignerTheme.Light,     PaletteTheme.Sepia),
            _ => (DesignerTheme.System,     PaletteTheme.Default)
        };

        private static void ApplyTheme(DesignerTheme theme)
        {
            if (Application.Current is Application app)
            {
                app.UserAppTheme = theme switch
                {
                    DesignerTheme.Light => AppTheme.Light,
                    DesignerTheme.Dark => AppTheme.Dark,
                    _ => AppTheme.Unspecified
                };
            }
        }

        private static void ApplyPalette(PaletteTheme palette)
        {
            ThemeService.ApplyPalette(palette);
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var (designerTheme, palette) = IndexToSelection(CombinedThemePicker.SelectedIndex);
            _settings.DesignerTheme = designerTheme;
            _settings.Palette = palette;
            _service.Save(_settings);
            ApplyTheme(_settings.DesignerTheme);
            ApplyPalette(_settings.Palette);
            await DisplayAlert("Saved", "General settings saved.", "OK");
            await Navigation.PopModalAsync();
        }
    }
}