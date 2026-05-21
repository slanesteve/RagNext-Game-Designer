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
                MaxTokens = Preferences.Get(Prefix + nameof(AISettings.MaxTokens), 2048),
                ApiKey = Preferences.Get(Prefix + nameof(AISettings.ApiKey), null),
                EnableAIHelp = Preferences.Get(Prefix + nameof(AISettings.EnableAIHelp), true),
                SystemPrompt = Preferences.Get(Prefix + nameof(AISettings.SystemPrompt), null),

                // Image AI settings
                ImageProvider = Enum.TryParse(Preferences.Get(Prefix + nameof(AISettings.ImageProvider), AIProviderKind.OpenAICompatible.ToString()), out AIProviderKind imgKind) ? imgKind : AIProviderKind.OpenAICompatible,
                ImageHost = Preferences.Get(Prefix + nameof(AISettings.ImageHost), "https://api.openai.com"),
                ImagePort = Preferences.Get(Prefix + nameof(AISettings.ImagePort), 0),
                ImageModel = Preferences.Get(Prefix + nameof(AISettings.ImageModel), "dall-e-3"),
                ImageApiKey = Preferences.Get(Prefix + nameof(AISettings.ImageApiKey), null),

                // ComfyUI-specific
                ComfyWorkflowPath = Preferences.Get(Prefix + nameof(AISettings.ComfyWorkflowPath), null),
                ComfyPositivePromptNode = Preferences.Get(Prefix + nameof(AISettings.ComfyPositivePromptNode), null),
                ComfyNegativePromptNode = Preferences.Get(Prefix + nameof(AISettings.ComfyNegativePromptNode), null),
                ComfySizeNode = Preferences.Get(Prefix + nameof(AISettings.ComfySizeNode), null)
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

            // Image AI settings
            Preferences.Set(Prefix + nameof(AISettings.ImageProvider), s.ImageProvider.ToString());
            Preferences.Set(Prefix + nameof(AISettings.ImageHost), s.ImageHost ?? "");
            Preferences.Set(Prefix + nameof(AISettings.ImagePort), s.ImagePort);
            Preferences.Set(Prefix + nameof(AISettings.ImageModel), s.ImageModel ?? "");
            if (s.ImageApiKey is not null)
                Preferences.Set(Prefix + nameof(AISettings.ImageApiKey), s.ImageApiKey);

            // ComfyUI-specific
            if (string.IsNullOrWhiteSpace(s.ComfyWorkflowPath))
                Preferences.Remove(Prefix + nameof(AISettings.ComfyWorkflowPath));
            else
                Preferences.Set(Prefix + nameof(AISettings.ComfyWorkflowPath), s.ComfyWorkflowPath);

            if (string.IsNullOrWhiteSpace(s.ComfyPositivePromptNode))
                Preferences.Remove(Prefix + nameof(AISettings.ComfyPositivePromptNode));
            else
                Preferences.Set(Prefix + nameof(AISettings.ComfyPositivePromptNode), s.ComfyPositivePromptNode);

            if (string.IsNullOrWhiteSpace(s.ComfyNegativePromptNode))
                Preferences.Remove(Prefix + nameof(AISettings.ComfyNegativePromptNode));
            else
                Preferences.Set(Prefix + nameof(AISettings.ComfyNegativePromptNode), s.ComfyNegativePromptNode);

            if (string.IsNullOrWhiteSpace(s.ComfySizeNode))
                Preferences.Remove(Prefix + nameof(AISettings.ComfySizeNode));
            else
                Preferences.Set(Prefix + nameof(AISettings.ComfySizeNode), s.ComfySizeNode);
        }
    }
}