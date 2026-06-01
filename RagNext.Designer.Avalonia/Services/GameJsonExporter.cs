using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using RagsCore.Actions;
using RagsCore.Models;

namespace RagNext.Designer.Avalonia.Services
{
    /// <summary>
    /// Produces Unity-compatible flat game.json with no $id/$ref reference tracking.
    ///
    /// The key difference from the Designer's internal save format:
    ///   - No ReferenceHandler.Preserve  → clean, flat JSON Unity can deserialize
    ///   - Player.StartingRoom → Player.StartingRoomId (Guid string) to break circular ref
    ///   - All collections serialized as plain JSON arrays (not $values wrappers)
    ///   - ActionStep polymorphism handled by StepDefinitionBaseJsonConverter ($type field)
    /// </summary>
    public static class GameJsonExporter
    {
        private static readonly JsonSerializerOptions _options = BuildOptions();

        private static JsonSerializerOptions BuildOptions()
        {
            var opts = new JsonSerializerOptions
            {
                WriteIndented        = true,
                PropertyNamingPolicy = null,                // PascalCase to match C# models
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                MaxDepth             = 128
            };
            opts.Converters.Add(new StepDefinitionBaseJsonConverter());
            return opts;
        }

        /// <summary>
        /// Serializes the Game to a Unity-compatible flat JSON string.
        /// </summary>
        public static string Export(Game game)
        {
            if (game is null) throw new ArgumentNullException(nameof(game));

            // Build a plain DTO — avoids circular references without reference tracking
            var dto = BuildDto(game);
            return JsonSerializer.Serialize(dto, _options);
        }

        // ── DTO construction ─────────────────────────────────────────────────
        // We use anonymous objects / dictionaries to produce clean, flat JSON
        // without carrying MAUI/ObservableCollection dependencies into the output.

        private static object BuildDto(Game game) => new
        {
            game.Title,
            game.Author,
            game.Version,
            Description = "",
            Player     = BuildPlayerDto(game.Player),
            Rooms      = game.Rooms.Select(r => BuildRoomDto(r)).ToList(),
            Objects    = game.Objects.Select(o => BuildObjectDto(o)).ToList(),
            Characters = game.Characters.Select(c => BuildObjectDto(c)).ToList(),
            Variables  = game.Variables.Select(v => new { v.Name, v.Value }).ToList(),
            MediaAssets= game.MediaAssets.Select(m => new
            {
                Id           = m.Id.ToString(),
                m.Name,
                m.RelativePath,
                MediaType    = m.ContentType,
                m.OriginalFileName
            }).ToList(),
            Functions  = game.Functions.Select(f => new
            {
                Id    = f.Id.ToString(),
                f.Name,
                Nodes = f.Nodes.ToList()
            }).ToList(),
            Timers     = game.Timers.Select(t => new
            {
                Id              = t.Id.ToString(),
                t.Name,
                t.IntervalSeconds,
                t.IsActive,
                t.IsRepeating,
                Nodes           = t.Nodes.ToList()
            }).ToList(),
            SplashScreen = game.SplashScreen != null ? new
            {
                game.SplashScreen.Enabled,
                game.SplashScreen.Mode,
                game.SplashScreen.ImageAssetId,
                game.SplashScreen.SoundAssetId,
                game.SplashScreen.Text,
                game.SplashScreen.FontName,
                game.SplashScreen.FontSize,
                game.SplashScreen.FontColor,
                game.SplashScreen.TextX,
                game.SplashScreen.TextY,
                game.SplashScreen.FadeInDuration,
                game.SplashScreen.DisplayDuration,
                game.SplashScreen.FadeOutDuration,
                game.SplashScreen.VideoAssetId,
                game.SplashScreen.TransitionStyle
            } : null
        };

        private static string NormalizeNewlines(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return string.Empty;
            return val.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static object BuildPlayerDto(Player p) => new
        {
            Id                = p.Id.ToString(),
            p.Name,
            Description       = NormalizeNewlines(p.Description),
            p.Gender,
            PortraitImagePath = p.PortraitImagePath,
            // Avoid circular reference: store room ID, not the full Room object
            StartingRoomId    = p.StartingRoom?.Id.ToString(),
            Inventory         = p.Inventory.Select(o => BuildObjectDto(o)).ToList(),
            Actions           = p.Actions.Select(a => BuildActionDto(a)).ToList()
        };

        private static object BuildRoomDto(Room r) => new
        {
            Id                = r.Id.ToString(),
            r.Name,
            Description       = NormalizeNewlines(r.Description),
            PortraitImagePath = r.PortraitImagePath,
            Exits             = r.Exits.ToDictionary(k => k.Key, v => v.Value.ToString()),
            LockedExits       = r.LockedExits.ToDictionary(k => k.Key, v => v.Value),
            ObjectIds         = r.ObjectIds.Select(id => id.ToString()).ToList(),
            Actions           = r.Actions.Select(a => BuildActionDto(a)).ToList()
        };

        private static object BuildObjectDto(GameObject o) => new
        {
            Id                = o.Id.ToString(),
            o.Name,
            Description       = NormalizeNewlines(o.Description),
            PortraitImagePath = (o as Character)?.PortraitImagePath ?? o.PortraitImagePath,
            IsCollectible     = o.IsCollectible,
            IsCharacter       = o is Character,
            IsContainer       = o.IsContainer,
            ContainerOpen     = o.ContainerOpen,
            ContainedObjectIds = o.ContainedObjectIds.Select(id => id.ToString()).ToList(),
            StartingRoomId    = (o as Character)?.StartingRoom?.Id.ToString(),
            Actions           = o.Actions.Select(a => BuildActionDto(a)).ToList(),
            Inventory         = o is Character ch
                                ? ch.Inventory.Select(i => BuildObjectDto(i)).ToList()
                                : new List<object>(),
            Properties        = o.Properties
        };

        private static object BuildActionDto(RagsCore.Models.Action a) => new
        {
            Id           = a.Id.ToString(),
            a.Name,
            a.InitallyActive,
            Trigger      = a.Trigger.ToString(),
            // ActionSteps are already serializable via StepDefinitionBaseJsonConverter
            Nodes        = a.Nodes.ToList()
        };
    }
}
