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
                "player.inRoom"     => typeof(PlayerInRoomConditionData),
                "room.hasObject"    => typeof(RoomHasObjectConditionData),
                "item.inRoom"       => typeof(ItemInRoomConditionData),
                "item.heldByPlayer" => typeof(ItemHeldByPlayerConditionData),
                "item.notHeldByPlayer" => typeof(ItemNotHeldByPlayerConditionData),
                "item.heldByChar"   => typeof(ItemHeldByCharacterConditionData),
                "item.inObject"     => typeof(ItemInObjectConditionData),
                "item.notInObject"  => typeof(ItemNotInObjectConditionData),
                "item.isWorn"       => typeof(ItemWornConditionData),
                "item.canWear"      => typeof(ItemCanWearConditionData),
                "player.sameRoom"   => typeof(PlayerInSameRoomAsConditionData),
                "char.inRoom"       => typeof(CharacterInRoomConditionData),
                "char.gender"       => typeof(CharacterGenderConditionData),
                "player.gender"     => typeof(PlayerGenderConditionData),
                "room.isExitLocked" => typeof(IsRoomExitLockedConditionData),
                "char.attributeCheck" => typeof(CharacterAttributeCheckConditionData),
                "char.customPropertyCheck" => typeof(CharacterAttributeCheckConditionData),
                "item.attributeCheck" => typeof(ItemAttributeCheckConditionData),
                "item.customPropertyCheck" => typeof(ItemAttributeCheckConditionData),
                "player.attributeCheck" => typeof(PlayerAttributeCheckConditionData),
                "player.customPropertyCheck" => typeof(PlayerAttributeCheckConditionData),
                "room.attributeCheck" => typeof(RoomAttributeCheckConditionData),
                "room.customPropertyCheck" => typeof(RoomAttributeCheckConditionData),
                "timer.isActive"    => typeof(TimerActiveConditionData),
                "date.partCompare"  => typeof(DateTimePartComparisonConditionData),
                "date.isPast"       => typeof(DateTimeIsPastConditionData),
                "date.isFuture"     => typeof(DateTimeIsFutureConditionData),
                "date.compareVars"  => typeof(DateTimeCompareVariablesConditionData),
                "date.diffCompare"  => typeof(DateTimeCompareDifferenceConditionData),
                "date.compareConst" => typeof(DateTimeCompareConstantConditionData),
                "date.isValid"      => typeof(DateTimeIsValidConditionData),

                // ── Commands ───────────────────────────────────────────────
                "general.displayText"    => typeof(DisplayTextCommandData),
                "general.addComment"     => typeof(AddCommentCommandData),
                "var.set"                => typeof(SetVariableCommandData),
                "var.evaluate"           => typeof(EvaluateFormulaCommandData),
                "var.inc"                => typeof(VariableIncrementCommandData),
                "var.dec"                => typeof(VariableDecrementCommandData),
                "var.setToVar"           => typeof(VariableSetToVariableCommandData),
                "var.setRandom"          => typeof(SetNumericRandomlyCommandData),
                "player.moveTo"          => typeof(MovePlayerToRoomCommandData),
                "room.addObject"         => typeof(AddObjectToRoomCommandData),
                "room.removeObject"      => typeof(RemoveObjectFromRoomCommandData),
                "item.showInteractiveScreen" => typeof(ShowInteractiveScreenCommandData),
                "room.setExit"           => typeof(SetRoomExitCommandData),
                "room.disableExit"       => typeof(DisableRoomExitCommandData),
                "room.lockExit"          => typeof(LockRoomExitCommandData),
                "room.unlockExit"        => typeof(UnlockRoomExitCommandData),
                "player.setName"         => typeof(PlayerSetNameCommandData),
                "player.setDescription"  => typeof(PlayerSetDescriptionCommandData),
                "player.setGender"       => typeof(PlayerSetGenderCommandData),
                "player.setPortraitMedia"=> typeof(PlayerSetPortraitMediaCommandData),
                "player.swapCharacter"   => typeof(SwapPlayerCharacterCommandData),
                "ui.showSplashScreen"    => typeof(ShowSplashScreenCommandData),
                "char.moveToRoom"        => typeof(CharacterMoveToRoomCommandData),
                "char.moveToRandomAdjacent" => typeof(CharacterMoveToRandomAdjacentCommandData),
                "char.moveAlongPatrolPath" => typeof(CharacterMoveAlongPatrolPathCommandData),
                "char.displayPortrait"   => typeof(CharacterDisplayPortraitCommandData),
                "char.setPortraitMedia"  => typeof(CharacterSetPortraitMediaCommandData),
                "media.playSound"        => typeof(PlaySoundEffectCommandData),
                "media.playVideo"        => typeof(PlayVideoCommandData),
                "media.stopSound"        => typeof(StopSoundEffectCommandData),
                "media.setBackgroundMusic" => typeof(SetBackgroundMusicCommandData),
                "media.stopBackgroundMusic" => typeof(StopBackgroundMusicCommandData),
                "media.displayMultimedia"=> typeof(DisplayMultimediaCommandData),
                "general.endGame"        => typeof(EndGameCommandData),
                "general.waitForContinue"=> typeof(WaitForContinueCommandData),
                "general.showMap"        => typeof(ShowMapCommandData),
                "general.promptInput"    => typeof(PromptPlayerInputCommandData),
                "general.openContainer"  => typeof(OpenContainerCommandData),
                "general.closeContainer" => typeof(CloseContainerCommandData),
                "item.wear"              => typeof(WearItemCommandData),
                "item.remove"            => typeof(RemoveItemCommandData),
                "general.callFunction"   => typeof(CallFunctionCommandData),
                "char.damage"            => typeof(DamageCharacterCommandData),
                "char.setState"          => typeof(SetCharacterStateCommandData),
                "general.triggerTurnTick"=> typeof(TriggerTurnTickCommandData),
                "char.setActionActive"   => typeof(CharacterSetActionActiveCommandData),
                // Bug #5: Scoped entity variants.
                "item.setActionActive"   => typeof(ItemSetActionActiveCommandData),
                "room.setActionActive"   => typeof(RoomSetActionActiveCommandData),
                "player.setActionActive" => typeof(PlayerSetActionActiveCommandData),
                "timer.setTimerActive"   => typeof(SetTimerActiveCommandData),
                "status.show"            => typeof(ShowStatusElementCommandData),
                "status.hide"            => typeof(HideStatusElementCommandData),
                "status.setText"         => typeof(SetStatusElementTextCommandData),
                "status.setImage"        => typeof(SetStatusElementImageCommandData),
                "status.isVisible"       => typeof(StatusElementVisibleConditionData),
                "general.startDialogue"  => typeof(StartDialogueCommandData),
                "general.addCustomChoice" => typeof(AddCustomChoiceCommandData),
                "general.clearCustomChoice" => typeof(ClearCustomChoiceCommandData),
                "general.removeCustomChoice" => typeof(RemoveCustomChoiceCommandData),

                "variable.forEachLoop"      => typeof(ForEachLoopCommandData),
                "variable.breakLoop"        => typeof(BreakLoopCommandData),
                "variable.setArrayElement"  => typeof(SetArrayElementCommandData),
                "variable.addArrayRow"      => typeof(AddArrayRowCommandData),
                "variable.removeArrayRow"   => typeof(RemoveArrayRowCommandData),
                "variable.appendText"       => typeof(AppendTextCommandData),
                "variable.appendLine"       => typeof(AppendLineCommandData),
                "general.switch"            => typeof(SwitchCommandData),

                "char.setAttribute"      => typeof(SetCharacterAttributeCommandData),
                "player.setAttribute"    => typeof(SetPlayerAttributeCommandData),
                "timer.setAttribute"     => typeof(SetTimerAttributeCommandData),
                "item.setAttribute"      => typeof(SetItemAttributeCommandData),
                "room.setAttribute"      => typeof(SetRoomAttributeCommandData),

                "object.displayDescription" => typeof(ObjectDisplayDescriptionCommandData),
                "player.displayDescription" => typeof(PlayerDisplayDescriptionCommandData),
                "char.displayDescription"   => typeof(CharacterDisplayDescriptionCommandData),
                "room.displayDescription"   => typeof(RoomDisplayDescriptionCommandData),
                "object.moveToCharacter"    => typeof(ObjectMoveToCharacterCommandData),
                "object.moveToInventory"    => typeof(ObjectMoveToInventoryCommandData),
                "object.moveInsideObject"   => typeof(ObjectMoveInsideObjectCommandData),
                
                "char.moveInventoryToPlayer" => typeof(CharacterMoveInventoryToPlayerCommandData),
                "char.moveToObject"         => typeof(CharacterMoveToObjectCommandData),
                "char.setDescription"       => typeof(CharacterSetDescriptionCommandData),
                "char.setDisplayName"       => typeof(CharacterSetDisplayNameCommandData),
                "room.setDescription"       => typeof(RoomSetDescriptionCommandData),
                "room.setPicture"           => typeof(RoomSetPictureCommandData),
                "ui.setStatusBarVisible"    => typeof(SetStatusBarVisibleCommandData),
                "ui.setHotspotActive"       => typeof(SetHotspotActiveCommandData),
                "char.setGender"            => typeof(CharacterSetGenderCommandData),
                "player.moveInventoryToChar" => typeof(PlayerMoveInventoryToCharacterCommandData),
                "player.moveInventoryToRoom" => typeof(PlayerMoveInventoryToRoomCommandData),
                "player.moveToChar"         => typeof(PlayerMoveToCharacterCommandData),
                "player.moveToObject"       => typeof(PlayerMoveToObjectCommandData),
                "player.screenShake"        => typeof(ScreenShakeCommandData),
                "room.moveItemsToPlayer"    => typeof(RoomMoveItemsToPlayerCommandData),

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

    /// <summary>
    /// Custom converter to safely map polymorphic attribute structures (objects vs. array of custom property objects)
    /// into a flat C# Dictionary<string, string> for fast lookup.
    /// </summary>
    public class AttributesConverter : JsonConverter<Dictionary<string, string>>
    {
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, Dictionary<string, string>? value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override Dictionary<string, string>? ReadJson(
            JsonReader reader,
            Type objectType,
            Dictionary<string, string>? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (reader.TokenType == JsonToken.Null) return dict;

            var token = JToken.Load(reader);
            if (token.Type == JTokenType.Object)
            {
                var jo = (JObject)token;
                var valuesArray = jo["$values"] as JArray;
                if (valuesArray != null)
                {
                    PopulateFromAttributeArray(valuesArray, dict);
                }
                else
                {
                    foreach (var prop in jo.Properties())
                    {
                        if (prop.Name.StartsWith("$")) continue;
                        var valStr = prop.Value?.ToString();
                        if (valStr != null)
                        {
                            dict[prop.Name] = valStr;
                        }
                    }
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                PopulateFromAttributeArray((JArray)token, dict);
            }

            return dict;
        }

        private void PopulateFromAttributeArray(JArray array, Dictionary<string, string> dict)
        {
            foreach (var item in array)
            {
                if (item is JObject itemObj)
                {
                    var name = itemObj["name"]?.ToString() ?? itemObj["Name"]?.ToString();
                    var val = itemObj["value"]?.ToString() ?? itemObj["Value"]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        dict[name] = val ?? string.Empty;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Converts switch command cases dictionary of string to List of polymorphic ActionSteps.
    /// </summary>
    public class SwitchCasesConverter : JsonConverter<Dictionary<string, List<ActionStepData>>>
    {
        public override bool CanWrite => false;
        public override void WriteJson(JsonWriter writer, Dictionary<string, List<ActionStepData>>? value, JsonSerializer serializer) => throw new NotImplementedException();
        public override Dictionary<string, List<ActionStepData>>? ReadJson(
            JsonReader reader,
            Type objectType,
            Dictionary<string, List<ActionStepData>>? existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null) return null;
            var dict = new Dictionary<string, List<ActionStepData>>();
            var jobj = JObject.Load(reader);
            foreach (var prop in jobj.Properties())
            {
                var list = new List<ActionStepData>();
                if (prop.Value is JArray array)
                {
                    foreach (var token in array)
                    {
                        if (token is JObject jo)
                        {
                            using var subReader = jo.CreateReader();
                            if (subReader.Read())
                            {
                                var step = serializer.Deserialize<ActionStepData>(subReader);
                                if (step != null) list.Add(step);
                            }
                        }
                    }
                }
                dict[prop.Name] = list;
            }
            return dict;
        }
    }
}
