using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using RagsCore.Models;

namespace RagNext.Designer.Avalonia.Services
{
    /// <summary>
    /// Publishes a standalone branded game executable by injecting the author's
    /// game data into a pre-built Unity shell player template.
    ///
    /// How it works:
    ///   1. Locate the correct Templates/{Platform}/ folder next to the Designer exe.
    ///   2. Copy the entire template to the output directory.
    ///   3. Rename RagNextPlayer.exe → MyGame.exe (and MyGame_Data/ on Windows/Linux).
    ///   4. Write game.json + media assets into StreamingAssets/.
    ///   5. Optionally zip the output for distribution.
    ///
    /// The end user receives a folder (or zip) containing their game branded with
    /// the game title. They just double-click the executable — no Unity required.
    /// </summary>
    public static class PublishEngine
    {
        private static string AppDataDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagNext");

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Reports progress steps during publish. Each string is a completed step.
        /// </summary>
        public static event Action<string>? OnProgress;

        /// <summary>
        /// Main entry point. Validates inputs then dispatches to the correct target handler.
        /// </summary>
        public static async Task PublishAsync(
            Game game,
            PackagingTarget target,
            string outputDirectory,
            bool createZip = false)
        {
            if (game is null)                       throw new ArgumentNullException(nameof(game));
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("Output directory cannot be empty.", nameof(outputDirectory));

            string cleanTitle = SanitizeName(game.Title ?? "Adventure");

            // Validate template exists before doing any file work
            string templateDir = GetTemplateDir(target);
            if (!Directory.Exists(templateDir))
                throw new DirectoryNotFoundException(
                    $"Shell template not found at:\n{templateDir}\n\n" +
                    $"Build the Unity player for {target} once from Unity, then copy the output into:\n{templateDir}");

            Report("Validating game data...");
            ValidateGame(game);

            Report($"Preparing output folder: {outputDirectory}");
            SafeClearDestination(templateDir, outputDirectory, cleanTitle, target);
            Directory.CreateDirectory(outputDirectory);

            switch (target)
            {
                case PackagingTarget.Windows:
                    await PublishWindowsAsync(game, cleanTitle, templateDir, outputDirectory);
                    break;
                case PackagingTarget.MacOS:
                    await PublishMacOSAsync(game, cleanTitle, templateDir, outputDirectory);
                    break;
                case PackagingTarget.Linux:
                    await PublishLinuxAsync(game, cleanTitle, templateDir, outputDirectory);
                    break;
                case PackagingTarget.WebGL:
                    await PublishWebGLAsync(game, cleanTitle, templateDir, outputDirectory);
                    break;
            }

            if (createZip)
            {
                Report("Creating distribution ZIP...");
                string cleanOutput = outputDirectory.TrimEnd(Path.DirectorySeparatorChar);
                string parentDir = Path.GetDirectoryName(cleanOutput) ?? cleanOutput;
                string zipName = cleanTitle + ".zip";
                string tempZipPath = Path.Combine(parentDir, zipName);

                CreateZipWithUnixPermissions(outputDirectory, tempZipPath);

                // Move the completed ZIP inside the output folder next to the executable/.app
                string finalZipPath = Path.Combine(outputDirectory, zipName);
                if (File.Exists(finalZipPath)) File.Delete(finalZipPath);
                File.Move(tempZipPath, finalZipPath);

                Report($"ZIP created: {zipName} (saved inside the export folder)");
            }

            Report("✅ Publish complete!");
        }

        // ── Windows ───────────────────────────────────────────────────────────
        // Output: MyGame/
        //           MyGame.exe
        //           MyGame_Data/
        //             StreamingAssets/game.json + Assets/
        //           UnityPlayer.dll
        //           (other Unity runtime DLLs)

        private static async Task PublishWindowsAsync(Game game, string title, string templateDir, string outputDir)
        {
            Report("Copying Windows shell player...");
            CopyDirectory(templateDir, outputDir);

            // Rename exe
            RenameFile(outputDir, "RagNextPlayer.exe", $"{title}.exe");

            // Rename _Data folder
            RenameDirectory(outputDir, "RagNextPlayer_Data", $"{title}_Data");

            // Inject game data
            string streamingDir = Path.Combine(outputDir, $"{title}_Data", "StreamingAssets");
            await InjectGameDataAsync(game, streamingDir);

        }

