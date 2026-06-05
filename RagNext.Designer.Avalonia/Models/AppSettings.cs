using System;

namespace RagNext.Designer.Avalonia.Models
{
    public class AppSettings
    {
        public string ThemeName { get; set; } = "Dark";

        // AI Co-Author Settings
        public string AiCoAuthorProvider { get; set; } = "OpenAICompatible";
        public string AiCoAuthorEndpoint { get; set; } = "https://api.openai.com/v1";
        public string AiCoAuthorKey { get; set; } = string.Empty;
        public string AiCoAuthorModel { get; set; } = "gpt-4o";
        public string AiCoAuthorHost { get; set; } = "http://localhost";
        public string AiCoAuthorPort { get; set; } = "1234";
        public double AiCoAuthorTemperature { get; set; } = 0.7;
        public int AiCoAuthorMaxTokens { get; set; } = 2048;
        public bool AiCoAuthorEnableHelper { get; set; } = true;
        public string AiCoAuthorAssistantPrompt { get; set; } = "You are an assistant game designer for a text adventure editor. Expand terse notes into vivid, second-person scene descriptions with sensory detail and interactive affordances. Maintain continuity with the provided context, use present tense, avoid spoilers or offensive content, and return plain text only.";

        // AI Image Generator Settings
        public string AiImageGenProvider { get; set; } = "Pollinations.ai";
        public string AiImageGenEndpoint { get; set; } = "https://api.openai.com/v1";
        public string AiImageGenKey { get; set; } = string.Empty;
        public string AiImageGenModel { get; set; } = "dall-e-3";
        public string AiImageGenHost { get; set; } = "http://localhost";
        public string AiImageGenPort { get; set; } = "7860";
        public bool AiImageGenEnableHelper { get; set; } = true;

        // AI Node Assistant Settings (Hybrid Override)
        public bool AiNodeAssistantUseCustom { get; set; } = false;
        public string AiNodeAssistantProvider { get; set; } = "OpenAICompatible";
        public string AiNodeAssistantEndpoint { get; set; } = "https://api.openai.com/v1";
        public string AiNodeAssistantKey { get; set; } = string.Empty;
        public string AiNodeAssistantModel { get; set; } = "gpt-4o";
        public string AiNodeAssistantHost { get; set; } = "http://localhost";
        public string AiNodeAssistantPort { get; set; } = "1234";
        public double AiNodeAssistantTemperature { get; set; } = 0.2;
        public int AiNodeAssistantMaxTokens { get; set; } = 2048;

        // Last Published Directory
        public string LastPublishDirectory { get; set; } = string.Empty;
    }
}
