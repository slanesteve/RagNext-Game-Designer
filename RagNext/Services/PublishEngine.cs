using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using RagsCore.Models;
using RagsCore.Actions;

namespace RagNext.Services
{
    public static class PublishEngine
    {
        private static JsonSerializerOptions Options
        {
            get
            {
                var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web)
                {
                    WriteIndented = true,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
                    MaxDepth = 128,
                    PropertyNameCaseInsensitive = true
                };
                opts.Converters.Add(new StepDefinitionBaseJsonConverter());
                return opts;
            }
        }

        public static async Task PublishAsync(Game game, int targetPlatform, string destinationPath)
        {
            if (game == null) throw new ArgumentNullException(nameof(game));
            if (string.IsNullOrWhiteSpace(destinationPath)) throw new ArgumentException("Destination path cannot be empty", nameof(destinationPath));

            // Clean title for folders and files
            string cleanTitle = SanitizeName(game.Title ?? "Adventure");

            if (targetPlatform == 2)
            {
                // Target: Universal Mobile/Hub Package (.rag)
                await PackageAsRagAsync(game, destinationPath, cleanTitle);
            }
            else
            {
                // Target: Windows Desktop (0) or macOS Desktop (1)
                await PackageAsStandaloneAsync(game, targetPlatform, destinationPath, cleanTitle);
            }
        }

        private static async Task PackageAsStandaloneAsync(Game game, int targetPlatform, string destinationPath, string cleanTitle)
        {
            // 1. Create target output directory
            Directory.CreateDirectory(destinationPath);

            // 2. Resolve source binary template folder
            string runDir = AppDomain.CurrentDomain.BaseDirectory;
            string platformSubDir = targetPlatform == 0 ? "Windows" : "MacCatalyst";
            string bundledDir = Path.Combine(runDir, "Templates", platformSubDir);
            string sourceBinDir;

            if (Directory.Exists(bundledDir))
            {
                sourceBinDir = bundledDir;
            }
            else
            {
                // Fallback to local developer workspace
                string projectRoot = GetWorkspaceRoot();
                if (targetPlatform == 0)
                {
                    // Windows build directory
                    sourceBinDir = Path.Combine(projectRoot, "RagsNextPlayer", "bin", "Debug", "net9.0-windows10.0.19041.0", "win10-x64");
                }
                else
                {
                    // macOS catalyst directory fallback
                    sourceBinDir = Path.Combine(projectRoot, "RagsNextPlayer", "bin", "Debug", "net9.0-maccatalyst");
                }

                if (!Directory.Exists(sourceBinDir))
                {
                    // Fallback: Check if there's any published output or standard bin dir
                    sourceBinDir = Path.Combine(projectRoot, "RagsNextPlayer", "bin", "Debug", "net9.0-windows10.0.19041.0");
                    if (!Directory.Exists(sourceBinDir))
                    {
                        throw new DirectoryNotFoundException($"Could not locate pre-compiled player templates next to the executable at:\n{bundledDir}\n\nor at the developer fallback paths:\n{sourceBinDir}\n\nPlease bundle templates or compile the RagsNextPlayer project in Visual Studio first.");
                    }
                }
            }

            // 3. Copy player binaries recursively to destination
            CopyDirectory(sourceBinDir, destinationPath, true);

            // 4. Export serialized game data as game.json
            string json = JsonSerializer.Serialize(game, Options);
            string gameJsonPath = Path.Combine(destinationPath, "game.json");
            await File.WriteAllTextAsync(gameJsonPath, json);

            // 5. Copy game assets
            string srcAssetsDir = Path.Combine(FileSystem.AppDataDirectory, game.Id.ToString("N"), "Assets");
            string destAssetsDir = Path.Combine(destinationPath, "Assets");

            if (Directory.Exists(srcAssetsDir))
            {
                Directory.CreateDirectory(destAssetsDir);
                foreach (var asset in game.MediaAssets)
                {
                    if (string.IsNullOrEmpty(asset.RelativePath)) continue;
                    
                    // RelativePath is usually "Assets\filename" or "Assets/filename"
                    string assetFileName = Path.GetFileName(asset.RelativePath);
                    string srcFile = Path.Combine(srcAssetsDir, assetFileName);
                    string destFile = Path.Combine(destAssetsDir, assetFileName);

                    if (File.Exists(srcFile))
                    {
                        File.Copy(srcFile, destFile, true);
                    }
                }
            }

            // 6. Rename executable to match game title
            if (targetPlatform == 0)
            {
                string oldExe = Path.Combine(destinationPath, "RagsNextPlayer.exe");
                string newExe = Path.Combine(destinationPath, $"{cleanTitle}.exe");
                if (File.Exists(oldExe))
                {
                    if (File.Exists(newExe)) File.Delete(newExe);
                    File.Move(oldExe, newExe);
                }
            }
        }

