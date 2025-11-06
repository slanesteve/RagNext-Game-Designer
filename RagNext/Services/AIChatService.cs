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
                var url = new Uri(s.BaseUri, "/api/generate");
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
                var resp = await _http.SendAsync(req, ct);
                var txt = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"AI provider error {(int)resp.StatusCode} {resp.ReasonPhrase}: {txt}");

                using var doc = JsonDocument.Parse(txt);
                if (doc.RootElement.TryGetProperty("response", out var response))
                    return response.GetString();

                return txt;
            }
            else
            {
                var url = new Uri(s.BaseUri, "/v1/chat/completions");
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

                var resp = await _http.SendAsync(req, ct);
                var txt = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                    throw new InvalidOperationException($"AI provider error {(int)resp.StatusCode} {resp.ReasonPhrase}: {txt}");

                using var doc = JsonDocument.Parse(txt);
                if (doc.RootElement.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                {
                    var msg = choices[0].GetProperty("message");
                    if (msg.TryGetProperty("content", out var content))
                        return content.GetString();
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
                    var url = new Uri(s.BaseUri, "/api/tags");
                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var resp = await _http.SendAsync(req, ct);
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
                    return await GetOpenAICompatibleModelsAsync(s, ct);

                default:
                    return Array.Empty<string>();
            }
        }

        private async Task<IReadOnlyList<string>> GetOpenAICompatibleModelsAsync(AISettings s, CancellationToken ct)
        {
            var url = new Uri(s.BaseUri, "/v1/models");
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            if (!string.IsNullOrWhiteSpace(s.ApiKey))
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.ApiKey);

            var resp = await _http.SendAsync(req, ct);
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
    }
}