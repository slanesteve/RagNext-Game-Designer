using System;
using System.Linq;
using System.Text.RegularExpressions;
using RagsCore.Actions;
using RagsCore.Models;

namespace RagsCore.Services
{
    public static class TemplateResolver
    {
        private static readonly Regex TokenRegex = new Regex(@"\{([^{}]+)\}", RegexOptions.Compiled);

        public static string Resolve(string? text, ActionContext? ctx)
        {
            if (string.IsNullOrEmpty(text) || ctx == null)
            {
                return text ?? string.Empty;
            }

            return TokenRegex.Replace(text, match =>
            {
                var path = match.Groups[1].Value.Trim();
                var resolved = ResolvePath(path, ctx);
                return resolved ?? match.Value; // Fallback to original token if unresolved
            });
        }

        private static string? ResolvePath(string path, ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;

            var parts = path.Split('.')
                            .Select(p => p.Trim('[', ']', '"', '\'', ' '))
                            .ToArray();

            if (parts.Length == 0) return null;

            var root = parts[0].ToLowerInvariant();

            switch (root)
            {
                case "player":
                    return ResolvePlayer(parts, ctx);

                case "room":
                    return ResolveRoom(parts, ctx);

                case "focus":
                case "object":
                case "this":
                case "self":
                    return ResolveFocus(parts, ctx);

                case "variables":
                case "variable":
                    return ResolveVariable(parts, ctx);

                case "characters":
                case "character":
                    return ResolveCharacter(parts, ctx);

                case "objects":
                case "gameobjects":
                case "gameobject":
                    return ResolveGameObject(parts, ctx);

                default:
                    // If no prefix, check if it matches a variable name directly
                    var directVar = ctx.GetVariable(path)?.Value;
                    if (directVar != null) return directVar;

                    var gameVarDirect = ctx.Game.Variables.FirstOrDefault(v => string.Equals(v.Name, path, StringComparison.OrdinalIgnoreCase));
                    if (gameVarDirect != null) return gameVarDirect.Value;

                    // Also support direct suffix fallback e.g. {my_var.value}
                    if (path.EndsWith(".value", StringComparison.OrdinalIgnoreCase))
                    {
                        var stripped = path.Substring(0, path.Length - 6);
                        var v = ctx.GetVariable(stripped) ?? ctx.Game.Variables.FirstOrDefault(x => string.Equals(x.Name, stripped, StringComparison.OrdinalIgnoreCase));
                        if (v != null) return v.Value;
                    }
                    else if (path.EndsWith(".name", StringComparison.OrdinalIgnoreCase))
                    {
                        var stripped = path.Substring(0, path.Length - 5);
                        var v = ctx.GetVariable(stripped) ?? ctx.Game.Variables.FirstOrDefault(x => string.Equals(x.Name, stripped, StringComparison.OrdinalIgnoreCase));
                        if (v != null) return v.Name;
                    }
                    return null;
            }
        }

        private static string? ResolvePlayer(string[] parts, ActionContext ctx)
        {
            if (parts.Length < 2) return ctx.Player.Name;

            var prop = parts[1].ToLowerInvariant();
            switch (prop)
            {
                case "name":
                    return ctx.Player.Name;
                case "description":
                    return ctx.Player.Description;
                case "gender":
                    return ctx.Player.Gender;
                case "portrait":
                case "characterportrait":
                case "portraitimagepath":
                case "portraitimage":
                    return ctx.Player.PortraitImagePath;
                case "attributes":
                case "attribute":
                    if (parts.Length < 3) return null;
                    var attrName = parts[2];
                    return CustomAttribute.GetAttribute(attrName, ctx.Player.Attributes);
                default:
                    return null;
            }
        }

        private static string? ResolveRoom(string[] parts, ActionContext ctx)
        {
            var room = ctx.CurrentRoom;
            if (room == null) return null;

            if (parts.Length < 2) return room.Name;

            var prop = parts[1].ToLowerInvariant();
            switch (prop)
            {
                case "name":
                    return room.Name;
                case "description":
                    return room.Description;
                case "portrait":
                case "characterportrait":
                case "portraitimagepath":
                case "portraitimage":
                    return room.PortraitImagePath;
                case "attributes":
                case "attribute":
                    if (parts.Length < 3) return null;
                    var attrName = parts[2];
                    return CustomAttribute.GetAttribute(attrName, room.Attributes);
                default:
                    return null;
            }
        }

