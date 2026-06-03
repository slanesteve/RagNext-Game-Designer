using System.Text.Json;
using System.Text.Json.Serialization;
using RagsCore.Models;
using RagsCore.Actions;
using System.Collections.Generic;

namespace RagsCore
{
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(Game))]
    [JsonSerializable(typeof(CommandCatalog))]
    [JsonSerializable(typeof(ConditionCatalog))]
    [JsonSerializable(typeof(RagsCore.Models.Action))]
    [JsonSerializable(typeof(Room))]
    [JsonSerializable(typeof(Character))]
    [JsonSerializable(typeof(GameObject))]
    [JsonSerializable(typeof(MediaAsset))]
    [JsonSerializable(typeof(SplashScreenSettings))]
    [JsonSerializable(typeof(StepDefinitionBase))]
    [JsonSerializable(typeof(CommandDefinition))]
    [JsonSerializable(typeof(ConditionDefinition))]
    [JsonSerializable(typeof(List<CommandDefinition>))]
    [JsonSerializable(typeof(List<ConditionDefinition>))]
    [JsonSerializable(typeof(ActionStep))]
    public partial class RagsJsonContext : JsonSerializerContext
    {
        private static RagsJsonContext? _default;
        public static RagsJsonContext CustomDefault
        {
            get
            {
                if (_default == null)
                {
                    var opts = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve
                    };
                    opts.Converters.Add(new StepDefinitionBaseJsonConverter());
                    _default = new RagsJsonContext(opts);
                }
                return _default;
            }
        }
    }
}
