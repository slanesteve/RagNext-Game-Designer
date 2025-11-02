using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using RagsCore.Models;

namespace RagNext.Services
{
    public static class ConditionCatalogLoader
    {
        // Loads the catalog from Resources/Raw/Conditions.json
        public static Task InitializeAsync(CancellationToken ct = default) =>
            Game.EnsureAvailableConditionsAsync(() => FileSystem.OpenAppPackageFileAsync("Conditions.json"), ct);
    }
}