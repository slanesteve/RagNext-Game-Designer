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
            GameObjectData? focusObject)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            return _tokenRegex.Replace(text, match =>
            {
                var path     = match.Groups[1].Value.Trim();
                var resolved = ResolvePath(path, game, currentRoom, focusObject);
                return resolved ?? match.Value;   // leave unknown tokens unchanged
            });
        }

        // ── Core resolver ──────────────────────────────────────────────────────

        private static string? ResolvePath(
            string         path,
            GameData       game,
            RoomData?      currentRoom,
            GameObjectData? focusObject)
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
                        case "name":        return game.Player?.Name;
                        case "gender":      return game.Player?.Gender;
                        case "description": return game.Player?.Description;
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
                        case "name":        return room.Name;
                        case "description": return room.Description;
                        default:            return null;
                    }
                }

                // ── this.* / focus.* (= the entity the action belongs to) ────
                case "this":
                case "focus":
                {
                    // Prefer focusObject; fall back to player if the action is on the player
                    var entity = focusObject;
                    if (entity is null) return null;
                    if (parts.Length < 2) return entity.Name;
                    switch (parts[1].Trim().ToLowerInvariant())
                    {
                        case "name":        return entity.Name;
                        case "description": return entity.Description;
                        case "portrait":
                        case "portraitimagepath":
                            return entity.PortraitImagePath;
                        default:
                            // Try Properties dictionary
                            entity.Properties.TryGetValue(parts[1], out var prop);
                            return prop;
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

        private static string? FindVariable(GameData game, string name) =>
            game.Variables.Find(v =>
                string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase))?.Value;
    }
}
