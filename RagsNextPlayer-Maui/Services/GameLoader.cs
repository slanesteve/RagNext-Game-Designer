using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using RagsCore.Models;
using RagsCore.Actions;

namespace RagsNextPlayer.Services
{
    public static class GameLoader
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            ReferenceHandler = ReferenceHandler.Preserve,
            MaxDepth = 128,
            PropertyNameCaseInsensitive = true,
            Converters = { new StepDefinitionBaseJsonConverter() }
        };

        public static async Task<Game?> LoadFromFileAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return null;

            try
            {
                var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                var game = JsonSerializer.Deserialize<Game>(json, JsonOptions);
                if (game is not null)
                {
                    game.FileName = Path.GetFileNameWithoutExtension(filePath);
                }
                return game;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GameLoader] Error loading game file {filePath}: {ex.Message}");
                return null;
            }
        }
    }
}
