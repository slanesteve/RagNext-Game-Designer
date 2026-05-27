using System;
using System.Collections.Generic;
using System.Linq;
using RagsCore.Models;
using RagsCore.Actions;

namespace RagNext.Services
{
    public static class ValidationEngine
    {
        public static List<string> TraceReferences(Game game, Guid targetId, string itemName)
        {
            var refs = new List<string>();
            var idStr = targetId.ToString();
            var normName = itemName.ToLowerInvariant().Replace(" ", "").Replace("_", "");

            // 1. Scan Room Exits
            foreach (var room in game.Rooms)
            {
                foreach (var exit in room.Exits)
                {
                    if (exit.Value == targetId)
                    {
                        refs.Add($"Room Exit: '{room.Name}' exit '{exit.Key}' points to this Room.");
                    }
                }
            }

            // 2. Scan Actions in Rooms
            foreach (var room in game.Rooms)
            {
                ScanActions(room.Actions, targetId, normName, refs, $"Room '{room.Name}'");
            }

            // 3. Scan Actions in Objects
            foreach (var obj in game.Objects)
            {
                ScanActions(obj.Actions, targetId, normName, refs, $"Object '{obj.Name}'");
            }

            // 4. Scan Actions in Characters
            foreach (var ch in game.Characters)
            {
                ScanActions(ch.Actions, targetId, normName, refs, $"Character '{ch.Name}'");
            }
            
            // 4b. Scan Actions in Player
            if (game.Player != null && game.Player.Actions != null)
            {
                ScanActions(game.Player.Actions, targetId, normName, refs, "Player");
            }

            // 5. Scan Actions in Timers
            foreach (var timer in game.Timers)
            {
                ScanActions(new[] { (RagsCore.Models.Action)timer }, targetId, normName, refs, $"Timer '{timer.Name}'");
            }

            // 6. Scan Actions in Functions
            foreach (var func in game.Functions)
            {
                ScanActions(new[] { (RagsCore.Models.Action)func }, targetId, normName, refs, $"Function '{func.Name}'");
            }

            return refs;
        }

        private static void ScanActions(IEnumerable<RagsCore.Models.Action> actions, Guid id, string normName, List<string> refs, string locationName)
        {
            foreach (var action in actions)
            {
                ScanSteps(action.Nodes, id, normName, refs, $"{locationName} -> Action '{action.Name}'");
            }
        }

        private static void ScanSteps(IEnumerable<ActionStep> steps, Guid id, string normName, List<string> refs, string path)
        {
            var idStr = id.ToString();
            foreach (var step in steps)
            {
                CheckStepFields(step, idStr, normName, refs, path);

                if (step is RagsCore.Actions.Condition cond)
                {
                    ScanSteps(cond.TrueBranch, id, normName, refs, $"{path} -> True Branch");
                    ScanSteps(cond.FalseBranch, id, normName, refs, $"{path} -> False Branch");
                }
            }
        }

