using System;
using System.Text.Json.Serialization;
using System.Text.Json;
using RagsCore.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;

namespace RagsCore.Actions
{
    [JsonConverter(typeof(JsonStringEnumConverter<ActionStepKind>))]
    public enum ActionStepKind { Condition, Command }
    // Polymorphic base for both conditions and commands.
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    // Conditions
    [JsonDerivedType(typeof(VariableEqualsCondition), "var.equals")]
    [JsonDerivedType(typeof(PlayerInRoomCondition), "player.inRoom")]
    [JsonDerivedType(typeof(RoomHasObjectCondition), "room.hasObject")]
    [JsonDerivedType(typeof(PlayerInSameRoomAsCondition), "player.sameRoom")]
    [JsonDerivedType(typeof(ItemHeldByPlayerCondition), "item.heldByPlayer")]
    [JsonDerivedType(typeof(VariableComparisonCondition), "var.compare")]
    [JsonDerivedType(typeof(CharacterGenderCondition), "char.gender")]
    [JsonDerivedType(typeof(CharacterInRoomCondition), "char.inRoom")]
    [JsonDerivedType(typeof(ItemInRoomCondition), "item.inRoom")]
    [JsonDerivedType(typeof(PlayerGenderCondition), "player.gender")]
    [JsonDerivedType(typeof(ItemHeldByCharacterCondition), "item.heldByChar")]
    [JsonDerivedType(typeof(ItemInObjectCondition), "item.inObject")]
    [JsonDerivedType(typeof(ItemNotHeldByPlayerCondition), "item.notHeldByPlayer")]
    [JsonDerivedType(typeof(ItemNotInObjectCondition), "item.notInObject")]
    [JsonDerivedType(typeof(ItemWornCondition), "item.isWorn")]
    [JsonDerivedType(typeof(ItemCanWearCondition), "item.canWear")]
    [JsonDerivedType(typeof(IsRoomExitLockedCondition), "room.isExitLocked")]
    [JsonDerivedType(typeof(CharacterAttributeCheckCondition), "char.attributeCheck")]
    [JsonDerivedType(typeof(ItemAttributeCheckCondition), "item.attributeCheck")]
    [JsonDerivedType(typeof(PlayerAttributeCheckCondition), "player.attributeCheck")]
    [JsonDerivedType(typeof(RoomAttributeCheckCondition), "room.attributeCheck")]
    [JsonDerivedType(typeof(TimerActiveCondition), "timer.isActive")]
    [JsonDerivedType(typeof(DateTimePartComparisonCondition), "date.partCompare")]
    [JsonDerivedType(typeof(DateTimeIsPastCondition), "date.isPast")]
    [JsonDerivedType(typeof(DateTimeIsFutureCondition), "date.isFuture")]
    [JsonDerivedType(typeof(DateTimeCompareVariablesCondition), "date.compareVars")]
    [JsonDerivedType(typeof(DateTimeCompareDifferenceCondition), "date.diffCompare")]
    [JsonDerivedType(typeof(DateTimeCompareConstantCondition), "date.compareConst")]
    [JsonDerivedType(typeof(DateTimeIsValidCondition), "date.isValid")]
    [JsonDerivedType(typeof(StatusElementVisibleCondition), "status.isVisible")]
    // Commands
    [JsonDerivedType(typeof(ShowStatusElementCommand), "status.show")]
    [JsonDerivedType(typeof(HideStatusElementCommand), "status.hide")]
    [JsonDerivedType(typeof(SetStatusElementTextCommand), "status.setText")]
    [JsonDerivedType(typeof(SetStatusElementImageCommand), "status.setImage")]
    [JsonDerivedType(typeof(SetVariableCommand), "var.set")]
    [JsonDerivedType(typeof(MovePlayerToRoomCommand), "player.moveTo")]
    [JsonDerivedType(typeof(ShowInteractiveScreenCommand), "item.showInteractiveScreen")]
    [JsonDerivedType(typeof(ScreenShakeCommand), "player.screenShake")]
    [JsonDerivedType(typeof(AddObjectToRoomCommand), "room.addObject")]
    [JsonDerivedType(typeof(RemoveObjectFromRoomCommand), "room.removeObject")]
    [JsonDerivedType(typeof(ObjectDisplayDescriptionCommand), "object.displayDescription")]
    [JsonDerivedType(typeof(PlayerDisplayDescriptionCommand), "player.displayDescription")]
    [JsonDerivedType(typeof(CharacterDisplayDescriptionCommand), "char.displayDescription")]
    [JsonDerivedType(typeof(RoomDisplayDescriptionCommand), "room.displayDescription")]
    [JsonDerivedType(typeof(ObjectMoveToCharacterCommand), "object.moveToCharacter")]
    [JsonDerivedType(typeof(ObjectMoveToInventoryCommand), "object.moveToInventory")]
    [JsonDerivedType(typeof(ObjectMoveInsideObjectCommand), "object.moveInsideObject")]
    [JsonDerivedType(typeof(DisplayTextCommand), "general.displayText")]
    [JsonDerivedType(typeof(AddCommentCommand), "general.addComment")]
    [JsonDerivedType(typeof(PlaySoundEffectCommand), "media.playSound")]
    [JsonDerivedType(typeof(PlayVideoCommand), "media.playVideo")]
    [JsonDerivedType(typeof(StopSoundEffectCommand), "media.stopSound")]
    [JsonDerivedType(typeof(PlayerSetNameCommand), "player.setName")]
    [JsonDerivedType(typeof(PlayerSetDescriptionCommand), "player.setDescription")]
    [JsonDerivedType(typeof(PlayerSetGenderCommand), "player.setGender")]
    [JsonDerivedType(typeof(CharacterSetGenderCommand), "char.setGender")]
    [JsonDerivedType(typeof(SetNumericRandomlyCommand), "var.setRandom")]
    [JsonDerivedType(typeof(CharacterMoveToRoomCommand), "char.moveToRoom")]
    [JsonDerivedType(typeof(CharacterMoveToRandomAdjacentCommand), "char.moveToRandomAdjacent")]
    [JsonDerivedType(typeof(CharacterMoveAlongPatrolPathCommand), "char.moveAlongPatrolPath")]
    [JsonDerivedType(typeof(DisplayMultimediaCommand), "media.displayMultimedia")]
    [JsonDerivedType(typeof(CharacterDisplayPortraitCommand), "char.displayPortrait")]
    [JsonDerivedType(typeof(CharacterSetPortraitMediaCommand), "char.setPortraitMedia")]
    [JsonDerivedType(typeof(PlayerSetPortraitMediaCommand), "player.setPortraitMedia")]
    [JsonDerivedType(typeof(VariableIncrementCommand), "var.inc")]
    [JsonDerivedType(typeof(VariableDecrementCommand), "var.dec")]
    [JsonDerivedType(typeof(VariableSetToVariableCommand), "var.setToVar")]
    [JsonDerivedType(typeof(EvaluateFormulaCommand), "var.evaluate")]
    [JsonDerivedType(typeof(SetRoomExitCommand), "room.setExit")]
    [JsonDerivedType(typeof(DisableRoomExitCommand), "room.disableExit")]
    [JsonDerivedType(typeof(LockRoomExitCommand), "room.lockExit")]
    [JsonDerivedType(typeof(UnlockRoomExitCommand), "room.unlockExit")]
    [JsonDerivedType(typeof(DamageCharacterCommand), "char.damage")]
    [JsonDerivedType(typeof(SetCharacterStateCommand), "char.setState")]
    [JsonDerivedType(typeof(TriggerTurnTickCommand), "general.triggerTurnTick")]
    [JsonDerivedType(typeof(EndGameCommand), "general.endGame")]
    [JsonDerivedType(typeof(PromptPlayerInputCommand), "general.promptInput")]
    [JsonDerivedType(typeof(OpenContainerCommand), "general.openContainer")]
    [JsonDerivedType(typeof(CloseContainerCommand), "general.closeContainer")]
    [JsonDerivedType(typeof(CallFunctionCommand), "general.callFunction")]
    [JsonDerivedType(typeof(StartDialogueCommand), "general.startDialogue")]
    [JsonDerivedType(typeof(AddCustomChoiceCommand), "general.addCustomChoice")]
    [JsonDerivedType(typeof(ClearCustomChoiceCommand), "general.clearCustomChoice")]
    [JsonDerivedType(typeof(RemoveCustomChoiceCommand), "general.removeCustomChoice")]
    [JsonDerivedType(typeof(SetRoomAttributeCommand), "room.setAttribute")]
    [JsonDerivedType(typeof(SetCharacterAttributeCommand), "char.setAttribute")]
    [JsonDerivedType(typeof(SetPlayerAttributeCommand), "player.setAttribute")]
    [JsonDerivedType(typeof(SetTimerAttributeCommand), "timer.setAttribute")]
    [JsonDerivedType(typeof(SetItemAttributeCommand), "item.setAttribute")]
    [JsonDerivedType(typeof(CharacterSetActionActiveCommand), "char.setActionActive")]
    [JsonDerivedType(typeof(ItemSetActionActiveCommand),      "item.setActionActive")]
    [JsonDerivedType(typeof(RoomSetActionActiveCommand),      "room.setActionActive")]
    [JsonDerivedType(typeof(PlayerSetActionActiveCommand),    "player.setActionActive")]
    [JsonDerivedType(typeof(SetTimerActiveCommand), "timer.setTimerActive")]
    [JsonDerivedType(typeof(ForEachLoopCommand), "variable.forEachLoop")]
    [JsonDerivedType(typeof(BreakLoopCommand), "variable.breakLoop")]
    [JsonDerivedType(typeof(SetArrayElementCommand), "variable.setArrayElement")]
    [JsonDerivedType(typeof(AddArrayRowCommand), "variable.addArrayRow")]
    [JsonDerivedType(typeof(RemoveArrayRowCommand), "variable.removeArrayRow")]
    [JsonDerivedType(typeof(AppendTextCommand), "variable.appendText")]
    [JsonDerivedType(typeof(AppendLineCommand), "variable.appendLine")]
    [JsonDerivedType(typeof(SwitchCommand), "general.switch")]
    [JsonDerivedType(typeof(WearItemCommand), "item.wear")]
    [JsonDerivedType(typeof(RemoveItemCommand), "item.remove")]
    [JsonDerivedType(typeof(PlayerMoveInventoryToCharacterCommand), "player.moveInventoryToChar")]
    [JsonDerivedType(typeof(PlayerMoveInventoryToRoomCommand), "player.moveInventoryToRoom")]
    [JsonDerivedType(typeof(PlayerMoveToCharacterCommand), "player.moveToChar")]
    [JsonDerivedType(typeof(PlayerMoveToObjectCommand), "player.moveToObject")]
    [JsonDerivedType(typeof(RoomMoveItemsToPlayerCommand), "room.moveItemsToPlayer")]
    [JsonDerivedType(typeof(CharacterMoveInventoryToPlayerCommand), "char.moveInventoryToPlayer")]
    [JsonDerivedType(typeof(CharacterMoveToObjectCommand), "char.moveToObject")]
    [JsonDerivedType(typeof(CharacterSetDescriptionCommand), "char.setDescription")]
    [JsonDerivedType(typeof(CharacterSetDisplayNameCommand), "char.setDisplayName")]
    [JsonDerivedType(typeof(RoomSetDescriptionCommand), "room.setDescription")]
    [JsonDerivedType(typeof(RoomSetPictureCommand), "room.setPicture")]
    [JsonDerivedType(typeof(SetStatusBarVisibleCommand), "ui.setStatusBarVisible")]
    [JsonDerivedType(typeof(SetHotspotActiveCommand), "ui.setHotspotActive")]
    [JsonDerivedType(typeof(SetBackgroundMusicCommand), "media.setBackgroundMusic")]
    [JsonDerivedType(typeof(StopBackgroundMusicCommand), "media.stopBackgroundMusic")]
    [JsonDerivedType(typeof(SwapPlayerCharacterCommand), "player.swapCharacter")]
    [JsonDerivedType(typeof(ShowSplashScreenCommand), "ui.showSplashScreen")]
    [JsonDerivedType(typeof(WaitForContinueCommand), "general.waitForContinue")]
    public abstract class ActionStep
    {
        public static string NormalizeLegacyDiscriminators(string json)
        {
            if (string.IsNullOrEmpty(json)) return json;

            // Repair System.Text.Json reference preservation bug on empty dictionary keys
            // where empty keys are serialized as duplicate "$id": { ... } or "$ref": { ... }
            json = System.Text.RegularExpressions.Regex.Replace(json, @"""\$id""\s*:\s*\{", @""""": {");
            json = System.Text.RegularExpressions.Regex.Replace(json, @"""\$ref""\s*:\s*\{", @""""": {");

            // Normalize legacy dictionary "Attributes" into array format so System.Text.Json with ReferenceHandler.Preserve can deserialize it
            try
            {
                var node = JsonNode.Parse(json);
                if (node != null)
                {
                    ConvertLegacyAttributes(node);
                    ConvertLegacyCompareVar(node);
                    json = node.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"Failed to parse or convert legacy attributes in JSON: {ex}");
            }

            var validDiscriminators = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                "var.equals", "player.inRoom", "room.hasObject", "player.sameRoom", "item.heldByPlayer",
                "var.compare", "char.gender", "char.inRoom", "item.inRoom", "player.gender",
                "item.heldByChar", "item.inObject", "item.notHeldByPlayer", "item.notInObject", "item.isWorn", "item.canWear",
                "room.isExitLocked", "char.attributeCheck", "item.attributeCheck",
                "player.attributeCheck", "room.attributeCheck", "timer.isActive", "date.partCompare",
                "date.isPast", "date.isFuture", "date.compareVars", "date.diffCompare", "date.compareConst",
                "date.isValid", "status.isVisible", "var.set", "player.moveTo", "room.addObject", "room.removeObject",
                "status.show", "status.hide", "status.setText", "status.setImage",
                "object.displayDescription", "player.displayDescription", "char.displayDescription",
                "room.displayDescription", "object.moveToCharacter", "object.moveToInventory",
                "object.moveInsideObject", "general.displayText", "general.addComment",
                "media.playSound", "media.playVideo", "media.stopSound", "player.setName",
                "player.setDescription", "player.setGender", "char.setGender", "var.setRandom",
                "char.moveToRoom", "char.moveToRandomAdjacent", "char.moveAlongPatrolPath", "media.displayMultimedia", "char.displayPortrait", "char.setPortraitMedia",
                "player.setPortraitMedia", "var.inc", "var.dec", "var.setToVar", "var.evaluate", "room.setExit",
                "room.disableExit", "room.lockExit", "room.unlockExit", "char.damage", "char.setState",
                "general.triggerTurnTick", "general.endGame", "general.promptInput", "general.openContainer",
                "general.closeContainer", "general.callFunction", "general.startDialogue",
                "general.addCustomChoice", "general.clearCustomChoice", "general.removeCustomChoice",
                "room.setAttribute", "char.setAttribute", "player.setAttribute", "timer.setAttribute",
                "item.setAttribute", "char.setActionActive", "item.setActionActive", "room.setActionActive",
                "player.setActionActive", "timer.setTimerActive", "variable.forEachLoop", "variable.breakLoop",
                "variable.setArrayElement", "variable.addArrayRow", "variable.removeArrayRow",
                "variable.appendText", "variable.appendLine", "general.switch", "item.wear", "item.remove",
                "player.moveInventoryToChar", "player.moveInventoryToRoom", "player.moveToChar", "player.moveToObject", "room.moveItemsToPlayer",
                "char.moveInventoryToPlayer", "char.moveToObject", "char.setDescription", "char.setDisplayName", "room.setDescription", "room.setPicture", "ui.setStatusBarVisible", "ui.setHotspotActive", "media.setBackgroundMusic", "media.stopBackgroundMusic", "player.screenShake", "player.swapCharacter", "ui.showSplashScreen", "item.showInteractiveScreen", "general.waitForContinue"
            };

            // Convert unrecognized/unknown $type values to general.addComment to prevent crashes
            json = System.Text.RegularExpressions.Regex.Replace(json, @"""\$type""\s*:\s*""([^""]+)""", m =>
            {
                var val = m.Groups[1].Value;
                if (!validDiscriminators.Contains(val))
                {
                    return @"""$type"": ""general.addComment"", ""CommentText"": ""Unrecognized command/condition: " + val + @"""";
                }
                return m.Value;
            });

            return json
                .Replace("\"text.output\"", "\"general.displayText\"")
                .Replace("\"char.customPropertyCheck\"", "\"char.attributeCheck\"")
                .Replace("\"item.customPropertyCheck\"", "\"item.attributeCheck\"")
                .Replace("\"player.customPropertyCheck\"", "\"player.attributeCheck\"")
                .Replace("\"room.customPropertyCheck\"", "\"room.attributeCheck\"");
        }

        private static void ConvertLegacyAttributes(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                var properties = obj.ToList();
                foreach (var prop in properties)
                {
                    if (prop.Key == "Attributes" && prop.Value is JsonObject attrObj)
                    {
                        if (!attrObj.ContainsKey("$values") && !attrObj.ContainsKey("$ref"))
                        {
                            var array = new JsonArray();
                            foreach (var attrProp in attrObj.ToList())
                            {
                                var attrVal = attrProp.Value?.ToString();
                                var item = new JsonObject
                                {
                                    ["Name"] = attrProp.Key,
                                    ["Value"] = attrVal
                                };
                                array.Add(item);
                            }
                            obj["Attributes"] = array;
                        }
                    }
                    else if (prop.Value != null)
                    {
                        ConvertLegacyAttributes(prop.Value);
                    }
                }
            }
            else if (node is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item != null)
                    {
                        ConvertLegacyAttributes(item);
                    }
                }
            }
        }

