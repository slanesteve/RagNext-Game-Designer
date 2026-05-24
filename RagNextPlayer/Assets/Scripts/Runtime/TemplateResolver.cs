using System;
using System.Text.RegularExpressions;
using RagNextPlayer.Runtime.Models;

namespace RagNextPlayer.Runtime
{
    /// <summary>
    /// Resolves {variable.name} tokens inside narrative text at runtime.
    /// Direct port of RagsCore.Services.TemplateResolver.
    /// </summary>
    public static class TemplateResolver
    {
        private static readonly Regex _tokenRegex =
            new Regex(@"\{([^}]+)\}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Replaces every {token} in <paramref name="text"/> with the matching
        /// variable value from the game state. Unknown tokens are left as-is.
        /// </summary>
        public static string Resolve(string? text, GameData game)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            return _tokenRegex.Replace(text, match =>
            {
                var name = match.Groups[1].Value.Trim();
                var variable = game.Variables.Find(
                    v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
                return variable?.Value ?? match.Value;
            });
        }
    }
}