        // ── macOS ─────────────────────────────────────────────────────────────
        // Output: MyGame.app/
        //           Contents/
        //             MacOS/MyGame          ← renamed binary
        //             Resources/Data/
        //               StreamingAssets/game.json + Assets/

        private static async Task PublishMacOSAsync(Game game, string title, string templateDir, string outputDir)
        {
            Report("Copying macOS shell player...");
            string appBundle = Path.Combine(outputDir, $"{title}.app");

            // Avoid nested .app directory (inception structure)
            string sourceApp = Path.Combine(templateDir, "MacOS.app");
            if (!Directory.Exists(sourceApp))
            {
                sourceApp = Directory.GetDirectories(templateDir, "*.app").FirstOrDefault() ?? templateDir;
            }
            CopyDirectory(sourceApp, appBundle);

            // Unity macOS .app has the binary in Contents/MacOS/RagNextPlayer
            string macOsDir = Path.Combine(appBundle, "Contents", "MacOS");
            string targetBinary = Path.Combine(macOsDir, title);
            RenameFile(macOsDir, "RagNextPlayer", title);

            // Set +x execution permissions for Unix filesystems
            try
            {
                if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
                {
                    File.SetUnixFileMode(targetBinary,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
            }
            catch (Exception ex)
            {
                Report($"Warning: Failed to set executable permissions: {ex.Message}");
            }

            // Update the Info.plist CFBundleName (simple string replacement — no XML lib needed)
            string plistPath = Path.Combine(appBundle, "Contents", "Info.plist");
            if (File.Exists(plistPath))
            {
                string plist = await File.ReadAllTextAsync(plistPath);
                plist = plist.Replace("RagNextPlayer", title);
                await File.WriteAllTextAsync(plistPath, plist);
            }

            // StreamingAssets lives under Contents/Resources/Data/
            string streamingDir = Path.Combine(appBundle, "Contents", "Resources", "Data", "StreamingAssets");
            await InjectGameDataAsync(game, streamingDir);
        }

        // ── Linux ─────────────────────────────────────────────────────────────
        // Same structure as Windows but executable has no extension and uses ELF format

        private static async Task PublishLinuxAsync(Game game, string title, string templateDir, string outputDir)
        {
            Report("Copying Linux shell player...");
            CopyDirectory(templateDir, outputDir);

            string targetBinary = Path.Combine(outputDir, title);
            RenameFile(outputDir, "RagNextPlayer", title);
            RenameDirectory(outputDir, "RagNextPlayer_Data", $"{title}_Data");

            // Set +x execution permissions for Unix filesystems
            try
            {
                if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
                {
                    File.SetUnixFileMode(targetBinary,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                        UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
                }
            }
            catch (Exception ex)
            {
                Report($"Warning: Failed to set executable permissions: {ex.Message}");
            }

            string streamingDir = Path.Combine(outputDir, $"{title}_Data", "StreamingAssets");
            await InjectGameDataAsync(game, streamingDir);
        }

        // ── WebGL ─────────────────────────────────────────────────────────────
        // Output: MyGame/
        //           index.html
        //           Build/
        //           StreamingAssets/game.json + Assets/
        //           TemplateData/

        private static async Task PublishWebGLAsync(Game game, string title, string templateDir, string outputDir)
        {
            Report("Copying WebGL shell player...");
            CopyDirectory(templateDir, outputDir);

            // Update the page title in index.html
            string indexPath = Path.Combine(outputDir, "index.html");
            if (File.Exists(indexPath))
            {
                string html = await File.ReadAllTextAsync(indexPath);
                html = html.Replace("RagNextPlayer", title)
                           .Replace("<title>Unity WebGL Player", $"<title>{title}");
                await File.WriteAllTextAsync(indexPath, html);
            }

            // WebGL StreamingAssets is directly at the root
            string streamingDir = Path.Combine(outputDir, "StreamingAssets");
            await InjectGameDataAsync(game, streamingDir);
        }

        // ── Game Data Injection ───────────────────────────────────────────────

        /// <summary>
        /// Writes game.json and copies all referenced media into the StreamingAssets folder.
        /// </summary>
        private static async Task InjectGameDataAsync(Game game, string streamingAssetsDir)
        {
            Directory.CreateDirectory(streamingAssetsDir);

            // 1. Write flat game.json (Unity-compatible, no $id/$ref tracking)
            Report("Writing game.json...");
            string json        = GameJsonExporter.Export(game);
            string gameJsonPath= Path.Combine(streamingAssetsDir, "game.json");
            await File.WriteAllTextAsync(gameJsonPath, json);
            Report($"  game.json written ({new FileInfo(gameJsonPath).Length / 1024} KB)");

            // 2. Copy media assets
            string srcAssetsDir  = Path.Combine(AppDataDirectory, game.Id.ToString("N"), "Assets");
            string destAssetsDir = Path.Combine(streamingAssetsDir, "Assets");

            int copiedCount = 0;
            if (Directory.Exists(srcAssetsDir) && game.MediaAssets.Count > 0)
            {
                Directory.CreateDirectory(destAssetsDir);
                foreach (var asset in game.MediaAssets)
                {
                    if (string.IsNullOrWhiteSpace(asset.RelativePath)) continue;
                    string assetFileName = Path.GetFileName(asset.RelativePath);
                    string srcFile       = Path.Combine(srcAssetsDir, assetFileName);
                    string destFile      = Path.Combine(destAssetsDir, assetFileName);

                    if (File.Exists(srcFile))
                    {
                        File.Copy(srcFile, destFile, overwrite: true);
                        copiedCount++;
                    }
                }
            }
            Report($"  {copiedCount} media asset(s) copied.");

            // 3. Copy theme presets
            string srcPresetsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagNext", "Themes");
            string fallbackPresetsDir = Path.Combine(AppContext.BaseDirectory, "Presets");
            string destPresetsDir = Path.Combine(streamingAssetsDir, "Presets");
            int copiedPresets = 0;

            void CopyPresetsFromFolder(string folderPath)
            {
                if (Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(destPresetsDir);
                    foreach (var file in Directory.GetFiles(folderPath, "*.json"))
                    {
                        string fileName = Path.GetFileName(file);
                        string destFile = Path.Combine(destPresetsDir, fileName);
                        File.Copy(file, destFile, overwrite: true);
                        copiedPresets++;
                    }
                }
            }

            CopyPresetsFromFolder(srcPresetsDir);
            CopyPresetsFromFolder(fallbackPresetsDir);

            Report($"  {copiedPresets} theme preset(s) copied.");

        }

        // ── Template Resolution ────────────────────────────────────────────────

        /// <summary>
        /// Returns the path to the shell template for the given target.
        /// Looks next to the running Designer executable, then falls back
        /// to the developer workspace for local testing.
        /// </summary>
        public static string GetTemplateDir(PackagingTarget target)
        {
            string subDir = target switch
            {
                PackagingTarget.Windows => "Windows",
                PackagingTarget.MacOS   => "MacOS",
                PackagingTarget.Linux   => "Linux",
                PackagingTarget.WebGL   => "WebGL",
                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };

            string exeDir = AppDomain.CurrentDomain.BaseDirectory;

            // On macOS inside an app bundle, check Contents/Resources/Templates
            if (OperatingSystem.IsMacOS())
            {
                string resourcesTemplates = Path.Combine(exeDir, "..", "Resources", "Templates", subDir);
                if (Directory.Exists(resourcesTemplates)) return resourcesTemplates;
            }

            // Primary: Templates/ folder next to the running Designer exe
            string primary = Path.Combine(exeDir, "Templates", subDir);
            if (Directory.Exists(primary)) return primary;

            // Developer fallback: workspace root Templates/
            string workspace = GetWorkspaceRoot();
            return Path.Combine(workspace, "Templates", subDir);
        }

        /// <summary>
        /// Returns true if the template for a given target is available.
        /// Used by the UI to show which platforms are ready to publish.
        /// </summary>
        public static bool IsTemplateAvailable(PackagingTarget target)
            => Directory.Exists(GetTemplateDir(target));

        /// <summary>
        /// Returns a summary of what is in the game — used for the publish confirmation dialog.
        /// </summary>
        public static PublishSummary GetPublishSummary(Game game) => new PublishSummary
        {
            Title        = game.Title ?? "Untitled",
            Author       = game.Author ?? "",
            Version      = game.Version ?? "1.0.0",
            RoomCount    = game.Rooms.Count,
            ObjectCount  = game.Objects.Count,
            CharacterCount = game.Characters.Count,
            VariableCount= game.Variables.Count,
            MediaCount   = game.MediaAssets.Count
        };

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void Report(string message) => OnProgress?.Invoke(message);

        public static void SafeClearDestination(string templateDir, string outputDir, string title, PackagingTarget target)
        {
            if (!Directory.Exists(outputDir)) return;

            // 1. Delete the ZIP file if it exists
            string cleanOutputDir = outputDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string folderZipName = Path.GetFileName(cleanOutputDir) + ".zip";
            string titleZipName = title + ".zip";
            
            string folderZipPath = Path.Combine(outputDir, folderZipName);
            string titleZipPath = Path.Combine(outputDir, titleZipName);
            
            if (File.Exists(folderZipPath))
            {
                try { File.Delete(folderZipPath); } catch { }
            }
            if (File.Exists(titleZipPath) && !string.Equals(folderZipPath, titleZipPath, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(titleZipPath); } catch { }
            }

            // 2. Clear only the template-specific files and folders from the destination
            try
            {
                if (!Directory.Exists(templateDir)) return;
                var templateItems = Directory.GetFileSystemEntries(templateDir);
                foreach (var item in templateItems)
                {
                    string name = Path.GetFileName(item);
                    string targetName = name;

                    // Map renamed template items to their build output names
                    if (target == PackagingTarget.Windows)
                    {
                        if (name == "RagNextPlayer.exe") targetName = $"{title}.exe";
                        else if (name == "RagNextPlayer_Data") targetName = $"{title}_Data";
                    }
                    else if (target == PackagingTarget.MacOS)
                    {
                        if (name.EndsWith(".app", StringComparison.OrdinalIgnoreCase) || name == "MacOS.app")
                            targetName = $"{title}.app";
                    }
                    else if (target == PackagingTarget.Linux)
                    {
                        if (name == "RagNextPlayer") targetName = title;
                        else if (name == "RagNextPlayer_Data") targetName = $"{title}_Data";
                    }

                    string destPath = Path.Combine(outputDir, targetName);
                    if (File.Exists(destPath))
                    {
                        File.Delete(destPath);
                    }
                    else if (Directory.Exists(destPath))
                    {
                        Directory.Delete(destPath, recursive: true);
                    }
                }
            }
            catch (Exception ex)
            {
                Report($"Warning during safe clear: {ex.Message}");
            }
        }

        private static void ValidateGame(Game game)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(game.Title))  errors.Add("Game title is required.");
            if (game.Rooms.Count == 0)                  errors.Add("The game must have at least one room.");
            if (errors.Count > 0)
                throw new InvalidOperationException("Cannot publish:\n• " + string.Join("\n• ", errors));
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(source, file);
                string destFile = Path.Combine(destination, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                File.Copy(file, destFile, overwrite: true);
            }
        }

        private static void RenameFile(string directory, string oldName, string newName)
        {
            string oldPath = Path.Combine(directory, oldName);
            string newPath = Path.Combine(directory, newName);
            if (File.Exists(oldPath))
            {
                if (File.Exists(newPath)) File.Delete(newPath);
                File.Move(oldPath, newPath);
            }
        }

        private static void RenameDirectory(string parentDir, string oldName, string newName)
        {
            string oldPath = Path.Combine(parentDir, oldName);
            string newPath = Path.Combine(parentDir, newName);
            if (Directory.Exists(oldPath))
                Directory.Move(oldPath, newPath);
        }

        private static string GetWorkspaceRoot()
        {
            string current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                if (File.Exists(Path.Combine(current, "RagNext.sln"))) return current;
                current = Path.GetDirectoryName(current)!;
            }
            return AppDomain.CurrentDomain.BaseDirectory;
        }

