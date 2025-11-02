using Microsoft.Maui.Storage;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Services
{
    // Designer path strategy
    public sealed class MauiMediaPathProvider : IMediaPathProvider
    {
        public string GetGameRoot(Game game) =>
            Path.Combine(FileSystem.AppDataDirectory, game.Id.ToString("N"));
    }
}