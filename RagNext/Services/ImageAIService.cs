using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using RagNext.Models;

namespace RagNext.Services
{
    /// <summary>
    /// Concrete implementation of IAIImageService that routes to the configured provider.
    /// Supports: OpenAI-compatible images API, Automatic1111 (Stable Diffusion) txt2img, and ComfyUI workflow POST.
    /// </summary>
    public sealed class ImageAIService : IAIImageService
    {
        private static readonly HttpClient _http = new HttpClient();

        public async Task<string?> GenerateImageAsync(string prompt, int? size = null, CancellationToken ct = default)
        {
            var settings = App.CurrentAISettings ?? new AISettings();
            var host = settings.ImageHost ?? "https://image.pollinations.ai";
            var baseUri = settings.ImageBaseUri;
            int pixels = size ?? 1024;
            var sizeStr = $"{pixels}x{pixels}";

            // Choose provider by host/port
            var isPollinations = host.Contains("pollinations.ai", StringComparison.OrdinalIgnoreCase);
            var isOpenAI = host.Contains("openai.com", StringComparison.OrdinalIgnoreCase);
            var isAutomatic = settings.ImagePort == 7860; // Automatic1111
            var isComfy = settings.ImagePort == 8188 || settings.ImagePort == 8000;
            var isGoogle = host.Contains("googleapis", StringComparison.OrdinalIgnoreCase);

            if (isPollinations)
                return await GenerateWithPollinationsAsync(prompt, pixels, ct);
            if (isOpenAI)
                return await GenerateWithOpenAIAsync(baseUri, settings.ImageModel ?? "dall-e-3", settings.ImageApiKey, prompt, sizeStr, ct);
            if (isAutomatic)
                return await GenerateWithAutomatic1111Async(baseUri, prompt, pixels, pixels, ct);
            if (isComfy)
                return await GenerateWithComfyUIAsync(baseUri, settings, prompt, pixels, pixels, ct);
            if (isGoogle)
                return await GenerateWithGoogleVertexAsync(baseUri, settings, prompt, pixels, ct);

            // Fallback: try OpenAI-compatible
            return await GenerateWithOpenAIAsync(baseUri, settings.ImageModel ?? "dall-e-3", settings.ImageApiKey, prompt, sizeStr, ct);
        }

        private static async Task<string?> GenerateWithPollinationsAsync(string prompt, int pixels, CancellationToken ct)
        {
            var encodedPrompt = Uri.EscapeDataString(prompt);
            var url = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width={pixels}&height={pixels}&nologo=true&private=true";
            
            var bytes = await _http.GetByteArrayAsync(url, ct);
            return await SaveImageAsync(bytes, "png");
        }

