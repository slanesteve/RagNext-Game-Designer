using System.Collections.Generic;

namespace RagNext.Designer.Avalonia.Views
{
    public class AICoAuthorRequest
    {
        public string? model { get; set; }
        public AICoAuthorMessage[]? messages { get; set; }
        public double temperature { get; set; }
        public AIResponseFormat? response_format { get; set; }
    }

    public class AICoAuthorMessage
    {
        public string? role { get; set; }
        public string? content { get; set; }
    }

    public class AIResponseFormat
    {
        public string? type { get; set; }
    }

    public class ImageGenRequest
    {
        public string? prompt { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public int steps { get; set; }
    }

    public class OpenAiImageGenRequest
    {
        public string? prompt { get; set; }
        public string? model { get; set; }
        public int n { get; set; }
        public string? size { get; set; }
    }

    public class CatalogsDto
    {
        public List<CatalogEntityDto>? Rooms { get; set; }
        public List<CatalogEntityDto>? Characters { get; set; }
        public List<CatalogEntityDto>? GameObjects { get; set; }
        public List<CatalogEntityDto>? Variables { get; set; }
        public CatalogPlayerDto? Player { get; set; }
        public CatalogPlayerDto? Owner { get; set; }
        public List<CatalogEntityDto>? Media { get; set; }
        public List<CatalogEntityDto>? Functions { get; set; }
        public List<CatalogEntityDto>? Timers { get; set; }
    }

    public class CatalogEntityDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public bool? IsContainer { get; set; }
        public List<string>? Attributes { get; set; }
    }

    public class CatalogPlayerDto
    {
        public List<string>? Attributes { get; set; }
    }

    public class ReflectionEntityDto
    {
        public string? TypeName { get; set; }
        public string? Discriminator { get; set; }
    }

    public class GeminiRequest
    {
        public GeminiContent[]? contents { get; set; }
        public GeminiSystemInstruction? systemInstruction { get; set; }
        public GeminiGenerationConfig? generationConfig { get; set; }
    }

    public class GeminiContent
    {
        public string? role { get; set; }
        public GeminiPart[]? parts { get; set; }
    }

    public class GeminiPart
    {
        public string? text { get; set; }
    }

    public class GeminiSystemInstruction
    {
        public GeminiPart[]? parts { get; set; }
    }

    public class GeminiGenerationConfig
    {
        public double? temperature { get; set; }
    }

    public class GeminiResponse
    {
        public GeminiCandidate[]? candidates { get; set; }
    }

    public class GeminiCandidate
    {
        public GeminiContent? content { get; set; }
        public string? finishReason { get; set; }
    }
}