        public static string SanitizeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var clean   = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim();
            return string.IsNullOrEmpty(clean) ? "Adventure" : clean.Replace(" ", "_");
        }

        private static void CreateZipWithUnixPermissions(string sourceDirectory, string zipFilePath)
        {
            if (File.Exists(zipFilePath)) File.Delete(zipFilePath);

            using (var zipStream = new FileStream(zipFilePath, FileMode.Create))
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
            {
                var files = Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    string relativePath = Path.GetRelativePath(sourceDirectory, file).Replace('\\', '/');
                    var entry = archive.CreateEntry(relativePath, CompressionLevel.Optimal);

                    // Write file bytes
                    using (var sourceStream = new FileStream(file, FileMode.Open, FileAccess.Read))
                    using (var entryStream = entry.Open())
                    {
                        sourceStream.CopyTo(entryStream);
                    }

                    // Detect if file is a macOS executable binary inside the app bundle
                    bool isMacExecutable = relativePath.Contains(".app/Contents/MacOS/", StringComparison.OrdinalIgnoreCase);

                    if (isMacExecutable)
                    {
                        // Permissions: -rwxr-xr-x (0755) -> 0x81ED in hex
                        entry.ExternalAttributes = unchecked((int)0x81ED0000);
                    }
                    else
                    {
                        // Permissions: -rw-r--r-- (0644) -> 0x81A4 in hex
                        entry.ExternalAttributes = unchecked((int)0x81A40000);
                    }
                }
            }

