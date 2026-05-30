using System;

namespace RagNext.Models
{
    public enum DesignerTheme
    {
        System,
        Light,
        Dark
    }

    // New: visual color palette independent from Light/Dark/System
    public enum PaletteTheme
    {
        Default,        // Use built-in Colors.xaml only
        Nord,
        Dracula,
        OneDark,
        Sepia
    }

    public class GeneralSettings
    {
        public DesignerTheme DesignerTheme { get; set; } = DesignerTheme.System;

        // New persisted palette choice
        public PaletteTheme Palette { get; set; } = PaletteTheme.Default;
    }
}