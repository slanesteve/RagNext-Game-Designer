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
    public class ExportGameDto
    {
        public string? Title { get; set; }
        public string? Author { get; set; }
        public string? Version { get; set; }
        public string Description { get; set; } = "";
        public ExportPlayerDto? Player { get; set; }
        public List<ExportRoomDto>? Rooms { get; set; }
        public List<ExportObjectDto>? Objects { get; set; }
        public List<ExportObjectDto>? Characters { get; set; }
        public List<ExportVariableDto>? Variables { get; set; }
        public List<ExportMediaAssetDto>? MediaAssets { get; set; }
        public List<ExportFunctionDto>? Functions { get; set; }
        public List<ExportTimerDto>? Timers { get; set; }
        public ExportSplashScreenDto? SplashScreen { get; set; }
    }

    public class ExportPlayerDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Gender { get; set; }
        public string? PortraitImagePath { get; set; }
        public string? StartingRoomId { get; set; }
        public List<ExportObjectDto>? Inventory { get; set; }
        public List<ExportActionDto>? Actions { get; set; }
        public Dictionary<string, string>? Attributes { get; set; }
    }

    public class ExportRoomDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? PortraitImagePath { get; set; }
        public Dictionary<string, string>? Exits { get; set; }
        public Dictionary<string, bool>? LockedExits { get; set; }
        public List<string>? ObjectIds { get; set; }
        public List<ExportActionDto>? Actions { get; set; }
        public Dictionary<string, string>? Attributes { get; set; }
    }

    public class ExportObjectDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? PortraitImagePath { get; set; }
        public bool IsCollectible { get; set; }
        public bool IsCharacter { get; set; }
        public bool IsContainer { get; set; }
        public bool ContainerOpen { get; set; }
        public List<string>? ContainedObjectIds { get; set; }
        public string? StartingRoomId { get; set; }
        public List<ExportActionDto>? Actions { get; set; }
        public List<ExportObjectDto>? Inventory { get; set; }
        public Dictionary<string, string>? Properties { get; set; }
        public Dictionary<string, string>? Attributes { get; set; }
    }

    public class ExportActionDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public bool InitallyActive { get; set; }
        public string? Trigger { get; set; }
        public string? DirectionFilter { get; set; }
        public List<ActionStep>? Nodes { get; set; }
    }

    public class ExportVariableDto
    {
        public string? Name { get; set; }
        public string? Value { get; set; }
        public string? Type { get; set; }
        public List<string> Columns { get; set; } = new List<string>();
        public List<List<string>> Rows { get; set; } = new List<List<string>>();
    }

    public class ExportMediaAssetDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? RelativePath { get; set; }
        public string? MediaType { get; set; }
        public string? OriginalFileName { get; set; }
    }

    public class ExportFunctionDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public List<ActionStep>? Nodes { get; set; }
    }

    public class ExportTimerDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public double IntervalSeconds { get; set; }
        public bool IsActive { get; set; }
        public bool IsRepeating { get; set; }
        public List<ActionStep>? Nodes { get; set; }
        public Dictionary<string, string>? Attributes { get; set; }
    }

    public class ExportSplashScreenDto
    {
        public bool Enabled { get; set; }
        public string? Mode { get; set; }
        public string? ImageAssetId { get; set; }
        public string? SoundAssetId { get; set; }
        public string? Text { get; set; }
        public string? FontName { get; set; }
        public double FontSize { get; set; }
        public string? FontColor { get; set; }
        public double TextX { get; set; }
        public double TextY { get; set; }
        public double FadeInDuration { get; set; }
        public double DisplayDuration { get; set; }
        public double FadeOutDuration { get; set; }
        public string? VideoAssetId { get; set; }
        public string? TransitionStyle { get; set; }
        public double BorderWidth { get; set; }
        public string? BorderColor { get; set; }
        public double BorderRadius { get; set; }
    }

    /// <summary>
    /// Produces Unity-compatible flat game.json with no $id/$ref reference tracking.
    /// </summary>
    public static class GameJsonExporter
    {
        /// <summary>
        /// Serializes the Game to a Unity-compatible flat JSON string.
        /// </summary>
        public static string Export(Game game)
        {
            if (game is null) throw new ArgumentNullException(nameof(game));

            var dto = BuildDto(game);
            return JsonSerializer.Serialize(dto, DesignerJsonContext.Default.ExportGameDto);
        }

        private static ExportGameDto BuildDto(Game game) => new ExportGameDto
        {
            Title = game.Title,
            Author = game.Author,
            Version = game.Version,
            Description = "",
            Player     = BuildPlayerDto(game.Player),
            Rooms      = game.Rooms.Select(r => BuildRoomDto(r)).ToList(),
            Objects    = game.Objects.Select(o => BuildObjectDto(o)).ToList(),
            Characters = game.Characters.Select(c => BuildObjectDto(c)).ToList(),
            Variables  = game.Variables.Select(v => new ExportVariableDto 
            { 
                Name = v.Name, 
                Value = v.Value,
                Type = v.Type,
                Columns = v.Columns.ToList(),
                Rows = v.Rows.Select(row => row.ToList()).ToList()
            }).ToList(),
            MediaAssets= game.MediaAssets.Select(m => new ExportMediaAssetDto
            {
                Id           = m.Id.ToString(),
                Name = m.Name,
                RelativePath = m.RelativePath,
                MediaType    = m.ContentType,
                OriginalFileName = m.OriginalFileName
            }).ToList(),
            Functions  = game.Functions.Select(f => new ExportFunctionDto
            {
                Id    = f.Id.ToString(),
                Name = f.Name,
                Nodes = f.Nodes.ToList()
            }).ToList(),
            Timers     = game.Timers.Select(t => new ExportTimerDto
            {
                Id              = t.Id.ToString(),
                Name = t.Name,
                IntervalSeconds = t.IntervalSeconds,
                IsActive = t.IsActive,
                IsRepeating = t.IsRepeating,
                Nodes           = t.Nodes.ToList(),
                Attributes      = t.Attributes.ToDictionary(a => a.Name, a => a.Value ?? "")
            }).ToList(),
            SplashScreen = game.SplashScreen != null ? new ExportSplashScreenDto
            {
                Enabled = game.SplashScreen.Enabled,
                Mode = game.SplashScreen.Mode,
                ImageAssetId = game.SplashScreen.ImageAssetId,
                SoundAssetId = game.SplashScreen.SoundAssetId,
                Text = game.SplashScreen.Text,
                FontName = game.SplashScreen.FontName,
                FontSize = game.SplashScreen.FontSize,
                FontColor = game.SplashScreen.FontColor,
                TextX = game.SplashScreen.TextX,
                TextY = game.SplashScreen.TextY,
                FadeInDuration = game.SplashScreen.FadeInDuration,
                DisplayDuration = game.SplashScreen.DisplayDuration,
                FadeOutDuration = game.SplashScreen.FadeOutDuration,
                VideoAssetId = game.SplashScreen.VideoAssetId,
                TransitionStyle = game.SplashScreen.TransitionStyle,
                BorderWidth = game.SplashScreen.BorderWidth,
                BorderColor = game.SplashScreen.BorderColor,
                BorderRadius = game.SplashScreen.BorderRadius
            } : null
        };

        private static string NormalizeNewlines(string? val)
        {
            if (string.IsNullOrWhiteSpace(val)) return string.Empty;
            return val.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        private static ExportPlayerDto BuildPlayerDto(Player p) => new ExportPlayerDto
        {
            Id                = p.Id.ToString(),
            Name = p.Name,
            Description       = NormalizeNewlines(p.Description),
            Gender = p.Gender,
            PortraitImagePath = p.PortraitImagePath,
            StartingRoomId    = p.StartingRoom?.Id.ToString(),
            Inventory         = p.Inventory.Select(o => BuildObjectDto(o)).ToList(),
            Actions           = p.Actions.Select(a => BuildActionDto(a)).ToList(),
            Attributes        = p.Attributes.ToDictionary(a => a.Name, a => a.Value ?? "")
        };

        private static ExportRoomDto BuildRoomDto(Room r) => new ExportRoomDto
        {
            Id                = r.Id.ToString(),
            Name = r.Name,
            Description       = NormalizeNewlines(r.Description),
            PortraitImagePath = r.PortraitImagePath,
            Exits             = r.Exits.ToDictionary(k => k.Key, v => v.Value.ToString()),
            LockedExits       = r.LockedExits.ToDictionary(k => k.Key, v => v.Value),
            ObjectIds         = r.ObjectIds.Select(id => id.ToString()).ToList(),
            Actions           = r.Actions.Select(a => BuildActionDto(a)).ToList(),
            Attributes        = r.Attributes.ToDictionary(a => a.Name, a => a.Value ?? "")
        };

        private static ExportObjectDto BuildObjectDto(GameObject o) => new ExportObjectDto
        {
            Id                = o.Id.ToString(),
            Name = o.Name,
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
                                : new List<ExportObjectDto>(),
            Properties        = o.Properties,
            Attributes        = o.Attributes.ToDictionary(a => a.Name, a => a.Value ?? "")
        };

        private static ExportActionDto BuildActionDto(RagsCore.Models.Action a) => new ExportActionDto
        {
            Id           = a.Id.ToString(),
            Name = a.Name,
            InitallyActive = a.InitallyActive,
            Trigger      = a.Trigger.ToString(),
            DirectionFilter = a.DirectionFilter,
            Nodes        = a.Nodes.ToList()
        };
    }
}
