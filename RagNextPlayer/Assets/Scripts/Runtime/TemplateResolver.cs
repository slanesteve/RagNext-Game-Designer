#nullable enable
using System;
using System.Text.RegularExpressions;
using RagNextPlayer.Runtime.Models;

namespace RagNextPlayer.Runtime
{
    /// <summary>
    /// Resolves {token} and {dot.path} patterns inside narrative text at runtime.
    /// Mirrors the full capability of RagsCore.Services.TemplateResolver.
    ///
    /// Supported paths:
    ///   player.name, player.gender, player.description
    ///   room.name,   room.description
    ///   this.Name,   this.Description, this.portrait   (= focusObject — the entity the action belongs to)
    ///   focus.Name,  focus.Description, focus.portrait (alias for this.*)
    ///   variables.varName  or  bare varName
    /// </summary>
    public static class TemplateResolver
    {
        private static readonly Regex _tokenRegex =
            new Regex(@"\{([^}]+)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // ── Overloads ──────────────────────────────────────────────────────────

        public static string Resolve(string? text, GameData game) =>
            Resolve(text, game, null, null);

        public static string Resolve(string? text, GameData game, RoomData? currentRoom) =>
            Resolve(text, game, currentRoom, null);

        /// <summary>
        /// Full resolution with optional room and focus-object context.
        /// </summary>
        public static string Resolve(
            string?        text,
            GameData       game,
            RoomData?      currentRoom,
            object?        focusEntity)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            return _tokenRegex.Replace(text, match =>
            {
                var path     = match.Groups[1].Value.Trim();
                var resolved = ResolvePath(path, game, currentRoom, focusEntity);
                return resolved ?? match.Value;   // leave unknown tokens unchanged
            });
        }

        // ── Core resolver ──────────────────────────────────────────────────────

        private static string? ResolvePath(
            string         path,
            GameData       game,
            RoomData?      currentRoom,
            object?        focusEntity)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var parts = path.Split('.');
            var root  = parts[0].Trim().ToLowerInvariant();