        private static void ConvertLegacyCompareVar(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                if (obj.TryGetPropertyValue("$type", out var typeVal) && typeVal != null)
                {
                    var valStr = typeVal.ToString();
                    if (string.Equals(valStr, "var.compareVar", StringComparison.OrdinalIgnoreCase))
                    {
                        obj["$type"] = "var.compare";
                        
                        if (obj.TryGetPropertyValue("NameA", out var nameA))
                        {
                            obj["Name"] = nameA?.ToString();
                            obj.Remove("NameA");
                        }
                        
                        if (obj.TryGetPropertyValue("NameB", out var nameB))
                        {
                            var nameBStr = nameB?.ToString();
                            if (!string.IsNullOrEmpty(nameBStr))
                            {
                                obj["Value"] = $"{{variables.{nameBStr}}}";
                            }
                            obj.Remove("NameB");
                        }
                    }
                }

                var keys = new List<string>(obj.Select(kvp => kvp.Key));
                foreach (var key in keys)
                {
                    if (obj[key] is JsonNode child)
                    {
                        ConvertLegacyCompareVar(child);
                    }
                }
            }
            else if (node is JsonArray arr)
            {
                foreach (var child in arr)
                {
                    if (child != null)
                    {
                        ConvertLegacyCompareVar(child);
                    }
                }
            }
        }

