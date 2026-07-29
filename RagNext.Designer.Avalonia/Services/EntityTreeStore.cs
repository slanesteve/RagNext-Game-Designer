using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using RagNext.Models;
using RagsCore.Models;

namespace RagNext.Designer.Avalonia.Services
{
    public interface IEntityTreeStore
    {
        Task<EntityTreeDocument> LoadAsync(Game game);
        Task SaveAsync(Game game, EntityTreeDocument doc);
    }

    public sealed class EntityTreeStore : IEntityTreeStore
    {
        private static string GetFolder(Game game) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagNext", game.Id.ToString("N"));

        private static string GetPath(Game game) =>
            Path.Combine(GetFolder(game), "entity_tree.json");

        public async Task<EntityTreeDocument> LoadAsync(Game game)
        {
            Directory.CreateDirectory(GetFolder(game));
            var path = GetPath(game);
            if (!File.Exists(path))
                return new EntityTreeDocument();

            try
            {
                var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                return JsonSerializer.Deserialize(json, DesignerJsonContext.Default.EntityTreeDocument)
                       ?? new EntityTreeDocument();
            }
            catch
            {
                return new EntityTreeDocument();
            }
        }

        public async Task SaveAsync(Game game, EntityTreeDocument doc)
        {
            Directory.CreateDirectory(GetFolder(game));
            var json = JsonSerializer.Serialize(doc, DesignerJsonContext.Default.EntityTreeDocument);
            await File.WriteAllTextAsync(GetPath(game), json).ConfigureAwait(false);
        }
    }
}