            switch (root)
            {
                // ── player.* ─────────────────────────────────────────────────
                case "player":
                    if (parts.Length < 2) return game.Player?.Name;
                    switch (parts[1].Trim().ToLowerInvariant())
                    {
                        case "id":          return game.Player?.Id;
                        case "currentroom":
                        case "currentroomid":
                            return currentRoom?.Id ?? FindVariable(game, "player.currentRoomId");
                        case "name":        return game.Player?.Name;
                        case "gender":      return game.Player?.Gender;
                        case "description": return game.Player?.Description;
                        case "portrait":
                        case "characterportrait":
                        case "portraitimagepath":
                        case "portraitimage":
                            return game.Player?.PortraitImagePath;
                        case "attributes":
                        case "attribute":
                            if (parts.Length < 3 || game.Player?.Attributes == null) return null;
                            game.Player.Attributes.TryGetValue(parts[2].Trim(), out var attrVal);
                            return attrVal;
                        default:            return null;
                    }

                // ── room.* ────────────────────────────────────────────────────
                case "room":
                {
                    var room = currentRoom;
                    if (room is null) return null;
                    if (parts.Length < 2) return room.Name;
                    switch (parts[1].Trim().ToLowerInvariant())
                    {
                        case "id":          return room.Id;
                        case "name":        return room.Name;
                        case "description": return room.Description;
                        case "portrait":
                        case "characterportrait":
                        case "portraitimagepath":
                        case "portraitimage":
                            return room.PortraitImagePath;
                        case "attributes":
                        case "attribute":
                            if (parts.Length < 3 || room.Attributes == null) return null;
                            room.Attributes.TryGetValue(parts[2].Trim(), out var attrVal);
                            return attrVal;
                        default:            return null;
                    }
                }

                // ── this.* / focus.* (= the entity the action belongs to) ────
                case "this":
                case "focus":
                {
                    var entity = focusEntity;
                    if (entity is null) return null;

                    string? id = null;
                    string? name = null;
                    string? description = null;
                    string? portraitImagePath = null;
                    System.Collections.Generic.Dictionary<string, string>? properties = null;
                    System.Collections.Generic.Dictionary<string, string>? attributes = null;

                    if (entity is GameObjectData go)
                    {
                        id = go.Id;
                        name = go.Name;
                        description = go.Description;
                        portraitImagePath = go.PortraitImagePath;
                        properties = go.Properties;
                        attributes = go.Attributes;
                    }
                    else if (entity is PlayerData pl)
                    {
                        id = pl.Id;
                        name = pl.Name;
                        description = pl.Description;
                        portraitImagePath = pl.PortraitImagePath;
                        attributes = pl.Attributes;
                    }
                    else if (entity is RoomData rm)
                    {
                        id = rm.Id;
                        name = rm.Name;
                        description = rm.Description;
                        portraitImagePath = rm.PortraitImagePath;
                        attributes = rm.Attributes;
                    }

                    if (parts.Length < 2) return name;
                    switch (parts[1].Trim().ToLowerInvariant())
                    {
                        case "id":          return id ?? entity.GetType().GetProperty("Id")?.GetValue(entity)?.ToString();
                        case "name":        return name;
                        case "description": return description;
                        case "portrait":
                        case "characterportrait":
                        case "portraitimagepath":
                        case "portraitimage":
                            return portraitImagePath;
                        case "attributes":
                        case "attribute":
                            if (parts.Length < 3 || attributes == null) return null;
                            attributes.TryGetValue(parts[2].Trim(), out var attrVal);
                            return attrVal;
                        default:
                            if (attributes is not null && attributes.TryGetValue(parts[1].Trim(), out var directAttrVal))
                            {
                                return directAttrVal;
                            }
                            if (properties is not null && properties.TryGetValue(parts[1].Trim(), out var prop))
                            {
                                return prop;
                            }
                            return null;
                    }
                }

                // ── variables.* / variable.* ──────────────────────────────────
                case "variables":
                case "variable":
                {
                    if (parts.Length < 2) return null;
                    var varName = string.Join(".", parts, 1, parts.Length - 1).Trim();
                    return FindVariable(game, varName);
                }

                // ── bare variable name ─────────────────────────────────────────
                default:
                    return FindVariable(game, path);
            }
        }

        private static string? FindVariable(GameData game, string name)
        {
            if (name != null && name.Contains(':'))
            {
                var index = name.IndexOf(':');
                var realName = name.Substring(0, index);
                var modifier = name.Substring(index + 1).ToLowerInvariant();
                var modifierVar = game.Variables.Find(v => string.Equals(v.Name, realName, StringComparison.OrdinalIgnoreCase));
                if (modifierVar != null && DateTime.TryParse(modifierVar.Value, out var dt))
                {
                    return modifier switch
                    {
                        "year" => dt.Year.ToString(),
                        "month" => dt.Month.ToString(),
                        "day" => dt.Day.ToString(),
                        "hour" => dt.Hour.ToString(),
                        "minute" => dt.Minute.ToString(),
                        "second" => dt.Second.ToString(),
                        "dayofweek" => ((int)dt.DayOfWeek).ToString(),
                        "date" => dt.ToString("yyyy-MM-dd"),
                        "time" => dt.ToString("HH:mm:ss"),
                        "datetime" => dt.ToString("yyyy-MM-ddTHH:mm:ss"),
                        _ => null
                    };
                }
            }
            var baseVar = game.Variables.Find(v =>
                string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
            if (baseVar != null)
            {
                if (DateTime.TryParse(baseVar.Value, out var dt))
                {
                    return dt.ToString("MMMM d, yyyy h:mm tt");
                }
                return baseVar.Value;
            }
            return null;
        }
    }
}
