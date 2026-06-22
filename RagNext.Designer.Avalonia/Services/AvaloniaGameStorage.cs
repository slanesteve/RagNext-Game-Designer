using System.Threading.Tasks;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Designer.Avalonia.Services
{
    public class AvaloniaGameStorage : IGameStorage
    {
        public Task SaveAsync(Game game, string? fileName = null, bool isExplicitUserSave = false)
        {
            return GameStorage.SaveAsync(game, fileName, isExplicitUserSave);
        }

        public Task<Game?> LoadAsync()
        {
            return GameStorage.LoadAsync();
        }

        public Task<Game?> LoadAsync(string fileName)
        {
            return GameStorage.LoadAsync(fileName);
        }

        public Task<string[]> ListSavesAsync()
        {
            return GameStorage.ListSavesAsync();
        }

        public Task DeleteSaveAsync(string fileName)
        {
            return GameStorage.DeleteAsync(fileName);
        }
    }
}