            // Post-process ZIP to change Platform compatibility byte of central directory headers to Unix (3)
            PatchZipPlatformToUnix(zipFilePath);
        }

        private static void PatchZipPlatformToUnix(string zipFilePath)
        {
            if (!File.Exists(zipFilePath)) return;

            try
            {
                byte[] bytes = File.ReadAllBytes(zipFilePath);
                bool modified = false;

                for (int i = 0; i < bytes.Length - 46; i++)
                {
                    // Central Directory Header Signature: 0x02014b50 (PK\x01\x02)
                    if (bytes[i] == 0x50 && bytes[i + 1] == 0x4B && bytes[i + 2] == 0x01 && bytes[i + 3] == 0x02)
                    {
                        // Read file name length to check matching entries
                        ushort fileNameLength = BitConverter.ToUInt16(bytes, i + 28);
                        if (i + 46 + fileNameLength <= bytes.Length)
                        {
                            string fileName = System.Text.Encoding.UTF8.GetString(bytes, i + 46, fileNameLength);

                            // Only change platform to Unix for files inside .app bundle to avoid breaking standard Windows files unnecessarily
                            if (fileName.Contains(".app/", StringComparison.OrdinalIgnoreCase))
                            {
                                // Offset 4: Version Made By (2 bytes). Upper byte (offset 5) is the host platform.
                                // Set it to 3 (Unix) so macOS Archive Utility reads Unix permissions (ExternalAttributes).
                                if (bytes[i + 5] != 3)
                               {
                                    bytes[i + 5] = 3;
                                    modified = true;
                               }
                            }
                        }
                    }
                }

                if (modified)
                {
                    File.WriteAllBytes(zipFilePath, bytes);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error patching ZIP file headers: {ex.Message}");
            }
        }

        public static bool IsTargetInUse(string outputDirectory, string cleanTitle, PackagingTarget target)
        {
            if (!Directory.Exists(outputDirectory)) return false;

            string exeName = target switch
            {
                PackagingTarget.Windows => cleanTitle + ".exe",
                PackagingTarget.Linux => cleanTitle,
                PackagingTarget.MacOS => cleanTitle + ".app",
                _ => ""
            };

            List<string> filesToCheck = new();
            if (!string.IsNullOrEmpty(exeName))
            {
                if (target == PackagingTarget.MacOS)
                {
                    string innerExe = Path.Combine(outputDirectory, exeName, "Contents", "MacOS", cleanTitle);
                    if (File.Exists(innerExe)) filesToCheck.Add(innerExe);
                }
                else
                {
                    string mainExe = Path.Combine(outputDirectory, exeName);
                    if (File.Exists(mainExe)) filesToCheck.Add(mainExe);
                }
            }

            string crashHandler = target == PackagingTarget.Windows ? "UnityCrashHandler64.exe" : "UnityCrashHandler64";
            string crashHandlerPath = Path.Combine(outputDirectory, crashHandler);
            if (File.Exists(crashHandlerPath)) filesToCheck.Add(crashHandlerPath);

            foreach (var filePath in filesToCheck)
            {
                try
                {
                    using (var stream = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    {
                        // Not locked
                    }
                }
                catch (IOException)
                {
                    return true;
                }
                catch (UnauthorizedAccessException)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Summary stats shown in the publish confirmation dialog.</summary>
    public class PublishSummary
    {
        public string Title         { get; init; } = string.Empty;
        public string Author        { get; init; } = string.Empty;
        public string Version       { get; init; } = string.Empty;
        public int    RoomCount     { get; init; }
        public int    ObjectCount   { get; init; }
        public int    CharacterCount{ get; init; }
        public int    VariableCount { get; init; }
        public int    MediaCount    { get; init; }
    }
}
