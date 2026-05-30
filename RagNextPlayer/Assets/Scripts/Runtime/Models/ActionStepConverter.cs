#nullable enable
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RagNextPlayer.Runtime.Models
{
    /// <summary>
    /// Reads the "$type" discriminator in each ActionStep JSON node and
    /// deserializes to the correct concrete class. Mirrors the
    /// polymorphic behavior of RagsCore.Actions.ActionStep.
    /// </summary>
    public class ActionStepConverter : JsonConverter
    {
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(ActionStepData);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            throw new NotImplementedException("ActionStepConverter is read-only in the Player.");
        }

        public override object? ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            JObject jo = JObject.Load(reader);
            string? typeName = jo["$type"]?.Value<string>();

            if (typeName is null)
                throw new JsonSerializationException("ActionStep JSON is missing required '$type' discriminator field.");

            Type targetType = typeName switch
            {
                // ── Conditions ─────────────────────────────────────────────
                "var.equals"        => typeof(VariableEqualsConditionData),
                "var.compare"       => typeof(VariableComparisonConditionData),
                "var.compareVar"    => typeof(VariableComparisonToVariableConditionData),
                "player.inRoom"     => typeof(PlayerInRoomConditionData),
                "room.hasObject"    => typeof(RoomHasObjectConditionData),
                "item.inRoom"       => typeof(ItemInRoomConditionData),
                "item.heldByPlayer" => typeof(ItemHeldByPlayerConditionData),
                "item.notHeldByPlayer" => typeof(ItemNotHeldByPlayerConditionData),
                "item.heldByChar"   => typeof(ItemHeldByCharacterConditionData),
                "item.inObject"     => typeof(ItemInObjectConditionData),
                "item.notInObject"  => typeof(ItemNotInObjectConditionData),
                "player.sameRoom"   => typeof(PlayerInSameRoomAsConditionData),
                "char.inRoom"       => typeof(CharacterInRoomConditionData),
                "char.gender"       => typeof(CharacterGenderConditionData),
                "player.gender"     => typeof(PlayerGenderConditionData),
                "room.isExitLocked" => typeof(IsRoomExitLockedConditionData),

                // ── Commands ───────────────────────────────────────────────
                "general.displayText"    => typeof(DisplayTextCommandData),
                "general.addComment"     => typeof(AddCommentCommandData),
                "var.set"                => typeof(SetVariableCommandData),
                "var.inc"                => typeof(VariableIncrementCommandData),
                "var.dec"                => typeof(VariableDecrementCommandData),
                "var.setToVar"           => typeof(VariableSetToVariableCommandData),
                "var.setRandom"          => typeof(SetNumericRandomlyCommandData),
                "player.moveTo"          => typeof(MovePlayerToRoomCommandData),
                "room.addObject"         => typeof(AddObjectToRoomCommandData),
                "room.removeObject"      => typeof(RemoveObjectFromRoomCommandData),
                "room.setExit"           => typeof(SetRoomExitCommandData),
                "room.disableExit"       => typeof(DisableRoomExitCommandData),
                "room.lockExit"          => typeof(LockRoomExitCommandData),
                "room.unlockExit"        => typeof(UnlockRoomExitCommandData),
                "player.setName"         => typeof(PlayerSetNameCommandData),
                "player.setDescription"  => typeof(PlayerSetDescriptionCommandData),
                "player.setGender"       => typeof(PlayerSetGenderCommandData),
                "player.setPortraitMedia"=> typeof(PlayerSetPortraitMediaCommandData),
                "char.moveToRoom"        => typeof(CharacterMoveToRoomCommandData),
                "char.displayPortrait"   => typeof(CharacterDisplayPortraitCommandData),
                "char.setPortraitMedia"  => typeof(CharacterSetPortraitMediaCommandData),
                "media.playSound"        => typeof(PlaySoundEffectCommandData),
                "media.stopSound"        => typeof(StopSoundEffectCommandData),
                "media.displayMultimedia"=> typeof(DisplayMultimediaCommandData),
                "general.endGame"        => typeof(EndGameCommandData),
                "general.promptInput"    => typeof(PromptPlayerInputCommandData),
                "general.openContainer"  => typeof(OpenContainerCommandData),
                "general.closeContainer" => typeof(CloseContainerCommandData),
                "general.callFunction"   => typeof(CallFunctionCommandData),
                "char.damage"            => typeof(DamageCharacterCommandData),
                "char.setState"          => typeof(SetCharacterStateCommandData),
                "general.triggerTurnTick"=> typeof(TriggerTurnTickCommandData),
                "general.startDialogue"  => typeof(StartDialogueCommandData),
                "general.addCustomChoice" => typeof(AddCustomChoiceCommandData),
                "general.clearCustomChoice" => typeof(ClearCustomChoiceCommandData),
                "general.removeCustomChoice" => typeof(RemoveCustomChoiceCommandData),

                "object.displayDescription" => typeof(ObjectDisplayDescriptionCommandData),
                "object.moveToCharacter"    => typeof(ObjectMoveToCharacterCommandData),
                "object.moveToInventory"    => typeof(ObjectMoveToInventoryCommandData),
                "object.moveInsideObject"   => typeof(ObjectMoveInsideObjectCommandData),

                _ => throw new JsonSerializationException($"Unknown ActionStep $type: '{typeName}'")
            };

            using var subReader = jo.CreateReader();
            var step = (ActionStepData?)serializer.Deserialize(subReader, targetType);
            if (step is not null)
            {
                step.Type = typeName;
            }
            return step;
        }
    }

    /// <summary>
    /// Converts a JSON array of polymorphic ActionSteps into a List of ActionStepData.
    /// </summary>
    public class ActionStepListConverter : JsonConverter<List<ActionStepData>>
    {
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, List<ActionStepData>? value, JsonSerializer serializer)
        {
            throw new NotImplementedException("ActionStepListConverter is read-only in the Player.");
        }

        public override List<ActionStepData>? ReadJson(
            JsonReader reader,
            Type objectType,
            List<ActionStepData>? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            var list = new List<ActionStepData>();
            JArray array = JArray.Load(reader);

            foreach (var token in array)
            {
                if (token is JObject jo)
                {
                    using var subReader = jo.CreateReader();
                    // Advance to the start object token
                    if (subReader.Read())
                    {
                        var step = serializer.Deserialize<ActionStepData>(subReader);
                        if (step != null)
                        {
                            list.Add(step);
                        }
                    }
                }
            }

            return list;
        }
    }
}
