using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RagsCore.Models;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.Logging;

namespace RagNext.Services
{
    public static class GameStorage
    {
        private static string SavesDirectory =>
            Path.Combine(FileSystem.Current.AppDataDirectory, "saves");

        private static JsonSerializerOptions Options => new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

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
            Path.Combine(FileSystem.Current.AppDataDirectory, "game.json");

        public static async Task SaveAsync(Game game, string? fileName = null)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                // default filename: sanitized title + timestamp
                var baseName = SanitizeFileName(game.Title ?? "save");
                fileName = $"{baseName}_{DateTime.Now:yyyyMMddHHmmss}";
            }

            EnsureDirectory();
            var fullPath = Path.Combine(SavesDirectory, $"{fileName}.json");
            var json = JsonSerializer.Serialize(game, Options);
            await File.WriteAllTextAsync(fullPath, json).ConfigureAwait(false);

            // optionally keep legacy file for apps that expect it
            try
            {
                var legacyJson = JsonSerializer.Serialize(game, Options);
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
                    return JsonSerializer.Deserialize<Game>(json, Options);
                }

                // Fallback to legacy single-file behavior
                if (!File.Exists(LegacyFilePath))
                    return null;

                attemptedPath = LegacyFilePath;
                var legacyJson = await File.ReadAllTextAsync(LegacyFilePath).ConfigureAwait(false);
                return JsonSerializer.Deserialize<Game>(legacyJson, Options);
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
            return JsonSerializer.Deserialize<Game>(json, Options);
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
    }
}