using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using RagsCore.Models;

namespace RagsCore.Services
{
    // Shared implementation: no MAUI dependencies
    public sealed class MediaLibrary : IMediaLibrary
    {
        private readonly IMediaPathProvider _paths;
        public MediaLibrary(IMediaPathProvider paths) => _paths = paths;

        private string GetAssetsFolder(Game game) =>
            Path.Combine(_paths.GetGameRoot(game), "Assets");

        public async Task<MediaAsset> AddAsync(Game game, string sourceFilePath, CancellationToken ct = default)
        {
            Directory.CreateDirectory(GetAssetsFolder(game));

            await using var fs = File.OpenRead(sourceFilePath);
            var hash = await ComputeSha256Async(fs, ct);
            var ext = Path.GetExtension(sourceFilePath);
            var targetFileName = $"{hash}{ext}".ToLowerInvariant();
            var targetPath = Path.Combine(GetAssetsFolder(game), targetFileName);

            if (!File.Exists(targetPath))
            {
                fs.Position = 0;
                await using var outFs = File.Create(targetPath);
                await fs.CopyToAsync(outFs, ct);
            }

            var existing = game.MediaAssets.FirstOrDefault(a => a.Sha256 == hash);
            if (existing is not null) return existing;

            var asset = new MediaAsset
            {
                OriginalFileName = Path.GetFileName(sourceFilePath),
                RelativePath = Path.Combine("Assets", targetFileName),
                ContentType = GuessContentType(ext),
                Kind = GuessKind(ext),
                Sha256 = hash
            };
            game.MediaAssets.Add(asset);
            return asset;
        }

        public Task<Stream> OpenReadAsync(Game game, MediaAsset asset, CancellationToken ct = default)
        {
            var full = GetLocalPath(game, asset);
            Stream s = File.OpenRead(full);
            return Task.FromResult(s);
        }

        public string GetLocalPath(Game game, MediaAsset asset) =>
            Path.Combine(_paths.GetGameRoot(game), asset.RelativePath);

        private static async Task<string> ComputeSha256Async(Stream s, CancellationToken ct)
        {
            using var sha = SHA256.Create();
            var hash = await sha.ComputeHashAsync(s, ct);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static string GuessContentType(string ext) => ext.ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".mp4" => "video/mp4",
            ".mov" => "video/quicktime",
            ".avi" => "video/x-msvideo",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            _ => "application/octet-stream"
        };

        private static MediaKind GuessKind(string ext) => ext.ToLowerInvariant() switch
        {
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".webp" => MediaKind.Image,
            ".mp4" or ".mov" or ".avi" => MediaKind.Video,
            ".mp3" or ".wav" => MediaKind.Audio,
            _ => MediaKind.Other
        };
    }
}