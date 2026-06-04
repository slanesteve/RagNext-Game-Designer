using System.Text.Json.Serialization;
using System.Collections.Generic;
using RagNext.Models;
using RagNext.Designer.Avalonia.Models;
using RagNext.Designer.Avalonia.ViewModels;
using RagNext.Designer.Avalonia.Views;

namespace RagNext.Designer.Avalonia.Services
{
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(MediaTreeDocument))]
    [JsonSerializable(typeof(MediaFolder))]
    [JsonSerializable(typeof(AppSettings))]
    [JsonSerializable(typeof(List<string>))]
    [JsonSerializable(typeof(List<ActionLibraryViewModel.ActionTemplate>))]
    [JsonSerializable(typeof(ExportGameDto))]
    [JsonSerializable(typeof(ExportPlayerDto))]
    [JsonSerializable(typeof(ExportRoomDto))]
    [JsonSerializable(typeof(ExportObjectDto))]
    [JsonSerializable(typeof(ExportActionDto))]
    [JsonSerializable(typeof(ExportVariableDto))]
    [JsonSerializable(typeof(ExportMediaAssetDto))]
    [JsonSerializable(typeof(ExportFunctionDto))]
    [JsonSerializable(typeof(ExportTimerDto))]
    [JsonSerializable(typeof(ExportSplashScreenDto))]
    [JsonSerializable(typeof(AICoAuthorRequest))]
    [JsonSerializable(typeof(AICoAuthorMessage))]
    [JsonSerializable(typeof(AIResponseFormat))]
    [JsonSerializable(typeof(ImageGenRequest))]
    [JsonSerializable(typeof(OpenAiImageGenRequest))]
    [JsonSerializable(typeof(CatalogsDto))]
    [JsonSerializable(typeof(CatalogEntityDto))]
    [JsonSerializable(typeof(CatalogPlayerDto))]
    [JsonSerializable(typeof(ReflectionEntityDto))]
    [JsonSerializable(typeof(List<ReflectionEntityDto>))]
    internal partial class DesignerJsonContext : JsonSerializerContext
    {
    }
}
