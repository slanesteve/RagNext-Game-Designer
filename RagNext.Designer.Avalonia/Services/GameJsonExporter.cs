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
        public List<ExportSplashScreenDto>? SplashScreens { get; set; }
        public ExportThemeSettingsDto? Theme { get; set; }
        public List<ExportStatusBarElementDto>? StatusBarElements { get; set; }
        public List<string>? WearSlots { get; set; }
    }

    public class ExportStatusBarElementDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? VisualOption { get; set; }
        public string? Text { get; set; }
        public string? TextColor { get; set; }
        public string? MediaAssetId { get; set; }
        public bool IsVisible { get; set; }
    }

    public class ExportPlayerDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? Gender { get; set; }
        public bool ShowGender { get; set; }
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
        public ExportInteractiveScreenSettingsDto? InteractiveScreenSettings { get; set; }
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
        public bool IsWearable { get; set; }
        public bool IsWorn { get; set; }
        public string? WearSlot { get; set; }
        public Dictionary<string, string>? Attributes { get; set; }
        public ExportInteractiveScreenSettingsDto? InteractiveScreenSettings { get; set; }
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
        public string Name { get; set; } = string.Empty;
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

    public class ExportInteractiveScreenSettingsDto
    {
        public bool Enabled { get; set; }
        public string? BackdropAssetId { get; set; }
        public bool ShowCloseButton { get; set; } = true;
        public string? OnCloseActionId { get; set; }
        public List<ExportScreenHotspotDto>? Hotspots { get; set; }
    }

    public class ExportScreenHotspotDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public string? StyleType { get; set; }
        public string? LabelText { get; set; }
        public string? FontColor { get; set; }
        public double FontSize { get; set; }
        public string? BackgroundColor { get; set; }
        public string? ImageAssetId { get; set; }
        public string? LinkedActionId { get; set; }
        public bool IsActive { get; set; } = true;
        public bool EnableHoverScale { get; set; } = true;
    }

    public class ExportThemeSettingsDto
    {
        public string? PrimaryBgColor { get; set; }
        public string? TextMainColor { get; set; }
        public string? BorderAccentColor { get; set; }
        public string? FontName { get; set; }
        public string? FontAssetId { get; set; }
        public string? BackgroundAssetId { get; set; }
        public string? FrameAssetId { get; set; }
        public string? InventoryDockPosition { get; set; }
        public string? RoomItemsDockPosition { get; set; }
        public string? NavigationDockPosition { get; set; }
        public double PanelPadding { get; set; }
        public double BorderRadius { get; set; }
        public double AspectRatio { get; set; }
        public string? TextBoxAlignment { get; set; }
        public double TextBoxWidth { get; set; }
        public double TextBoxHeight { get; set; }
        public string? PortraitAlignment { get; set; }
        public double SidebarWidth { get; set; }
        public double BottomBarHeight { get; set; }
        public string? ActivePreset { get; set; }
        public double FontSize { get; set; }
        public bool FrameApplyToGameScreen { get; set; }
        public bool FrameApplyToMainText { get; set; }
        public bool FrameApplyToPopups { get; set; }
        public bool FrameApplyToSidebars { get; set; }
        public double BorderThickness { get; set; }
        public string? PlayerStatusBoxShape { get; set; }
        public string? PlayerPortraitShape { get; set; }
        public double PortraitSize { get; set; }
        public string? MapStyle { get; set; }
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
            Theme = game.Theme == null ? null : new ExportThemeSettingsDto
            {
                PrimaryBgColor = game.Theme.PrimaryBgColor,
                TextMainColor = game.Theme.TextMainColor,
                BorderAccentColor = game.Theme.BorderAccentColor,
                FontName = game.Theme.FontName,
                FontAssetId = game.Theme.FontAssetId,
                BackgroundAssetId = game.Theme.BackgroundAssetId,
                FrameAssetId = game.Theme.FrameAssetId,
                InventoryDockPosition = game.Theme.InventoryDockPosition,
                RoomItemsDockPosition = game.Theme.RoomItemsDockPosition,
                NavigationDockPosition = game.Theme.NavigationDockPosition,
                PanelPadding = game.Theme.PanelPadding,
                BorderRadius = game.Theme.BorderRadius,
                AspectRatio = game.Theme.AspectRatio,
                TextBoxAlignment = game.Theme.TextBoxAlignment,
                TextBoxWidth = game.Theme.TextBoxWidth,
                TextBoxHeight = game.Theme.TextBoxHeight,
                PortraitAlignment = game.Theme.PortraitAlignment,
                SidebarWidth = game.Theme.SidebarWidth,
                BottomBarHeight = game.Theme.BottomBarHeight,
                ActivePreset = game.Theme.ActivePreset,
                FontSize = game.Theme.FontSize,
                FrameApplyToGameScreen = game.Theme.FrameApplyToGameScreen,
                FrameApplyToMainText = game.Theme.FrameApplyToMainText,
                FrameApplyToPopups = game.Theme.FrameApplyToPopups,
                FrameApplyToSidebars = game.Theme.FrameApplyToSidebars,
                BorderThickness = game.Theme.BorderThickness,
                PlayerStatusBoxShape = game.Theme.PlayerStatusBoxShape,
                PlayerPortraitShape = game.Theme.PlayerPortraitShape,
                PortraitSize = game.Theme.PortraitSize,
                MapStyle = game.Theme.MapStyle
            },
            Player     = BuildPlayerDto(game.Player),
            Rooms      = game.Rooms.Select(r => BuildRoomDto(r)).ToList(),
            Objects    = game.Objects.Select(o => BuildObjectDto(o)).ToList(),
            Characters = game.Characters.Select(c => BuildObjectDto(c)).ToList(),
            WearSlots  = game.WearSlots.ToList(),
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
            } : null,
            SplashScreens = game.SplashScreens?.Select(s => new ExportSplashScreenDto
            {
                Name = s.Name ?? string.Empty,
                Enabled = s.Enabled,
                Mode = s.Mode,
                ImageAssetId = s.ImageAssetId,
                SoundAssetId = s.SoundAssetId,
                Text = s.Text,
                FontName = s.FontName,
                FontSize = s.FontSize,
                FontColor = s.FontColor,
                TextX = s.TextX,
                TextY = s.TextY,
                FadeInDuration = s.FadeInDuration,
                DisplayDuration = s.DisplayDuration,
                FadeOutDuration = s.FadeOutDuration,
                VideoAssetId = s.VideoAssetId,
                TransitionStyle = s.TransitionStyle,
                BorderWidth = s.BorderWidth,
                BorderColor = s.BorderColor,
                BorderRadius = s.BorderRadius
            }).ToList(),
            StatusBarElements = game.StatusBarElements.Select(s => new ExportStatusBarElementDto
            {
                Id = s.Id.ToString(),
                Name = s.Name,
                VisualOption = s.VisualOption,
                Text = s.Text,
                TextColor = s.TextColor,
                MediaAssetId = s.MediaAssetId?.ToString(),
                IsVisible = s.IsVisible
            }).ToList()
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
            ShowGender = p.ShowGender,
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
            Attributes        = r.Attributes.ToDictionary(a => a.Name, a => a.Value ?? ""),
            InteractiveScreenSettings = BuildInteractiveScreenSettingsDto(r.InteractiveScreenSettings)
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
            IsWearable        = o.IsWearable,
            IsWorn            = o.IsWorn,
            WearSlot          = o.WearSlot,
            Attributes        = o.Attributes.ToDictionary(a => a.Name, a => a.Value ?? ""),
            InteractiveScreenSettings = BuildInteractiveScreenSettingsDto(o.InteractiveScreenSettings)
        };

        private static ExportInteractiveScreenSettingsDto? BuildInteractiveScreenSettingsDto(InteractiveScreenSettings? s)
        {
            if (s == null) return null;
            return new ExportInteractiveScreenSettingsDto
            {
                Enabled = s.Enabled,
                BackdropAssetId = s.BackdropAssetId,
                ShowCloseButton = s.ShowCloseButton,
                OnCloseActionId = s.OnCloseActionId,
                Hotspots = s.Hotspots?.Select(h => new ExportScreenHotspotDto
                {
                    Id = h.Id,
                    Name = h.Name,
                    X = h.X,
                    Y = h.Y,
                    Width = h.Width,
                    Height = h.Height,
                    StyleType = h.StyleType,
                    LabelText = h.LabelText,
                    FontColor = h.FontColor,
                    FontSize = h.FontSize,
                    BackgroundColor = h.BackgroundColor,
                    ImageAssetId = h.ImageAssetId,
                    LinkedActionId = h.LinkedActionId,
                    IsActive = h.IsActive,
                    EnableHoverScale = h.EnableHoverScale
                }).ToList()
            };
        }

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