        public abstract ActionStepKind Kind { get; }
        // Optional user label common to both
        public string? Label { get; set; }
        public virtual string TypeName => GetType().Name;
        public double X { get; set; }
        public double Y { get; set; }
        public double? Width { get; set; }
        public double? Height { get; set; }
    }

    public abstract class Condition : ActionStep
    {
        public override ActionStepKind Kind => ActionStepKind.Condition;
        
        // These fields allow Conditions to hold nested Commands or other Conditions
        public ObservableCollection<ActionStep> TrueBranch { get; set; } = new();
        public ObservableCollection<ActionStep> FalseBranch { get; set; } = new();

        public abstract bool Evaluate(ActionContext ctx);
    }

    public abstract class GameCommand : ActionStep
    {
        public override ActionStepKind Kind => ActionStepKind.Command;
        public abstract void Execute(ActionContext ctx);
    }

    public sealed class VariableEqualsCondition : Condition
    {
        public string Name { get; set; } = string.Empty;
        public string? Value { get; set; }
        public bool CaseInsensitive { get; set; }
        public override string TypeName => "Variable: Equals String";
        public override bool Evaluate(ActionContext ctx)
        {
            var v = (Name.StartsWith("{") && Name.EndsWith("}")) 
                ? RagsCore.Services.TemplateResolver.Resolve(Name, ctx) 
                : ctx.GetVariable(Name)?.Value;
            var isBool = string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || 
                         string.Equals(v, "false", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(Value, "true", StringComparison.OrdinalIgnoreCase) || 
                         string.Equals(Value, "false", StringComparison.OrdinalIgnoreCase);

            return (CaseInsensitive || isBool)
                ? string.Equals(v, Value, StringComparison.OrdinalIgnoreCase)
                : string.Equals(v, Value, StringComparison.Ordinal);
        }
    }

    public sealed class PlayerInRoomCondition : Condition
    {
        public string RoomId { get; set; } = string.Empty;
        public override string TypeName => "Player in room";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            return Guid.TryParse(resolved, out var g) && ctx.CurrentRoom?.Id == g;
        }
    }

    public sealed class CharacterAttributeCheckCondition : Condition
    {
        public string CharacterId { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string ExpectedValue { get; set; } = string.Empty;
        public override string TypeName => "Character: Attribute Check";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(ExpectedValue, ctx);
            var resolvedAttr = RagsCore.Services.TemplateResolver.Resolve(AttributeName, ctx);
            
            var character = ctx.Game.Characters.FirstOrDefault(c => 
                string.Equals(c.Id.ToString(), resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
            if (character == null)
            {
                var obj = ctx.Game.Objects.FirstOrDefault(o => 
                    string.Equals(o.Id.ToString(), resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(o.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(o.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (obj != null)
                {
                    return string.Equals(CustomAttribute.GetAttribute(resolvedAttr, obj.Attributes), resolvedVal, StringComparison.OrdinalIgnoreCase);
                }
                return false;
            }
            return string.Equals(CustomAttribute.GetAttribute(resolvedAttr, character.Attributes), resolvedVal, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ItemAttributeCheckCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string ExpectedValue { get; set; } = string.Empty;
        public override string TypeName => "Item: Attribute Check";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedItem = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(ExpectedValue, ctx);
            var resolvedAttr = RagsCore.Services.TemplateResolver.Resolve(AttributeName, ctx);

            var obj = ctx.Game.Objects.FirstOrDefault(o => string.Equals(o.Id.ToString(), resolvedItem, StringComparison.OrdinalIgnoreCase));
            return obj != null && string.Equals(CustomAttribute.GetAttribute(resolvedAttr, obj.Attributes), resolvedVal, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class PlayerAttributeCheckCondition : Condition
    {
        public string AttributeName { get; set; } = string.Empty;
        public string ExpectedValue { get; set; } = string.Empty;
        public override string TypeName => "Player: Attribute Check";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(ExpectedValue, ctx);
            var resolvedAttr = RagsCore.Services.TemplateResolver.Resolve(AttributeName, ctx);
            return string.Equals(CustomAttribute.GetAttribute(resolvedAttr, ctx.Player.Attributes), resolvedVal, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class RoomAttributeCheckCondition : Condition
    {
        public string RoomId { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string ExpectedValue { get; set; } = string.Empty;
        public override string TypeName => "Room: Attribute Check";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(ExpectedValue, ctx);
            var resolvedAttr = RagsCore.Services.TemplateResolver.Resolve(AttributeName, ctx);

            var room = ctx.Game.Rooms.FirstOrDefault(r => string.Equals(r.Id.ToString(), resolvedRoom, StringComparison.OrdinalIgnoreCase));
            return room != null && string.Equals(CustomAttribute.GetAttribute(resolvedAttr, room.Attributes), resolvedVal, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class TimerActiveCondition : Condition
    {
        public string TimerId { get; set; } = string.Empty;
        public override string TypeName => "Timer: Is Active";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedTimer = RagsCore.Services.TemplateResolver.Resolve(TimerId, ctx);
            var timer = ctx.Game.Timers.FirstOrDefault(t => string.Equals(t.Name, resolvedTimer, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Id.ToString(), resolvedTimer, StringComparison.OrdinalIgnoreCase));
            return timer != null && timer.IsActive;
        }
    }

    public sealed class RoomHasObjectCondition : Condition
    {
        public string RoomId { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Room has object";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (!Guid.TryParse(resolvedRoom, out var rId) || !Guid.TryParse(resolvedObj, out var oId)) return false;
            
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId);
            return room is not null && room.ObjectIds.Contains(oId);
        }
    }

    public sealed class SetVariableCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public string? Value { get; set; }
        public override string TypeName => "Variable: Set";
        public override void Execute(ActionContext ctx) => ctx.SetVariable(Name, Value);
    }

    public sealed class MovePlayerToRoomCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public string TransitionStyle { get; set; } = "None";
        public float TransitionDuration { get; set; } = 0f;
        public override string TypeName => "Move player to room";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            if (Guid.TryParse(resolved, out var g))
                ctx.SetVariable("player.currentRoomId", g.ToString());
            else if (!string.IsNullOrEmpty(resolved))
                ctx.SetVariable("player.currentRoomId", resolved);
        }
    }

    public sealed class ShowInteractiveScreenCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Item: Show Interactive Screen";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (!string.IsNullOrEmpty(resolved))
                ctx.SetVariable("player.activeInteractiveScreenObjectId", resolved);
        }
    }

    public sealed class SwapPlayerCharacterCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public override string TypeName => "Player: Swap Character";
        public override void Execute(ActionContext ctx)
        {
            if (ctx.Game == null) return;
            var resolvedId = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (!Guid.TryParse(resolvedId, out var targetCharId)) return;

            // 1. Find or create the character representing the current player
            var currentPlayerChar = System.Linq.Enumerable.FirstOrDefault(ctx.Game.Characters, c => c.Id == ctx.Game.ActivePlayerCharacterId);
            if (currentPlayerChar == null)
            {
                currentPlayerChar = System.Linq.Enumerable.FirstOrDefault(ctx.Game.Characters, c => c.Id == ctx.Game.Player.Id);
            }
            if (currentPlayerChar == null)
            {
                currentPlayerChar = new Character
                {
                    Id = ctx.Game.Player.Id,
                    Name = ctx.Game.Player.Name,
                    Description = ctx.Game.Player.Description,
                    Gender = ctx.Game.Player.Gender,
                    PortraitImagePath = ctx.Game.Player.PortraitImagePath
                };
                ctx.Game.Characters.Add(currentPlayerChar);
                ctx.Game.ActivePlayerCharacterId = currentPlayerChar.Id;
            }

            // 2. Save current player data into currentPlayerChar
            currentPlayerChar.Attributes.Clear();
            foreach (var attr in ctx.Game.Player.Attributes) currentPlayerChar.Attributes.Add(attr);

            currentPlayerChar.Actions.Clear();
            foreach (var act in ctx.Game.Player.Actions) currentPlayerChar.Actions.Add(act);

            currentPlayerChar.Inventory.Clear();
            foreach (var item in ctx.Game.Player.Inventory) currentPlayerChar.Inventory.Add(item);

            currentPlayerChar.Name = ctx.Game.Player.Name;
            currentPlayerChar.Description = ctx.Game.Player.Description;
            currentPlayerChar.Gender = ctx.Game.Player.Gender;
            currentPlayerChar.PortraitImagePath = ctx.Game.Player.PortraitImagePath;

            string? currentRoomId = ctx.GetVariable("player.currentRoomId")?.Value;
            if (currentRoomId != null && Guid.TryParse(currentRoomId, out var crId))
            {
                currentPlayerChar.StartingRoom = System.Linq.Enumerable.FirstOrDefault(ctx.Game.Rooms, r => r.Id == crId);
            }

            // 3. Find the target character to swap in
            var targetChar = System.Linq.Enumerable.FirstOrDefault(ctx.Game.Characters, c => c.Id == targetCharId);
            if (targetChar != null)
            {
                // 4. Load targetChar data into Player
                ctx.Game.Player.Id = targetChar.Id;
                ctx.Game.Player.Name = targetChar.Name;
                ctx.Game.Player.Description = targetChar.Description;
                ctx.Game.Player.Gender = targetChar.Gender;
                ctx.Game.Player.PortraitImagePath = targetChar.PortraitImagePath;

                ctx.Game.Player.Attributes.Clear();
                foreach (var attr in targetChar.Attributes) ctx.Game.Player.Attributes.Add(attr);

                ctx.Game.Player.Actions.Clear();
                foreach (var act in targetChar.Actions) ctx.Game.Player.Actions.Add(act);

                ctx.Game.Player.Inventory.Clear();
                foreach (var item in targetChar.Inventory) ctx.Game.Player.Inventory.Add(item);

                var targetRoom = targetChar.StartingRoom;
                if (targetRoom != null)
                {
                    ctx.SetVariable("player.currentRoomId", targetRoom.Id.ToString());
                }

                ctx.Game.ActivePlayerCharacterId = targetChar.Id;
            }
        }
    }

    public sealed class ShowSplashScreenCommand : GameCommand
    {
        public string SplashScreenName { get; set; } = "Default";
        public override string TypeName => "UI: Show Splash Screen";
        public override void Execute(ActionContext ctx)
        {
            // Handled client side
        }
    }

    public sealed class WaitForContinueCommand : GameCommand
    {
        public string ButtonText { get; set; } = "Continue";
        public override string TypeName => "General: Wait for Continue";
        public override void Execute(ActionContext ctx)
        {
            // Handled client side
        }
    }

    public sealed class ScreenShakeCommand : GameCommand
    {
        public float Intensity { get; set; } = 0.3f;
        public float Duration { get; set; } = 1.0f;
        public override string TypeName => "General: Screen Shake";
        public override void Execute(ActionContext ctx)
        {
            // State command handled on client side in CommandEffectRouter
        }
    }

    public sealed class AddObjectToRoomCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Object: Move to Room";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (!Guid.TryParse(resolvedRoom, out var rId) || !Guid.TryParse(resolvedObj, out var oId)) return;

            RemoveObjectFromEverywhere(ctx, oId);

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId);
            if (room is null) return;
            if (!room.ObjectIds.Contains(oId))
                room.ObjectIds.Add(oId);
        }

        public static void RemoveObjectFromEverywhere(ActionContext ctx, Guid oId)
        {
            foreach (var r in ctx.Game.Rooms)
            {
                r.ObjectIds.Remove(oId);
            }
            foreach (var c in ctx.Game.Characters)
            {
                var item = c.Inventory.FirstOrDefault(i => i.Id == oId);
                if (item != null) c.Inventory.Remove(item);
            }
            var pItem = ctx.Player.Inventory.FirstOrDefault(i => i.Id == oId);
            if (pItem != null) ctx.Player.Inventory.Remove(pItem);

            foreach (var o in ctx.Game.Objects)
            {
                if (o.ContainedObjectIds != null)
                {
                    o.ContainedObjectIds.Remove(oId);
                }
            }

            var targetObj = ctx.Game.Objects.FirstOrDefault(o => o.Id == oId);
            if (targetObj != null)
            {
                targetObj.Properties.Remove("ParentContainerId");
            }
        }
    }

    public sealed class RemoveObjectFromRoomCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Remove object from room";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (!Guid.TryParse(resolvedRoom, out var rId) || !Guid.TryParse(resolvedObj, out var oId)) return;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId);
            room?.ObjectIds.Remove(oId);
        }
    }

    public sealed class DisplayTextCommand : GameCommand
    {
        public string Text { get; set; } = string.Empty;
        public override string TypeName => "Display Text";
        public override void Execute(ActionContext ctx)
        {
            ctx.SetVariable("system.lastDisplayedText", RagsCore.Services.TemplateResolver.Resolve(Text, ctx));
        }
    }

    public sealed class AddCommentCommand : GameCommand
    {
        public string CommentText { get; set; } = string.Empty;
        public override string TypeName => "Add A Comment";
        public override void Execute(ActionContext ctx)
        {
            // Comments are for design-time documentation. No runtime action needed.
        }
    }


    /// <summary>Sets a specific character's action active or inactive (Bug #5 enhanced).</summary>
    public sealed class CharacterSetActionActiveCommand : GameCommand
    {
        public string ActionName { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        /// <summary>GUID of the character who owns the action. Empty = legacy global name-match.</summary>
        public string CharacterId { get; set; } = string.Empty;
        public override string TypeName => "Character: Set Action To Active/Inactive";
        public override void Execute(ActionContext ctx)
        {
            // Scoped resolution: when CharacterId is set, only toggle on that character.
            if (!string.IsNullOrEmpty(CharacterId))
            {
                var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
                Guid? charGuid = null;
                if (Guid.TryParse(resolvedChar, out var parsedCharId))
                {
                    charGuid = parsedCharId;
                }
                else
                {
                    var match = ctx.Game.Characters.FirstOrDefault(c => 
                        string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                    if (match != null) charGuid = match.Id;
                }

                if (charGuid != null)
                {
                    var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == charGuid.Value);
                    if (character != null)
                    {
                        foreach (var action in character.Actions)
                        {
                            if (string.Equals(action.Name, ActionName, StringComparison.OrdinalIgnoreCase))
                                action.InitallyActive = Active;
                        }
                    }
                    return;
                }
            }

            // Legacy global name-match (backward compat when CharacterId is empty).
            foreach (var action in ctx.Game.Player.Actions)
            {
                if (string.Equals(action.Name, ActionName, StringComparison.OrdinalIgnoreCase))
                    action.InitallyActive = Active;
            }
            foreach (var room in ctx.Game.Rooms)
            {
                foreach (var action in room.Actions)
                {
                    if (string.Equals(action.Name, ActionName, StringComparison.OrdinalIgnoreCase))
                        action.InitallyActive = Active;
                }
            }
            foreach (var obj in ctx.Game.Objects)
            {
                foreach (var action in obj.Actions)
                {
                    if (string.Equals(action.Name, ActionName, StringComparison.OrdinalIgnoreCase))
                        action.InitallyActive = Active;
                }
            }
            foreach (var character in ctx.Game.Characters)
            {
                foreach (var action in character.Actions)
                {
                    if (string.Equals(action.Name, ActionName, StringComparison.OrdinalIgnoreCase))
                        action.InitallyActive = Active;
                }
            }
        }
    }

    /// <summary>Sets a specific item/object's action active or inactive.</summary>
    public sealed class ItemSetActionActiveCommand : GameCommand
    {
        public string ActionName { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        /// <summary>GUID of the GameObject/item who owns the action.</summary>
        public string ItemId { get; set; } = string.Empty;
        public override string TypeName => "Item: Set Action To Active/Inactive";
        public override void Execute(ActionContext ctx)
        {
            if (string.IsNullOrEmpty(ItemId) || !Guid.TryParse(ItemId, out var itemGuid)) return;
            var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == itemGuid);
            if (obj == null) return;
            foreach (var action in obj.Actions)
            {
                if (string.Equals(action.Name, ActionName, StringComparison.OrdinalIgnoreCase))
                    action.InitallyActive = Active;
            }
        }
    }

    /// <summary>Sets a specific room's action active or inactive.</summary>
    public sealed class RoomSetActionActiveCommand : GameCommand
    {
        public string ActionName { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        /// <summary>GUID of the Room who owns the action.</summary>
        public string RoomId { get; set; } = string.Empty;
        public override string TypeName => "Room: Set Action To Active/Inactive";
        public override void Execute(ActionContext ctx)
        {
            if (string.IsNullOrEmpty(RoomId) || !Guid.TryParse(RoomId, out var roomGuid)) return;
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == roomGuid);
            if (room == null) return;
            foreach (var action in room.Actions)
            {
                if (string.Equals(action.Name, ActionName, StringComparison.OrdinalIgnoreCase))
                    action.InitallyActive = Active;
            }
        }
    }

    /// <summary>Sets a player action active or inactive.</summary>
    public sealed class PlayerSetActionActiveCommand : GameCommand
    {
        public string ActionName { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public override string TypeName => "Player: Set Action To Active/Inactive";
        public override void Execute(ActionContext ctx)
        {
            foreach (var action in ctx.Game.Player.Actions)
            {
                if (string.Equals(action.Name, ActionName, StringComparison.OrdinalIgnoreCase))
                    action.InitallyActive = Active;
            }
        }
    }

    public sealed class PlaySoundEffectCommand : GameCommand
    {
        public string SoundId { get; set; } = string.Empty;
        public double Volume { get; set; } = 100.0;
        public bool Loop { get; set; } = false;
        public double StartTime { get; set; } = 0.0;
        public double EndTime { get; set; } = 0.0;
        public override string TypeName => "Media: Play Sound Effect";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(SoundId, ctx);
            ctx.SetVariable("media.lastSoundId", resolved);
            ctx.SetVariable("media.lastSoundVolume", Volume.ToString());
            ctx.SetVariable("media.lastSoundLoop", Loop.ToString().ToLower());
            ctx.SetVariable("media.lastSoundStartTime", StartTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
            ctx.SetVariable("media.lastSoundEndTime", EndTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public sealed class PlayVideoCommand : GameCommand
    {
        public string VideoId { get; set; } = string.Empty;
        public double Volume { get; set; } = 100.0;
        public bool Loop { get; set; } = false;
        public double StartTime { get; set; } = 0.0;
        public double EndTime { get; set; } = 0.0;
        public override string TypeName => "Media: Play Video";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(VideoId, ctx);
            ctx.SetVariable("media.lastVideoId", resolved);
            ctx.SetVariable("media.lastVideoVolume", Volume.ToString());
            ctx.SetVariable("media.lastVideoLoop", Loop.ToString().ToLower());
            ctx.SetVariable("media.lastVideoStartTime", StartTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
            ctx.SetVariable("media.lastVideoEndTime", EndTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public sealed class StopSoundEffectCommand : GameCommand
    {
        public string SoundId { get; set; } = string.Empty;
        public bool StopAllLooping { get; set; } = false;
        public override string TypeName => "Media: Stop Sound Effect";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(SoundId, ctx);
            ctx.SetVariable("media.stopSoundId", resolved);
            ctx.SetVariable("media.stopAllLooping", StopAllLooping.ToString().ToLower());
        }
    }

    public sealed class PlayerSetNameCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public override string TypeName => "Player: Set Name";
        public override void Execute(ActionContext ctx)
        {
            ctx.Player.Name = Name;
        }
    }

    public sealed class PlayerSetDescriptionCommand : GameCommand
    {
        public string Description { get; set; } = string.Empty;
        public override string TypeName => "Player: Set Description";
        public override void Execute(ActionContext ctx)
        {
            ctx.Player.Description = Description;
        }
    }

    public sealed class PlayerSetGenderCommand : GameCommand
    {
        public string Gender { get; set; } = "Male";
        public override string TypeName => "Player: Set Gender";
        public override void Execute(ActionContext ctx)
        {
            ctx.Player.Gender = Gender;
        }
    }

    public sealed class CharacterSetGenderCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string Gender { get; set; } = "Male";
        public override string TypeName => "Character: Set Gender";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            Guid? cId = null;
            if (Guid.TryParse(resolvedChar, out var parsedCharId))
            {
                cId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolvedChar))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (match != null) cId = match.Id;
            }

            if (cId != null)
            {
                var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId.Value);
                if (character != null)
                {
                    character.Properties["Gender"] = Gender;
                }
            }
        }
    }

    public sealed class SetNumericRandomlyCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public double Minimum { get; set; }
        public double Maximum { get; set; }
        public override string TypeName => "Variable: Set Numeric Randomly";
        private static readonly Random _rnd = new();
        public override void Execute(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(Name)) return;
            var val = _rnd.Next((int)Minimum, (int)Maximum + 1);
            ctx.SetVariable(Name, val.ToString());
        }
    }

    internal static class DateTimeHelper
    {
        public static DateTime? AddToDateTime(DateTime dt, string value, bool isAddition)
        {
            var cleanValue = value.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(cleanValue)) return null;

            double amount = 0;
            string unit = "minutes"; // default

            var match = System.Text.RegularExpressions.Regex.Match(cleanValue, @"^(-?\d+(?:\.\d+)?)\s*([a-zA-Z]+)?$");
            if (match.Success)
            {
                if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedAmount))
                {
                    amount = parsedAmount;
                }
                if (match.Groups[2].Success)
                {
                    var unitStr = match.Groups[2].Value;
                    if (unitStr.StartsWith("s")) unit = "seconds";
                    else if (unitStr.StartsWith("h")) unit = "hours";
                    else if (unitStr.StartsWith("d")) unit = "days";
                    else if (unitStr.StartsWith("mo") || unitStr == "mth") unit = "months";
                    else if (unitStr.StartsWith("y")) unit = "years";
                    else if (unitStr.StartsWith("m")) unit = "minutes";
                }
            }
            else
            {
                return null;
            }

            if (!isAddition) amount = -amount;

            try
            {
                return unit switch
                {
                    "seconds" => dt.AddSeconds(amount),
                    "hours" => dt.AddHours(amount),
                    "days" => dt.AddDays(amount),
                    "months" => dt.AddMonths((int)amount),
                    "years" => dt.AddYears((int)amount),
                    _ => dt.AddMinutes(amount)
                };
            }
            catch
            {
                return null;
            }
        }

        public static TimeSpan? ParseDuration(string value)
        {
            var cleanValue = value.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(cleanValue)) return null;

            double amount = 0;
            string unit = "minutes"; // default

            var match = System.Text.RegularExpressions.Regex.Match(cleanValue, @"^(-?\d+(?:\.\d+)?)\s*([a-zA-Z]+)?$");
            if (match.Success)
            {
                if (double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var parsedAmount))
                {
                    amount = parsedAmount;
                }
                if (match.Groups[2].Success)
                {
                    var unitStr = match.Groups[2].Value;
                    if (unitStr.StartsWith("s")) unit = "seconds";
                    else if (unitStr.StartsWith("h")) unit = "hours";
                    else if (unitStr.StartsWith("d")) unit = "days";
                    else if (unitStr.StartsWith("mo") || unitStr == "mth") unit = "months";
                    else if (unitStr.StartsWith("y")) unit = "years";
                    else if (unitStr.StartsWith("m")) unit = "minutes";
                }

                return unit switch
                {
                    "seconds" => TimeSpan.FromSeconds(amount),
                    "hours" => TimeSpan.FromHours(amount),
                    "days" => TimeSpan.FromDays(amount),
                    "months" => TimeSpan.FromDays(amount * 30),
                    "years" => TimeSpan.FromDays(amount * 365),
                    _ => TimeSpan.FromMinutes(amount)
                };
            }
            return null;
        }
    }

    public sealed class VariableIncrementCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Variable: Increment";
        public override void Execute(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(Name)) return;
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            var existing = ctx.GetVariable(Name)?.Value;

            if (existing != null && DateTime.TryParse(existing, out var existingDt))
            {
                var newDt = DateTimeHelper.AddToDateTime(existingDt, resolvedVal, true);
                if (newDt.HasValue)
                {
                    ctx.SetVariable(Name, newDt.Value.ToString("yyyy-MM-ddTHH:mm:ss"));
                    return;
                }
            }
            
            if (double.TryParse(existing, out var existingNum) && double.TryParse(resolvedVal, out var addNum))
            {
                ctx.SetVariable(Name, (existingNum + addNum).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            else
            {
                ctx.SetVariable(Name, (existing ?? string.Empty) + resolvedVal);
            }
        }
    }

    public sealed class VariableDecrementCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Variable: Decrement";
        public override void Execute(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(Name)) return;
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            var existing = ctx.GetVariable(Name)?.Value;

            if (existing != null && DateTime.TryParse(existing, out var existingDt))
            {
                var newDt = DateTimeHelper.AddToDateTime(existingDt, resolvedVal, false);
                if (newDt.HasValue)
                {
                    ctx.SetVariable(Name, newDt.Value.ToString("yyyy-MM-ddTHH:mm:ss"));
                    return;
                }
            }
            
            if (double.TryParse(existing, out var existingNum) && double.TryParse(resolvedVal, out var subNum))
            {
                ctx.SetVariable(Name, (existingNum - subNum).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }
    }

    public sealed class VariableSetToVariableCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public override string TypeName => "Variable: Set to Variable";
        public override void Execute(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(SourceName)) return;
            var sourceVal = ctx.GetVariable(SourceName)?.Value;
            ctx.SetVariable(Name, sourceVal);
        }
    }

    public sealed class EvaluateFormulaCommand : GameCommand
    {
        public string Name { get; set; } = string.Empty;
        public string Formula { get; set; } = string.Empty;
        public override string TypeName => "Variable: Evaluate Formula";
        public override void Execute(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(Name)) return;
            var resolved = RagsCore.Services.TemplateResolver.Resolve(Formula, ctx);
            try
            {
                var val = RagsCore.Services.MathEvaluator.Evaluate(resolved);
                ctx.SetVariable(Name, val.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                ctx.SetVariable("system.error", $"Formula error: {ex.Message}");
            }
        }
    }

    public sealed class CharacterMoveToRoomCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public override string TypeName => "Character: Move To Room";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);

            Guid? cId = null;
            if (Guid.TryParse(resolvedChar, out var parsedCharId))
            {
                cId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolvedChar))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (match != null) cId = match.Id;
            }

            Guid? rId = null;
            if (Guid.TryParse(resolvedRoom, out var parsedRoomId))
            {
                rId = parsedRoomId;
            }
            else if (!string.IsNullOrEmpty(resolvedRoom))
            {
                var match = ctx.Game.Rooms.FirstOrDefault(r => 
                    string.Equals(r.Name, resolvedRoom, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.Name.Replace(" ", ""), resolvedRoom, StringComparison.OrdinalIgnoreCase));
                if (match != null) rId = match.Id;
            }

            if (cId == null || rId == null) return;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId.Value);
            if (character is not null)
            {
                ctx.SetVariable($"char.{cId.Value}.currentRoomId", rId.Value.ToString());
            }
        }
    }

    public sealed class CharacterMoveToRandomAdjacentCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public override string TypeName => "Character: Move To Random Adjacent Room";

        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            Guid? cId = null;
            if (Guid.TryParse(resolvedChar, out var parsedCharId))
            {
                cId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolvedChar))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (match != null) cId = match.Id;
            }

            if (cId == null) return;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId.Value);
            if (character == null) return;

            // Find current room of character
            var currentRoomIdStr = ctx.GetVariable($"char.{cId.Value}.currentRoomId")?.Value;
            Guid? currentRoomId = null;
            if (Guid.TryParse(currentRoomIdStr, out var parsedRoomId))
            {
                currentRoomId = parsedRoomId;
            }
            else
            {
                currentRoomId = character.StartingRoom?.Id;
            }

            if (currentRoomId == null) return;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == currentRoomId.Value);
            if (room == null) return;

            // Find all unlocked exits
            var validExits = new List<Guid>();
            foreach (var exit in room.Exits)
            {
                bool isLocked = room.LockedExits.TryGetValue(exit.Key, out var locked) && locked;
                if (!isLocked)
                {
                    validExits.Add(exit.Value);
                }
            }

            if (validExits.Count == 0) return;

            var rnd = new Random();
            var targetRoomId = validExits[rnd.Next(validExits.Count)];

            ctx.SetVariable($"char.{cId.Value}.currentRoomId", targetRoomId.ToString());
        }
    }

    public sealed class CharacterMoveAlongPatrolPathCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string PatrolPath { get; set; } = string.Empty;
        public string IndexVariable { get; set; } = string.Empty;
        public bool PingPong { get; set; }
        public override string TypeName => "Character: Move Along Patrol Path";

        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            Guid? cId = null;
            if (Guid.TryParse(resolvedChar, out var parsedCharId))
            {
                cId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolvedChar))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (match != null) cId = match.Id;
            }

            if (cId == null) return;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId.Value);
            if (character == null) return;

            var resolvedPath = RagsCore.Services.TemplateResolver.Resolve(PatrolPath, ctx);
            if (string.IsNullOrEmpty(resolvedPath)) return;

            var rooms = resolvedPath.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(r => r.Trim())
                                    .ToList();
            if (rooms.Count == 0) return;

            if (string.IsNullOrEmpty(IndexVariable)) return;

            var idxStr = ctx.GetVariable(IndexVariable)?.Value ?? "0";
            if (!int.TryParse(idxStr, out var currentIndex))
            {
                currentIndex = 0;
            }

            var dirVarName = $"{IndexVariable}_dir";
            var dirStr = ctx.GetVariable(dirVarName)?.Value ?? "1";
            if (!int.TryParse(dirStr, out var direction))
            {
                direction = 1;
            }

            int nextIndex = currentIndex + direction;

            if (PingPong)
            {
                if (nextIndex >= rooms.Count)
                {
                    direction = -1;
                    nextIndex = Math.Max(0, rooms.Count - 2);
                }
                else if (nextIndex < 0)
                {
                    direction = 1;
                    nextIndex = Math.Min(rooms.Count - 1, 1);
                }
                ctx.SetVariable(dirVarName, direction.ToString());
            }
            else
            {
                if (nextIndex >= rooms.Count)
                {
                    nextIndex = 0;
                }
                else if (nextIndex < 0)
                {
                    nextIndex = rooms.Count - 1;
                }
            }

            if (nextIndex >= 0 && nextIndex < rooms.Count)
            {
                var targetRoomRef = rooms[nextIndex];
                Guid? rId = null;
                if (Guid.TryParse(targetRoomRef, out var parsedRoomId))
                {
                    rId = parsedRoomId;
                }
                else
                {
                    var match = ctx.Game.Rooms.FirstOrDefault(r => 
                        string.Equals(r.Name, targetRoomRef, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(r.Name.Replace(" ", ""), targetRoomRef, StringComparison.OrdinalIgnoreCase));
                    if (match != null) rId = match.Id;
                }

                if (rId != null)
                {
                    ctx.SetVariable(IndexVariable, nextIndex.ToString());
                    ctx.SetVariable($"char.{cId.Value}.currentRoomId", rId.Value.ToString());
                }
            }
        }
    }

    public sealed class PlayerInSameRoomAsCondition : Condition
    {
        public string CharacterId { get; set; } = string.Empty;
        public override string TypeName => "Player: In Same Room As";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            Guid? charId = null;
            if (Guid.TryParse(resolvedChar, out var parsedCharId))
            {
                charId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolvedChar))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (match != null) charId = match.Id;
            }

            var finalCharKey = charId?.ToString() ?? resolvedChar;
            var charRoomVar = ctx.GetVariable($"char.{finalCharKey}.currentRoomId")?.Value;
            var playerRoomId = ctx.CurrentRoom?.Id.ToString() ?? ctx.GetVariable("player.currentRoomId")?.Value;
            return charRoomVar != null && playerRoomId != null && string.Equals(charRoomVar, playerRoomId, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ItemHeldByPlayerCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public override string TypeName => "Item: Held By Player";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            if (!Guid.TryParse(resolved, out var itemId)) return false;
            return ctx.Player.Inventory.Any(item => item.Id == itemId);
        }
    }

    public sealed class VariableComparisonCondition : Condition
    {
        public string Name { get; set; } = string.Empty;
        public string Comparison { get; set; } = "=";
        public string? Value { get; set; }
        public override string TypeName => "Variable: Comparison";
        public override bool Evaluate(ActionContext ctx)
        {
            var varVal = (Name.StartsWith("{") && Name.EndsWith("}"))
                ? RagsCore.Services.TemplateResolver.Resolve(Name, ctx) ?? string.Empty
                : ctx.GetVariable(Name)?.Value ?? string.Empty;
            var compVal = Value ?? string.Empty;

            if (double.TryParse(varVal, out double varNum) && double.TryParse(compVal, out double compNum))
            {
                return Comparison switch
                {
                    "=" => varNum == compNum,
                    "!=" => varNum != compNum,
                    ">" => varNum > compNum,
                    ">=" => varNum >= compNum,
                    "<" => varNum < compNum,
                    "<=" => varNum <= compNum,
                    _ => false
                };
            }

            return Comparison switch
            {
                "=" => string.Equals(varVal, compVal, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(varVal, compVal, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
    }

    public sealed class DateTimePartComparisonCondition : Condition
    {
        public string VariableName { get; set; } = string.Empty;
        public string DateTimeComponent { get; set; } = "minute";
        public string Comparison { get; set; } = "=";
        public double ExpectedValue { get; set; }
        public override string TypeName => "Variable: DateTime Part Comparison";
        public override bool Evaluate(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(VariableName)) return false;
            var rawVal = ctx.GetVariable(VariableName)?.Value;
            if (string.IsNullOrWhiteSpace(rawVal) || !DateTime.TryParse(rawVal, out var dt)) return false;

            double actualVal = (DateTimeComponent ?? "").ToLowerInvariant() switch
            {
                "second" => dt.Second,
                "hour" => dt.Hour,
                "day" => dt.Day,
                "month" => dt.Month,
                "year" => dt.Year,
                _ => dt.Minute
            };

            return Comparison switch
            {
                "=" => actualVal == ExpectedValue,
                "!=" => actualVal != ExpectedValue,
                ">" => actualVal > ExpectedValue,
                ">=" => actualVal >= ExpectedValue,
                "<" => actualVal < ExpectedValue,
                "<=" => actualVal <= ExpectedValue,
                _ => false
            };
        }
    }

    public sealed class DateTimeIsPastCondition : Condition
    {
        public string VariableName { get; set; } = string.Empty;
        public override string TypeName => "DateTime: Is Past";
        public override bool Evaluate(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(VariableName)) return false;
            var rawVal = ctx.GetVariable(VariableName)?.Value;
            if (string.IsNullOrWhiteSpace(rawVal) || !DateTime.TryParse(rawVal, out var dt)) return false;
            return dt < DateTime.Now;
        }
    }

    public sealed class DateTimeIsFutureCondition : Condition
    {
        public string VariableName { get; set; } = string.Empty;
        public override string TypeName => "DateTime: Is Future";
        public override bool Evaluate(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(VariableName)) return false;
            var rawVal = ctx.GetVariable(VariableName)?.Value;
            if (string.IsNullOrWhiteSpace(rawVal) || !DateTime.TryParse(rawVal, out var dt)) return false;
            return dt > DateTime.Now;
        }
    }

    public sealed class DateTimeCompareVariablesCondition : Condition
    {
        public string VariableNameA { get; set; } = string.Empty;
        public string Comparison { get; set; } = "=";
        public string VariableNameB { get; set; } = string.Empty;
        public override string TypeName => "DateTime: Compare Two Variables";
        public override bool Evaluate(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(VariableNameA) || string.IsNullOrWhiteSpace(VariableNameB)) return false;
            var valA = ctx.GetVariable(VariableNameA)?.Value;
            var valB = ctx.GetVariable(VariableNameB)?.Value;

            if (string.IsNullOrWhiteSpace(valA) || !DateTime.TryParse(valA, out var dtA)) return false;
            if (string.IsNullOrWhiteSpace(valB) || !DateTime.TryParse(valB, out var dtB)) return false;

            return Comparison switch
            {
                "=" => dtA == dtB,
                "!=" => dtA != dtB,
                ">" => dtA > dtB,
                ">=" => dtA >= dtB,
                "<" => dtA < dtB,
                "<=" => dtA <= dtB,
                _ => false
            };
        }
    }

    public sealed class DateTimeCompareDifferenceCondition : Condition
    {
        public string VariableNameA { get; set; } = string.Empty;
        public string VariableNameB { get; set; } = string.Empty;
        public string Comparison { get; set; } = "=";
        public string Duration { get; set; } = string.Empty;
        public override string TypeName => "DateTime: Compare Difference";

        public override bool Evaluate(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(VariableNameA) || string.IsNullOrWhiteSpace(VariableNameB)) return false;
            var valA = ctx.GetVariable(VariableNameA)?.Value;
            var valB = ctx.GetVariable(VariableNameB)?.Value;

            if (string.IsNullOrWhiteSpace(valA) || !DateTime.TryParse(valA, out var dtA)) return false;
            if (string.IsNullOrWhiteSpace(valB) || !DateTime.TryParse(valB, out var dtB)) return false;

            var resolvedDuration = RagsCore.Services.TemplateResolver.Resolve(Duration ?? "", ctx);
            var tsOpt = DateTimeHelper.ParseDuration(resolvedDuration);
            if (!tsOpt.HasValue) return false;
            var targetSpan = tsOpt.Value;

            var actualSpan = dtA - dtB;

            return Comparison switch
            {
                "=" => actualSpan == targetSpan,
                "!=" => actualSpan != targetSpan,
                ">" => actualSpan > targetSpan,
                ">=" => actualSpan >= targetSpan,
                "<" => actualSpan < targetSpan,
                "<=" => actualSpan <= targetSpan,
                _ => false
            };
        }
    }

    public sealed class DateTimeCompareConstantCondition : Condition
    {
        public string VariableName { get; set; } = string.Empty;
        public string Comparison { get; set; } = "=";
        public string ConstantValue { get; set; } = string.Empty;
        public override string TypeName => "DateTime: Compare Constant";

        public override bool Evaluate(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(VariableName)) return false;
            var rawVal = ctx.GetVariable(VariableName)?.Value;
            if (string.IsNullOrWhiteSpace(rawVal) || !DateTime.TryParse(rawVal, out var dt)) return false;

            var resolvedConst = RagsCore.Services.TemplateResolver.Resolve(ConstantValue ?? "", ctx);
            if (!DateTime.TryParse(resolvedConst, out var dtConst)) return false;

            return Comparison switch
            {
                "=" => dt == dtConst,
                "!=" => dt != dtConst,
                ">" => dt > dtConst,
                ">=" => dt >= dtConst,
                "<" => dt < dtConst,
                "<=" => dt <= dtConst,
                _ => false
            };
        }
    }

    public sealed class DateTimeIsValidCondition : Condition
    {
        public string VariableName { get; set; } = string.Empty;
        public override string TypeName => "DateTime: Is Valid";

        public override bool Evaluate(ActionContext ctx)
        {
            if (string.IsNullOrWhiteSpace(VariableName)) return false;
            var rawVal = ctx.GetVariable(VariableName)?.Value;
            if (string.IsNullOrWhiteSpace(rawVal)) return false;
            return DateTime.TryParse(rawVal, out _);
        }
    }

    public sealed class CharacterGenderCondition : Condition
    {
        public string CharacterId { get; set; } = string.Empty;
        public string Gender { get; set; } = "Male";
        public override string TypeName => "Character: Gender";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            Guid? cId = null;
            if (Guid.TryParse(resolved, out var parsedCharId))
            {
                cId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolved))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolved, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolved, StringComparison.OrdinalIgnoreCase));
                if (match != null) cId = match.Id;
            }

            if (cId == null) return false;
            
            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId.Value);
            if (character is null) return false;
            var charGender = character.Properties.TryGetValue("Gender", out var g) ? g : "Male";
            return string.Equals(charGender, Gender, StringComparison.OrdinalIgnoreCase);
        }
    }

    // NEW COMMANDS IMPLEMENTATION

    public sealed class DisplayMultimediaCommand : GameCommand
    {
        public string MediaId { get; set; } = string.Empty;
        public override string TypeName => "Media: Display Multimedia";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(MediaId, ctx);
            ctx.SetVariable("media.lastDisplayedMediaId", resolved);
        }
    }

    public sealed class CharacterDisplayPortraitCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string PortraitId { get; set; } = string.Empty;
        public override string TypeName => "Character: Display Portrait";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var resolvedPort = RagsCore.Services.TemplateResolver.Resolve(PortraitId, ctx);
            Guid? charId = null;
            if (Guid.TryParse(resolvedChar, out var parsedCharId))
            {
                charId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolvedChar))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (match != null) charId = match.Id;
            }

            var finalCharKey = charId?.ToString() ?? resolvedChar;
            ctx.SetVariable($"char.{finalCharKey}.displayedPortraitId", resolvedPort);
        }
    }

    public sealed class CharacterSetPortraitMediaCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string MediaId { get; set; } = string.Empty;
        public override string TypeName => "Character: Set Portrait Media";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var resolvedMedia = RagsCore.Services.TemplateResolver.Resolve(MediaId, ctx);
            
            Guid? charId = null;
            if (Guid.TryParse(resolvedChar, out var parsedCharId))
            {
                charId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolvedChar))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (match != null) charId = match.Id;
            }

            if (charId != null)
            {
                var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == charId.Value);
                if (character is not null)
                {
                    if (Guid.TryParse(resolvedMedia, out var mediaGuid))
                    {
                        var mediaAsset = ctx.Game.MediaAssets.FirstOrDefault(m => m.Id == mediaGuid);
                        if (mediaAsset is not null)
                        {
                            character.PortraitImagePath = mediaAsset.RelativePath;
                        }
                    }
                    else
                    {
                        character.PortraitImagePath = resolvedMedia;
                    }
                }
            }
        }
    }

    public sealed class PlayerSetPortraitMediaCommand : GameCommand
    {
        public string MediaId { get; set; } = string.Empty;
        public override string TypeName => "Player: Set Portrait Media";
        public override void Execute(ActionContext ctx)
        {
            var resolvedMedia = RagsCore.Services.TemplateResolver.Resolve(MediaId, ctx);
            if (Guid.TryParse(resolvedMedia, out var mediaGuid))
            {
                var mediaAsset = ctx.Game.MediaAssets.FirstOrDefault(m => m.Id == mediaGuid);
                if (mediaAsset is not null)
                {
                    ctx.Player.PortraitImagePath = mediaAsset.RelativePath;
                }
            }
            else
            {
                ctx.Player.PortraitImagePath = resolvedMedia;
            }
        }
    }

    // NEW CONDITIONS IMPLEMENTATION

    public sealed class CharacterInRoomCondition : Condition
    {
        public string CharacterId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public override string TypeName => "Character: In Room";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);

            Guid? cId = null;
            if (Guid.TryParse(resolvedChar, out var parsedCharId))
            {
                cId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolvedChar))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (match != null) cId = match.Id;
            }

            Guid? rId = null;
            if (Guid.TryParse(resolvedRoom, out var parsedRoomId))
            {
                rId = parsedRoomId;
            }
            else if (!string.IsNullOrEmpty(resolvedRoom))
            {
                var match = ctx.Game.Rooms.FirstOrDefault(r => 
                    string.Equals(r.Name, resolvedRoom, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.Name.Replace(" ", ""), resolvedRoom, StringComparison.OrdinalIgnoreCase));
                if (match != null) rId = match.Id;
            }

            if (cId == null || rId == null) return false;

            var charRoomVar = ctx.GetVariable($"char.{cId.Value}.currentRoomId")?.Value;
            return charRoomVar != null && string.Equals(charRoomVar, rId.Value.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ItemInRoomCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public override string TypeName => "Item: In Room";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedItem = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            if (!Guid.TryParse(resolvedItem, out var itemId) || !Guid.TryParse(resolvedRoom, out var roomId)) return false;
            
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == roomId);
            return room is not null && room.ObjectIds.Contains(itemId);
        }
    }

    public sealed class PlayerGenderCondition : Condition
    {
        public string Gender { get; set; } = "Male";
        public override string TypeName => "Player: Gender";
        public override bool Evaluate(ActionContext ctx)
        {
            return string.Equals(ctx.Player.Gender, Gender, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ItemHeldByCharacterCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public string CharacterId { get; set; } = string.Empty;
        public override string TypeName => "Item: Held By Character";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedItem = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (!Guid.TryParse(resolvedItem, out var itemId)) return false;

            Guid? charId = null;
            if (Guid.TryParse(resolvedChar, out var parsedCharId))
            {
                charId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolvedChar))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (match != null) charId = match.Id;
            }

            if (charId == null) return false;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == charId.Value);
            return character is not null && character.Inventory.Any(i => i.Id == itemId);
        }
    }

    public sealed class ItemInObjectCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public string ContainerObjectId { get; set; } = string.Empty;
        public override string TypeName => "Item: In Object";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedItem = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var resolvedContainer = RagsCore.Services.TemplateResolver.Resolve(ContainerObjectId, ctx);
            if (!Guid.TryParse(resolvedItem, out var itemId)) return false;

            var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == itemId);
            if (obj == null) return false;
            obj.Properties.TryGetValue("ParentContainerId", out var pId);
            return string.Equals(pId, resolvedContainer, StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ItemNotHeldByPlayerCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public override string TypeName => "Item: Not Held By Player";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            if (!Guid.TryParse(resolved, out var itemId)) return true;
            return !ctx.Player.Inventory.Any(item => item.Id == itemId);
        }
    }

    public sealed class ItemNotInObjectCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Item: Not In Object";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedItem = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (!Guid.TryParse(resolvedItem, out var itemId)) return true;

            var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == itemId);
            if (obj == null) return true;
            obj.Properties.TryGetValue("ParentContainerId", out var parentId);
            return !string.Equals(parentId, resolvedObj, StringComparison.OrdinalIgnoreCase);
        }
    }


    /// <summary>
    /// Sets (or clears) a specific directional exit on a room at runtime.
    /// Setting DestinationRoomId to empty/blank removes the exit.
    /// </summary>
    public sealed class SetRoomExitCommand : GameCommand
    {
        /// <summary>The room whose exit to modify. Leave blank to use the current room.</summary>
        public string RoomId { get; set; } = string.Empty;
        /// <summary>The direction key (e.g. "North", "South", "East", "West", "Up", "Down", "In", "Out").</summary>
        public string Direction { get; set; } = string.Empty;
        /// <summary>The destination room. Leave blank to remove the exit in this direction.</summary>
        public string DestinationRoomId { get; set; } = string.Empty;
        public override string TypeName => "Room: Set Exit";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedDest = RagsCore.Services.TemplateResolver.Resolve(DestinationRoomId, ctx);
            var resolvedDir = RagsCore.Services.TemplateResolver.Resolve(Direction, ctx);

            var rId = Guid.TryParse(resolvedRoom, out var g1) ? g1 : ctx.CurrentRoom?.Id;
            if (rId == null || rId == Guid.Empty || string.IsNullOrWhiteSpace(resolvedDir)) return;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId.Value);
            if (room is null) return;

            if (string.IsNullOrWhiteSpace(resolvedDest))
            {
                room.Exits.Remove(resolvedDir);
            }
            else if (Guid.TryParse(resolvedDest, out var destId) && destId != Guid.Empty)
            {
                room.Exits[resolvedDir] = destId;
            }
        }
    }

    /// <summary>
    /// Removes (disables) a specific directional exit from a room at runtime.
    /// </summary>
    public sealed class DisableRoomExitCommand : GameCommand
    {
        /// <summary>The room whose exit to disable. Leave blank to use the current room.</summary>
        public string RoomId { get; set; } = string.Empty;
        /// <summary>The direction key to remove (e.g. "North").</summary>
        public string Direction { get; set; } = string.Empty;
        public override string TypeName => "Room: Disable Exit";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedDir = RagsCore.Services.TemplateResolver.Resolve(Direction, ctx);

            var rId = Guid.TryParse(resolvedRoom, out var g) ? g : ctx.CurrentRoom?.Id;
            if (rId == null || rId == Guid.Empty || string.IsNullOrWhiteSpace(resolvedDir)) return;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId.Value);
            room?.Exits.Remove(resolvedDir);
        }
    }

    public class PlayerInputTypeConverter : JsonConverter<PlayerInputType>
    {
        public override PlayerInputType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var val = reader.GetString();
                if (string.IsNullOrWhiteSpace(val))
                {
                    return PlayerInputType.Text;
                }
                if (Enum.TryParse<PlayerInputType>(val, true, out var result))
                {
                    return result;
                }
            }
            else if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt32(out var intVal))
                {
                    return (PlayerInputType)intVal;
                }
            }
            return PlayerInputType.Text;
        }

        public override void Write(Utf8JsonWriter writer, PlayerInputType value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }

    [JsonConverter(typeof(PlayerInputTypeConverter))]
    public enum PlayerInputType { Text, Objects, Characters, Custom }

    public sealed class EndGameCommand : GameCommand
    {
        public string FinalMessage { get; set; } = string.Empty;
        public override string TypeName => "General: End Game";
        public override void Execute(ActionContext ctx)
        {
            ctx.SetVariable("system.isGameOver", "true");
            ctx.SetVariable("system.endGameMessage", RagsCore.Services.TemplateResolver.Resolve(FinalMessage, ctx));
        }
    }

    public sealed class PromptPlayerInputCommand : GameCommand
    {
        public string PromptName { get; set; } = string.Empty;
        public string PromptText { get; set; } = string.Empty;
        public PlayerInputType InputType { get; set; } = PlayerInputType.Text;
        public string CustomOptions { get; set; } = string.Empty;
        public string StoreVariableName { get; set; } = string.Empty;
        public override string TypeName => "General: Prompt Player Input";
        public override void Execute(ActionContext ctx)
        {
            ctx.SetVariable("system.prompt.name", RagsCore.Services.TemplateResolver.Resolve(PromptName, ctx));
            ctx.SetVariable("system.prompt.text", RagsCore.Services.TemplateResolver.Resolve(PromptText, ctx));
            ctx.SetVariable("system.prompt.type", InputType.ToString());
            ctx.SetVariable("system.prompt.options", CustomOptions);
            ctx.SetVariable("system.prompt.targetVar", StoreVariableName);
            ctx.SetVariable("system.prompt.active", "true");
        }
    }

    public sealed class OpenContainerCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "General: Open Container";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            ctx.SetVariable($"obj.{resolved}.containerOpen", "true");
        }
    }

    public sealed class CloseContainerCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "General: Close Container";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            ctx.SetVariable($"obj.{resolved}.containerOpen", "false");
        }
    }

    public sealed class CallFunctionCommand : GameCommand
    {
        public string FunctionId { get; set; } = string.Empty;
        public override string TypeName => "General: Call Function";
        public override void Execute(ActionContext ctx)
        {
            var target = ctx.Game?.Functions.FirstOrDefault(f => f.Id.ToString() == FunctionId || string.Equals(f.Name, FunctionId, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                ExecuteSteps(target.Nodes, ctx);
            }
        }

        private void ExecuteSteps(System.Collections.Generic.IEnumerable<ActionStep> steps, ActionContext ctx)
        {
            foreach (var step in steps)
            {
                if (step is GameCommand cmd)
                {
                    cmd.Execute(ctx);
                }
                else if (step is Condition cond)
                {
                    bool result = cond.Evaluate(ctx);
                    ExecuteSteps(result ? cond.TrueBranch : cond.FalseBranch, ctx);
                }
            }
        }
    }

    public sealed class IsRoomExitLockedCondition : Condition
    {
        public string RoomId { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public override string TypeName => "Room: Exit Is Locked";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedDir = RagsCore.Services.TemplateResolver.Resolve(Direction, ctx);

            var rId = Guid.TryParse(resolvedRoom, out var g) ? g : ctx.CurrentRoom?.Id;
            if (rId == null || rId == Guid.Empty || string.IsNullOrWhiteSpace(resolvedDir)) return false;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId.Value);
            return room != null && room.LockedExits.TryGetValue(resolvedDir, out var isLocked) && isLocked;
        }
    }

    public sealed class LockRoomExitCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public override string TypeName => "Room: Lock Exit";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedDir = RagsCore.Services.TemplateResolver.Resolve(Direction, ctx);

            Guid? rId = null;
            if (Guid.TryParse(resolvedRoom, out var parsedId))
            {
                rId = parsedId;
            }
            else if (!string.IsNullOrEmpty(resolvedRoom))
            {
                var match = ctx.Game.Rooms.FirstOrDefault(r => 
                    string.Equals(r.Name, resolvedRoom, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.Name.Replace(" ", ""), resolvedRoom, StringComparison.OrdinalIgnoreCase));
                if (match != null) rId = match.Id;
            }
            if (rId == null) rId = ctx.CurrentRoom?.Id;

            if (rId == null || rId == Guid.Empty || string.IsNullOrWhiteSpace(resolvedDir)) return;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId.Value);
            if (room is not null)
            {
                room.LockedExits[resolvedDir] = true;
            }
        }
    }

    public sealed class UnlockRoomExitCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty;
        public override string TypeName => "Room: Unlock Exit";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedDir = RagsCore.Services.TemplateResolver.Resolve(Direction, ctx);

            Guid? rId = null;
            if (Guid.TryParse(resolvedRoom, out var parsedId))
            {
                rId = parsedId;
            }
            else if (!string.IsNullOrEmpty(resolvedRoom))
            {
                var match = ctx.Game.Rooms.FirstOrDefault(r => 
                    string.Equals(r.Name, resolvedRoom, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(r.Name.Replace(" ", ""), resolvedRoom, StringComparison.OrdinalIgnoreCase));
                if (match != null) rId = match.Id;
            }
            if (rId == null) rId = ctx.CurrentRoom?.Id;

            if (rId == null || rId == Guid.Empty || string.IsNullOrWhiteSpace(resolvedDir)) return;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId.Value);
            if (room is not null)
            {
                room.LockedExits[resolvedDir] = false;
            }
        }
    }

    public sealed class DamageCharacterCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public int Amount { get; set; } = 0;
        public override string TypeName => "Character: Damage / Heal";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            Guid? charId = null;
            if (Guid.TryParse(resolvedChar, out var parsedCharId))
            {
                charId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolvedChar))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (match != null) charId = match.Id;
            }

            if (charId == null) return;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == charId.Value);
            if (character is not null)
            {
                character.Properties.TryGetValue("Health", out var hpStr);
                int hp = int.TryParse(hpStr, out var val) ? val : 100;
                hp += Amount;
                character.Properties["Health"] = hp.ToString();

                if (hp <= 0)
                {
                    character.Properties["State"] = "Dead";
                }
            }
        }
    }

    public sealed class SetCharacterStateCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string State { get; set; } = "Alive";
        public override string TypeName => "Character: Set State";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            Guid? charId = null;
            if (Guid.TryParse(resolvedChar, out var parsedCharId))
            {
                charId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolvedChar))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (match != null) charId = match.Id;
            }

            if (charId == null) return;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == charId.Value);
            if (character is not null)
            {
                character.Properties["State"] = State;
            }
        }
    }

    public sealed class SetCharacterAttributeCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Character: Set Attribute";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            Guid? charId = null;
            if (Guid.TryParse(resolvedChar, out var parsedCharId))
            {
                charId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolvedChar))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (match != null) charId = match.Id;
            }

            if (charId == null) return;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == charId.Value);
            if (character is not null)
            {
                CustomAttribute.SetAttribute(AttributeName, resolvedVal, character.Attributes);
            }
        }
    }

    public sealed class SetPlayerAttributeCommand : GameCommand
    {
        public string AttributeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Player: Set Attribute";
        public override void Execute(ActionContext ctx)
        {
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            CustomAttribute.SetAttribute(AttributeName, resolvedVal, ctx.Player.Attributes);
        }
    }

    public sealed class SetTimerAttributeCommand : GameCommand
    {
        public string TimerId { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Timer: Set Attribute";
        public override void Execute(ActionContext ctx)
        {
            var resolvedTimer = RagsCore.Services.TemplateResolver.Resolve(TimerId, ctx);
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            var timer = ctx.Game.Timers.FirstOrDefault(t => string.Equals(t.Name, resolvedTimer, StringComparison.OrdinalIgnoreCase));
            if (timer is not null)
            {
                CustomAttribute.SetAttribute(AttributeName, resolvedVal, timer.Attributes);
            }
        }
    }

    public sealed class SetTimerActiveCommand : GameCommand
    {
        public string TimerId { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public override string TypeName => "Timer: Set Timer To Active/Inactive";
        public override void Execute(ActionContext ctx)
        {
            var resolvedTimer = RagsCore.Services.TemplateResolver.Resolve(TimerId, ctx);
            var timer = ctx.Game.Timers.FirstOrDefault(t => string.Equals(t.Name, resolvedTimer, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Id.ToString(), resolvedTimer, StringComparison.OrdinalIgnoreCase));
            if (timer != null)
            {
                timer.IsActive = Active;
            }
        }
     }

    public sealed class SetItemAttributeCommand : GameCommand
    {
        public string ItemId { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Item: Set Attribute";
        public override void Execute(ActionContext ctx)
        {
            var resolvedItem = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            if (!Guid.TryParse(resolvedItem, out var itemId)) return;

            var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == itemId);
            if (obj is not null)
            {
                CustomAttribute.SetAttribute(AttributeName, resolvedVal, obj.Attributes);
            }
        }
    }

    public sealed class SetRoomAttributeCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public string AttributeName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Room: Set Attribute";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            
            var room = ctx.Game.Rooms.FirstOrDefault(r => string.Equals(r.Id.ToString(), resolvedRoom, StringComparison.OrdinalIgnoreCase) || string.Equals(r.Name, resolvedRoom, StringComparison.OrdinalIgnoreCase));
            if (room is not null)
            {
                CustomAttribute.SetAttribute(AttributeName, resolvedVal, room.Attributes);
            }
        }
    }

    public sealed class TriggerTurnTickCommand : GameCommand
    {
        public override string TypeName => "Game: Trigger Turn Tick";
        public override void Execute(ActionContext ctx)
        {
            // Handled inside target environment runtime context updates.
        }
    }

    public sealed class DialogueChoice
    {
        public string Text { get; set; } = string.Empty;
        public string DestinationNodeId { get; set; } = string.Empty;
        public ObservableCollection<ActionStep> Commands { get; set; } = new();
    }

    public sealed class StartDialogueCommand : GameCommand
    {
        public string DialogueId { get; set; } = string.Empty;
        public string CharacterLines { get; set; } = string.Empty;
        public ObservableCollection<DialogueChoice> Choices { get; set; } = new();
        public override string TypeName => "Start Dialogue";
        public override void Execute(ActionContext ctx)
        {
            // Handled by custom visual game dialogues system
        }
    }

    public sealed class AddCustomChoiceCommand : GameCommand
    {
        public string PromptName { get; set; } = string.Empty;
        public string ChoiceText { get; set; } = string.Empty;
        public override string TypeName => "Action: Add Custom Choice";
        public override void Execute(ActionContext ctx)
        {
            // Handled inside target environment runtime
        }
    }

    public sealed class ClearCustomChoiceCommand : GameCommand
    {
        public string PromptName { get; set; } = string.Empty;
        public override string TypeName => "Action: Clear Custom Choice";
        public override void Execute(ActionContext ctx)
        {
            // Handled inside target environment runtime
        }
    }

    public sealed class RemoveCustomChoiceCommand : GameCommand
    {
        public string PromptName { get; set; } = string.Empty;
        public string ChoiceText { get; set; } = string.Empty;
        public override string TypeName => "Action: Remove Custom Choice";
        public override void Execute(ActionContext ctx)
        {
            // Handled inside target environment runtime
        }
    }

    public sealed class ObjectDisplayDescriptionCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Object: Display Description";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (Guid.TryParse(resolved, out var oId))
            {
                var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == oId);
                if (obj != null)
                {
                    ctx.SetVariable("system.lastDisplayedText", RagsCore.Services.TemplateResolver.Resolve(obj.Description, ctx));
                }
            }
        }
    }

    public sealed class ObjectMoveToCharacterCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public string CharacterId { get; set; } = string.Empty;
        public override string TypeName => "Object: Move to Character";
        public override void Execute(ActionContext ctx)
        {
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);

            Guid? cId = null;
            if (Guid.TryParse(resolvedChar, out var parsedCharId))
            {
                cId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolvedChar))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolvedChar, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolvedChar, StringComparison.OrdinalIgnoreCase));
                if (match != null) cId = match.Id;
            }

            if (Guid.TryParse(resolvedObj, out var oId) && cId != null)
            {
                AddObjectToRoomCommand.RemoveObjectFromEverywhere(ctx, oId);
                var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId.Value);
                if (character != null && !character.Inventory.Any(i => i.Id == oId))
                {
                    var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == oId);
                    if (obj != null)
                        character.Inventory.Add(obj);
                }
            }
        }
    }

    public sealed class ObjectMoveToInventoryCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Object: Move to Inventory";
        public override void Execute(ActionContext ctx)
        {
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (Guid.TryParse(resolvedObj, out var oId))
            {
                AddObjectToRoomCommand.RemoveObjectFromEverywhere(ctx, oId);
                if (!ctx.Player.Inventory.Any(i => i.Id == oId))
                {
                    var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == oId);
                    if (obj != null)
                        ctx.Player.Inventory.Add(obj);
                }
            }
        }
    }

    public sealed class ObjectMoveInsideObjectCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public string ContainerObjectId { get; set; } = string.Empty;
        public override string TypeName => "Object: Move Inside Object";
        public override void Execute(ActionContext ctx)
        {
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            var resolvedContainer = RagsCore.Services.TemplateResolver.Resolve(ContainerObjectId, ctx);
            if (Guid.TryParse(resolvedObj, out var oId) && Guid.TryParse(resolvedContainer, out var containerId))
            {
                AddObjectToRoomCommand.RemoveObjectFromEverywhere(ctx, oId);
                var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == oId);
                if (obj != null)
                {
                    obj.Properties["ParentContainerId"] = containerId.ToString();
                }
            }
        }
    }

    public sealed class PlayerDisplayDescriptionCommand : GameCommand
    {
        public override string TypeName => "Player: Display Description";
        public override void Execute(ActionContext ctx)
        {
            ctx.SetVariable("system.lastDisplayedText", RagsCore.Services.TemplateResolver.Resolve(ctx.Player.Description, ctx));
        }
    }

    public sealed class CharacterDisplayDescriptionCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public override string TypeName => "Character: Display Description";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            Guid? cId = null;
            if (Guid.TryParse(resolved, out var parsedCharId))
            {
                cId = parsedCharId;
            }
            else if (!string.IsNullOrEmpty(resolved))
            {
                var match = ctx.Game.Characters.FirstOrDefault(c => 
                    string.Equals(c.Name, resolved, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Name.Replace(" ", ""), resolved, StringComparison.OrdinalIgnoreCase));
                if (match != null) cId = match.Id;
            }

            if (cId != null)
            {
                var chr = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId.Value);
                if (chr != null)
                {
                    ctx.SetVariable("system.lastDisplayedText", RagsCore.Services.TemplateResolver.Resolve(chr.Description, ctx));
                }
            }
        }
    }

    public sealed class RoomDisplayDescriptionCommand : GameCommand
    {
        public override string TypeName => "Room: Display Description";
        public override void Execute(ActionContext ctx)
        {
            ctx.SetVariable("system.lastDisplayedText", RagsCore.Services.TemplateResolver.Resolve(ctx.CurrentRoom?.Description ?? string.Empty, ctx));
        }
    }

    public sealed class ForEachLoopCommand : Condition
    {
        public string ArrayVariableName { get; set; } = string.Empty;
        public string LoopSource { get; set; } = "Variable";
        public string FilterType { get; set; } = "All";
        public override string TypeName => "For Each Loop";
        public override bool Evaluate(ActionContext ctx)
        {
            // For Each Loop inherits from Condition so it has TrueBranch (the loop body).
            // The visual graph / runner evaluates it. The C# design-side implementation can return false.
            return false;
        }
    }

    public sealed class BreakLoopCommand : GameCommand
    {
        public override string TypeName => "Variable: Break Loop";
        public override void Execute(ActionContext ctx) { }
    }

    public sealed class SetArrayElementCommand : GameCommand
    {
        public string ArrayVariableName { get; set; } = string.Empty;
        public string RowIndex { get; set; } = "0";
        public string ColumnName { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public override string TypeName => "Variable: Set Array Element";
        public override void Execute(ActionContext ctx)
        {
            var resolvedVal = RagsCore.Services.TemplateResolver.Resolve(Value, ctx);
            var resolvedRow = RagsCore.Services.TemplateResolver.Resolve(RowIndex, ctx);
            var resolvedCol = RagsCore.Services.TemplateResolver.Resolve(ColumnName, ctx);

            var v = ctx.GetVariable(ArrayVariableName);
            if (v != null && v.Type == "array" && int.TryParse(resolvedRow, out int rIdx) && rIdx >= 0)
            {
                int colIdx = v.Columns.IndexOf(resolvedCol);
                if (colIdx >= 0 && rIdx < v.Rows.Count)
                {
                    var row = v.Rows[rIdx];
                    while (row.Count <= colIdx) row.Add(string.Empty);
                    row[colIdx] = resolvedVal;
                }
            }
        }
    }

    public sealed class AddArrayRowCommand : GameCommand
    {
        public string ArrayVariableName { get; set; } = string.Empty;
        public string ValuesCommaSeparated { get; set; } = string.Empty;
        public override string TypeName => "Variable: Add Array Row";
        public override void Execute(ActionContext ctx)
        {
            var resolvedValues = RagsCore.Services.TemplateResolver.Resolve(ValuesCommaSeparated, ctx);
            var v = ctx.GetVariable(ArrayVariableName);
            if (v != null && v.Type == "array")
            {
                var row = new ObservableCollection<string>();
                var parts = resolvedValues.Split(',');
                for (int i = 0; i < v.Columns.Count; i++)
                {
                    row.Add(i < parts.Length ? parts[i].Trim() : string.Empty);
                }
                v.Rows.Add(row);
            }
        }
    }

    public sealed class RemoveArrayRowCommand : GameCommand
    {
        public string ArrayVariableName { get; set; } = string.Empty;
        public string RowIndex { get; set; } = "0";
        public override string TypeName => "Variable: Remove Array Row";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRow = RagsCore.Services.TemplateResolver.Resolve(RowIndex, ctx);
            var v = ctx.GetVariable(ArrayVariableName);
            if (v != null && v.Type == "array" && int.TryParse(resolvedRow, out int rIdx) && rIdx >= 0 && rIdx < v.Rows.Count)
            {
                v.Rows.RemoveAt(rIdx);
            }
        }
    }

    public sealed class AppendTextCommand : GameCommand
    {
        public string VariableName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public override string TypeName => "Variable: Append Text";
        public override void Execute(ActionContext ctx)
        {
            var resolvedText = RagsCore.Services.TemplateResolver.Resolve(Text, ctx);
            var v = ctx.GetVariable(VariableName);
            if (v != null)
            {
                v.Value = (v.Value ?? string.Empty) + resolvedText;
            }
            else
            {
                ctx.SetVariable(VariableName, resolvedText);
            }
        }
    }

    public sealed class AppendLineCommand : GameCommand
    {
        public string VariableName { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public override string TypeName => "Variable: Append Line";
        public override void Execute(ActionContext ctx)
        {
            var resolvedText = RagsCore.Services.TemplateResolver.Resolve(Text, ctx);
            var v = ctx.GetVariable(VariableName);
            if (v != null)
            {
                v.Value = (v.Value ?? string.Empty) + resolvedText + "\n";
            }
            else
            {
                ctx.SetVariable(VariableName, resolvedText + "\n");
            }
        }
    }

    public sealed class SwitchCommand : GameCommand
    {
        public string Expression { get; set; } = string.Empty;
        public Dictionary<string, ObservableCollection<ActionStep>> Cases { get; set; } = new();
        public ObservableCollection<ActionStep> DefaultBranch { get; set; } = new();

        public override string TypeName => "General: Switch";

        public override void Execute(ActionContext ctx)
        {
            // Executed contextually at runtime in Player interpreter.
        }
    }

    public sealed class WearItemCommand : GameCommand
    {
        public string ItemId { get; set; } = string.Empty;
        public override string TypeName => "Item: Wear Item";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var obj = ctx.Game.Objects.FirstOrDefault(o => string.Equals(o.Id.ToString(), resolved, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, resolved, StringComparison.OrdinalIgnoreCase));
            if (obj != null)
            {
                obj.IsWorn = true;
            }
        }
    }

    public sealed class RemoveItemCommand : GameCommand
    {
        public string ItemId { get; set; } = string.Empty;
        public override string TypeName => "Item: Remove Item";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var obj = ctx.Game.Objects.FirstOrDefault(o => string.Equals(o.Id.ToString(), resolved, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, resolved, StringComparison.OrdinalIgnoreCase));
            if (obj != null)
            {
                obj.IsWorn = false;
            }
        }
    }

    public sealed class ItemWornCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public override string TypeName => "Item: Is Item Worn";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var obj = ctx.Game.Objects.FirstOrDefault(o => string.Equals(o.Id.ToString(), resolved, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, resolved, StringComparison.OrdinalIgnoreCase));
            return obj != null && obj.IsWorn;
        }
    }

    public sealed class ItemCanWearCondition : Condition
    {
        public string ItemId { get; set; } = string.Empty;
        public override string TypeName => "Item: Can Item Be Worn";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ItemId, ctx);
            var obj = ctx.Game.Objects.FirstOrDefault(o => string.Equals(o.Id.ToString(), resolved, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, resolved, StringComparison.OrdinalIgnoreCase));
            if (obj == null) return false;
            if (!obj.IsWearable) return false;
            if (string.IsNullOrEmpty(obj.WearSlot)) return true;
            
            var conflict = ctx.Player.Inventory.FirstOrDefault(i => i.IsWorn && string.Equals(i.WearSlot, obj.WearSlot, StringComparison.OrdinalIgnoreCase));
            return conflict == null || string.Equals(conflict.Id.ToString(), obj.Id.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }

    public sealed class ShowStatusElementCommand : GameCommand
    {
        public string ElementId { get; set; } = string.Empty;
        public override string TypeName => "Status: Show Status Element";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ElementId, ctx);
            var element = ctx.Game.StatusBarElements.FirstOrDefault(e => e.Id.ToString() == resolved || string.Equals(e.Name, resolved, StringComparison.OrdinalIgnoreCase));
            if (element != null)
            {
                element.IsVisible = true;
            }
        }
    }

    public sealed class HideStatusElementCommand : GameCommand
    {
        public string ElementId { get; set; } = string.Empty;
        public override string TypeName => "Status: Hide Status Element";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ElementId, ctx);
            var element = ctx.Game.StatusBarElements.FirstOrDefault(e => e.Id.ToString() == resolved || string.Equals(e.Name, resolved, StringComparison.OrdinalIgnoreCase));
            if (element != null)
            {
                element.IsVisible = false;
            }
        }
    }

    public sealed class SetStatusElementTextCommand : GameCommand
    {
        public string ElementId { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public override string TypeName => "Status: Set Status Element Text";
        public override void Execute(ActionContext ctx)
        {
            var resolvedId = RagsCore.Services.TemplateResolver.Resolve(ElementId, ctx);
            var resolvedText = RagsCore.Services.TemplateResolver.Resolve(Text, ctx);
            var element = ctx.Game.StatusBarElements.FirstOrDefault(e => e.Id.ToString() == resolvedId || string.Equals(e.Name, resolvedId, StringComparison.OrdinalIgnoreCase));
            if (element != null)
            {
                element.Text = resolvedText;
            }
        }
    }

    public sealed class SetStatusElementImageCommand : GameCommand
    {
        public string ElementId { get; set; } = string.Empty;
        public string MediaId { get; set; } = string.Empty;
        public override string TypeName => "Status: Set Status Element Image";
        public override void Execute(ActionContext ctx)
        {
            var resolvedId = RagsCore.Services.TemplateResolver.Resolve(ElementId, ctx);
            var resolvedMedia = RagsCore.Services.TemplateResolver.Resolve(MediaId, ctx);
            var element = ctx.Game.StatusBarElements.FirstOrDefault(e => e.Id.ToString() == resolvedId || string.Equals(e.Name, resolvedId, StringComparison.OrdinalIgnoreCase));
            if (element != null && Guid.TryParse(resolvedMedia, out var mediaGuid))
            {
                element.MediaAssetId = mediaGuid;
            }
        }
    }

    public sealed class StatusElementVisibleCondition : Condition
    {
        public string ElementId { get; set; } = string.Empty;
        public override string TypeName => "Status: Is Status Element Visible";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(ElementId, ctx);
            var element = ctx.Game.StatusBarElements.FirstOrDefault(e => e.Id.ToString() == resolved || string.Equals(e.Name, resolved, StringComparison.OrdinalIgnoreCase));
            return element != null && element.IsVisible;
        }
    }

    public sealed class PlayerMoveInventoryToCharacterCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public override string TypeName => "Player: Move Inventory To Character";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (!Guid.TryParse(resolvedChar, out var cId)) return;
            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId);
            if (character is null) return;
            
            var items = ctx.Player.Inventory.ToList();
            foreach (var item in items)
            {
                ctx.Player.Inventory.Remove(item);
                character.Inventory.Add(item);
            }
        }
    }

    public sealed class PlayerMoveInventoryToRoomCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public override string TypeName => "Player: Move Inventory To Room";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            if (!Guid.TryParse(resolvedRoom, out var rId)) return;
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId);
            if (room is null) return;
            
            var items = ctx.Player.Inventory.ToList();
            foreach (var item in items)
            {
                ctx.Player.Inventory.Remove(item);
                if (!room.ObjectIds.Contains(item.Id))
                {
                    room.ObjectIds.Add(item.Id);
                }
            }
        }
    }

    public sealed class PlayerMoveToCharacterCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public override string TypeName => "Player: Move To Character";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (!Guid.TryParse(resolvedChar, out var cId)) return;
            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId);
            if (character is null) return;
            
            var charRoomVar = ctx.GetVariable($"char.{cId}.currentRoomId")?.Value;
            Guid? targetRoomId = null;
            if (Guid.TryParse(charRoomVar, out var parsedId))
            {
                targetRoomId = parsedId;
            }
            else
            {
                targetRoomId = character.StartingRoom?.Id;
            }
            
            if (targetRoomId != null)
            {
                ctx.SetVariable("player.currentRoomId", targetRoomId.Value.ToString());
            }
        }
    }

    public sealed class PlayerMoveToObjectCommand : GameCommand
    {
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Player: Move To Object";
        public override void Execute(ActionContext ctx)
        {
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (!Guid.TryParse(resolvedObj, out var oId)) return;
            
            Guid? currentLocId = oId;
            const int maxIterations = 20;
            int iterations = 0;
            
            while (currentLocId != null && iterations++ < maxIterations)
            {
                var room = ctx.Game.Rooms.FirstOrDefault(r => r.ObjectIds.Contains(currentLocId.Value));
                if (room != null)
                {
                    ctx.SetVariable("player.currentRoomId", room.Id.ToString());
                    return;
                }
                
                var container = ctx.Game.Objects.FirstOrDefault(o => o.ContainedObjectIds != null && o.ContainedObjectIds.Contains(currentLocId.Value));
                if (container != null)
                {
                    currentLocId = container.Id;
                    continue;
                }
                
                var character = ctx.Game.Characters.FirstOrDefault(c => c.Inventory.Any(i => i.Id == currentLocId.Value));
                if (character != null)
                {
                    var charRoomVar = ctx.GetVariable($"char.{character.Id}.currentRoomId")?.Value;
                    Guid? charRoomId = null;
                    if (Guid.TryParse(charRoomVar, out var parsedId)) charRoomId = parsedId;
                    else charRoomId = character.StartingRoom?.Id;
                    
                    if (charRoomId != null)
                    {
                         ctx.SetVariable("player.currentRoomId", charRoomId.Value.ToString());
                    }
                    return;
                }
                
                if (ctx.Player.Inventory.Any(i => i.Id == currentLocId.Value))
                {
                    return;
                }
                
                break;
            }
        }
    }

    public sealed class RoomMoveItemsToPlayerCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public override string TypeName => "Room: Move Items To Player";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            if (!Guid.TryParse(resolvedRoom, out var rId)) return;
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId);
            if (room is null) return;
            
            var objectGuids = room.ObjectIds.ToList();
            foreach (var oId in objectGuids)
            {
                var obj = ctx.Game.Objects.FirstOrDefault(o => o.Id == oId);
                if (obj != null && obj.IsCollectible)
                {
                    room.ObjectIds.Remove(oId);
                    if (!ctx.Player.Inventory.Contains(obj))
                    {
                        ctx.Player.Inventory.Add(obj);
                    }
                }
            }
        }
    }

    public sealed class CharacterMoveInventoryToPlayerCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public override string TypeName => "Character: Move Inventory To Player";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (!Guid.TryParse(resolvedChar, out var cId)) return;
            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId);
            if (character is null) return;
            
            var items = character.Inventory.ToList();
            foreach (var item in items)
            {
                if (item.IsCollectible)
                {
                    character.Inventory.Remove(item);
                    if (!ctx.Player.Inventory.Contains(item))
                    {
                        ctx.Player.Inventory.Add(item);
                    }
                }
            }
        }
    }

    public sealed class CharacterMoveToObjectCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Character: Move To Object";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (!Guid.TryParse(resolvedChar, out var cId)) return;
            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId);
            if (character is null) return;

            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (!Guid.TryParse(resolvedObj, out var oId)) return;
            
            Guid? currentLocId = oId;
            const int maxIterations = 20;
            int iterations = 0;
            
            while (currentLocId != null && iterations++ < maxIterations)
            {
                var room = ctx.Game.Rooms.FirstOrDefault(r => r.ObjectIds.Contains(currentLocId.Value));
                if (room != null)
                {
                    ctx.SetVariable($"char.{character.Id}.currentRoomId", room.Id.ToString());
                    return;
                }
                
                var container = ctx.Game.Objects.FirstOrDefault(o => o.ContainedObjectIds != null && o.ContainedObjectIds.Contains(currentLocId.Value));
                if (container != null)
                {
                    currentLocId = container.Id;
                    continue;
                }
                
                var otherCharacter = ctx.Game.Characters.FirstOrDefault(c => c.Inventory.Any(i => i.Id == currentLocId.Value));
                if (otherCharacter != null)
                {
                    var charRoomVar = ctx.GetVariable($"char.{otherCharacter.Id}.currentRoomId")?.Value;
                    Guid? charRoomId = null;
                    if (Guid.TryParse(charRoomVar, out var parsedId)) charRoomId = parsedId;
                    else charRoomId = otherCharacter.StartingRoom?.Id;
                    
                    if (charRoomId != null)
                    {
                         ctx.SetVariable($"char.{character.Id}.currentRoomId", charRoomId.Value.ToString());
                    }
                    return;
                }
                
                if (ctx.Player.Inventory.Any(i => i.Id == currentLocId.Value))
                {
                    var playerRoomVar = ctx.GetVariable("player.currentRoomId")?.Value;
                    if (playerRoomVar != null)
                    {
                        ctx.SetVariable($"char.{character.Id}.currentRoomId", playerRoomVar);
                    }
                    return;
                }
                
                break;
            }
        }
    }

    public sealed class CharacterSetDescriptionCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public override string TypeName => "Character: Set Description";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (!Guid.TryParse(resolvedChar, out var cId)) return;
            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId);
            if (character is null) return;
            
            character.Description = Description;
        }
    }

    public sealed class CharacterSetDisplayNameCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public override string TypeName => "Character: Set Display Name";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (!Guid.TryParse(resolvedChar, out var cId)) return;
            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId);
            if (character is null) return;
            
            character.Name = Name;
        }
    }


    public sealed class RoomSetDescriptionCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public override string TypeName => "Room: Set Description";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            if (!Guid.TryParse(resolvedRoom, out var rId)) return;
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId);
            if (room is null) return;
            
            room.Description = Description;
        }
    }

    public sealed class RoomSetPictureCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public string Picture { get; set; } = string.Empty;
        public override string TypeName => "Room: Set Picture";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            if (!Guid.TryParse(resolvedRoom, out var rId)) return;
            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId);
            if (room is null) return;
            
            var resolvedMedia = RagsCore.Services.TemplateResolver.Resolve(Picture, ctx);
            if (Guid.TryParse(resolvedMedia, out var mediaId))
            {
                var media = ctx.Game.MediaAssets.FirstOrDefault(m => m.Id == mediaId);
                if (media != null)
                {
                    room.PortraitImagePath = media.RelativePath;
                }
            }
            else
            {
                room.PortraitImagePath = resolvedMedia;
            }
        }
    }

    public sealed class SetStatusBarVisibleCommand : GameCommand
    {
        public bool Visible { get; set; }
        public override string TypeName => "StatusBar: Set Visible/Invisible";
        public override void Execute(ActionContext ctx)
        {
            ctx.SetVariable("ui.statusBarVisible", Visible.ToString().ToLower());
        }
    }

    public sealed class SetBackgroundMusicCommand : GameCommand
    {
        public string MusicFile { get; set; } = string.Empty;
        public int Volume { get; set; } = 100;
        public bool Loop { get; set; } = true;
        public double StartTime { get; set; } = 0;
        public double EndTime { get; set; } = 0;
        public override string TypeName => "Media: Set Background Music";
        public override void Execute(ActionContext ctx)
        {
        }
    }

    public sealed class StopBackgroundMusicCommand : GameCommand
    {
        public override string TypeName => "Media: Stop Background Music";
        public override void Execute(ActionContext ctx)
        {
        }
    }

    public sealed class SetHotspotActiveCommand : GameCommand
    {
        public string HotspotIdOrName { get; set; } = string.Empty;
        public bool Active { get; set; } = true;
        public override string TypeName => "UI: Set Hotspot Active State";
        public override void Execute(ActionContext ctx)
        {
        }
    }
}