        private static async Task PackageAsRagAsync(Game game, string destinationPath, string cleanTitle)
        {
            // 1. Create temporary working folder
            string tempDir = Path.Combine(FileSystem.AppDataDirectory, "TempPublish_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                // 2. Serialize game.json
                string json = JsonSerializer.Serialize(game, Options);
                string gameJsonPath = Path.Combine(tempDir, "game.json");
                await File.WriteAllTextAsync(gameJsonPath, json);

                // 3. Copy referenced active media assets
                string srcAssetsDir = Path.Combine(FileSystem.AppDataDirectory, game.Id.ToString("N"), "Assets");
                string destAssetsDir = Path.Combine(tempDir, "Assets");

                if (Directory.Exists(srcAssetsDir))
                {
                    Directory.CreateDirectory(destAssetsDir);
                    foreach (var asset in game.MediaAssets)
                    {
                        if (string.IsNullOrEmpty(asset.RelativePath)) continue;

                        string assetFileName = Path.GetFileName(asset.RelativePath);
                        string srcFile = Path.Combine(srcAssetsDir, assetFileName);
                        string destFile = Path.Combine(destAssetsDir, assetFileName);

                        if (File.Exists(srcFile))
                        {
                            File.Copy(srcFile, destFile, true);
                        }
                    }
                }

                // 4. Zip target zip path (.rag)
                string outputZipPath = destinationPath;
                if (!outputZipPath.EndsWith(".rag", StringComparison.OrdinalIgnoreCase))
                {
                    // If destinationPath is a directory, make a file path in it
                    if (Directory.Exists(outputZipPath))
                    {
                        outputZipPath = Path.Combine(outputZipPath, $"{cleanTitle}.rag");
                    }
                    else
                    {
                        outputZipPath += ".rag";
                    }
                }

                // Ensure parent directory of output zip exists
                string? parentDir = Path.GetDirectoryName(outputZipPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                if (File.Exists(outputZipPath))
                {
                    File.Delete(outputZipPath);
                }

                ZipFile.CreateFromDirectory(tempDir, outputZipPath);
            }
            finally
            {
                // 5. Cleanup temporary folder
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private static void CopyDirectory(string sourceDir, string destinationDir, bool recursive)
        {
            var dir = new DirectoryInfo(sourceDir);
            if (!dir.Exists)
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");

            Directory.CreateDirectory(destinationDir);

            foreach (var file in dir.GetFiles())
            {
                string targetFilePath = Path.Combine(destinationDir, file.Name);
                file.CopyTo(targetFilePath, true);
            }

            if (recursive)
            {
                foreach (var subDir in dir.GetDirectories())
                {
                    string newDestinationDir = Path.Combine(destinationDir, subDir.Name);
                    CopyDirectory(subDir.FullName, newDestinationDir, true);
                }
            }
        }

        private static string GetWorkspaceRoot()
        {
            // We search upwards from AppDataDirectory or use the active user directory mapping
            // c:\Users\steve\source\repos\RagNext is our target workspace
            string checkPath = @"c:\Users\steve\source\repos\RagNext";
            if (Directory.Exists(checkPath))
            {
                return checkPath;
            }

            // Fallback: scan relative to running directory
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "RagNext.sln")))
                {
                    return current;
                }
                current = Path.GetDirectoryName(current)!;
            }

            return AppDomain.CurrentDomain.BaseDirectory;
        }

        private static string SanitizeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            return string.IsNullOrEmpty(clean) ? "Adventure" : clean.Replace(" ", "_");
        }
    }
}
