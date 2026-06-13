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
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonSerializable(typeof(Game))]
    [JsonSerializable(typeof(CommandCatalog))]
    [JsonSerializable(typeof(ConditionCatalog))]
    [JsonSerializable(typeof(RagsCore.Models.Action))]
    [JsonSerializable(typeof(List<RagsCore.Models.Action>))]
    [JsonSerializable(typeof(Room))]
    [JsonSerializable(typeof(Character))]
    [JsonSerializable(typeof(Player))]
    [JsonSerializable(typeof(GameObject))]
    [JsonSerializable(typeof(MediaAsset))]
    [JsonSerializable(typeof(SplashScreenSettings))]
    [JsonSerializable(typeof(StepDefinitionBase))]
    [JsonSerializable(typeof(CommandDefinition))]
    [JsonSerializable(typeof(ConditionDefinition))]
    [JsonSerializable(typeof(List<CommandDefinition>))]
    [JsonSerializable(typeof(List<ConditionDefinition>))]
    [JsonSerializable(typeof(GameVariable))]
    [JsonSerializable(typeof(System.Collections.ObjectModel.ObservableCollection<string>), TypeInfoPropertyName = "GameVariableColumnsCollection")]
    [JsonSerializable(typeof(System.Collections.ObjectModel.ObservableCollection<System.Collections.ObjectModel.ObservableCollection<string>>), TypeInfoPropertyName = "GameVariableRowsCollection")]
    [JsonSerializable(typeof(ActionStep))]
    [JsonSerializable(typeof(PlayerInputType))]
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
                        NumberHandling = JsonNumberHandling.AllowReadingFromString,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve
                    };
                    opts.Converters.Add(new StepDefinitionBaseJsonConverter());
                    opts.Converters.Add(new LenientBooleanConverter());
                    opts.Converters.Add(new LenientDoubleConverter());
                    opts.Converters.Add(new LenientSingleConverter());
                    opts.Converters.Add(new LenientInt32Converter());
                    _default = new RagsJsonContext(opts);
                }
                return _default;
            }
        }

        private static RagsJsonContext? _flatContext;
        public static RagsJsonContext FlatContext
        {
            get
            {
                if (_flatContext == null)
                {
                    var opts = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNameCaseInsensitive = true,
                        NumberHandling = JsonNumberHandling.AllowReadingFromString,
                        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                        ReferenceHandler = null
                    };
                    opts.Converters.Add(new StepDefinitionBaseJsonConverter());
                    opts.Converters.Add(new LenientBooleanConverter());
                    opts.Converters.Add(new LenientDoubleConverter());
                    opts.Converters.Add(new LenientSingleConverter());
                    opts.Converters.Add(new LenientInt32Converter());
                    _flatContext = new RagsJsonContext(opts);
                }
                return _flatContext;
            }
        }
    }

    public class LenientBooleanConverter : JsonConverter<bool>
    {
        public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.True) return true;
            if (reader.TokenType == JsonTokenType.False) return false;
            if (reader.TokenType == JsonTokenType.String)
            {
                var val = reader.GetString();
                if (bool.TryParse(val, out var result))
                {
                    return result;
                }
                if (val == "1" || string.Equals(val, "true", StringComparison.OrdinalIgnoreCase)) return true;
                if (val == "0" || string.Equals(val, "false", StringComparison.OrdinalIgnoreCase)) return false;
            }
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt32(out var intVal))
                {
                    return intVal != 0;
                }
            }
            return false;
        }

        public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        {
            writer.WriteBooleanValue(value);
        }
    }

    public class LenientDoubleConverter : JsonConverter<double>
    {
        public override double Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number) return reader.GetDouble();
            if (reader.TokenType == JsonTokenType.String)
            {
                var val = reader.GetString();
                if (string.IsNullOrWhiteSpace(val)) return 0.0;
                if (double.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var res)) return res;
            }
            return 0.0;
        }
        public override void Write(Utf8JsonWriter writer, double value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
    }

    public class LenientSingleConverter : JsonConverter<float>
    {
        public override float Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number) return reader.GetSingle();
            if (reader.TokenType == JsonTokenType.String)
            {
                var val = reader.GetString();
                if (string.IsNullOrWhiteSpace(val)) return 0f;
                if (float.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var res)) return res;
            }
            return 0f;
        }
        public override void Write(Utf8JsonWriter writer, float value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
    }

    public class LenientInt32Converter : JsonConverter<int>
    {
        public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Number) return reader.GetInt32();
            if (reader.TokenType == JsonTokenType.String)
            {
                var val = reader.GetString();
                if (string.IsNullOrWhiteSpace(val)) return 0;
                if (int.TryParse(val, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var res)) return res;
            }
            return 0;
        }
        public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
    }
}
