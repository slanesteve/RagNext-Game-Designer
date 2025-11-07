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
            if (palette == PaletteTheme.Default) return;
            ApplyThemeDictionary(palette.ToString());
        }
    }
}