using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using RagNext.Models;
using RagsCore.Models;

namespace RagNext.Designer.Avalonia.Services
{
    public interface IMediaTreeStore
    {
        Task<MediaTreeDocument> LoadAsync(Game game);
        Task SaveAsync(Game game, MediaTreeDocument doc);
    }

    public sealed class MediaTreeStore : IMediaTreeStore
    {
        private static string GetFolder(Game game) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagNext", game.Id.ToString("N"));

        private static string GetPath(Game game) =>
            Path.Combine(GetFolder(game), "media_tree.json");

        public async Task<MediaTreeDocument> LoadAsync(Game game)
        {
            Directory.CreateDirectory(GetFolder(game));
            var path = GetPath(game);
            if (!File.Exists(path))
                return new MediaTreeDocument();

            try
            {
                var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                return JsonSerializer.Deserialize(json, DesignerJsonContext.Default.MediaTreeDocument)
                       ?? new MediaTreeDocument();
            }
            catch
            {
                return new MediaTreeDocument();
            }
        }

        public async Task SaveAsync(Game game, MediaTreeDocument doc)
        {
            Directory.CreateDirectory(GetFolder(game));
            var json = JsonSerializer.Serialize(doc, DesignerJsonContext.Default.MediaTreeDocument);
            await File.WriteAllTextAsync(GetPath(game), json).ConfigureAwait(false);
        }
    }
}