        private static async Task<string?> GenerateWithOpenAIAsync(Uri baseUri, string model, string? apiKey, string prompt, string size, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("OpenAI Images requires an API key.");
            //temp hack due to size restrictions in openai
            size="1024x1024";
            var endpoint = new Uri(baseUri, "/v1/images/generations");
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var body = JsonSerializer.Serialize(new { prompt, model, size });
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"OpenAI error {(int)resp.StatusCode} {resp.ReasonPhrase}: {err}");
            }
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            // OpenAI returns either URLs or b64_json; prefer b64 if present
            var data = doc.RootElement.GetProperty("data");
            if (data.GetArrayLength() == 0) return null;
            var item = data[0];
            string? path = null;
            if (item.TryGetProperty("b64_json", out var b64))
            {
                var bytes = Convert.FromBase64String(b64.GetString()!);
                path = await SaveImageAsync(bytes, "png");
            }
            else if (item.TryGetProperty("url", out var urlProp))
            {
                var url = urlProp.GetString();
                if (!string.IsNullOrWhiteSpace(url))
                {
                    var bytes = await _http.GetByteArrayAsync(url!, ct);
                    path = await SaveImageAsync(bytes, "png");
                }
            }
            return path;
        }

        private static async Task<string?> GenerateWithAutomatic1111Async(Uri baseUri, string prompt, int width, int height, CancellationToken ct)
        {
            var endpoint = new Uri(baseUri, "/sdapi/v1/txt2img");
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    prompt,
                    width,
                    height,
                    steps = 25,
                    sampler_name = "Euler a"
                }), Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("images", out var images) || images.GetArrayLength() == 0) return null;
            var b64 = images[0].GetString();
            if (string.IsNullOrWhiteSpace(b64)) return null;
            var bytes = Convert.FromBase64String(b64);
            return await SaveImageAsync(bytes, "png");
        }

        private static async Task<string?> GenerateWithComfyUIAsync(Uri baseUri, AISettings s, string prompt, int width, int height, CancellationToken ct)
        {
            // Load workflow and replace simple placeholders for prompt/size if present
            var path = s.ComfyWorkflowPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new InvalidOperationException("ComfyUI workflow JSON path not set or file missing.");
            var json = await File.ReadAllTextAsync(path, ct);
            //json = json.Replace("{{prompt}}", JsonEsc(prompt))
            //           .Replace("{{width}}", width.ToString())
            //           .Replace("{{height}}", height.ToString());

            var endpoint = new Uri(baseUri, "/prompt");
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { prompt = JsonSerializer.Deserialize<object>(json) }), Encoding.UTF8, "application/json")
            };

            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            using var sResp = await resp.Content.ReadAsStreamAsync(ct);
            using var submitted = await JsonDocument.ParseAsync(sResp, cancellationToken: ct);
            var promptId = submitted.RootElement.TryGetProperty("prompt_id", out var pid) ? pid.GetString() : null;
            if (string.IsNullOrWhiteSpace(promptId)) return null;

            // Poll history until complete, then fetch first saved file
            var historyEndpoint = new Uri(baseUri, $"/history/{promptId}");
            for (int i = 0; i < 60; i++)
            {
                await Task.Delay(1000, ct);
                using var hResp = await _http.GetAsync(historyEndpoint, ct);
                if (!hResp.IsSuccessStatusCode) continue;
                using var hStream = await hResp.Content.ReadAsStreamAsync(ct);
                using var hDoc = await JsonDocument.ParseAsync(hStream, cancellationToken: ct);
                // Try to find image file reference in outputs
                foreach (var prop in hDoc.RootElement.EnumerateObject())
                {
                    var node = prop.Value;
                    if (node.TryGetProperty("outputs", out var outputs))
                    {
                        foreach (var outProp in outputs.EnumerateObject())
                        {
                            var outNode = outProp.Value;
                            if (outNode.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array && imgs.GetArrayLength() > 0)
                            {
                                var imgObj = imgs[0];
                                var filename = imgObj.TryGetProperty("filename", out var fn) ? fn.GetString() : null;
                                var subfolder = imgObj.TryGetProperty("subfolder", out var sf) ? sf.GetString() : null;
                                if (!string.IsNullOrWhiteSpace(filename))
                                {
                                    // Download the saved image via /view if available
                                    var viewUrl = new Uri(baseUri, $"/view?filename={Uri.EscapeDataString(filename!)}&subfolder={Uri.EscapeDataString(subfolder ?? string.Empty)}");
                                    var bytes = await _http.GetByteArrayAsync(viewUrl, ct);
                                    return await SaveImageAsync(bytes, Path.GetExtension(filename!)?.Trim('.') ?? "png");
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        private static async Task<string?> GenerateWithGoogleVertexAsync(Uri baseUri, AISettings s, string prompt, int pixels, CancellationToken ct)
        {
            // Vertex AI Imagen endpoint (requires service account or API key)
            // POST https://LOCATION-aiplatform.googleapis.com/v1/projects/PROJECT/locations/LOCATION/publishers/google/models/imagen-3:generateImage
            var endpoint = new Uri($"{s.ImageHost?.TrimEnd('/')}/v1/projects/PROJECT/locations/LOCATION/publishers/google/models/imagen-3:generateImage");
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
            if (!string.IsNullOrWhiteSpace(s.ImageApiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.ImageApiKey);
            var body = JsonSerializer.Serialize(new
            {
                instances = new[] { new { prompt } },
                parameters = new { sampleCount = 1, imageSize = new { width = pixels, height = pixels } }
            });
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
            using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            // parse base64 image (provider-dependent)
            var bytesProp = doc.RootElement.GetProperty("predictions")[0].GetProperty("bytesBase64Encoded");
            var bytes = Convert.FromBase64String(bytesProp.GetString()!);
            return await SaveImageAsync(bytes, "png");
        }

        private static async Task<string> SaveImageAsync(byte[] bytes, string extension)
        {
            var dir = FileSystem.AppDataDirectory;
            var file = Path.Combine(dir, $"portrait_{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.{extension}");
            await File.WriteAllBytesAsync(file, bytes);
            return file;
        }

        private static string JsonEsc(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
