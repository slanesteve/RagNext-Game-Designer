using System;
using System.Text.Json.Serialization;
using RagsCore.Models;
using System.Collections.ObjectModel;
using System.Linq;

namespace RagsCore.Actions
{
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
    [JsonDerivedType(typeof(VariableComparisonToVariableCondition), "var.compareVar")]
    // Commands
    [JsonDerivedType(typeof(SetVariableCommand), "var.set")]
    [JsonDerivedType(typeof(MovePlayerToRoomCommand), "player.moveTo")]
    [JsonDerivedType(typeof(AddObjectToRoomCommand), "room.addObject")]
    [JsonDerivedType(typeof(RemoveObjectFromRoomCommand), "room.removeObject")]
    [JsonDerivedType(typeof(DisplayTextCommand), "general.displayText")]
    [JsonDerivedType(typeof(AddCommentCommand), "general.addComment")]
    [JsonDerivedType(typeof(PlaySoundEffectCommand), "media.playSound")]
    [JsonDerivedType(typeof(PlayerSetNameCommand), "player.setName")]
    [JsonDerivedType(typeof(PlayerSetDescriptionCommand), "player.setDescription")]
    [JsonDerivedType(typeof(PlayerSetGenderCommand), "player.setGender")]
    [JsonDerivedType(typeof(SetNumericRandomlyCommand), "var.setRandom")]
    [JsonDerivedType(typeof(CharacterMoveToRoomCommand), "char.moveToRoom")]
    [JsonDerivedType(typeof(DisplayMultimediaCommand), "media.displayMultimedia")]
    [JsonDerivedType(typeof(CharacterDisplayPortraitCommand), "char.displayPortrait")]
    [JsonDerivedType(typeof(CharacterSetPortraitMediaCommand), "char.setPortraitMedia")]
    [JsonDerivedType(typeof(PlayerSetPortraitMediaCommand), "player.setPortraitMedia")]
    public abstract class ActionStep
    {
        public abstract ActionStepKind Kind { get; }
        // Optional user label common to both
        public string? Label { get; set; }
        public virtual string TypeName => GetType().Name;
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
        public override string TypeName => "Variable equals";
        public override bool Evaluate(ActionContext ctx)
        {
            var v = ctx.GetVariable(Name)?.Value;
            return CaseInsensitive
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
        public override string TypeName => "Set variable";
        public override void Execute(ActionContext ctx) => ctx.SetVariable(Name, Value);
    }

    public sealed class MovePlayerToRoomCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
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

    public sealed class AddObjectToRoomCommand : GameCommand
    {
        public string RoomId { get; set; } = string.Empty;
        public string ObjectId { get; set; } = string.Empty;
        public override string TypeName => "Add object to room";
        public override void Execute(ActionContext ctx)
        {
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            var resolvedObj = RagsCore.Services.TemplateResolver.Resolve(ObjectId, ctx);
            if (!Guid.TryParse(resolvedRoom, out var rId) || !Guid.TryParse(resolvedObj, out var oId)) return;

            var room = ctx.Game.Rooms.FirstOrDefault(r => r.Id == rId);
            if (room is null) return;
            if (!room.ObjectIds.Contains(oId))
                room.ObjectIds.Add(oId);
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

    public sealed class PlaySoundEffectCommand : GameCommand
    {
        public string SoundId { get; set; } = string.Empty;
        public double Volume { get; set; } = 100.0;
        public override string TypeName => "Media: Play Sound Effect";
        public override void Execute(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(SoundId, ctx);
            ctx.SetVariable("media.lastSoundId", resolved);
            ctx.SetVariable("media.lastSoundVolume", Volume.ToString());
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

    public sealed class CharacterMoveToRoomCommand : GameCommand
    {
        public string CharacterId { get; set; } = string.Empty;
        public string RoomId { get; set; } = string.Empty;
        public override string TypeName => "Character: Move To Room";
        public override void Execute(ActionContext ctx)
        {
            var resolvedChar = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            var resolvedRoom = RagsCore.Services.TemplateResolver.Resolve(RoomId, ctx);
            if (!Guid.TryParse(resolvedChar, out var cId) || !Guid.TryParse(resolvedRoom, out var rId)) return;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId);
            if (character is not null)
            {
                ctx.SetVariable($"char.{cId}.currentRoomId", rId.ToString());
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
            var charRoomVar = ctx.GetVariable($"char.{resolvedChar}.currentRoomId")?.Value;
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
            var varVal = ctx.GetVariable(Name)?.Value ?? string.Empty;
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

    public sealed class CharacterGenderCondition : Condition
    {
        public string CharacterId { get; set; } = string.Empty;
        public string Gender { get; set; } = "Male";
        public override string TypeName => "Character: Gender";
        public override bool Evaluate(ActionContext ctx)
        {
            var resolved = RagsCore.Services.TemplateResolver.Resolve(CharacterId, ctx);
            if (!Guid.TryParse(resolved, out var cId)) return false;
            
            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == cId);
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
            ctx.SetVariable($"char.{resolvedChar}.displayedPortraitId", resolvedPort);
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
            
            if (Guid.TryParse(resolvedChar, out var charId))
            {
                var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == charId);
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
            var charRoomVar = ctx.GetVariable($"char.{resolvedChar}.currentRoomId")?.Value;
            return charRoomVar != null && string.Equals(charRoomVar, resolvedRoom, StringComparison.OrdinalIgnoreCase);
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
            if (!Guid.TryParse(resolvedItem, out var itemId) || !Guid.TryParse(resolvedChar, out var charId)) return false;

            var character = ctx.Game.Characters.FirstOrDefault(c => c.Id == charId);
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

    public sealed class VariableComparisonToVariableCondition : Condition
    {
        public string NameA { get; set; } = string.Empty;
        public string Comparison { get; set; } = "=";
        public string NameB { get; set; } = string.Empty;
        public override string TypeName => "Variable: Comparison To Variable";
        public override bool Evaluate(ActionContext ctx)
        {
            var valA = ctx.GetVariable(NameA)?.Value ?? string.Empty;
            var valB = ctx.GetVariable(NameB)?.Value ?? string.Empty;

            if (double.TryParse(valA, out double numA) && double.TryParse(valB, out double numB))
            {
                return Comparison switch
                {
                    "=" => numA == numB,
                    "!=" => numA != numB,
                    ">" => numA > numB,
                    ">=" => numA >= numB,
                    "<" => numA < numB,
                    "<=" => numA <= numB,
                    _ => false
                };
            }

            return Comparison switch
            {
                "=" => string.Equals(valA, valB, StringComparison.OrdinalIgnoreCase),
                "!=" => !string.Equals(valA, valB, StringComparison.OrdinalIgnoreCase),
                _ => false
            };
        }
    }
}