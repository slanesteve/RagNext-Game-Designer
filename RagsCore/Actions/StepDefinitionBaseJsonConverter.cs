using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RagsCore.Actions
{
    public sealed class StepDefinitionBaseJsonConverter : JsonConverter<StepDefinitionBase>
    {
        public override StepDefinitionBase? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var root = doc.RootElement;

            // Try discriminator "kind" first (supports old/new saves)
            string? kind = null;
            if (root.TryGetProperty("kind", out var kindProp))
            {
                kind = kindProp.ValueKind switch
                {
                    JsonValueKind.String => kindProp.GetString(),
                    JsonValueKind.Number => kindProp.GetInt32().ToString(),
                    _ => null
                };
            }

            var json = root.GetRawText();

            if (string.Equals(kind, "Command", StringComparison.OrdinalIgnoreCase) || kind == "0")
                return JsonSerializer.Deserialize<CommandDefinition>(json, options);

            if (string.Equals(kind, "Condition", StringComparison.OrdinalIgnoreCase) || kind == "1")
                return JsonSerializer.Deserialize<ConditionDefinition>(json, options);

            // Heuristic fallback: presence of "steps" means ConditionDefinition
            if (root.TryGetProperty("steps", out _))
                return JsonSerializer.Deserialize<ConditionDefinition>(json, options);

            // Default to CommandDefinition
            return JsonSerializer.Deserialize<CommandDefinition>(json, options);
        }

        public override void Write(Utf8JsonWriter writer, StepDefinitionBase value, JsonSerializerOptions options)
        {
            switch (value)
            {
                case CommandDefinition cmd:
                    JsonSerializer.Serialize(writer, cmd, options);
                    break;
                case ConditionDefinition cond:
                    JsonSerializer.Serialize(writer, cond, options);
                    break;
                default:
                    throw new NotSupportedException($"Unknown StepDefinitionBase type: {value.GetType().FullName}");
            }
        }
    }
}