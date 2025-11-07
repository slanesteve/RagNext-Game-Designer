using Microsoft.Maui.Storage;
using RagNext.Models;

namespace RagNext.Services
{
    public interface IGeneralSettingsService
    {
        GeneralSettings Load();
        void Save(GeneralSettings settings);
    }

    public sealed class GeneralSettingsService : IGeneralSettingsService
    {
        private const string Prefix = "GeneralSettings.";

        public GeneralSettings Load()
        {
            var themeStr = Preferences.Get(Prefix + nameof(GeneralSettings.DesignerTheme), DesignerTheme.System.ToString());
            var paletteStr = Preferences.Get(Prefix + nameof(GeneralSettings.Palette), PaletteTheme.Default.ToString());

            var parsedTheme = Enum.TryParse(themeStr, out DesignerTheme t) ? t : DesignerTheme.System;
            var parsedPalette = Enum.TryParse(paletteStr, out PaletteTheme p) ? p : PaletteTheme.Default;

            return new GeneralSettings
            {
                DesignerTheme = parsedTheme,
                Palette = (RagNext.Models.PaletteTheme)(int)parsedPalette
            };
        }

        public void Save(GeneralSettings s)
        {
            Preferences.Set(Prefix + nameof(GeneralSettings.DesignerTheme), s.DesignerTheme.ToString());
            Preferences.Set(Prefix + nameof(GeneralSettings.Palette), s.Palette.ToString());
        }
    }
}