        private static void CheckStepFields(ActionStep step, string idStr, string normName, List<string> refs, string path)
        {
            if (step is PlayerInRoomCondition pic && pic.RoomId == idStr)
                refs.Add($"{path}: Condition 'Player in Room' references this Room.");
            else if (step is RoomHasObjectCondition rhc && (rhc.RoomId == idStr || rhc.ObjectId == idStr))
                refs.Add($"{path}: Condition 'Room Has Object' references this {(rhc.RoomId == idStr ? "Room" : "Object")}.");
            else if (step is MovePlayerToRoomCommand mpc && mpc.RoomId == idStr)
                refs.Add($"{path}: Command 'Move Player to Room' references this Room.");
            else if (step is AddObjectToRoomCommand aoc && (aoc.RoomId == idStr || aoc.ObjectId == idStr))
                refs.Add($"{path}: Command 'Add Object to Room' references this {(aoc.RoomId == idStr ? "Room" : "Object")}.");
            else if (step is RemoveObjectFromRoomCommand roc && (roc.RoomId == idStr || roc.ObjectId == idStr))
                refs.Add($"{path}: Command 'Remove Object from Room' references this {(roc.RoomId == idStr ? "Room" : "Object")}.");
            else if (step is CharacterMoveToRoomCommand cmc && (cmc.CharacterId == idStr || cmc.RoomId == idStr))
                refs.Add($"{path}: Command 'Move Character to Room' references this {(cmc.CharacterId == idStr ? "Character" : "Room")}.");
            else if (step is PlayerInSameRoomAsCondition psc && psc.CharacterId == idStr)
                refs.Add($"{path}: Condition 'Player In Same Room As' references this Character.");
            else if (step is ItemHeldByPlayerCondition ihc && ihc.ItemId == idStr)
                refs.Add($"{path}: Condition 'Item Held By Player' references this Object.");
            else if (step is CharacterGenderCondition cgc && cgc.CharacterId == idStr)
                refs.Add($"{path}: Condition 'Character Gender' references this Character.");
            else if (step is CharacterSetPortraitMediaCommand cpm && cpm.CharacterId == idStr)
                refs.Add($"{path}: Command 'Set Character Portrait Media' references this Character.");
            else if (step is CharacterInRoomCondition crc && (crc.CharacterId == idStr || crc.RoomId == idStr))
                refs.Add($"{path}: Condition 'Character In Room' references this {(crc.CharacterId == idStr ? "Character" : "Room")}.");
            else if (step is ItemInRoomCondition iir && (iir.ItemId == idStr || iir.RoomId == idStr))
                refs.Add($"{path}: Condition 'Item In Room' references this {(iir.ItemId == idStr ? "Object" : "Room")}.");
            else if (step is ItemHeldByCharacterCondition ihcc && (ihcc.ItemId == idStr || ihcc.CharacterId == idStr))
                refs.Add($"{path}: Condition 'Item Held By Character' references this {(ihcc.ItemId == idStr ? "Object" : "Character")}.");
            else if (step is ItemInObjectCondition iio && (iio.ItemId == idStr || iio.ContainerObjectId == idStr))
                refs.Add($"{path}: Condition 'Item In Object' references this Object.");
            else if (step is ItemNotHeldByPlayerCondition inh && inh.ItemId == idStr)
                refs.Add($"{path}: Condition 'Item Not Held By Player' references this Object.");
            else if (step is ItemNotInObjectCondition ino && (ino.ItemId == idStr || ino.ObjectId == idStr))
                refs.Add($"{path}: Condition 'Item Not In Object' references this Object.");
            else if (step is SetRoomExitCommand sec && (sec.RoomId == idStr || sec.DestinationRoomId == idStr))
                refs.Add($"{path}: Command 'Set Room Exit' references this Room.");
            else if (step is DisableRoomExitCommand dec && dec.RoomId == idStr)
                refs.Add($"{path}: Command 'Disable Room Exit' references this Room.");
            else if (step is OpenContainerCommand occ && occ.ObjectId == idStr)
                refs.Add($"{path}: Command 'Open Container' references this Object.");
            else if (step is CloseContainerCommand ccc && ccc.ObjectId == idStr)
                refs.Add($"{path}: Command 'Close Container' references this Object.");
            else if (step is CallFunctionCommand cfc && cfc.FunctionId == idStr)
                refs.Add($"{path}: Command 'Call Function' references this Function.");

            // Generic scan of all string properties on the action step for text template references (like {objects.deletethis.description})
            if (!string.IsNullOrEmpty(normName))
            {
                var props = step.GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                foreach (var prop in props)
                {
                    if (prop.PropertyType == typeof(string))
                    {
                        var val = prop.GetValue(step) as string;
                        if (!string.IsNullOrEmpty(val))
                        {
                            var lowerVal = val.ToLowerInvariant();
                            if (lowerVal.Contains("{" + normName + ".") || 
                                lowerVal.Contains(".{" + normName + ".") ||
                                lowerVal.Contains("objects." + normName) ||
                                lowerVal.Contains("rooms." + normName) ||
                                lowerVal.Contains("chars." + normName) ||
                                lowerVal.Contains("characters." + normName) ||
                                lowerVal.Contains("functions." + normName) ||
                                lowerVal.Contains("timers." + normName))
                            {
                                refs.Add($"{path}: Text template property '{prop.Name}' references this item via '{val}'.");
                            }
                        }
                    }
                }
            }
        }
    }
}
