using System;

namespace RagNext.Models
{
    public enum AIProviderKind
    {
        Ollama,
        LMStudio,
        OpenAICompatible
    }

    public class AISettings
    {
        // Shared default so UI and service stay consistent
        public const string DefaultSystemPrompt =
            "You are an assistant game designer for a text adventure editor. Expand terse notes into vivid, second-person scene descriptions with sensory detail and interactive affordances. Maintain continuity with the provided context, use present tense, avoid spoilers or offensive content, and return plain text only.";

        public AIProviderKind Provider { get; set; } = AIProviderKind.Ollama;
        public string Host { get; set; } = "http://localhost";
        public int Port { get; set; } = 11434; // Ollama default
        public string Model { get; set; } = "llama3";
        public double Temperature { get; set; } = 0.7;
        public int MaxTokens { get; set; } = 512;
        public string? ApiKey { get; set; } // for OpenAI-compatible endpoints
        public bool EnableAIHelp { get; set; } = true;

        // User-configurable system prompt (optional). Leave empty/null to use default.
        public string? SystemPrompt { get; set; }

        public Uri BaseUri => new($"{Host.TrimEnd('/')}" + (Port > 0 ? $":{Port}" : ""));
    }
}