        private static string? ResolveFocus(string[] parts, ActionContext ctx)
        {
            var focus = ctx.FocusEntity ?? ctx.FocusObject;
            if (focus == null) return null;

            string? name = null;
            string? description = null;
            string? portraitImagePath = null;
            System.Collections.IEnumerable? attributes = null;

            if (focus is GameObject go)
            {
                name = go.Name;
                description = go.Description;
                portraitImagePath = go.PortraitImagePath;
                attributes = go.Attributes;
            }
            else if (focus is Player pl)
            {
                name = pl.Name;
                description = pl.Description;
                portraitImagePath = pl.PortraitImagePath;
                attributes = pl.Attributes;
            }
            else if (focus is Room rm)
            {
                name = rm.Name;
                description = rm.Description;
                portraitImagePath = rm.PortraitImagePath;
                attributes = rm.Attributes;
            }

            if (name == null)
            {
                var type = focus.GetType();
                name = type.GetProperty("Name")?.GetValue(focus) as string;
                description = type.GetProperty("Description")?.GetValue(focus) as string;
                portraitImagePath = type.GetProperty("PortraitImagePath")?.GetValue(focus) as string;
                attributes = type.GetProperty("Attributes")?.GetValue(focus) as System.Collections.IEnumerable;
            }

            if (parts.Length < 2) return name ?? focus.ToString();

            var prop = parts[1].ToLowerInvariant();
            switch (prop)
            {
                case "name":
                    return name;
                case "description":
                    return description;
                case "gender":
                    if (focus is Player p) return p.Gender;
                    return focus.GetType().GetProperty("Gender")?.GetValue(focus) as string;
                case "portrait":
                case "characterportrait":
                case "portraitimagepath":
                case "portraitimage":
                    return portraitImagePath;
                case "attributes":
                case "attribute":
                    if (parts.Length < 3 || attributes == null) return null;
                    var attrName = parts[2];
                    if (attributes is System.Collections.ObjectModel.ObservableCollection<CustomAttribute> customAttrs)
                    {
                        return CustomAttribute.GetAttribute(attrName, customAttrs);
                    }
                    return null;
                default:
                    if (attributes is System.Collections.ObjectModel.ObservableCollection<CustomAttribute> defaultAttrs)
                    {
                        var directVal = CustomAttribute.GetAttribute(parts[1], defaultAttrs);
                        if (directVal != null) return directVal;
                    }
                    return null;
            }
        }

        private static string? ResolveVariable(string[] parts, ActionContext ctx)
        {
            if (parts.Length < 2) return null;
            var varName = string.Join(".", parts.Skip(1));
            
            // First check exact match in ActionContext
            var exactVar = ctx.GetVariable(varName);
            if (exactVar != null) return exactVar.Value;

            // Otherwise check case-insensitive match in game variables list
            var gameVar = ctx.Game.Variables.FirstOrDefault(v => string.Equals(v.Name, varName, StringComparison.OrdinalIgnoreCase));
            if (gameVar != null) return gameVar.Value;

            // If not found, and varName ends with .value or .name, handle accordingly
            if (varName.EndsWith(".value", StringComparison.OrdinalIgnoreCase))
            {
                var stripped = varName.Substring(0, varName.Length - 6);
                var v = ctx.GetVariable(stripped) ?? ctx.Game.Variables.FirstOrDefault(x => string.Equals(x.Name, stripped, StringComparison.OrdinalIgnoreCase));
                if (v != null) return v.Value;
            }
            else if (varName.EndsWith(".name", StringComparison.OrdinalIgnoreCase))
            {
                var stripped = varName.Substring(0, varName.Length - 5);
                var v = ctx.GetVariable(stripped) ?? ctx.Game.Variables.FirstOrDefault(x => string.Equals(x.Name, stripped, StringComparison.OrdinalIgnoreCase));
                if (v != null) return v.Name;
            }
            return null;
        }

        private static string? ResolveCharacter(string[] parts, ActionContext ctx)
        {
            if (parts.Length < 2) return null;
            var charName = parts[1];
            var character = ctx.Game.Characters.FirstOrDefault(c => string.Equals(c.Name, charName, StringComparison.OrdinalIgnoreCase));
            if (character == null) return null;

            if (parts.Length < 3) return character.Name;

            var prop = parts[2].ToLowerInvariant();
            switch (prop)
            {
                case "name":
                    return character.Name;
                case "description":
                    return character.Description;
                case "ishostile":
                    return character.IsHostile.ToString();
                case "health":
                    return character.Health.ToString();
                case "portrait":
                case "characterportrait":
                case "portraitimagepath":
                case "portraitimage":
                    return character.PortraitImagePath;
                case "attributes":
                case "attribute":
                    if (parts.Length < 4) return null;
                    var attrName = parts[3];
                    return CustomAttribute.GetAttribute(attrName, character.Attributes);
                default:
                    return null;
            }
        }

        private static string? ResolveGameObject(string[] parts, ActionContext ctx)
        {
            if (parts.Length < 2) return null;
            var objName = parts[1];
            var obj = ctx.Game.Objects.FirstOrDefault(o => string.Equals(o.Name, objName, StringComparison.OrdinalIgnoreCase));
            if (obj == null) return null;

            if (parts.Length < 3) return obj.Name;

            var prop = parts[2].ToLowerInvariant();
            switch (prop)
            {
                case "name":
                    return obj.Name;
                case "description":
                    return obj.Description;
                case "iscollectible":
                    return obj.IsCollectible.ToString();
                case "portrait":
                case "characterportrait":
                case "portraitimagepath":
                case "portraitimage":
                    return obj.PortraitImagePath;
                case "attributes":
                case "attribute":
                    if (parts.Length < 4) return null;
                    var attrName = parts[3];
                    return CustomAttribute.GetAttribute(attrName, obj.Attributes);
                default:
                    return null;
            }
        }
    }
}
