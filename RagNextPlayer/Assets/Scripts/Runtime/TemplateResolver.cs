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
            new Regex(@"\{([^{}]+)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

            var resolved = _tokenRegex.Replace(text, match =>
            {
                var path     = match.Groups[1].Value.Trim();
                var resolved = ResolvePath(path, game, currentRoom, focusEntity);
                return resolved ?? match.Value;   // leave unknown tokens unchanged
            });

            if (resolved != text && resolved.Contains('{') && resolved.Contains('}'))
            {
                resolved = Resolve(resolved, game, currentRoom, focusEntity);
            }

            // Rearrange any AARRGGBB hex colors to RRGGBBAA for Unity UI Toolkit / TMP
            resolved = System.Text.RegularExpressions.Regex.Replace(resolved, @"<(color|mark)=(#[a-f0-9]{8})>", m => {
                var tag = m.Groups[1].Value;
                var color = m.Groups[2].Value; // #AARRGGBB
                var correctedColor = "#" + color.Substring(3) + color.Substring(1, 2);
                return $"<{tag}={correctedColor}>";
            }, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            return resolved;
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
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = parts[i].Trim('[', ']', '"', '\'', ' ');
            }
            var root  = parts[0].ToLowerInvariant();

            switch (root)
            {
                // ── player.* ─────────────────────────────────────────────────
                case "player":
                    if (parts.Length < 2) return game.Player?.Name;
                    switch (parts[1].ToLowerInvariant())
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
                            game.Player.Attributes.TryGetValue(parts[2], out var attrVal);
                            return attrVal;
                        case "wornin":
                        case "wornslot":
                            if (parts.Length < 3 || game.Player == null) return null;
                            var slotItem = game.Player.Inventory.Find(i => i.IsWorn && string.Equals(i.WearSlot, parts[2], StringComparison.OrdinalIgnoreCase));
                            return slotItem?.Name ?? string.Empty;
                        default:            return null;
                    }

                // ── loop.* ────────────────────────────────────────────────────
                case "loop":
                    if (parts.Length > 1)
                    {
                        var suffix = string.Join(".", parts, 1, parts.Length - 1);
                        return FindVariable(game, $"Loop.{suffix}") ?? string.Empty;
                    }
                    return null;

                // ── room.* ────────────────────────────────────────────────────
                case "room":
                {
                    var room = currentRoom;
                    if (room is null) return null;
                    if (parts.Length < 2) return room.Name;
                    switch (parts[1].ToLowerInvariant())
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
                            room.Attributes.TryGetValue(parts[2], out var attrVal);
                            return attrVal;
                        default:            return null;
                    }
                }

                // ── rooms.* (specific room by name) ───────────────────────────
                case "rooms":
                {
                    if (parts.Length < 2) return null;
                    var roomName = parts[1];
                    var room = game.Rooms.Find(r => 
                        string.Equals(r.Name, roomName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(r.Name.Replace(" ", ""), roomName, StringComparison.OrdinalIgnoreCase));
                    if (room == null) return null;
                    if (parts.Length < 3) return room.Name;
                    switch (parts[2].ToLowerInvariant())
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
                            if (parts.Length < 4 || room.Attributes == null) return null;
                            room.Attributes.TryGetValue(parts[3], out var attrVal);
                            return attrVal;
                        default:            return null;
                    }
                }

                // ── objects.* / gameobjects.* / gameobject.* ───────────────────
                case "objects":
                {
                    if (parts.Length < 2) return null;
                    var objName = parts[1];
                    var obj = game.Objects.Find(o => 
                        string.Equals(o.Name, objName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(o.Name.Replace(" ", ""), objName, StringComparison.OrdinalIgnoreCase));
                    if (obj == null) return null;
                    if (parts.Length < 3) return obj.Name;
                    switch (parts[2].ToLowerInvariant())
                    {
                        case "id":          return obj.Id;
                        case "name":        return obj.Name;
                        case "description": return obj.Description;
                        case "portrait":
                        case "characterportrait":
                        case "portraitimagepath":
                        case "portraitimage":
                            return obj.PortraitImagePath;
                        case "attributes":
                        case "attribute":
                            if (parts.Length < 4 || obj.Attributes == null) return null;
                            obj.Attributes.TryGetValue(parts[3], out var attrVal);
                            return attrVal;
                        default:            return null;
                    }
                }
                case "gameobjects":
                case "gameobject":
                {
                    if (parts.Length < 2) return null;
                    var objName = parts[1];
                    var obj = game.Objects.Find(o => 
                        string.Equals(o.Name, objName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(o.Name.Replace(" ", ""), objName, StringComparison.OrdinalIgnoreCase));
                    if (obj == null) return null;
                    if (parts.Length < 3) return obj.Name;
                    switch (parts[2].ToLowerInvariant())
                    {
                        case "id":          return obj.Id;
                        case "name":        return obj.Name;
                        case "description": return obj.Description;
                        case "portrait":
                        case "characterportrait":
                        case "portraitimagepath":
                        case "portraitimage":
                            return obj.PortraitImagePath;
                        case "attributes":
                        case "attribute":
                            if (parts.Length < 4 || obj.Attributes == null) return null;
                            obj.Attributes.TryGetValue(parts[3], out var attrVal);
                            return attrVal;
                        default:            return null;
                    }
                }

                // ── characters.* / character.* ─────────────────────────────────
                case "characters":
                case "character":
                {
                    if (parts.Length < 2) return null;
                    var charName = parts[1];
                    var character = game.Characters.Find(c => 
                        string.Equals(c.Name, charName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.Name.Replace(" ", ""), charName, StringComparison.OrdinalIgnoreCase) ||
                        (c.Properties != null && c.Properties.TryGetValue("OriginalName", out var origName) && (
                            string.Equals(origName, charName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(origName.Replace(" ", ""), charName, StringComparison.OrdinalIgnoreCase)
                        ))
                    );
                    if (character == null) return null;
                    if (parts.Length < 3) return character.Name;
                    switch (parts[2].ToLowerInvariant())
                    {
                        case "id":          return character.Id;
                        case "name":        return character.Name;
                        case "description": return character.Description;
                        case "portrait":
                        case "characterportrait":
                        case "portraitimagepath":
                        case "portraitimage":
                            return character.PortraitImagePath;
                        case "attributes":
                        case "attribute":
                            if (parts.Length < 4 || character.Attributes == null) return null;
                            character.Attributes.TryGetValue(parts[3], out var attrVal);
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
                    switch (parts[1].ToLowerInvariant())
                    {
                        case "id":          return id ?? entity.GetType().GetProperty("Id")?.GetValue(entity)?.ToString();
                        case "name":        return name;
                        case "description": return description;
                        case "wearslot":    return (entity as GameObjectData)?.WearSlot;
                        case "portrait":
                        case "characterportrait":
                        case "portraitimagepath":
                        case "portraitimage":
                            return portraitImagePath;
                        case "attributes":
                        case "attribute":
                            if (parts.Length < 3 || attributes == null) return null;
                            attributes.TryGetValue(parts[2], out var attrVal);
                            return attrVal;
                        default:
                            if (attributes is not null && attributes.TryGetValue(parts[1], out var directAttrVal))
                            {
                                return directAttrVal;
                            }
                            if (properties is not null && properties.TryGetValue(parts[1], out var prop))
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

                    // Check if parts[1] is an array variable
                    var baseVar = game.Variables.Find(v => string.Equals(v.Name, parts[1], StringComparison.OrdinalIgnoreCase));
                    if (baseVar != null && (string.Equals(baseVar.Type ?? "", "array", StringComparison.OrdinalIgnoreCase) || (baseVar.Columns != null && baseVar.Columns.Count > 0)) && parts.Length >= 4)
                    {
                        int rowIndex = -1;
                        string colName = "";
                        if (int.TryParse(parts[2], out var idx1))
                        {
                            rowIndex = idx1;
                            colName = parts[3];
                        }
                        else if (int.TryParse(parts[3], out var idx2))
                        {
                            rowIndex = idx2;
                            colName = parts[2];
                        }

                        if (rowIndex >= 0 && baseVar.Rows != null && rowIndex < baseVar.Rows.Count)
                        {
                            int colIndex = baseVar.Columns.FindIndex(c => string.Equals(c, colName, StringComparison.OrdinalIgnoreCase));
                            if (colIndex >= 0 && colIndex < baseVar.Rows[rowIndex].Count)
                            {
                                return baseVar.Rows[rowIndex][colIndex];
                            }
                        }
                    }

                    var varName = string.Join(".", parts, 1, parts.Length - 1).Trim();
                    return FindVariable(game, varName);
                }

                // ── bare variable name ─────────────────────────────────────────
                default:
                {
                    // Check if parts[0] is an array variable
                    var rootVar = game.Variables.Find(v => string.Equals(v.Name, parts[0], StringComparison.OrdinalIgnoreCase));
                    if (rootVar != null && (string.Equals(rootVar.Type ?? "", "array", StringComparison.OrdinalIgnoreCase) || (rootVar.Columns != null && rootVar.Columns.Count > 0)) && parts.Length >= 3)
                    {
                        int rowIndex = -1;
                        string colName = "";
                        if (int.TryParse(parts[1], out var idx1))
                        {
                            rowIndex = idx1;
                            colName = parts[2];
                        }
                        else if (int.TryParse(parts[2], out var idx2))
                        {
                            rowIndex = idx2;
                            colName = parts[1];
                        }

                        if (rowIndex >= 0 && rootVar.Rows != null && rowIndex < rootVar.Rows.Count)
                        {
                            int colIndex = rootVar.Columns.FindIndex(c => string.Equals(c, colName, StringComparison.OrdinalIgnoreCase));
                            if (colIndex >= 0 && colIndex < rootVar.Rows[rowIndex].Count)
                            {
                                return rootVar.Rows[rowIndex][colIndex];
                            }
                        }
                    }

                    return FindVariable(game, path);
                }
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
                if (string.Equals(baseVar.Type, "datetime", StringComparison.OrdinalIgnoreCase) && DateTime.TryParse(baseVar.Value, out var dt))
                {
                    return dt.ToString("MMMM d, yyyy h:mm tt");
                }
                return baseVar.Value;
            }
            return null;
        }
    }
}
