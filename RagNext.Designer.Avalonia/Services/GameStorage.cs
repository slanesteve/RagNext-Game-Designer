using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RagsCore.Models;
using Microsoft.Extensions.Logging;
using RagsCore.Actions;

namespace RagNext.Designer.Avalonia.Services
{
    public static class GameStorage
    {
        private static string AppDataDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagNext");

        private static string SavesDirectory =>
            Path.Combine(AppDataDirectory, "saves");



        // Optional: configured from MauiProgram
        private static ILogger? _logger;
        public static void ConfigureLogger(ILogger logger) => _logger = logger;

        private static void EnsureDirectory()
        {
            if (!Directory.Exists(SavesDirectory))
                Directory.CreateDirectory(SavesDirectory);
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            return string.IsNullOrEmpty(sanitized) ? "save" : sanitized;
        }

        private static string LegacyFilePath =>
            Path.Combine(AppDataDirectory, "game.json");

        public static async Task SaveAsync(Game game, string? fileName = null, bool isExplicitUserSave = false)
        {
            // If this is a quick/auto save (not an explicit user-triggered save with a custom name),
            // and we have a tracked loaded filename, redirect back to that filename.
            if (!isExplicitUserSave && !string.IsNullOrWhiteSpace(game.FileName))
            {
                fileName = game.FileName;
            }

            if (string.IsNullOrWhiteSpace(fileName))
            {
                // default filename: sanitized title + timestamp
                var baseName = SanitizeFileName(game.Title ?? "save");
                fileName = $"{baseName}_{DateTime.Now:yyyyMMddHHmmss}";
            }

            EnsureDirectory();
            var fullPath = Path.Combine(SavesDirectory, $"{fileName}.json");
            System.Diagnostics.Debug.WriteLine($"[DEBUG] GameStorage.SaveAsync: Player.StartingRoom is {(game.Player?.StartingRoom != null ? game.Player.StartingRoom.Name : "null")}");
            Console.WriteLine($"[DEBUG] GameStorage.SaveAsync: Player.StartingRoom is {(game.Player?.StartingRoom != null ? game.Player.StartingRoom.Name : "null")}");
            var json = JsonSerializer.Serialize(game, RagsCore.RagsJsonContext.CustomDefault.Game);
            await File.WriteAllTextAsync(fullPath, json).ConfigureAwait(false);

            // Track the successfully saved filename on the game object
            game.FileName = fileName;

            // optionally keep legacy file for apps that expect it
            try
            {
                var legacyJson = JsonSerializer.Serialize(game, RagsCore.RagsJsonContext.CustomDefault.Game);
                await File.WriteAllTextAsync(LegacyFilePath, legacyJson).ConfigureAwait(false);
            }
            catch
            {
                // ignore legacy write failures
            }
        }

        // Load the most-recent save (keeps original behavior)
        public static async Task<Game?> LoadAsync()
        {
            string? attemptedPath = null;
            try
            {
                // Prefer most recent file in saves folder
                EnsureDirectory();
                var files = Directory.GetFiles(SavesDirectory, "*.json");
                if (files.Length > 0)
                {
                    var latest = files.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).First();
                    attemptedPath = latest;
                    var json = await File.ReadAllTextAsync(latest).ConfigureAwait(false);
                    json = ActionStep.NormalizeLegacyDiscriminators(json);
                    var game = JsonSerializer.Deserialize(json, RagsCore.RagsJsonContext.CustomDefault.Game);
                    if (game is not null)
                    {
                        game.FileName = Path.GetFileNameWithoutExtension(latest);
                    }
                    return game;
                }

                // Fallback to legacy single-file behavior
                if (!File.Exists(LegacyFilePath))
                    return null;

                attemptedPath = LegacyFilePath;
                var legacyJson = await File.ReadAllTextAsync(LegacyFilePath).ConfigureAwait(false);
                legacyJson = ActionStep.NormalizeLegacyDiscriminators(legacyJson);
                var legacyGame = JsonSerializer.Deserialize(legacyJson, RagsCore.RagsJsonContext.CustomDefault.Game);
                if (legacyGame is not null)
                {
                    legacyGame.FileName = "game";
                }
                return legacyGame;
            }
            catch (Exception err)
            {
                _logger?.LogError(err, "Failed to load game. Path: {Path}", attemptedPath ?? "(unknown)");
                System.Diagnostics.Trace.TraceError($"Failed to load game. Path: {attemptedPath ?? "(unknown)"}; {err}");
                return null;
            }
        }

        // Load a specific named save (name without extension)
        public static async Task<Game?> LoadAsync(string fileName)
        {
            EnsureDirectory();
            var candidate = Path.Combine(SavesDirectory, $"{fileName}.json");
            if (!File.Exists(candidate))
                return null;

            var json = await File.ReadAllTextAsync(candidate).ConfigureAwait(false);
            json = ActionStep.NormalizeLegacyDiscriminators(json);
            var game = JsonSerializer.Deserialize(json, RagsCore.RagsJsonContext.CustomDefault.Game);
            if (game is not null)
            {
                game.FileName = fileName;
            }
            return game;
        }

        // List available saves (file names without extension)
        public static Task<string[]> ListSavesAsync()
        {
            EnsureDirectory();
            var files = Directory.GetFiles(SavesDirectory, "*.json");
            var names = files
                .Select(Path.GetFileNameWithoutExtension)
                .OrderByDescending(n =>
                {
                    var full = Path.Combine(SavesDirectory, $"{n}.json");
                    return File.GetLastWriteTimeUtc(full);
                })
                .ToArray();
            return Task.FromResult(names);
        }

        // Delete a specific named save
        public static Task DeleteAsync(string name)
        {
            EnsureDirectory();
            var sanitized = SanitizeFileName(name);
            var fullPath = Path.Combine(SavesDirectory, $"{sanitized}.json");
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
            return Task.CompletedTask;
        }
    }
}
