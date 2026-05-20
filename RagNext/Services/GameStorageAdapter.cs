using System.Threading.Tasks;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Services
{
    // Adapter that implements the core IGameStorage from RagsCore and forwards to the existing static helper.
    public class GameStorageAdapter : IGameStorage
    {
        public Task SaveAsync(Game game, string? fileName = null, bool isExplicitUserSave = false) =>
            GameStorage.SaveAsync(game, fileName, isExplicitUserSave);

        public Task<Game?> LoadAsync() =>
            GameStorage.LoadAsync();

        public Task<Game?> LoadAsync(string fileName) =>
            GameStorage.LoadAsync(fileName);

        public Task<string[]> ListSavesAsync() =>
            GameStorage.ListSavesAsync();
    }
}