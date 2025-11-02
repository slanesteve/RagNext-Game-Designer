using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using RagsCore.Models;

namespace RagNext.Services
{
    public static class CommandCatalogLoader
    {
        // Ensure the name matches the file in Resources/Raw exactly (case-sensitive on Android)
      
        public static Task InitializeAsync(CancellationToken ct = default) =>
            Game.EnsureAvailableCommandsAsync(() => FileSystem.OpenAppPackageFileAsync("Commands.json"), ct);
    }
}