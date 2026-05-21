using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using RagNext.Models;
using System.Collections.Generic;
using System.Linq;

namespace RagNext.Services
{
    public interface IAIChatService
    {
        Task<string?> AskAsync(string prompt, CancellationToken ct = default);
        Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken ct = default);
    }

    public class AIChatService : IAIChatService
    {
        private readonly HttpClient _http;

        public AIChatService(HttpClient? http = null)
        {
            _http = http ?? new HttpClient();
        }

        public async Task<string?> AskAsync(string prompt, CancellationToken ct = default)
        {
            var s = App.CurrentAISettings;
            if (s is null || !s.EnableAIHelp)
                throw new InvalidOperationException("AI is disabled. Enable it in AI Settings.");

            var systemPrompt = string.IsNullOrWhiteSpace(s.SystemPrompt) ? AISettings.DefaultSystemPrompt : s.SystemPrompt!;

            if (s.Provider == AIProviderKind.Ollama)
            {
                var url = new Uri(s.BaseUri, "api/generate");
                var body = new
                {
                    model = s.Model,
                    prompt,
                    system = systemPrompt,
                    options = new { temperature = s.Temperature },
                    stream = false
                };
                var json = JsonSerializer.Serialize(body);
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };
                var resp = await SendRequestWithGracefulErrorsAsync(req, s, ct);
                var txt = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"AI provider error {(int)resp.StatusCode} {resp.ReasonPhrase}: {txt}");

                using var doc = JsonDocument.Parse(txt);
                string? responseStr = null;
                if (doc.RootElement.TryGetProperty("response", out var response))
                {
                    responseStr = response.GetString();
                }

                // Check if Ollama hit the length limit (ran out of tokens)
                if (doc.RootElement.TryGetProperty("done_reason", out var doneReason) && 
                    doneReason.GetString() == "length")
                {
                    if (string.IsNullOrWhiteSpace(responseStr))
                    {
                        throw new AITruncatedException(
                            "The Ollama model ran out of tokens before it could write any description.\n\n" +
                            "Please open AI Settings and increase 'Max Tokens' (e.g. to 2048 or 4096), then try again.");
                    }
                    else
                    {
                        throw new AITruncatedException(
                            "The Ollama model ran out of tokens and was cut off mid-generation (it reached the 'Max Tokens' limit).\n\n" +
                            "Please open AI Settings and increase 'Max Tokens' to get complete descriptions.", 
                            responseStr);
                    }
                }

                return responseStr ?? txt;
            }
            else
            {
                var url = new Uri(s.BaseUri, "v1/chat/completions");
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                if (!string.IsNullOrWhiteSpace(s.ApiKey))
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);

                var body = new
                {
                    model = s.Model,
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = prompt }
                    },
                    temperature = s.Temperature,
                    max_tokens = s.MaxTokens
                };
                var json = JsonSerializer.Serialize(body);
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");

                var resp = await SendRequestWithGracefulErrorsAsync(req, s, ct);
                var txt = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"AI provider error {(int)resp.StatusCode} {resp.ReasonPhrase}: {txt}");

                using var doc = JsonDocument.Parse(txt);
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                {
                    var firstChoice = choices[0];
                    var msg = firstChoice.GetProperty("message");
                    
                    string? contentStr = null;
                    if (msg.TryGetProperty("content", out var contentElement))
                    {
                        contentStr = contentElement.GetString();
                    }

                    // Check if the model hit the length limit (ran out of tokens)
                    if (firstChoice.TryGetProperty("finish_reason", out var finishReason) && 
                        finishReason.GetString() == "length")
                    {
                        if (string.IsNullOrWhiteSpace(contentStr))
                        {
                            throw new AITruncatedException(
                                "The AI model ran out of tokens before it could write the final description (it spent its token budget thinking).\n\n" +
                                "Please open AI Settings and increase 'Max Tokens' (e.g. to 2048 or 4096), then try again.");
                        }
                        else
                        {
                            throw new AITruncatedException(
                                "The AI model ran out of tokens and was cut off mid-generation (it reached the 'Max Tokens' limit).\n\n" +
                                "Please open AI Settings and increase 'Max Tokens' to get complete descriptions.", 
                                contentStr);
                        }
                    }

                    return contentStr;
                }

                if (doc.RootElement.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msgProp))
                    throw new InvalidOperationException(msgProp.GetString() ?? "AI provider returned an error.");

                return txt;
            }
        }

        public async Task<IReadOnlyList<string>> GetModelsAsync(CancellationToken ct = default)
        {
            var s = App.CurrentAISettings ?? throw new InvalidOperationException("AI settings are not configured.");

            switch (s.Provider)
            {
                case AIProviderKind.Ollama:
                {
                    var url = new Uri(s.BaseUri, "api/tags");
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var resp = await SendRequestWithGracefulErrorsAsync(req, s, ct);
                    var txt = await resp.Content.ReadAsStringAsync(ct);
                    if (!resp.IsSuccessStatusCode)
                        throw new InvalidOperationException($"Failed to fetch models: {(int)resp.StatusCode} {resp.ReasonPhrase}: {txt}");

                    using var doc = JsonDocument.Parse(txt);
                    if (doc.RootElement.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
                    {
                        var names = new List<string>();
                        foreach (var m in models.EnumerateArray())
                        {
                            if (m.TryGetProperty("name", out var name))
                                names.Add(name.GetString() ?? "");
                        }
                        return names.Where(n => !string.IsNullOrWhiteSpace(n)).Distinct().OrderBy(n => n).ToList();
                    }
                    return Array.Empty<string>();
                }

                case AIProviderKind.LMStudio:
                case AIProviderKind.OpenAICompatible:
                case AIProviderKind.OpenRouter:
                    return await GetOpenAICompatibleModelsAsync(s, ct);

                default:
                    return Array.Empty<string>();
            }
        }

        private async Task<IReadOnlyList<string>> GetOpenAICompatibleModelsAsync(AISettings s, CancellationToken ct)
        {
            var url = new Uri(s.BaseUri, "v1/models");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(s.ApiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);

            var resp = await SendRequestWithGracefulErrorsAsync(req, s, ct);
            var txt = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Failed to fetch models: {(int)resp.StatusCode} {resp.ReasonPhrase}: {txt}");

            using var doc = JsonDocument.Parse(txt);
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Standard OpenAI-compatible structure: { "data": [ { "id": "model-id", ... } ] }
            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in data.EnumerateArray())
                {
                    if (m.TryGetProperty("id", out var idEl))
                    {
                        var id = idEl.GetString();
                        if (!string.IsNullOrWhiteSpace(id)) results.Add(id!);
                    }
                    else if (m.TryGetProperty("name", out var nameEl))
                    {
                        var name = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(name)) results.Add(name!);
                    }
                }
            }
            // Be resilient if a server returns { "models": [ { "name": "..." } ] }
            else if (doc.RootElement.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
            {
                foreach (var m in models.EnumerateArray())
                {
                    if (m.TryGetProperty("id", out var idEl))
                    {
                        var id = idEl.GetString();
                        if (!string.IsNullOrWhiteSpace(id)) results.Add(id!);
                    }
                    else if (m.TryGetProperty("name", out var nameEl))
                    {
                        var name = nameEl.GetString();
                        if (!string.IsNullOrWhiteSpace(name)) results.Add(name!);
                    }
                }
            }

            return results.Where(x => !string.IsNullOrWhiteSpace(x)).OrderBy(x => x).ToList();
        }

        private async Task<HttpResponseMessage> SendRequestWithGracefulErrorsAsync(HttpRequestMessage req, AISettings s, CancellationToken ct)
        {
            try
            {
                return await _http.SendAsync(req, ct);
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                var providerName = s.Provider switch
                {
                    AIProviderKind.Ollama => "Ollama",
                    AIProviderKind.LMStudio => "LM Studio",
                    AIProviderKind.OpenAICompatible => "OpenAI-Compatible server",
                    AIProviderKind.OpenRouter => "OpenRouter",
                    _ => "AI provider"
                };

                throw new InvalidOperationException(
                    $"Could not connect to {providerName}.\n\n" +
                    $"Please make sure that the server is currently running and reachable at:\n" +
                    $"{s.BaseUri}\n\n" +
                    $"Details: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                throw new InvalidOperationException($"An error occurred communicating with the AI service: {ex.Message}", ex);
            }
        }
    }

    public class AITruncatedException : Exception
    {
        public string? PartialContent { get; }

        public AITruncatedException(string message, string? partialContent = null) : base(message)
        {
            PartialContent = partialContent;
        }
    }
}