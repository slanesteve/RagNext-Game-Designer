using System;
using System.IO;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Designer.Avalonia.Services
{
    public sealed class AvaloniaMediaPathProvider : IMediaPathProvider
    {
        public string GetGameRoot(Game game) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RagNext", game.Id.ToString("N"));
    }
}
