using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using RagsCore.Models;

namespace RagNext.Designer.Avalonia.Services
{
    // Builds:
    // 1) Platform-neutral bundle (.rags): game.json + /Assets/*
    // 2) Platform-specific zips that embed the .rags in the expected place for a future Player
    public static class GamePackaging
    {
        private static string AppDataDirectory =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagNext");

        // Keeps your existing export intact
        public static async Task ExportAsync(Game game, string destinationZipPath, CancellationToken ct = default)
        {
            var root = Path.Combine(AppDataDirectory, game.Id.ToString("N"));
            if (File.Exists(destinationZipPath)) File.Delete(destinationZipPath);

            using var zip = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create);

            var gameEntry = zip.CreateEntry("game.json", CompressionLevel.Fastest);
            await using (var s = gameEntry.Open())
                await JsonSerializer.SerializeAsync(s, game, cancellationToken: ct);

            var assetsDir = Path.Combine(root, "Assets");
            if (Directory.Exists(assetsDir))
            {
                foreach (var file in Directory.EnumerateFiles(assetsDir))
                {
                    var name = Path.GetFileName(file);
                    zip.CreateEntryFromFile(file, Path.Combine("Assets", name), CompressionLevel.Fastest);
                }
            }
        }

        // Build a platform-neutral bundle file (.rags is just a zip)
        public static async Task<string> CreateBundleAsync(Game game, string destinationBundlePath, CancellationToken ct = default)
        {
            if (File.Exists(destinationBundlePath)) File.Delete(destinationBundlePath);

            var root = Path.Combine(AppDataDirectory, game.Id.ToString("N"));

            using var zip = ZipFile.Open(destinationBundlePath, ZipArchiveMode.Create);

            var gameEntry = zip.CreateEntry("game.json", CompressionLevel.Optimal);
            await using (var s = gameEntry.Open())
                await JsonSerializer.SerializeAsync(s, game, cancellationToken: ct);

            var assetsDir = Path.Combine(root, "Assets");
            if (Directory.Exists(assetsDir))
            {
                foreach (var file in Directory.EnumerateFiles(assetsDir))
                {
                    var name = Path.GetFileName(file);
                    zip.CreateEntryFromFile(file, Path.Combine("Assets", name), CompressionLevel.Optimal);
                }
            }

            return destinationBundlePath;
        }

        // Create a platform-specific package with placeholders for the future Player
        public static async Task ExportForTargetAsync(Game game, PackagingTarget target, string destinationZipPath, CancellationToken ct = default)
        {
            if (File.Exists(destinationZipPath)) File.Delete(destinationZipPath);

            // 1) Build the neutral bundle into memory
            using var bundleMs = new MemoryStream();
            using (var tempZip = new ZipArchive(bundleMs, ZipArchiveMode.Create, leaveOpen: true))
            {
                // write game.json
                var entry = tempZip.CreateEntry("game.json", CompressionLevel.Optimal);
                await using (var s = entry.Open())
                    await JsonSerializer.SerializeAsync(s, game, cancellationToken: ct);

                // write assets
                var assetsDir = Path.Combine(AppDataDirectory, game.Id.ToString("N"), "Assets");
                if (Directory.Exists(assetsDir))
                {
                    foreach (var file in Directory.EnumerateFiles(assetsDir))
                    {
                        var name = Path.GetFileName(file);
                        tempZip.CreateEntryFromFile(file, Path.Combine("Assets", name), CompressionLevel.Optimal);
                    }
                }
            }
            bundleMs.Position = 0;

            // 2) Create the target-specific wrapper zip and place the bundle where the Player will look
            using var outZip = ZipFile.Open(destinationZipPath, ZipArchiveMode.Create);

            switch (target)
            {
                case PackagingTarget.Windows:
                {
                    AddPlaceholder(outZip, "Player/README.txt",
                        "Replace this folder with the Windows Player build.\r\nExpected to run Player.exe next to Game.rags.");
                    // Drop the bundle next to the Player
                    AddFile(outZip, "Game.rags", bundleMs);
                    break;
                }
                case PackagingTarget.MacOS:
                {
                    // Create a minimal .app-like layout with placeholders
                    AddPlaceholder(outZip, "MyGame.app/Contents/MacOS/PlayerPlaceholder",
                        "#!/bin/sh\n# Replace with macOS Player binary\n");
                    AddPlaceholder(outZip, "MyGame.app/Contents/Info.plist",
                        "<?xml version=\"1.0\" encoding=\"UTF-8\"?><plist version=\"1.0\"><dict></dict></plist>");
                    // Put the bundle under Resources
                    AddFile(outZip, "MyGame.app/Contents/Resources/Game.rags", bundleMs);
                    break;
                }
                case PackagingTarget.Linux:
                {
                    AddPlaceholder(outZip, "Player/README.txt",
                        "Replace this folder with the Linux Player build.\r\nExpected to run Player binary next to Game.rags.");
                    AddFile(outZip, "Game.rags", bundleMs);
                    break;
                }
                case PackagingTarget.WebGL:
                {
                    AddPlaceholder(outZip, "Player/README.txt",
                        "Replace this folder with the WebGL Player build.");
                    AddFile(outZip, "Game.rags", bundleMs);
                    break;
                }
            }

            // helpful top-level note
            AddPlaceholder(outZip, "INSTRUCTIONS.txt",
                "This package contains a platform-neutral game bundle (Game.rags).\r\n" +
                "Replace the placeholder Player files with the real Player for the target platform.\r\n" +
                "Windows: run Player.exe next to Game.rags\r\n" +
                "macOS: Player reads Game.rags from MyGame.app/Contents/Resources\r\n" +
                "Android: include assets/Game.rags as a MauiAsset in the Player project");
        }

        private static void AddPlaceholder(ZipArchive zip, string entryPath, string content)
        {
            var entry = zip.CreateEntry(entryPath, CompressionLevel.Fastest);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(content);
        }

        private static void AddFile(ZipArchive zip, string entryPath, Stream source)
        {
            source.Position = 0;
            var entry = zip.CreateEntry(entryPath, CompressionLevel.Optimal);
            using var dst = entry.Open();
            source.CopyTo(dst);
        }
    }
}
