using Microsoft.Maui.Storage;
using RagNext.Models;

namespace RagNext.Services
{
    public interface IAISettingsService
    {
        AISettings Load();
        void Save(AISettings settings);
    }

    public class AISettingsService : IAISettingsService
    {
        private const string Prefix = "AISettings.";

        public AISettings Load()
        {
            return new AISettings
            {
                Provider = Enum.TryParse(Preferences.Get(Prefix + nameof(AISettings.Provider), "Ollama"), out AIProviderKind kind) ? kind : AIProviderKind.Ollama,
                Host = Preferences.Get(Prefix + nameof(AISettings.Host), "http://localhost"),
                Port = Preferences.Get(Prefix + nameof(AISettings.Port), 11434),
                Model = Preferences.Get(Prefix + nameof(AISettings.Model), "llama3"),
                Temperature = Preferences.Get(Prefix + nameof(AISettings.Temperature), 0.7),
                MaxTokens = Preferences.Get(Prefix + nameof(AISettings.MaxTokens), 512),
                ApiKey = Preferences.Get(Prefix + nameof(AISettings.ApiKey), null),
                EnableAIHelp = Preferences.Get(Prefix + nameof(AISettings.EnableAIHelp), true),
                SystemPrompt = Preferences.Get(Prefix + nameof(AISettings.SystemPrompt), null)
            };
        }

        public void Save(AISettings s)
        {
            Preferences.Set(Prefix + nameof(AISettings.Provider), s.Provider.ToString());
            Preferences.Set(Prefix + nameof(AISettings.Host), s.Host);
            Preferences.Set(Prefix + nameof(AISettings.Port), s.Port);
            Preferences.Set(Prefix + nameof(AISettings.Model), s.Model);
            Preferences.Set(Prefix + nameof(AISettings.Temperature), s.Temperature);
            Preferences.Set(Prefix + nameof(AISettings.MaxTokens), s.MaxTokens);
            if (s.ApiKey is not null)
                Preferences.Set(Prefix + nameof(AISettings.ApiKey), s.ApiKey);
            Preferences.Set(Prefix + nameof(AISettings.EnableAIHelp), s.EnableAIHelp);

            var spKey = Prefix + nameof(AISettings.SystemPrompt);
            if (string.IsNullOrWhiteSpace(s.SystemPrompt))
                Preferences.Remove(spKey);
            else
                Preferences.Set(spKey, s.SystemPrompt);
        }
    }
}