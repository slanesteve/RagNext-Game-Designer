using System.Threading.Tasks;
using RagsCore.Models;

namespace RagsCore.Services
{
    public interface IGameStorage
    {
        Task SaveAsync(Game game, string? fileName = null, bool isExplicitUserSave = false);
        Task<Game?> LoadAsync();                 // most recent
        Task<Game?> LoadAsync(string fileName);  // named save
        Task<string[]> ListSavesAsync();
        Task DeleteSaveAsync(string fileName);
    }
}