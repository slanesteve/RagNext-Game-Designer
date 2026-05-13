using System.Threading;
using System.Threading.Tasks;

namespace RagNext.Services
{
    public interface IAIImageService
    {
        /// <summary>
        /// Generates an image based on a prompt and returns a local file path to the saved image.
        /// </summary>
        /// <param name="prompt">User prompt describing the image.</param>
        /// <param name="size">Optional square size in pixels (e.g., 480, 720, 1024). If null, provider default is used.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<string?> GenerateImageAsync(string prompt, int? size = null, CancellationToken ct = default);
    }
}
