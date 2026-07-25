using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using RagNext.Designer.Avalonia.Models;
using RagsCore.Actions;
using RagsCore.Models;
using RagsCore.Services;

namespace RagNext.Designer.Avalonia.Services
{
    public static class ValidationEngine
    {
        public static List<ValidationErrorItem> Validate(Game game, string? projectMediaDirectory = null)
        {
            var errors = new List<ValidationErrorItem>();
            if (game == null)
            {
                errors.Add(new ValidationErrorItem
                {
                    Message = "Game database instance is null.",
                    Severity = "Error",
                    Category = "General"
                });
                return errors;
            }

            // 1. Structural & Metadata Diagnostics
            ValidateMetadata(game, errors);

            // Build entity lookups for fast reference checking
            var roomIds = new HashSet<Guid>(game.Rooms?.Select(r => r.Id) ?? Enumerable.Empty<Guid>());
            var roomNames = new HashSet<string>(game.Rooms?.Select(r => NormalizeName(r.Name)) ?? Enumerable.Empty<string>());
            
            var objectIds = new HashSet<Guid>(game.Objects?.Select(o => o.Id) ?? Enumerable.Empty<Guid>());
            var objectNames = new HashSet<string>(game.Objects?.Select(o => NormalizeName(o.Name)) ?? Enumerable.Empty<string>());

            var characterIds = new HashSet<Guid>(game.Characters?.Select(c => c.Id) ?? Enumerable.Empty<Guid>());
            var characterNames = new HashSet<string>(game.Characters?.Select(c => NormalizeName(c.Name)) ?? Enumerable.Empty<string>());

            var functionIds = new HashSet<Guid>(game.Functions?.Select(f => f.Id) ?? Enumerable.Empty<Guid>());
            var functionNames = new HashSet<string>(game.Functions?.Select(f => NormalizeName(f.Name)) ?? Enumerable.Empty<string>());

            var timerIds = new HashSet<Guid>(game.Timers?.Select(t => t.Id) ?? Enumerable.Empty<Guid>());
            var timerNames = new HashSet<string>(game.Timers?.Select(t => NormalizeName(t.Name)) ?? Enumerable.Empty<string>());

            var varNames = new HashSet<string>(game.Variables?.Select(v => NormalizeName(v.Name)) ?? Enumerable.Empty<string>());

            var mediaAssetIds = new HashSet<Guid>(game.MediaAssets?.Select(m => m.Id) ?? Enumerable.Empty<Guid>());

            // 2. Validate Room Exits
            ValidateRooms(game, roomIds, errors);

            // 3. Validate Variables & Math Initializers
            ValidateVariables(game, errors);

            // 4. Validate Action Steps in Rooms, Objects, Characters, Player, Timers, Functions
            ValidateActionTrees(game, roomIds, objectIds, characterIds, functionIds, timerIds, varNames, objectNames, roomNames, characterNames, functionNames, timerNames, errors);

            // 5. Validate Media Asset References
            ValidateMediaAssets(game, mediaAssetIds, projectMediaDirectory, errors);

            // 6. Circular Function Call Graph Diagnostics
            ValidateFunctionCallGraph(game, errors);

            return errors;
        }

        private static string NormalizeName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;
            return name.Trim().ToLowerInvariant().Replace(" ", "").Replace("_", "");
        }

        private static void ValidateMetadata(Game game, List<ValidationErrorItem> errors)
        {
            if (string.IsNullOrWhiteSpace(game.Title))
            {
                errors.Add(new ValidationErrorItem
                {
                    Message = "Game Title is required.",
                    Severity = "Error",
                    Category = "General"
                });
            }

            if (string.IsNullOrWhiteSpace(game.Author))
            {
                errors.Add(new ValidationErrorItem
                {
                    Message = "Game Author name is empty.",
                    Severity = "Warning",
                    Category = "General"
                });
            }

            if (game.Rooms == null || game.Rooms.Count == 0)
            {
                errors.Add(new ValidationErrorItem
                {
                    Message = "The project must contain at least one Room.",
                    Severity = "Error",
                    Category = "Room"
                });
            }
        }

