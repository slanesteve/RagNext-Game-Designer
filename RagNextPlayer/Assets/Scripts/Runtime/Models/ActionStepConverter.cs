using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using RagNextPlayer.Runtime.Models;

namespace RagNextPlayer.Runtime.Models
{
    /// <summary>
    /// Reads the "$type" discriminator in each ActionStep JSON node and
    /// deserializes to the correct concrete class. Mirrors the
    /// [JsonDerivedType] attributes declared on RagsCore.Actions.ActionStep.
    /// </summary>
    public class ActionStepConverter : JsonConverter<ActionStepData>
    {
        public override ActionStepData? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            // Clone the reader to sniff the $type field without consuming it
            Utf8JsonReader readerClone = reader;
            string? typeName = null;

            if (readerClone.TokenType != JsonTokenType.StartObject)
                throw new JsonException("Expected StartObject for ActionStep");

            while (readerClone.Read())
            {
                if (readerClone.TokenType == JsonTokenType.EndObject) break;

                if (readerClone.TokenType == JsonTokenType.PropertyName)
                {
                    string propName = readerClone.GetString() ?? string.Empty;
                    readerClone.Read();
                    if (propName == "$type")
                    {
                        typeName = readerClone.GetString();
                        break;
                    }
                }
            }

            if (typeName is null)
                throw new JsonException("ActionStep JSON is missing required '$type' discriminator field.");

            return typeName switch
            {
                // ── Conditions ─────────────────────────────────────────────
                "var.equals"        => JsonSerializer.Deserialize<VariableEqualsConditionData>(ref reader, options),
                "var.compare"       => JsonSerializer.Deserialize<VariableComparisonConditionData>(ref reader, options),
                "var.compareVar"    => JsonSerializer.Deserialize<VariableComparisonToVariableConditionData>(ref reader, options),
                "player.inRoom"     => JsonSerializer.Deserialize<PlayerInRoomConditionData>(ref reader, options),
                "room.hasObject"    => JsonSerializer.Deserialize<RoomHasObjectConditionData>(ref reader, options),
                "item.inRoom"       => JsonSerializer.Deserialize<ItemInRoomConditionData>(ref reader, options),
                "item.heldByPlayer" => JsonSerializer.Deserialize<ItemHeldByPlayerConditionData>(ref reader, options),
                "item.notHeldByPlayer" => JsonSerializer.Deserialize<ItemNotHeldByPlayerConditionData>(ref reader, options),
                "item.heldByChar"   => JsonSerializer.Deserialize<ItemHeldByCharacterConditionData>(ref reader, options),
                "item.inObject"     => JsonSerializer.Deserialize<ItemInObjectConditionData>(ref reader, options),
                "item.notInObject"  => JsonSerializer.Deserialize<ItemNotInObjectConditionData>(ref reader, options),
                "player.sameRoom"   => JsonSerializer.Deserialize<PlayerInSameRoomAsConditionData>(ref reader, options),
                "char.inRoom"       => JsonSerializer.Deserialize<CharacterInRoomConditionData>(ref reader, options),
                "char.gender"       => JsonSerializer.Deserialize<CharacterGenderConditionData>(ref reader, options),
                "player.gender"     => JsonSerializer.Deserialize<PlayerGenderConditionData>(ref reader, options),

                // ── Commands ───────────────────────────────────────────────
                "general.displayText"    => JsonSerializer.Deserialize<DisplayTextCommandData>(ref reader, options),
                "general.addComment"     => JsonSerializer.Deserialize<AddCommentCommandData>(ref reader, options),
                "var.set"                => JsonSerializer.Deserialize<SetVariableCommandData>(ref reader, options),
                "var.inc"                => JsonSerializer.Deserialize<VariableIncrementCommandData>(ref reader, options),
                "var.dec"                => JsonSerializer.Deserialize<VariableDecrementCommandData>(ref reader, options),
                "var.setToVar"           => JsonSerializer.Deserialize<VariableSetToVariableCommandData>(ref reader, options),
                "var.setRandom"          => JsonSerializer.Deserialize<SetNumericRandomlyCommandData>(ref reader, options),
                "player.moveTo"          => JsonSerializer.Deserialize<MovePlayerToRoomCommandData>(ref reader, options),
                "room.addObject"         => JsonSerializer.Deserialize<AddObjectToRoomCommandData>(ref reader, options),
                "room.removeObject"      => JsonSerializer.Deserialize<RemoveObjectFromRoomCommandData>(ref reader, options),
                "room.setExit"           => JsonSerializer.Deserialize<SetRoomExitCommandData>(ref reader, options),
                "room.disableExit"       => JsonSerializer.Deserialize<DisableRoomExitCommandData>(ref reader, options),
                "player.setName"         => JsonSerializer.Deserialize<PlayerSetNameCommandData>(ref reader, options),
                "player.setDescription"  => JsonSerializer.Deserialize<PlayerSetDescriptionCommandData>(ref reader, options),
                "player.setGender"       => JsonSerializer.Deserialize<PlayerSetGenderCommandData>(ref reader, options),
                "player.setPortraitMedia"=> JsonSerializer.Deserialize<PlayerSetPortraitMediaCommandData>(ref reader, options),
                "char.moveToRoom"        => JsonSerializer.Deserialize<CharacterMoveToRoomCommandData>(ref reader, options),
                "char.displayPortrait"   => JsonSerializer.Deserialize<CharacterDisplayPortraitCommandData>(ref reader, options),
                "char.setPortraitMedia"  => JsonSerializer.Deserialize<CharacterSetPortraitMediaCommandData>(ref reader, options),
                "media.playSound"        => JsonSerializer.Deserialize<PlaySoundEffectCommandData>(ref reader, options),
                "media.displayMultimedia"=> JsonSerializer.Deserialize<DisplayMultimediaCommandData>(ref reader, options),

                _ => throw new JsonException($"Unknown ActionStep $type: '{typeName}'")
            };
        }

        public override void Write(Utf8JsonWriter writer, ActionStepData value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value, value.GetType(), options);
    }
}
