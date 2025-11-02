using System.IO;
using System.Threading;
using System.Threading.Tasks;
using RagsCore.Models;

namespace RagsCore.Services
{
    public interface IMediaLibrary
    {
        Task<MediaAsset> AddAsync(Game game, string sourceFilePath, CancellationToken ct = default);
        Task<Stream> OpenReadAsync(Game game, MediaAsset asset, CancellationToken ct = default);
        string GetLocalPath(Game game, MediaAsset asset);
    }
}