        private static void ValidateRooms(Game game, HashSet<Guid> roomIds, List<ValidationErrorItem> errors)
        {
            if (game.Rooms == null) return;

            foreach (var r in game.Rooms)
            {
                if (string.IsNullOrWhiteSpace(r.Name))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Message = $"Room with ID '{r.Id}' has an empty or white-space name.",
                        Severity = "Error",
                        Category = "Room",
                        TargetId = r.Id,
                        TargetName = r.Id.ToString()
                    });
                }

                if (r.Exits != null)
                {
                    foreach (var exit in r.Exits)
                    {
                        if (exit.Value != Guid.Empty && !roomIds.Contains(exit.Value))
                        {
                            errors.Add(new ValidationErrorItem
                            {
                                Message = $"Room '{r.Name}' exit '{exit.Key}' points to invalid/deleted Room ID '{exit.Value}'.",
                                Severity = "Error",
                                Category = "Room",
                                TargetId = r.Id,
                                TargetName = r.Name
                            });
                        }
                    }
                }
            }
        }

        private static void ValidateVariables(Game game, List<ValidationErrorItem> errors)
        {
            if (game.Variables == null) return;

            foreach (var v in game.Variables)
            {
                if (string.IsNullOrWhiteSpace(v.Name))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Message = $"Variable with ID '{v.Id}' has an empty or white-space name.",
                        Severity = "Error",
                        Category = "Variable",
                        TargetId = v.Id
                    });
                }

                if ((v.Type == "int" || v.Type == "number") && !string.IsNullOrWhiteSpace(v.Value))
                {
                    try
                    {
                        MathEvaluator.Evaluate(v.Value);
                    }
                    catch (Exception ex)
                    {
                        errors.Add(new ValidationErrorItem
                        {
                            Message = $"Variable '{v.Name}' initial math expression '{v.Value}' is invalid: {ex.Message}",
                            Severity = "Error",
                            Category = "Variable",
                            TargetId = v.Id,
                            TargetName = v.Name
                        });
                    }
                }
            }
        }

        private static void ValidateActionTrees(
            Game game,
            HashSet<Guid> roomIds,
            HashSet<Guid> objectIds,
            HashSet<Guid> characterIds,
            HashSet<Guid> functionIds,
            HashSet<Guid> timerIds,
            HashSet<string> varNames,
            HashSet<string> objectNames,
            HashSet<string> roomNames,
            HashSet<string> characterNames,
            HashSet<string> functionNames,
            HashSet<string> timerNames,
            List<ValidationErrorItem> errors)
        {
            // Rooms
            if (game.Rooms != null)
            {
                foreach (var room in game.Rooms)
                {
                    if (room.Actions != null)
                    {
                        foreach (var action in room.Actions)
                        {
                            ScanActionNodes(action.Nodes, room.Id, $"Room '{room.Name}' -> Action '{action.Name}'", "Room", roomIds, objectIds, characterIds, functionIds, timerIds, varNames, objectNames, roomNames, characterNames, functionNames, timerNames, errors);
                        }
                    }
                }
            }

            // Objects
            if (game.Objects != null)
            {
                foreach (var obj in game.Objects)
                {
                    if (obj.Actions != null)
                    {
                        foreach (var action in obj.Actions)
                        {
                            ScanActionNodes(action.Nodes, obj.Id, $"Object '{obj.Name}' -> Action '{action.Name}'", "Object", roomIds, objectIds, characterIds, functionIds, timerIds, varNames, objectNames, roomNames, characterNames, functionNames, timerNames, errors);
                        }
                    }
                }
            }

            // Characters
            if (game.Characters != null)
            {
                foreach (var ch in game.Characters)
                {
                    if (ch.Actions != null)
                    {
                        foreach (var action in ch.Actions)
                        {
                            ScanActionNodes(action.Nodes, ch.Id, $"Character '{ch.Name}' -> Action '{action.Name}'", "Character", roomIds, objectIds, characterIds, functionIds, timerIds, varNames, objectNames, roomNames, characterNames, functionNames, timerNames, errors);
                        }
                    }
                }
            }

            // Player
            if (game.Player != null && game.Player.Actions != null)
            {
                foreach (var action in game.Player.Actions)
                {
                    ScanActionNodes(action.Nodes, Guid.Empty, $"Player -> Action '{action.Name}'", "Player", roomIds, objectIds, characterIds, functionIds, timerIds, varNames, objectNames, roomNames, characterNames, functionNames, timerNames, errors);
                }
            }

            // Timers
            if (game.Timers != null)
            {
                foreach (var timer in game.Timers)
                {
                    var timerAction = (RagsCore.Models.Action)timer;
                    if (timerAction != null && timerAction.Nodes != null)
                    {
                        ScanActionNodes(timerAction.Nodes, timer.Id, $"Timer '{timer.Name}'", "Timer", roomIds, objectIds, characterIds, functionIds, timerIds, varNames, objectNames, roomNames, characterNames, functionNames, timerNames, errors);
                    }
                }
            }

            // Functions
            if (game.Functions != null)
            {
                foreach (var func in game.Functions)
                {
                    var funcAction = (RagsCore.Models.Action)func;
                    if (funcAction != null && funcAction.Nodes != null)
                    {
                        ScanActionNodes(funcAction.Nodes, func.Id, $"Function '{func.Name}'", "Function", roomIds, objectIds, characterIds, functionIds, timerIds, varNames, objectNames, roomNames, characterNames, functionNames, timerNames, errors);
                    }
                }
            }
        }

        private static void ScanActionNodes(
            IEnumerable<ActionStep> steps,
            Guid entityId,
            string locationContext,
            string category,
            HashSet<Guid> roomIds,
            HashSet<Guid> objectIds,
            HashSet<Guid> characterIds,
            HashSet<Guid> functionIds,
            HashSet<Guid> timerIds,
            HashSet<string> varNames,
            HashSet<string> objectNames,
            HashSet<string> roomNames,
            HashSet<string> characterNames,
            HashSet<string> functionNames,
            HashSet<string> timerNames,
            List<ValidationErrorItem> errors)
        {
            if (steps == null) return;

            foreach (var step in steps)
            {
                CheckStepReferences(step, entityId, locationContext, category, roomIds, objectIds, characterIds, functionIds, timerIds, errors);
                CheckStepTextTemplates(step, entityId, locationContext, category, varNames, objectNames, roomNames, characterNames, functionNames, timerNames, errors);
                CheckStepNodeSettings(step, entityId, locationContext, category, errors);

                if (step is Condition cond)
                {
                    if (cond.TrueBranch != null)
                        ScanActionNodes(cond.TrueBranch, entityId, $"{locationContext} -> True Branch", category, roomIds, objectIds, characterIds, functionIds, timerIds, varNames, objectNames, roomNames, characterNames, functionNames, timerNames, errors);
                    if (cond.FalseBranch != null)
                        ScanActionNodes(cond.FalseBranch, entityId, $"{locationContext} -> False Branch", category, roomIds, objectIds, characterIds, functionIds, timerIds, varNames, objectNames, roomNames, characterNames, functionNames, timerNames, errors);
                }
            }
        }

        private static void CheckStepNodeSettings(
            ActionStep step,
            Guid entityId,
            string locationContext,
            string category,
            List<ValidationErrorItem> errors)
        {
            if (step is ForEachLoopCommand loop)
            {
                if (string.IsNullOrWhiteSpace(loop.ArrayVariableName) || loop.ArrayVariableName == "-- Select --")
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Message = $"{locationContext}: Step 'For Each Loop' has no Array Variable selected (-- Select --).",
                        Severity = "Warning",
                        Category = category,
                        TargetId = entityId
                    });
                }
            }
            else if (step is SetVariableCommand setVar)
            {
                if (string.IsNullOrWhiteSpace(setVar.Name) || setVar.Name == "-- Select --")
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Message = $"{locationContext}: Step 'Set Variable' has no target variable selected.",
                        Severity = "Warning",
                        Category = category,
                        TargetId = entityId
                    });
                }
            }
        }

        private static void CheckStepReferences(
            ActionStep step,
            Guid entityId,
            string locationContext,
            string category,
            HashSet<Guid> roomIds,
            HashSet<Guid> objectIds,
            HashSet<Guid> characterIds,
            HashSet<Guid> functionIds,
            HashSet<Guid> timerIds,
            List<ValidationErrorItem> errors)
        {
            // Room References
            void VerifyRoom(string? rawId, string stepName)
            {
                if (!string.IsNullOrEmpty(rawId) && Guid.TryParse(rawId, out var g) && g != Guid.Empty && !roomIds.Contains(g))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Message = $"{locationContext}: {stepName} references non-existent Room ID '{rawId}'.",
                        Severity = "Error",
                        Category = category,
                        TargetId = entityId
                    });
                }
            }

            // Object References
            void VerifyObject(string? rawId, string stepName)
            {
                if (!string.IsNullOrEmpty(rawId) && Guid.TryParse(rawId, out var g) && g != Guid.Empty && !objectIds.Contains(g))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Message = $"{locationContext}: {stepName} references non-existent Object ID '{rawId}'.",
                        Severity = "Error",
                        Category = category,
                        TargetId = entityId
                    });
                }
            }

            // Character References
            void VerifyCharacter(string? rawId, string stepName)
            {
                if (!string.IsNullOrEmpty(rawId) && Guid.TryParse(rawId, out var g) && g != Guid.Empty && !characterIds.Contains(g))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Message = $"{locationContext}: {stepName} references non-existent Character ID '{rawId}'.",
                        Severity = "Error",
                        Category = category,
                        TargetId = entityId
                    });
                }
            }

            // Function References
            void VerifyFunction(string? rawId, string stepName)
            {
                if (!string.IsNullOrEmpty(rawId) && Guid.TryParse(rawId, out var g) && g != Guid.Empty && !functionIds.Contains(g))
                {
                    errors.Add(new ValidationErrorItem
                    {
                        Message = $"{locationContext}: {stepName} references non-existent Function ID '{rawId}'.",
                        Severity = "Error",
                        Category = category,
                        TargetId = entityId
                    });
                }
            }

            if (step is PlayerInRoomCondition pic) VerifyRoom(pic.RoomId, "Condition 'Player in Room'");
            else if (step is MovePlayerToRoomCommand mpc) VerifyRoom(mpc.RoomId, "Command 'Move Player to Room'");
            else if (step is SetRoomExitCommand sec) { VerifyRoom(sec.RoomId, "Command 'Set Room Exit'"); VerifyRoom(sec.DestinationRoomId, "Command 'Set Room Exit (Destination)'"); }
            else if (step is DisableRoomExitCommand dec) VerifyRoom(dec.RoomId, "Command 'Disable Room Exit'");

            else if (step is RoomHasObjectCondition rhc) { VerifyRoom(rhc.RoomId, "Condition 'Room Has Object'"); VerifyObject(rhc.ObjectId, "Condition 'Room Has Object'"); }
            else if (step is AddObjectToRoomCommand aoc) { VerifyRoom(aoc.RoomId, "Command 'Add Object to Room'"); VerifyObject(aoc.ObjectId, "Command 'Add Object to Room'"); }
            else if (step is RemoveObjectFromRoomCommand roc) { VerifyRoom(roc.RoomId, "Command 'Remove Object from Room'"); VerifyObject(roc.ObjectId, "Command 'Remove Object from Room'"); }
            else if (step is ItemHeldByPlayerCondition ihc) VerifyObject(ihc.ItemId, "Condition 'Item Held by Player'");
            else if (step is ItemNotHeldByPlayerCondition inh) VerifyObject(inh.ItemId, "Condition 'Item Not Held by Player'");
            else if (step is OpenContainerCommand occ) VerifyObject(occ.ObjectId, "Command 'Open Container'");
            else if (step is CloseContainerCommand ccc) VerifyObject(ccc.ObjectId, "Command 'Close Container'");

            else if (step is CharacterMoveToRoomCommand cmc) { VerifyCharacter(cmc.CharacterId, "Command 'Move Character to Room'"); VerifyRoom(cmc.RoomId, "Command 'Move Character to Room'"); }
            else if (step is PlayerInSameRoomAsCondition psc) VerifyCharacter(psc.CharacterId, "Condition 'Player in Same Room As'");
            else if (step is CharacterGenderCondition cgc) VerifyCharacter(cgc.CharacterId, "Condition 'Character Gender'");
            else if (step is CharacterSetPortraitMediaCommand cpm) VerifyCharacter(cpm.CharacterId, "Command 'Set Character Portrait Media'");
            else if (step is CharacterInRoomCondition crc) { VerifyCharacter(crc.CharacterId, "Condition 'Character in Room'"); VerifyRoom(crc.RoomId, "Condition 'Character in Room'"); }
            else if (step is ItemHeldByCharacterCondition ihcc) { VerifyObject(ihcc.ItemId, "Condition 'Item Held by Character'"); VerifyCharacter(ihcc.CharacterId, "Condition 'Item Held by Character'"); }

            else if (step is CallFunctionCommand cfc) VerifyFunction(cfc.FunctionId, "Command 'Call Function'");
        }

        private static readonly Regex TemplateRegex = new Regex(@"\{([a-zA-Z0-9_\.]+)\}", RegexOptions.Compiled);

        private static void CheckStepTextTemplates(
            ActionStep step,
            Guid entityId,
            string locationContext,
            string category,
            HashSet<string> varNames,
            HashSet<string> objectNames,
            HashSet<string> roomNames,
            HashSet<string> characterNames,
            HashSet<string> functionNames,
            HashSet<string> timerNames,
            List<ValidationErrorItem> errors)
        {
            var props = step.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var prop in props)
            {
                if (prop.PropertyType == typeof(string))
                {
                    var val = prop.GetValue(step) as string;
                    if (string.IsNullOrWhiteSpace(val)) continue;

                    var matches = TemplateRegex.Matches(val);
                    foreach (Match m in matches)
                    {
                        var token = m.Groups[1].Value.Trim().ToLowerInvariant();
                        var parts = token.Split('.');
                        if (parts.Length < 2) continue;

                        var prefix = parts[0];
                        var name = parts[1].Replace("_", "");

                        if (prefix == "vars" || prefix == "var" || prefix == "variable")
                        {
                            if (!varNames.Contains(name))
                            {
                                errors.Add(new ValidationErrorItem
                                {
                                    Message = $"{locationContext}: Property '{prop.Name}' references non-existent Variable token '{{{m.Value}}}'.",
                                    Severity = "Warning",
                                    Category = category,
                                    TargetId = entityId
                                });
                            }
                        }
                        else if (prefix == "objects" || prefix == "object" || prefix == "obj")
                        {
                            if (!objectNames.Contains(name))
                            {
                                errors.Add(new ValidationErrorItem
                                {
                                    Message = $"{locationContext}: Property '{prop.Name}' references non-existent Object token '{{{m.Value}}}'.",
                                    Severity = "Warning",
                                    Category = category,
                                    TargetId = entityId
                                });
                            }
                        }
                        else if (prefix == "rooms" || prefix == "room")
                        {
                            if (!roomNames.Contains(name))
                            {
                                errors.Add(new ValidationErrorItem
                                {
                                    Message = $"{locationContext}: Property '{prop.Name}' references non-existent Room token '{{{m.Value}}}'.",
                                    Severity = "Warning",
                                    Category = category,
                                    TargetId = entityId
                                });
                            }
                        }
                        else if (prefix == "chars" || prefix == "char" || prefix == "characters" || prefix == "character")
                        {
                            if (!characterNames.Contains(name))
                            {
                                errors.Add(new ValidationErrorItem
                                {
                                    Message = $"{locationContext}: Property '{prop.Name}' references non-existent Character token '{{{m.Value}}}'.",
                                    Severity = "Warning",
                                    Category = category,
                                    TargetId = entityId
                                });
                            }
                        }
                    }
                }
            }
        }

        private static void ValidateMediaAssets(Game game, HashSet<Guid> mediaAssetIds, string? projectMediaDir, List<ValidationErrorItem> errors)
        {
            if (game.MediaAssets == null) return;

            // Check physical existence of MediaAssets if projectMediaDir provided
            if (!string.IsNullOrEmpty(projectMediaDir) && Directory.Exists(projectMediaDir))
            {
                foreach (var media in game.MediaAssets)
                {
                    if (!string.IsNullOrWhiteSpace(media.RelativePath))
                    {
                        string fullPath = Path.IsPathRooted(media.RelativePath) 
                            ? media.RelativePath 
                            : Path.Combine(projectMediaDir, media.RelativePath);

                        if (!File.Exists(fullPath))
                        {
                            errors.Add(new ValidationErrorItem
                            {
                                Message = $"Media asset '{media.Name}' file not found on disk: '{media.RelativePath}'.",
                                Severity = "Warning",
                                Category = "Media",
                                TargetId = media.Id,
                                TargetName = media.Name
                            });
                        }
                    }
                }
            }
        }

        private static void ValidateFunctionCallGraph(Game game, List<ValidationErrorItem> errors)
        {
            if (game.Functions == null || game.Functions.Count == 0) return;

            var functionMap = game.Functions.ToDictionary(f => f.Id, f => f);
            var callGraph = new Dictionary<Guid, List<Guid>>();

            foreach (var func in game.Functions)
            {
                var calledFuncs = new List<Guid>();
                var funcAction = (RagsCore.Models.Action)func;
                if (funcAction?.Nodes != null)
                {
                    FindCalledFunctions(funcAction.Nodes, calledFuncs);
                }
                callGraph[func.Id] = calledFuncs;
            }

            // Cycle detection using DFS
            foreach (var func in game.Functions)
            {
                var visited = new HashSet<Guid>();
                var stack = new List<Guid>();

                if (HasCycle(func.Id, callGraph, visited, stack))
                {
                    var cyclePath = string.Join(" -> ", stack.Select(id => functionMap.TryGetValue(id, out var f) ? f.Name : id.ToString()));
                    errors.Add(new ValidationErrorItem
                    {
                        Message = $"Circular Function Call detected: {cyclePath}",
                        Severity = "Error",
                        Category = "Function",
                        TargetId = func.Id,
                        TargetName = func.Name
                    });
                }
            }
        }

        private static void FindCalledFunctions(IEnumerable<ActionStep> steps, List<Guid> calledFuncs)
        {
            if (steps == null) return;
            foreach (var step in steps)
            {
                if (step is CallFunctionCommand cfc && Guid.TryParse(cfc.FunctionId, out var g) && g != Guid.Empty)
                {
                    calledFuncs.Add(g);
                }
                else if (step is Condition cond)
                {
                    if (cond.TrueBranch != null) FindCalledFunctions(cond.TrueBranch, calledFuncs);
                    if (cond.FalseBranch != null) FindCalledFunctions(cond.FalseBranch, calledFuncs);
                }
            }
        }

        private static bool HasCycle(Guid current, Dictionary<Guid, List<Guid>> graph, HashSet<Guid> visited, List<Guid> stack)
        {
            if (stack.Contains(current))
            {
                stack.Add(current);
                return true;
            }
            if (visited.Contains(current)) return false;

            visited.Add(current);
            stack.Add(current);

            if (graph.TryGetValue(current, out var neighbors))
            {
                foreach (var neighbor in neighbors)
                {
                    if (HasCycle(neighbor, graph, visited, stack)) return true;
                }
            }

            stack.RemoveAt(stack.Count - 1);
            return false;
        }
    }
}
