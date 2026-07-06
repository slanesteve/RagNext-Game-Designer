#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using RagNextPlayer.Runtime.Models;
using RagNextPlayer.Managers;
using UnityEngine;

namespace RagNextPlayer.Runtime
{
    /// <summary>
    /// Lightweight runtime context passed to every ActionStep during execution.
    /// Holds mutable game state references without duplicating data.
    /// Mirrors RagsCore.Actions.ActionContext.
    /// </summary>
    public class GameExecutionContext
    {
        public GameData       Game        { get; }
        public RoomData?      CurrentRoom { get; set; }
        public GameObjectData? FocusObject{ get; }
        public object?        FocusEntity { get; }

        public PlayerData Player => Game.Player;

        public GameExecutionContext(GameData game, RoomData? currentRoom = null, GameObjectData? focusObject = null, object? focusEntity = null)
        {
            Game        = game ?? throw new ArgumentNullException(nameof(game));
            CurrentRoom = currentRoom;
            FocusObject = focusObject;
            FocusEntity = focusEntity ?? focusObject;
        }

        public GameVariableData? GetVariable(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (name.StartsWith("variables.", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(10);
            }
            else if (name.StartsWith("variable.", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(9);
            }

            if (name.Contains(':'))
            {
                var index = name.IndexOf(':');
                var realName = name.Substring(0, index);
                var modifier = name.Substring(index + 1).ToLowerInvariant();
                var baseVar = Game.Variables.Find(v => string.Equals(v.Name, realName, StringComparison.OrdinalIgnoreCase));
                if (baseVar != null && DateTime.TryParse(baseVar.Value, out var dt))
                {
                    string? val = modifier switch
                    {
                        "year" => dt.Year.ToString(),
                        "month" => dt.Month.ToString(),
                        "day" => dt.Day.ToString(),
                        "hour" => dt.Hour.ToString(),
                        "minute" => dt.Minute.ToString(),
                        "second" => dt.Second.ToString(),
                        "dayofweek" => ((int)dt.DayOfWeek).ToString(),
                        "date" => dt.ToString("yyyy-MM-dd"),
                        "time" => dt.ToString("HH:mm:ss"),
                        "datetime" => dt.ToString("yyyy-MM-ddTHH:mm:ss"),
                        _ => null
                    };
                    if (val != null)
                    {
                        return new GameVariableData { Name = name, Value = val };
                    }
                }
            }
            return Game.Variables.Find(v => string.Equals(v.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public void SetVariable(string name, string? value)
        {
            if (string.IsNullOrEmpty(name)) return;

            var cleanName = name;
            if (cleanName.StartsWith("{") && cleanName.EndsWith("}"))
            {
                cleanName = cleanName.Substring(1, cleanName.Length - 2);
            }

            if (cleanName.StartsWith("variables.", StringComparison.OrdinalIgnoreCase))
            {
                cleanName = cleanName.Substring(10);
            }
            else if (cleanName.StartsWith("variable.", StringComparison.OrdinalIgnoreCase))
            {
                cleanName = cleanName.Substring(9);
            }

            var parts = cleanName.Split('.');
            if (parts.Length >= 3)
            {
                var baseVar = Game.Variables.Find(v => string.Equals(v.Name, parts[0], StringComparison.OrdinalIgnoreCase));
                if (baseVar != null && (string.Equals(baseVar.Type, "array", StringComparison.OrdinalIgnoreCase) || baseVar.Columns.Count > 0))
                {
                    int rowIndex = -1;
                    string colName = "";
                    if (int.TryParse(parts[1], out var idx1))
                    {
                        rowIndex = idx1;
                        colName = parts[2];
                    }
                    else if (int.TryParse(parts[2], out var idx2))
                    {
                        rowIndex = idx2;
                        colName = parts[1];
                    }

                    if (rowIndex >= 0 && rowIndex < baseVar.Rows.Count)
                    {
                        int colIdx = baseVar.Columns.FindIndex(c => string.Equals(c, colName, StringComparison.OrdinalIgnoreCase));
                        if (colIdx >= 0)
                        {
                            var row = baseVar.Rows[rowIndex];
                            while (row.Count <= colIdx) row.Add(string.Empty);
                            row[colIdx] = value ?? string.Empty;
                            return;
                        }
                    }
                }
            }

            var v = GetVariable(cleanName);
            if (v is null)
            {
                v = new GameVariableData { Name = cleanName, Value = value };
                Game.Variables.Add(v);
            }
            else
            {
                v.Value = value;
            }
        }

        /// <summary>Returns the resolved text of {token} patterns in a string.</summary>
        public string Resolve(string? text) =>
            TemplateResolver.Resolve(text, Game, CurrentRoom, FocusEntity);

    }

    /// <summary>
    /// Executes a game action (list of ActionSteps) recursively, propagating
    /// results to the caller through IGameEventSink.
    ///
    /// Design principles (ported from MAUI ActionExecutor):
    ///   - Model objects mutate state only — no Unity API calls inside Execute().
    ///   - The sink (CommandEffectRouter) drives all UI/audio side-effects.
    ///   - Conditions recurse fully: TrueBranch / FalseBranch can contain further
    ///     commands or deeply-nested conditions with no depth limit.
    ///   - Exceptions are caught per-node so a single bad command never aborts
    ///     the rest of the action sequence.
    /// </summary>
    public class LoopScopeTracker : IEnumerator<ActionStepData>
    {
        private readonly ForEachLoopCommandData _loopNode;
        private readonly GameExecutionContext _ctx;
        private readonly List<List<string>> _rows;
        private readonly List<string> _columns;
        private int _currentRowIndex = -1;
        private IEnumerator<ActionStepData>? _currentBodyEnumerator;

        public LoopScopeTracker(ForEachLoopCommandData loopNode, GameExecutionContext ctx, List<List<string>> rows, List<string> columns = null)
        {
            _loopNode = loopNode;
            _ctx = ctx;
            _rows = rows;
            _columns = columns;
        }

        public ForEachLoopCommandData LoopNode => _loopNode;

        public ActionStepData Current => _currentBodyEnumerator?.Current ?? _loopNode;

        object System.Collections.IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_currentRowIndex == -1)
            {
                _currentRowIndex = 0;
                if (_rows.Count == 0) return false;
                SetupRowVariables();
                _currentBodyEnumerator = _loopNode.TrueBranch.GetEnumerator();
            }

            while (true)
            {
                if (_currentBodyEnumerator != null && _currentBodyEnumerator.MoveNext())
                {
                    return true;
                }

                // Advance to next row
                _currentRowIndex++;
                if (_currentRowIndex >= _rows.Count)
                {
                    return false;
                }

                SetupRowVariables();
                _currentBodyEnumerator = _loopNode.TrueBranch.GetEnumerator();
            }
        }

        private void SetupRowVariables()
        {
            // Clear any old Loop variables first to prevent contamination
            var oldLoopVars = _ctx.Game.Variables.Where(v => v.Name.StartsWith("Loop.", StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var oldVar in oldLoopVars)
            {
                _ctx.Game.Variables.Remove(oldVar);
            }

            var rowData = _rows[_currentRowIndex];

            if (_columns != null && _columns.Count > 0)
            {
                for (int i = 0; i < _columns.Count; i++)
                {
                    string colName = _columns[i];
                    string value = i < rowData.Count ? rowData[i] : string.Empty;
                    _ctx.SetVariable($"Loop.{colName}", value);
                    // Expose dynamic custom attributes under loop.attributes.Name as well
                    if (i >= 3)
                    {
                        _ctx.SetVariable($"Loop.attributes.{colName}", value);
                    }
                }
            }
            else
            {
                var varObj = _ctx.Game.Variables.Find(v => string.Equals(v.Name, _loopNode.ArrayVariableName, StringComparison.OrdinalIgnoreCase));
                if (varObj != null)
                {
                    for (int i = 0; i < varObj.Columns.Count; i++)
                    {
                        string colName = varObj.Columns[i];
                        string value = i < rowData.Count ? rowData[i] : string.Empty;
                        _ctx.SetVariable($"Loop.{colName}", value);
                    }
                }
            }
        }

        public void Reset()
        {
            _currentRowIndex = -1;
            _currentBodyEnumerator = null;
        }

        public void Dispose()
        {
            _currentBodyEnumerator?.Dispose();
        }
    }

    public class ActionRunner
    {
        private Stack<IEnumerator<ActionStepData>> _scopes = new();
        private GameExecutionContext _ctx;
        private IGameEventSink? _sink;
        
        public bool IsSuspended { get; private set; }
        public string ActionName { get; }
        
        public ActionRunner(ActionData action, GameExecutionContext ctx, IGameEventSink? sink)
        {
            _ctx = ctx;
            _sink = sink;
            ActionName = action?.Name ?? "";
            _scopes.Push(action.Nodes.GetEnumerator());
            ActionExecutor.RegisterRunner(this);
        }
        
        public void Resume()
        {
            IsSuspended = false;
            _ctx.SetVariable("system.prompt.active", "false");
            ExecuteNext();
        }
        
        public void ExecuteNext()
        {
            while (_scopes.Count > 0 && !IsSuspended)
            {
                var currentScope = _scopes.Peek();
                if (currentScope.MoveNext())
                {
                    var node = currentScope.Current;
                    if (node is null) continue;
                    
                    if (node is CommandData cmd)
                    {
                        try
                        {
                            if (cmd is CallFunctionCommandData callCmd)
                            {
                                var func = _ctx.Game.Functions.Find(f => f.Id == callCmd.FunctionId || string.Equals(f.Name, callCmd.FunctionId, System.StringComparison.OrdinalIgnoreCase));
                                if (func != null && func.Nodes != null && func.Nodes.Count > 0)
                                {
                                    _scopes.Push(func.Nodes.GetEnumerator());
                                }
                            }
                            else if (cmd is BreakLoopCommandData)
                            {
                                // Pop scopes until we escape the current LoopScopeTracker
                                while (_scopes.Count > 0 && !(_scopes.Peek() is LoopScopeTracker))
                                {
                                    var top = _scopes.Pop();
                                    top.Dispose();
                                }
                                if (_scopes.Count > 0)
                                {
                                    var loopScope = _scopes.Pop();
                                    loopScope.Dispose();
                                    if (loopScope is LoopScopeTracker loopTracker)
                                    {
                                        var completedBranch = loopTracker.LoopNode.FalseBranch;
                                        if (completedBranch != null && completedBranch.Count > 0)
                                        {
                                            _scopes.Push(completedBranch.GetEnumerator());
                                        }
                                    }
                                }
                            }
                            else
                            {
                                ActionExecutor.ExecuteCommand(cmd, _ctx);
                                _sink?.OnCommandExecuted(cmd, _ctx);
                            }

                            if (cmd is PromptPlayerInputCommandData)
                            {
                                IsSuspended = true;
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[ActionExecutor] Error executing {node.Type}: {ex.Message}");
                        }
                    }
                    else if (node is ConditionData cond)
                    {
                        try
                        {
                            if (cond is SwitchCommandData switchNode)
                            {
                                var expr = switchNode.Expression;
                                var resolvedVal = "";
                                if (!string.IsNullOrEmpty(expr))
                                {
                                    if (!expr.Contains("{") && !expr.Contains("}"))
                                    {
                                        var variable = _ctx.Game.Variables.Find(v => string.Equals(v.Name, expr, StringComparison.OrdinalIgnoreCase));
                                        if (variable != null)
                                        {
                                            resolvedVal = variable.Value;
                                        }
                                        else
                                        {
                                            // Support path-like bare names (e.g. 'variables.InputData') by wrapping in curly braces
                                            resolvedVal = _ctx.Resolve("{" + expr + "}");
                                        }
                                    }
                                    else
                                    {
                                        resolvedVal = _ctx.Resolve(expr);
                                    }
                                }

                                resolvedVal = resolvedVal?.Trim() ?? "";
                                Debug.Log($"[ActionExecutor] Evaluating Switch node. Expression: '{expr}', Resolved Value: '{resolvedVal}'");

                                List<ActionStepData> caseBranch = null;
                                if (switchNode.Cases != null)
                                {
                                    foreach (var kvp in switchNode.Cases)
                                    {
                                        if (string.Equals(kvp.Key?.Trim(), resolvedVal, StringComparison.OrdinalIgnoreCase))
                                        {
                                            caseBranch = kvp.Value;
                                            Debug.Log($"[ActionExecutor] Switch node matched case: '{kvp.Key}'");
                                            break;
                                        }
                                    }
                                }

                                if (caseBranch != null && caseBranch.Count > 0)
                                {
                                    _scopes.Push(caseBranch.GetEnumerator());
                                }
                                else if (switchNode.DefaultBranch != null && switchNode.DefaultBranch.Count > 0)
                                {
                                    Debug.Log($"[ActionExecutor] Switch node did not match any case. Falling back to default branch.");
                                    _scopes.Push(switchNode.DefaultBranch.GetEnumerator());
                                }
                                else
                                {
                                    Debug.Log($"[ActionExecutor] Switch node did not match any case and default branch is empty.");
                                }
                            }
                            else if (cond is ForEachLoopCommandData loopNode)
                            {
                                List<List<string>> rows = new List<List<string>>();
                                List<string> columns = new List<string>();

                                var loopSource = loopNode.LoopSource ?? "Variable";
                                var filterType = loopNode.FilterType ?? "All";

                                if (string.Equals(loopSource, "Variable", StringComparison.OrdinalIgnoreCase))
                                {
                                    var varName = loopNode.ArrayVariableName;
                                    var arrayVar = _ctx.Game.Variables.Find(v => string.Equals(v.Name, varName, StringComparison.OrdinalIgnoreCase));
                                    if (arrayVar != null && arrayVar.Rows != null)
                                    {
                                        columns = arrayVar.Columns;
                                        rows = arrayVar.Rows;
                                    }
                                }
                                else if (string.Equals(loopSource, "Items", StringComparison.OrdinalIgnoreCase))
                                {
                                    columns = new List<string> { "Id", "Name", "Description", "IsWorn", "IsContainer", "IsCollectible" };
                                    IEnumerable<GameObjectData> items = _ctx.Game.Objects.Where(o => !o.IsCharacter);
                                    if (string.Equals(filterType, "Inventory", StringComparison.OrdinalIgnoreCase))
                                    {
                                        items = _ctx.Player.Inventory;
                                    }
                                    else if (string.Equals(filterType, "Worn", StringComparison.OrdinalIgnoreCase))
                                    {
                                        items = _ctx.Player.Inventory.Where(i => i.IsWearable && (i.IsWorn || (i.Attributes.TryGetValue("Worn", out var wVal) && string.Equals(wVal, "True", StringComparison.OrdinalIgnoreCase))));
                                    }
                                    else if (string.Equals(filterType, "Room", StringComparison.OrdinalIgnoreCase) || string.Equals(filterType, "In Current Room", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var currentRoomId = _ctx.GetVariable("player.currentRoomId")?.Value;
                                        var currentRoom = _ctx.Game.Rooms.FirstOrDefault(r => string.Equals(r.Id, currentRoomId, StringComparison.OrdinalIgnoreCase));
                                        if (currentRoom != null)
                                        {
                                            items = _ctx.Game.Objects.Where(o => currentRoom.ObjectIds.Contains(o.Id));
                                        }
                                    }

                                    var itemsList = items.ToList();
                                    foreach (var item in itemsList)
                                    {
                                        var row = new List<string> { item.Id, item.Name, item.Description, (item.IsWorn || (item.Attributes.TryGetValue("Worn", out var wVal) && string.Equals(wVal, "True", StringComparison.OrdinalIgnoreCase))).ToString(), item.IsContainer.ToString(), item.IsCollectible.ToString() };
                                        rows.Add(row);
                                    }

                                    var allAttrKeys = itemsList.SelectMany(i => i.Attributes.Keys).Distinct().ToList();
                                    foreach (var attrKey in allAttrKeys)
                                    {
                                        columns.Add(attrKey);
                                    }

                                    for (int i = 0; i < itemsList.Count; i++)
                                    {
                                        var item = itemsList[i];
                                        var row = rows[i];
                                        foreach (var attrKey in allAttrKeys)
                                        {
                                            item.Attributes.TryGetValue(attrKey, out var attrVal);
                                            row.Add(attrVal ?? string.Empty);
                                        }
                                    }
                                }
                                else if (string.Equals(loopSource, "Characters", StringComparison.OrdinalIgnoreCase))
                                {
                                    columns = new List<string> { "Id", "Name", "Description" };
                                    IEnumerable<GameObjectData> chars = _ctx.Game.Characters;
                                    if (string.Equals(filterType, "Room", StringComparison.OrdinalIgnoreCase) || string.Equals(filterType, "In Current Room", StringComparison.OrdinalIgnoreCase))
                                    {
                                        var currentRoomId = _ctx.GetVariable("player.currentRoomId")?.Value;
                                        chars = _ctx.Game.Characters.Where(c => {
                                            var charRoomId = _ctx.GetVariable($"char.{c.Id}.currentRoomId")?.Value ?? c.StartingRoomId;
                                            return string.Equals(charRoomId, currentRoomId, StringComparison.OrdinalIgnoreCase);
                                        });
                                    }

                                    var charsList = chars.ToList();
                                    foreach (var ch in charsList)
                                    {
                                        var row = new List<string> { ch.Id, ch.Name, ch.Description };
                                        rows.Add(row);
                                    }

                                    var allAttrKeys = charsList.SelectMany(c => c.Attributes.Keys).Distinct().ToList();
                                    foreach (var attrKey in allAttrKeys)
                                    {
                                        columns.Add(attrKey);
                                    }

                                    for (int i = 0; i < charsList.Count; i++)
                                    {
                                        var ch = charsList[i];
                                        var row = rows[i];
                                        foreach (var attrKey in allAttrKeys)
                                        {
                                            ch.Attributes.TryGetValue(attrKey, out var attrVal);
                                            row.Add(attrVal ?? string.Empty);
                                        }
                                    }
                                }
                                else if (string.Equals(loopSource, "Rooms", StringComparison.OrdinalIgnoreCase))
                                {
                                    columns = new List<string> { "Id", "Name", "Description" };
                                    var rooms = _ctx.Game.Rooms;

                                    foreach (var rm in rooms)
                                    {
                                        var row = new List<string> { rm.Id, rm.Name, rm.Description };
                                        rows.Add(row);
                                    }

                                    var allAttrKeys = rooms.SelectMany(r => r.Attributes.Keys).Distinct().ToList();
                                    foreach (var attrKey in allAttrKeys)
                                    {
                                        columns.Add(attrKey);
                                    }

                                    for (int i = 0; i < rooms.Count; i++)
                                    {
                                        var rm = rooms[i];
                                        var row = rows[i];
                                        foreach (var attrKey in allAttrKeys)
                                        {
                                            rm.Attributes.TryGetValue(attrKey, out var attrVal);
                                            row.Add(attrVal ?? string.Empty);
                                        }
                                    }
                                }

                                if (rows.Count > 0)
                                {
                                    var loopTracker = new LoopScopeTracker(loopNode, _ctx, rows, columns);
                                    _scopes.Push(loopTracker);
                                }
                                else
                                {
                                    if (loopNode.FalseBranch != null && loopNode.FalseBranch.Count > 0)
                                    {
                                        _scopes.Push(loopNode.FalseBranch.GetEnumerator());
                                    }
                                }
                            }
                            else
                            {
                                bool result = ActionExecutor.EvaluateCondition(cond, _ctx);
                                _sink?.OnConditionEvaluated(cond, result, _ctx);
                                
                                var branch = result ? cond.TrueBranch : cond.FalseBranch;
                                if (branch != null && branch.Count > 0)
                                {
                                    _scopes.Push(branch.GetEnumerator());
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[ActionExecutor] Error evaluating {node.Type}: {ex.Message}");
                        }
                    }
                }
                else
                {
                    currentScope.Dispose();
                    _scopes.Pop();
                    if (currentScope is LoopScopeTracker loopTracker)
                    {
                        var completedBranch = loopTracker.LoopNode.FalseBranch;
                        if (completedBranch != null && completedBranch.Count > 0)
                        {
                            _scopes.Push(completedBranch.GetEnumerator());
                        }
                    }
                }
            }
            if (_scopes.Count == 0)
            {
                ActionExecutor.UnregisterRunner(this);
            }
        }
    }

    /// <summary>
    /// Executes a game action (list of ActionSteps) recursively, propagating
    /// results to the caller through IGameEventSink.
    ///
    /// Design principles (ported from MAUI ActionExecutor):
    ///   - Model objects mutate state only — no Unity API calls inside Execute().
    ///   - The sink (CommandEffectRouter) drives all UI/audio side-effects.
    ///   - Conditions recurse fully: TrueBranch / FalseBranch can contain further
    ///     commands or deeply-nested conditions with no depth limit.
    ///   - Exceptions are caught per-node so a single bad command never aborts
    ///     the rest of the action sequence.
    /// </summary>
    public static class ActionExecutor
    {
        private static readonly System.Collections.Generic.List<ActionRunner> _runners = new();
        private static int _executionDepth = 0;

        public static ActionRunner? ActiveRunner { get; set; }

        public static void RegisterRunner(ActionRunner runner)
        {
            _runners.Add(runner);
            ActiveRunner = runner;
        }

        public static void UnregisterRunner(ActionRunner runner)
        {
            _runners.Remove(runner);
            if (ActiveRunner == runner)
            {
                ActiveRunner = _runners.Count > 0 ? _runners[_runners.Count - 1] : null;
            }
        }

        public static void ResumeSuspended()
        {
            for (int i = _runners.Count - 1; i >= 0; i--)
            {
                if (_runners[i].IsSuspended)
                {
                    Debug.Log($"[ActionExecutor] Resuming suspended runner: '{_runners[i].ActionName}'");
                    RagNextPlayer.Managers.UIManager.Instance?.PrepareForNewAction();
                    _runners[i].Resume();
                    return;
                }
            }
            RagNextPlayer.Managers.UIManager.Instance?.PrepareForNewAction();
            ActiveRunner?.Resume();
        }

        public static void Execute(ActionData action, GameExecutionContext ctx, IGameEventSink? sink = null, bool isUserInteraction = true)
        {
            if (action is null || ctx is null) return;
            if (!action.InitallyActive)
            {
                Debug.Log($"[ActionExecutor] Skip executing inactive action: '{action.Name}' ({action.Id}).");
                return;
            }
            
            _executionDepth++;
            try
            {
                if (_executionDepth == 1 && isUserInteraction)
                {
                    RagNextPlayer.Managers.UIManager.Instance?.PrepareForNewAction();
                }

                Debug.Log($"[ActionExecutor] Execute called for action: '{action.Name}' ({action.Id}).");
                var runner = new ActionRunner(action, ctx, sink);
                runner.ExecuteNext();
            }
            finally
            {
                _executionDepth--;
            }
        }

        // ── Command Dispatch ──────────────────────────────────────────────────

        public static void ExecuteCommand(CommandData cmd, GameExecutionContext ctx)
        {
            Debug.Log($"[ActionExecutor] ExecuteCommand: Type={cmd.GetType().Name}, cmd.Type={cmd.Type}");
            switch (cmd)
            {
                case DisplayTextCommandData c:
                    ctx.SetVariable("system.lastDisplayedText", ctx.Resolve(c.Text));
                    break;

                case SetVariableCommandData c:
                    ctx.SetVariable(c.Name, ctx.Resolve(c.Value));
                    break;

                case EvaluateFormulaCommandData c:
                    {
                        var resolved = ctx.Resolve(c.Formula);
                        try
                        {
                            var val = MathEvaluator.Evaluate(resolved);
                            ctx.SetVariable(c.Name, val.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        }
                        catch (System.Exception ex)
                        {
                            ctx.SetVariable("system.error", $"Formula error: {ex.Message}");
                        }
                    }
                    break;

                case VariableIncrementCommandData c:
                    {
                        var resolvedVal = ctx.Resolve(c.Value);
                        var existing = ctx.GetVariable(c.Name)?.Value;

                        if (existing != null && DateTime.TryParse(existing, out var existingDt))
                        {
                            var newDt = DateTimeHelper.AddToDateTime(existingDt, resolvedVal, true);
                            if (newDt.HasValue)
                            {
                                ctx.SetVariable(c.Name, newDt.Value.ToString("yyyy-MM-ddTHH:mm:ss"));
                                break;
                            }
                        }

                        if (double.TryParse(existing, out double a) && double.TryParse(resolvedVal, out double b))
                            ctx.SetVariable(c.Name, (a + b).ToString(System.Globalization.CultureInfo.InvariantCulture));
                        else
                            ctx.SetVariable(c.Name, (existing ?? "") + resolvedVal);
                    }
                    break;

                case VariableDecrementCommandData c:
                    {
                        var resolvedVal = ctx.Resolve(c.Value);
                        var existing = ctx.GetVariable(c.Name)?.Value;

                        if (existing != null && DateTime.TryParse(existing, out var existingDt))
                        {
                            var newDt = DateTimeHelper.AddToDateTime(existingDt, resolvedVal, false);
                            if (newDt.HasValue)
                            {
                                ctx.SetVariable(c.Name, newDt.Value.ToString("yyyy-MM-ddTHH:mm:ss"));
                                break;
                            }
                        }

                        if (double.TryParse(existing, out double a) && double.TryParse(resolvedVal, out double b))
                            ctx.SetVariable(c.Name, (a - b).ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    break;

                case VariableSetToVariableCommandData c:
                    ctx.SetVariable(c.Name, ctx.GetVariable(c.SourceName)?.Value);
                    break;

                case SetNumericRandomlyCommandData c:
                    {
                        var val = UnityEngine.Random.Range((int)c.Minimum, (int)c.Maximum + 1);
                        ctx.SetVariable(c.Name, val.ToString());
                    }
                    break;

                case SetArrayElementCommandData c:
                    {
                        var resolvedVal = ctx.Resolve(c.Value);
                        var resolvedRow = ctx.Resolve(c.RowIndex);
                        var resolvedCol = ctx.Resolve(c.ColumnName);
                        var v = ctx.Game.Variables.Find(varb => string.Equals(varb.Name, c.ArrayVariableName, StringComparison.OrdinalIgnoreCase));
                        if (v != null && v.Columns != null && v.Rows != null && int.TryParse(resolvedRow, out int rIdx) && rIdx >= 0)
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
                    break;

                case AddArrayRowCommandData c:
                    {
                        var resolvedValues = ctx.Resolve(c.ValuesCommaSeparated);
                        var v = ctx.Game.Variables.Find(varb => string.Equals(varb.Name, c.ArrayVariableName, StringComparison.OrdinalIgnoreCase));
                        if (v != null && v.Columns != null && v.Rows != null)
                        {
                            var row = new List<string>();
                            var parts = resolvedValues.Split(',');
                            for (int i = 0; i < v.Columns.Count; i++)
                            {
                                row.Add(i < parts.Length ? parts[i].Trim() : string.Empty);
                            }
                            v.Rows.Add(row);
                        }
                    }
                    break;

                case RemoveArrayRowCommandData c:
                    {
                        var resolvedRow = ctx.Resolve(c.RowIndex);
                        var v = ctx.Game.Variables.Find(varb => string.Equals(varb.Name, c.ArrayVariableName, StringComparison.OrdinalIgnoreCase));
                        if (v != null && v.Rows != null && int.TryParse(resolvedRow, out int rIdx) && rIdx >= 0 && rIdx < v.Rows.Count)
                        {
                            v.Rows.RemoveAt(rIdx);
                        }
                    }
                    break;

                case AppendTextCommandData c:
                    {
                        var resolvedText = ctx.Resolve(c.Text);
                        var v = ctx.Game.Variables.Find(varb => string.Equals(varb.Name, c.VariableName, StringComparison.OrdinalIgnoreCase));
                        if (v != null)
                        {
                            v.Value = (v.Value ?? string.Empty) + resolvedText;
                        }
                        else
                        {
                            ctx.SetVariable(c.VariableName, resolvedText);
                        }
                    }
                    break;

                case AppendLineCommandData c:
                    {
                        var resolvedText = ctx.Resolve(c.Text);
                        var v = ctx.Game.Variables.Find(varb => string.Equals(varb.Name, c.VariableName, StringComparison.OrdinalIgnoreCase));
                        if (v != null)
                        {
                            v.Value = (v.Value ?? string.Empty) + resolvedText + "\n";
                        }
                        else
                        {
                            ctx.SetVariable(c.VariableName, resolvedText + "\n");
                        }
                    }
                    break;

                case MovePlayerToRoomCommandData c:
                    {
                        var resolved = ctx.Resolve(c.RoomId);
                        ctx.SetVariable("player.currentRoomId", resolved);
                    }
                    break;

                case AddObjectToRoomCommandData c:
                    {
                        var resolvedRoom = ctx.Resolve(c.RoomId);
                        var resolvedObj  = ctx.Resolve(c.ObjectId);
                        RemoveObjectFromEverywhere(resolvedObj, ctx);
                        var room = ctx.Game.Rooms.Find(r => r.Id == resolvedRoom);
                        if (room is not null && !room.ObjectIds.Contains(resolvedObj))
                        {
                            room.ObjectIds.Add(resolvedObj);
                            
                            // If this room is the current room, fire OnObjectDropped
                            if (ctx.CurrentRoom != null && string.Equals(ctx.CurrentRoom.Id, room.Id, StringComparison.OrdinalIgnoreCase))
                            {
                                var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedObj, StringComparison.OrdinalIgnoreCase));
                                if (obj?.Actions != null)
                                {
                                    var objCtx = new GameExecutionContext(ctx.Game, room, obj, obj);
                                    foreach (var action in obj.Actions)
                                    {
                                        if (string.Equals(action.Trigger, "OnObjectDropped", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ActionExecutor.Execute(action, objCtx, RagNextPlayer.Managers.InteractionController.Instance?.GetComponent<RagNextPlayer.Managers.CommandEffectRouter>());
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;

                case RemoveObjectFromRoomCommandData c:
                    {
                        var resolvedRoom = ctx.Resolve(c.RoomId);
                        var resolvedObj  = ctx.Resolve(c.ObjectId);
                        var room = ctx.Game.Rooms.Find(r => r.Id == resolvedRoom);
                        room?.ObjectIds.Remove(resolvedObj);
                    }
                    break;

                case SetRoomExitCommandData c:
                    {
                        var resolvedRoom = ctx.Resolve(c.RoomId);
                        var resolvedDir  = ctx.Resolve(c.Direction);
                        var resolvedDest = ctx.Resolve(c.DestinationRoomId);
                        var rId = string.IsNullOrWhiteSpace(resolvedRoom) ? ctx.CurrentRoom?.Id : resolvedRoom;
                        var room = ctx.Game.Rooms.Find(r => r.Id == rId);
                        if (room is null || string.IsNullOrWhiteSpace(resolvedDir)) break;
                        if (string.IsNullOrWhiteSpace(resolvedDest))
                            room.Exits.Remove(resolvedDir);
                        else
                            room.Exits[resolvedDir] = resolvedDest;
                    }
                    break;

                case DisableRoomExitCommandData c:
                    {
                        var resolvedRoom = ctx.Resolve(c.RoomId);
                        var resolvedDir  = ctx.Resolve(c.Direction);
                        var rId = string.IsNullOrWhiteSpace(resolvedRoom) ? ctx.CurrentRoom?.Id : resolvedRoom;
                        var room = ctx.Game.Rooms.Find(r => r.Id == rId);
                        room?.Exits.Remove(resolvedDir);
                    }
                    break;

                case LockRoomExitCommandData c:
                    {
                        var resolvedRoom = string.IsNullOrWhiteSpace(c.RoomId) ? "" : ResolveRoomId(c.RoomId, ctx);
                        var resolvedDir  = ctx.Resolve(c.Direction);
                        var rId = string.IsNullOrWhiteSpace(resolvedRoom) ? ctx.CurrentRoom?.Id : resolvedRoom;
                        var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, rId, StringComparison.OrdinalIgnoreCase));
                        if (room is not null && !string.IsNullOrWhiteSpace(resolvedDir))
                        {
                            room.LockedExits[resolvedDir] = true;
                        }
                    }
                    break;

                case UnlockRoomExitCommandData c:
                    {
                        var resolvedRoom = string.IsNullOrWhiteSpace(c.RoomId) ? "" : ResolveRoomId(c.RoomId, ctx);
                        var resolvedDir  = ctx.Resolve(c.Direction);
                        var rId = string.IsNullOrWhiteSpace(resolvedRoom) ? ctx.CurrentRoom?.Id : resolvedRoom;
                        var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, rId, StringComparison.OrdinalIgnoreCase));
                        if (room is not null && !string.IsNullOrWhiteSpace(resolvedDir))
                        {
                            room.LockedExits[resolvedDir] = false;
                        }
                    }
                    break;

                case PlayerSetNameCommandData c:
                    ctx.Player.Name = ctx.Resolve(c.Name);
                    break;

                case PlayerSetDescriptionCommandData c:
                    ctx.Player.Description = ctx.Resolve(c.Description);
                    break;

                case PlayerSetGenderCommandData c:
                    ctx.Player.Gender = ctx.Resolve(c.Gender);
                    break;

                case PlayerSetPortraitMediaCommandData c:
                    {
                        var resolved = ctx.Resolve(c.MediaId);
                        var asset = ctx.Game.MediaAssets.Find(a => a.Id == resolved 
                            || string.Equals(a.Name, resolved, StringComparison.OrdinalIgnoreCase) 
                            || string.Equals(System.IO.Path.GetFileNameWithoutExtension(a.Name), resolved, StringComparison.OrdinalIgnoreCase));
                        ctx.Player.PortraitImagePath = asset?.RelativePath ?? resolved;
                    }
                    break;

                case CharacterSetPortraitMediaCommandData c:
                    {
                        var charId = ResolveCharacterId(c.CharacterId, ctx);
                        var resolved = ctx.Resolve(c.MediaId);
                        var asset = ctx.Game.MediaAssets.Find(a => a.Id == resolved 
                            || string.Equals(a.Name, resolved, StringComparison.OrdinalIgnoreCase) 
                            || string.Equals(System.IO.Path.GetFileNameWithoutExtension(a.Name), resolved, StringComparison.OrdinalIgnoreCase));
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, charId, StringComparison.OrdinalIgnoreCase));
                        if (character != null)
                        {
                            character.PortraitImagePath = asset?.RelativePath ?? resolved;
                        }
                    }
                    break;


                case CharacterMoveToRoomCommandData c:
                    {
                        var charId = ResolveCharacterId(c.CharacterId, ctx);
                        var targetRoomId = ResolveRoomId(c.RoomId, ctx);
                        MoveCharacterToRoomHelper(charId, targetRoomId, ctx);
                    }
                    break;

                case CharacterMoveToRandomAdjacentCommandData c:
                    {
                        var charId = ResolveCharacterId(c.CharacterId, ctx);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, charId, StringComparison.OrdinalIgnoreCase));
                        if (character == null) break;

                        var currentRoomId = ctx.GetVariable($"char.{charId}.currentRoomId")?.Value;
                        if (string.IsNullOrEmpty(currentRoomId))
                        {
                            currentRoomId = character.StartingRoomId;
                        }

                        if (string.IsNullOrEmpty(currentRoomId)) break;

                        var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, currentRoomId, StringComparison.OrdinalIgnoreCase));
                        if (room == null) break;

                        var validExits = new List<string>();
                        foreach (var exit in room.Exits)
                        {
                            bool isLocked = false;
                            if (room.LockedExits != null && room.LockedExits.TryGetValue(exit.Key, out var locked))
                            {
                                isLocked = locked;
                            }
                            if (!isLocked)
                            {
                                validExits.Add(exit.Value);
                            }
                        }

                        if (validExits.Count == 0) break;

                        int rndIndex = UnityEngine.Random.Range(0, validExits.Count);
                        var targetRoomId = validExits[rndIndex];

                        MoveCharacterToRoomHelper(charId, targetRoomId, ctx);
                    }
                    break;

                case CharacterMoveAlongPatrolPathCommandData c:
                    {
                        var charId = ResolveCharacterId(c.CharacterId, ctx);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, charId, StringComparison.OrdinalIgnoreCase));
                        if (character == null) break;

                        var resolvedPath = ctx.Resolve(c.PatrolPath);
                        if (string.IsNullOrEmpty(resolvedPath)) break;

                        var rooms = resolvedPath.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries);
                        if (rooms.Length == 0) break;

                        if (string.IsNullOrEmpty(c.IndexVariable)) break;

                        var idxStr = ctx.GetVariable(c.IndexVariable)?.Value ?? "0";
                        if (!int.TryParse(idxStr, out var currentIndex))
                        {
                            currentIndex = 0;
                        }

                        var dirVarName = $"{c.IndexVariable}_dir";
                        var dirStr = ctx.GetVariable(dirVarName)?.Value ?? "1";
                        if (!int.TryParse(dirStr, out var direction))
                        {
                            direction = 1;
                        }

                        int nextIndex = currentIndex + direction;

                        if (c.PingPong)
                        {
                            if (nextIndex >= rooms.Length)
                            {
                                direction = -1;
                                nextIndex = System.Math.Max(0, rooms.Length - 2);
                            }
                            else if (nextIndex < 0)
                            {
                                direction = 1;
                                nextIndex = System.Math.Min(rooms.Length - 1, 1);
                            }
                            ctx.SetVariable(dirVarName, direction.ToString());
                        }
                        else
                        {
                            if (nextIndex >= rooms.Length)
                            {
                                nextIndex = 0;
                            }
                            else if (nextIndex < 0)
                            {
                                nextIndex = rooms.Length - 1;
                            }
                        }

                        if (nextIndex >= 0 && nextIndex < rooms.Length)
                        {
                            var targetRoomRef = rooms[nextIndex].Trim();
                            var targetRoomId = ResolveRoomId(targetRoomRef, ctx);
                            if (!string.IsNullOrEmpty(targetRoomId))
                            {
                                ctx.SetVariable(c.IndexVariable, nextIndex.ToString());
                                MoveCharacterToRoomHelper(charId, targetRoomId, ctx);
                            }
                        }
                    }
                    break;

                case CharacterDisplayPortraitCommandData c:
                    ctx.SetVariable($"char.{ResolveCharacterId(c.CharacterId, ctx)}.displayedPortraitId", ctx.Resolve(c.PortraitId));
                    break;

                case PlaySoundEffectCommandData c:
                    {
                        var resolved = ctx.Resolve(c.SoundId);
                        ctx.SetVariable("media.lastSoundId", resolved);
                        ctx.SetVariable("media.lastSoundVolume", c.Volume.ToString());
                        ctx.SetVariable("media.lastSoundLoop", c.Loop.ToString().ToLower());
                        ctx.SetVariable("media.lastSoundStartTime", c.StartTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        ctx.SetVariable("media.lastSoundEndTime", c.EndTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    break;

                case PlayVideoCommandData c:
                    {
                        var resolved = ctx.Resolve(c.VideoId);
                        ctx.SetVariable("media.lastVideoId", resolved);
                        ctx.SetVariable("media.lastVideoVolume", c.Volume.ToString());
                        ctx.SetVariable("media.lastVideoLoop", c.Loop.ToString().ToLower());
                        ctx.SetVariable("media.lastVideoStartTime", c.StartTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        ctx.SetVariable("media.lastVideoEndTime", c.EndTime.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    }
                    break;

                case StopSoundEffectCommandData c:
                    {
                        var resolved = ctx.Resolve(c.SoundId);
                        ctx.SetVariable("media.stopSoundId", resolved);
                        ctx.SetVariable("media.stopAllLooping", c.StopAllLooping.ToString().ToLower());
                    }
                    break;

                case DisplayMultimediaCommandData c:
                    ctx.SetVariable("media.lastDisplayedMediaId", ctx.Resolve(c.MediaId));
                    break;

                case AddCommentCommandData:
                    break; // Design-time only — no runtime effect

                case EndGameCommandData c:
                    ctx.SetVariable("system.isGameOver", "true");
                    ctx.SetVariable("system.endGameMessage", ctx.Resolve(c.FinalMessage));
                    break;

                case PromptPlayerInputCommandData c:
                    ctx.SetVariable("system.prompt.name", ctx.Resolve(c.PromptName));
                    ctx.SetVariable("system.prompt.text", ctx.Resolve(c.PromptText));
                    ctx.SetVariable("system.prompt.type", c.InputType);
                    ctx.SetVariable("system.prompt.options", c.CustomOptions);
                    ctx.SetVariable("system.prompt.targetVar", c.StoreVariableName);
                    ctx.SetVariable("system.prompt.active", "true");
                    break;

                case AddCustomChoiceCommandData c:
                    {
                        var promptName = ctx.Resolve(c.PromptName);
                        var choiceText = ctx.Resolve(c.ChoiceText);
                        var varName = ctx.Resolve(c.VariableName);
                        ctx.Game.CustomChoices.Add(new RuntimeCustomChoice { PromptName = promptName, ChoiceText = choiceText, VariableName = varName });
                    }
                    break;

                case ClearCustomChoiceCommandData c:
                    {
                        var promptName = ctx.Resolve(c.PromptName);
                        ctx.Game.CustomChoices.RemoveAll(ch => string.Equals(ch.PromptName, promptName, System.StringComparison.OrdinalIgnoreCase));
                    }
                    break;

                case RemoveCustomChoiceCommandData c:
                    {
                        var promptName = ctx.Resolve(c.PromptName);
                        var choiceText = ctx.Resolve(c.ChoiceText);
                        ctx.Game.CustomChoices.RemoveAll(ch => string.Equals(ch.PromptName, promptName, System.StringComparison.OrdinalIgnoreCase) && string.Equals(ch.ChoiceText, choiceText, System.StringComparison.OrdinalIgnoreCase));
                    }
                    break;

                case StartDialogueCommandData c:
                    break;

                case OpenContainerCommandData c:
                    {
                        var id = ctx.Resolve(c.ObjectId);
                        var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
                        if (obj is not null)
                        {
                            obj.ContainerOpen = true;
                            ctx.SetVariable($"obj.{id}.containerOpen", "true");
                        }
                    }
                    break;

                case CloseContainerCommandData c:
                    {
                        var id = ctx.Resolve(c.ObjectId);
                        var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, id, StringComparison.OrdinalIgnoreCase));
                        if (obj is not null)
                        {
                            obj.ContainerOpen = false;
                            ctx.SetVariable($"obj.{id}.containerOpen", "false");
                        }
                    }
                    break;

                case DamageCharacterCommandData c:
                    {
                        var resolvedChar = ResolveCharacterId(c.CharacterId, ctx);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        if (character == null)
                        {
                            character = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        }
                        if (character is not null)
                        {
                            character.Properties.TryGetValue("Health", out var hpStr);
                            int hp = int.TryParse(hpStr, out var val) ? val : 100;
                            hp += c.Amount;
                            character.Properties["Health"] = hp.ToString();
                            ctx.SetVariable($"char.{character.Id}.Health", hp.ToString());

                            if (hp <= 0)
                            {
                                character.Properties["State"] = "Dead";
                                ctx.SetVariable($"char.{character.Id}.State", "Dead");
                                var gm = RagNextPlayer.Managers.GameManager.Instance;
                                if (gm != null)
                                {
                                    foreach (var action in character.Actions)
                                    {
                                        if (string.Equals(action.Trigger, "OnCharacterKilled", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ActionExecutor.Execute(action, gm.MakeContext(character), gm.GetComponent<RagNextPlayer.Managers.CommandEffectRouter>());
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;

                case SetCharacterStateCommandData c:
                    {
                        var resolvedChar = ResolveCharacterId(c.CharacterId, ctx);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        if (character == null)
                        {
                            character = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        }
                        if (character is not null)
                        {
                            character.Properties["State"] = c.State;
                            ctx.SetVariable($"char.{character.Id}.State", c.State);
                            if (string.Equals(c.State, "Dead", StringComparison.OrdinalIgnoreCase))
                            {
                                var gm = RagNextPlayer.Managers.GameManager.Instance;
                                if (gm != null)
                                {
                                    foreach (var action in character.Actions)
                                    {
                                        if (string.Equals(action.Trigger, "OnCharacterKilled", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ActionExecutor.Execute(action, gm.MakeContext(character), gm.GetComponent<RagNextPlayer.Managers.CommandEffectRouter>());
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;

                case TriggerTurnTickCommandData:
                    {
                        var gm = RagNextPlayer.Managers.GameManager.Instance;
                        if (gm != null)
                        {
                            // Run global OnTurnTick actions on the Player
                            if (gm.ActiveGame?.Player?.Actions != null)
                            {
                                foreach (var action in gm.ActiveGame.Player.Actions)
                                {
                                    if (string.Equals(action.Trigger, "OnTurnTick", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ActionExecutor.Execute(action, gm.MakeContext(), gm.GetComponent<RagNextPlayer.Managers.CommandEffectRouter>());
                                    }
                                }
                            }
                            // Run OnRoomTick on the current room
                            if (gm.CurrentRoom?.Actions != null)
                            {
                                foreach (var action in gm.CurrentRoom.Actions)
                                {
                                    if (string.Equals(action.Trigger, "OnRoomTick", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ActionExecutor.Execute(action, gm.MakeContext(), gm.GetComponent<RagNextPlayer.Managers.CommandEffectRouter>());
                                    }
                                }
                            }
                        }
                    }
                    break;

                case DebugTextCommandData:
                    // Comment/log node, no execution needed.
                    break;

                case ItemSetActionActiveCommandData c:
                    {
                        var resolvedItem = ctx.Resolve(c.ItemId);
                        var actionName = ctx.Resolve(c.ActionName);
                        
                        UnityEngine.Debug.Log($"[ActionExecutor] ItemSetActionActive: ItemId='{c.ItemId}' (resolved='{resolvedItem}'), ActionName='{c.ActionName}' (resolved='{actionName}'), c.Active={c.Active}");
                        
                        var items = new List<GameObjectData>();
                        
                        // Find in main objects list
                        var mainObj = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedItem, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, resolvedItem, StringComparison.OrdinalIgnoreCase));
                        if (mainObj != null)
                        {
                            UnityEngine.Debug.Log($"[ActionExecutor] Found in main Game.Objects: '{mainObj.Name}'");
                            items.Add(mainObj);
                        }
                        
                        // Find in player inventory
                        if (ctx.Player?.Inventory != null)
                        {
                            var invObj = ctx.Player.Inventory.Find(o => string.Equals(o.Id, resolvedItem, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, resolvedItem, StringComparison.OrdinalIgnoreCase));
                            if (invObj != null)
                            {
                                UnityEngine.Debug.Log($"[ActionExecutor] Found in Player.Inventory: '{invObj.Name}'");
                                items.Add(invObj);
                            }
                            else
                            {
                                UnityEngine.Debug.Log($"[ActionExecutor] Not found in Player.Inventory. Current inventory items: {string.Join(", ", ctx.Player.Inventory.ConvertAll(i => i.Name))}");
                            }
                        }
                        
                        // Find in character inventories
                        if (ctx.Game?.Characters != null)
                        {
                            foreach (var ch in ctx.Game.Characters)
                            {
                                if (ch.Inventory != null)
                                {
                                    var chObj = ch.Inventory.Find(o => string.Equals(o.Id, resolvedItem, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, resolvedItem, StringComparison.OrdinalIgnoreCase));
                                    if (chObj != null)
                                    {
                                        UnityEngine.Debug.Log($"[ActionExecutor] Found in Character '{ch.Name}' inventory: '{chObj.Name}'");
                                        items.Add(chObj);
                                    }
                                }
                            }
                        }

                        if (items.Count == 0)
                        {
                            UnityEngine.Debug.LogWarning($"[ActionExecutor] ItemSetActionActive failed: No matching items found for '{resolvedItem}'!");
                        }

                        foreach (var item in items)
                        {
                            if (item.Actions != null)
                            {
                                foreach (var act in item.Actions)
                                {
                                    UnityEngine.Debug.Log($"[ActionExecutor] Item '{item.Name}' action: '{act.Name}' (active={act.InitallyActive})");
                                    if (string.Equals(act.Name, actionName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        act.InitallyActive = c.Active;
                                        UnityEngine.Debug.Log($"[ActionExecutor] Updated action '{act.Name}' on '{item.Name}' to active={c.Active}");
                                    }
                                }
                            }
                        }
                    }
                    break;

                case RoomSetActionActiveCommandData c:
                    {
                        var resolvedRoom = ctx.Resolve(c.RoomId);
                        var actionName = ctx.Resolve(c.ActionName);
                        UnityEngine.Debug.Log($"[ActionExecutor] RoomSetActionActive: RoomId='{c.RoomId}' (resolved='{resolvedRoom}'), ActionName='{c.ActionName}' (resolved='{actionName}'), c.Active={c.Active}");
                        var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, resolvedRoom, StringComparison.OrdinalIgnoreCase) || string.Equals(r.Name, resolvedRoom, StringComparison.OrdinalIgnoreCase));
                        if (room != null && room.Actions != null)
                        {
                            foreach (var act in room.Actions)
                            {
                                if (string.Equals(act.Name, actionName, StringComparison.OrdinalIgnoreCase))
                                {
                                    act.InitallyActive = c.Active;
                                    UnityEngine.Debug.Log($"[ActionExecutor] Updated action '{act.Name}' on Room '{room.Name}' to active={c.Active}");
                                }
                            }
                        }
                    }
                    break;

                case PlayerSetActionActiveCommandData c:
                    {
                        var actionName = ctx.Resolve(c.ActionName);
                        UnityEngine.Debug.Log($"[ActionExecutor] PlayerSetActionActive: ActionName='{c.ActionName}' (resolved='{actionName}'), c.Active={c.Active}");
                        if (ctx.Game.Player.Actions != null)
                        {
                            foreach (var act in ctx.Game.Player.Actions)
                            {
                                if (string.Equals(act.Name, actionName, StringComparison.OrdinalIgnoreCase))
                                {
                                    act.InitallyActive = c.Active;
                                    UnityEngine.Debug.Log($"[ActionExecutor] Updated player action '{act.Name}' to active={c.Active}");
                                }
                            }
                        }
                    }
                    break;

                case CharacterSetActionActiveCommandData c:
                    {
                        var actionName = ctx.Resolve(c.ActionName);
                        UnityEngine.Debug.Log($"[ActionExecutor] CharacterSetActionActive: CharacterId='{c.CharacterId}', ActionName='{c.ActionName}' (resolved='{actionName}'), c.Active={c.Active}");
                        
                        var resolvedCharId = string.IsNullOrEmpty(c.CharacterId) ? null : ResolveCharacterId(c.CharacterId, ctx);
                        if (!string.IsNullOrEmpty(resolvedCharId))
                        {
                            var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, resolvedCharId, StringComparison.OrdinalIgnoreCase));
                            if (character != null && character.Actions != null)
                            {
                                foreach (var act in character.Actions)
                                {
                                    if (string.Equals(act.Name, actionName, StringComparison.OrdinalIgnoreCase))
                                    {
                                        act.InitallyActive = c.Active;
                                        UnityEngine.Debug.Log($"[ActionExecutor] Updated character action '{act.Name}' on Character '{character.Name}' to active={c.Active}");
                                    }
                                }
                            }
                            break;
                        }

                        // 1. Player actions
                        if (ctx.Game.Player.Actions != null)
                        {
                            foreach (var act in ctx.Game.Player.Actions)
                            {
                                if (string.Equals(act.Name, actionName, StringComparison.OrdinalIgnoreCase))
                                {
                                    act.InitallyActive = c.Active;
                                    UnityEngine.Debug.Log($"[ActionExecutor] Updated player action '{act.Name}' to active={c.Active}");
                                }
                            }
                        }
                        // 2. Room actions
                        if (ctx.Game.Rooms != null)
                        {
                            foreach (var room in ctx.Game.Rooms)
                            {
                                if (room.Actions != null)
                                {
                                    foreach (var act in room.Actions)
                                    {
                                        if (string.Equals(act.Name, actionName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            act.InitallyActive = c.Active;
                                            UnityEngine.Debug.Log($"[ActionExecutor] Updated room action '{act.Name}' on Room '{room.Name}' to active={c.Active}");
                                        }
                                    }
                                }
                            }
                        }
                        // 3. Characters & GameObjects
                        if (ctx.Game.Characters != null)
                        {
                            foreach (var character in ctx.Game.Characters)
                            {
                                if (character.Actions != null)
                                {
                                    foreach (var act in character.Actions)
                                    {
                                        if (string.Equals(act.Name, actionName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            act.InitallyActive = c.Active;
                                            UnityEngine.Debug.Log($"[ActionExecutor] Updated character action '{act.Name}' on Character '{character.Name}' to active={c.Active}");
                                        }
                                    }
                                }
                            }
                        }
                        if (ctx.Game.Objects != null)
                        {
                            foreach (var obj in ctx.Game.Objects)
                            {
                                if (obj.Actions != null)
                                {
                                    foreach (var act in obj.Actions)
                                    {
                                        if (string.Equals(act.Name, actionName, StringComparison.OrdinalIgnoreCase))
                                        {
                                            act.InitallyActive = c.Active;
                                            UnityEngine.Debug.Log($"[ActionExecutor] Updated object action '{act.Name}' on Object '{obj.Name}' to active={c.Active}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;

                case SetTimerActiveCommandData c:
                    {
                        var timerId = ctx.Resolve(c.TimerId);
                        var timer = ctx.Game.Timers.Find(t => string.Equals(t.Name, timerId, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Id, timerId, StringComparison.OrdinalIgnoreCase));
                        if (timer != null)
                        {
                            timer.IsActive = c.Active;
                        }
                    }
                    break;

                case ObjectDisplayDescriptionCommandData c:
                    {
                        var resolved = ctx.Resolve(c.ObjectId);
                        var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolved, StringComparison.OrdinalIgnoreCase));
                        if (obj != null)
                        {
                            ctx.SetVariable("system.lastDisplayedText", ctx.Resolve(obj.Description));
                            if (obj.Actions != null)
                            {
                                var objCtx = new GameExecutionContext(ctx.Game, ctx.CurrentRoom, obj, obj);
                                foreach (var action in obj.Actions)
                                {
                                    if (string.Equals(action.Trigger, "OnObjectExamined", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ActionExecutor.Execute(action, objCtx, RagNextPlayer.Managers.InteractionController.Instance?.GetComponent<RagNextPlayer.Managers.CommandEffectRouter>());
                                    }
                                }
                            }
                        }
                    }
                    break;

                case PlayerDisplayDescriptionCommandData:
                    ctx.SetVariable("system.lastDisplayedText", ctx.Resolve(ctx.Player.Description));
                    break;

                case CharacterDisplayDescriptionCommandData c:
                    {
                        var resolved = ResolveCharacterId(c.CharacterId, ctx);
                        var ch = ctx.Game.Characters.Find(charac => string.Equals(charac.Id, resolved, StringComparison.OrdinalIgnoreCase));
                        if (ch != null)
                        {
                            ctx.SetVariable("system.lastDisplayedText", ctx.Resolve(ch.Description));
                        }
                    }
                    break;

                case RoomDisplayDescriptionCommandData c:
                    {
                        var resolved = ctx.Resolve(c.RoomId);
                        var rId = string.IsNullOrWhiteSpace(resolved) ? ctx.CurrentRoom?.Id : resolved;
                        var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, rId, StringComparison.OrdinalIgnoreCase));
                        if (room != null)
                        {
                            ctx.SetVariable("system.lastDisplayedText", ctx.Resolve(room.Description));
                        }
                    }
                    break;

                case ObjectMoveToCharacterCommandData c:
                    {
                        var resolvedObj = ctx.Resolve(c.ObjectId);
                        var resolvedChar = ResolveCharacterId(c.CharacterId, ctx);
                        RemoveObjectFromEverywhere(resolvedObj, ctx);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        if (character == null)
                        {
                            character = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        }
                        if (character != null && !character.Inventory.Exists(i => string.Equals(i.Id, resolvedObj, StringComparison.OrdinalIgnoreCase)))
                        {
                            var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedObj, StringComparison.OrdinalIgnoreCase));
                            if (obj != null)
                                character.Inventory.Add(obj);
                        }
                    }
                    break;

                case ObjectMoveToInventoryCommandData c:
                    {
                        var resolvedObj = ctx.Resolve(c.ObjectId);
                        RemoveObjectFromEverywhere(resolvedObj, ctx);
                        if (!ctx.Player.Inventory.Exists(i => string.Equals(i.Id, resolvedObj, StringComparison.OrdinalIgnoreCase)))
                        {
                            var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedObj, StringComparison.OrdinalIgnoreCase));
                            if (obj != null)
                            {
                                ctx.Player.Inventory.Add(obj);
                                if (obj.Actions != null)
                                {
                                    var objCtx = new GameExecutionContext(ctx.Game, ctx.CurrentRoom, obj, obj);
                                    foreach (var action in obj.Actions)
                                    {
                                        if (string.Equals(action.Trigger, "OnObjectTaken", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ActionExecutor.Execute(action, objCtx, RagNextPlayer.Managers.InteractionController.Instance?.GetComponent<RagNextPlayer.Managers.CommandEffectRouter>());
                                        }
                                    }
                                }
                            }
                        }
                    }
                    break;

                case ObjectMoveInsideObjectCommandData c:
                    {
                        var resolvedObj = ctx.Resolve(c.ObjectId);
                        var resolvedContainer = ctx.Resolve(c.ContainerObjectId);
                        RemoveObjectFromEverywhere(resolvedObj, ctx);
                        var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedObj, StringComparison.OrdinalIgnoreCase));
                        if (obj != null)
                        {
                            obj.Properties["ParentContainerId"] = resolvedContainer;
                        }
                    }
                    break;

                case SetCharacterAttributeCommandData c:
                    {
                        var resolvedChar = ResolveCharacterId(c.CharacterId, ctx);
                        var resolvedVal = ctx.Resolve(c.Value);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        if (character == null)
                        {
                            character = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedChar, StringComparison.OrdinalIgnoreCase));
                        }
                        if (character is not null)
                        {
                            character.Attributes[c.AttributeName] = resolvedVal;
                            ctx.SetVariable($"char.{resolvedChar}.{c.AttributeName}", resolvedVal);
                        }
                    }
                    break;

                case SetPlayerAttributeCommandData c:
                    {
                        var resolvedVal = ctx.Resolve(c.Value);
                        ctx.Player.Attributes[c.AttributeName] = resolvedVal;
                        ctx.SetVariable($"player.{c.AttributeName}", resolvedVal);
                    }
                    break;

                case SetTimerAttributeCommandData c:
                    {
                        var resolvedTimer = ctx.Resolve(c.TimerId);
                        var resolvedVal = ctx.Resolve(c.Value);
                        var timer = ctx.Game.Timers.Find(t => string.Equals(t.Name, resolvedTimer, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Id, resolvedTimer, StringComparison.OrdinalIgnoreCase));
                        if (timer is not null)
                        {
                            timer.Attributes[c.AttributeName] = resolvedVal;
                            ctx.SetVariable($"timer.{resolvedTimer}.{c.AttributeName}", resolvedVal);
                        }
                    }
                    break;

                case SetItemAttributeCommandData c:
                    {
                        var resolvedItem = ctx.Resolve(c.ItemId);
                        var resolvedVal = ctx.Resolve(c.Value);
                        var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedItem, StringComparison.OrdinalIgnoreCase));
                        if (obj is not null)
                        {
                            obj.Attributes[c.AttributeName] = resolvedVal;
                            ctx.SetVariable($"obj.{resolvedItem}.{c.AttributeName}", resolvedVal);
                        }
                    }
                    break;

                case SetRoomAttributeCommandData c:
                    {
                        var resolvedRoom = ctx.Resolve(c.RoomId);
                        var resolvedVal = ctx.Resolve(c.Value);
                        var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, resolvedRoom, StringComparison.OrdinalIgnoreCase) || string.Equals(r.Name, resolvedRoom, StringComparison.OrdinalIgnoreCase));
                        if (room is not null)
                        {
                            room.Attributes[c.AttributeName] = resolvedVal;
                            ctx.SetVariable($"room.{resolvedRoom}.{c.AttributeName}", resolvedVal);
                        }
                    }
                    break;

                case WearItemCommandData c:
                    {
                        var resolvedItem = ctx.Resolve(c.ItemId);
                        var targetItem = ctx.Game.Player.Inventory.Find(o => string.Equals(o.Id, resolvedItem, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, resolvedItem, StringComparison.OrdinalIgnoreCase)) ?? 
                                         ctx.Game.Objects.Find(o => string.Equals(o.Id, resolvedItem, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, resolvedItem, StringComparison.OrdinalIgnoreCase));
                                         
                        if (targetItem != null && !string.IsNullOrEmpty(targetItem.WearSlot))
                        {
                            var conflict = ctx.Game.Player.Inventory.Find(i => i.IsWorn && string.Equals(i.WearSlot, targetItem.WearSlot, StringComparison.OrdinalIgnoreCase));
                            if (conflict != null && !string.Equals(conflict.Id, targetItem.Id, StringComparison.OrdinalIgnoreCase))
                            {
                                var msg = $"\n(You must take off the {conflict.Name} first.)";
                                ctx.SetVariable("system.lastDisplayedText", msg);
                                RagNextPlayer.Managers.UIManager.Instance?.AppendNarrativeText(msg);
                                break;
                            }
                        }

                        SetItemWornState(resolvedItem, true, ctx);
                    }
                    break;

                case RemoveItemCommandData c:
                    {
                        var resolvedItem = ctx.Resolve(c.ItemId);
                        SetItemWornState(resolvedItem, false, ctx);
                    }
                    break;

                case ShowStatusElementCommandData c:
                    {
                        var resolved = ctx.Resolve(c.ElementId);
                        var element = ctx.Game.StatusBarElements.Find(e => e.Id == resolved || string.Equals(e.Name, resolved, StringComparison.OrdinalIgnoreCase));
                        if (element != null)
                        {
                            element.IsVisible = true;
                        }
                    }
                    break;

                case HideStatusElementCommandData c:
                    {
                        var resolved = ctx.Resolve(c.ElementId);
                        var element = ctx.Game.StatusBarElements.Find(e => e.Id == resolved || string.Equals(e.Name, resolved, StringComparison.OrdinalIgnoreCase));
                        if (element != null)
                        {
                            element.IsVisible = false;
                        }
                    }
                    break;

                case SetStatusElementTextCommandData c:
                    {
                        var resolvedId = ctx.Resolve(c.ElementId);
                        var resolvedText = ctx.Resolve(c.Text);
                        var element = ctx.Game.StatusBarElements.Find(e => e.Id == resolvedId || string.Equals(e.Name, resolvedId, StringComparison.OrdinalIgnoreCase));
                        if (element != null)
                        {
                            element.Text = resolvedText;
                        }
                    }
                    break;

                case SetStatusElementImageCommandData c:
                    {
                        var resolvedId = ctx.Resolve(c.ElementId);
                        var resolvedMedia = ctx.Resolve(c.MediaId);
                        var element = ctx.Game.StatusBarElements.Find(e => e.Id == resolvedId || string.Equals(e.Name, resolvedId, StringComparison.OrdinalIgnoreCase));
                        if (element != null)
                        {
                            element.MediaAssetId = resolvedMedia;
                        }
                    }
                    break;

                case CharacterMoveInventoryToPlayerCommandData c:
                    {
                        var charId = ResolveCharacterId(c.CharacterId, ctx);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, charId, StringComparison.OrdinalIgnoreCase));
                        if (character != null)
                        {
                            var items = new System.Collections.Generic.List<GameObjectData>(character.Inventory);
                            foreach (var item in items)
                            {
                                if (item.IsCollectible)
                                {
                                    character.Inventory.Remove(item);
                                    if (!ctx.Game.Player.Inventory.Contains(item))
                                    {
                                        ctx.Game.Player.Inventory.Add(item);
                                    }
                                }
                            }
                        }
                    }
                    break;

                case CharacterMoveToObjectCommandData c:
                    {
                        var charId = ResolveCharacterId(c.CharacterId, ctx);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, charId, StringComparison.OrdinalIgnoreCase));
                        if (character == null) break;

                        var resolvedObj = ctx.Resolve(c.ObjectId);
                        string currentLocId = resolvedObj;
                        const int maxIterations = 20;
                        int iterations = 0;

                        while (!string.IsNullOrEmpty(currentLocId) && iterations++ < maxIterations)
                        {
                            var room = ctx.Game.Rooms.Find(r => r.ObjectIds.Contains(currentLocId));
                            if (room != null)
                            {
                                MoveCharacterToRoomHelper(charId, room.Id, ctx);
                                return;
                            }

                            var container = ctx.Game.Objects.Find(o => o.ContainedObjectIds != null && o.ContainedObjectIds.Contains(currentLocId));
                            if (container != null)
                            {
                                currentLocId = container.Id;
                                continue;
                            }

                            var otherCharacter = ctx.Game.Characters.Find(ch => ch.Inventory.Exists(i => string.Equals(i.Id, currentLocId, StringComparison.OrdinalIgnoreCase)));
                            if (otherCharacter != null)
                            {
                                var charRoomVar = ctx.GetVariable($"char.{otherCharacter.Id}.currentRoomId")?.Value;
                                var charRoomId = !string.IsNullOrEmpty(charRoomVar) ? charRoomVar : otherCharacter.StartingRoomId;
                                if (!string.IsNullOrEmpty(charRoomId))
                                {
                                    MoveCharacterToRoomHelper(charId, charRoomId, ctx);
                                }
                                return;
                            }

                            if (ctx.Game.Player.Inventory.Exists(i => string.Equals(i.Id, currentLocId, StringComparison.OrdinalIgnoreCase)))
                            {
                                var playerRoomVar = ctx.GetVariable("player.currentRoomId")?.Value;
                                var playerRoomId = !string.IsNullOrEmpty(playerRoomVar) ? playerRoomVar : ctx.Game.Player.StartingRoomId;
                                if (!string.IsNullOrEmpty(playerRoomId))
                                {
                                    MoveCharacterToRoomHelper(charId, playerRoomId, ctx);
                                }
                                return;
                            }

                            break;
                        }
                    }
                    break;

                case CharacterSetDescriptionCommandData c:
                    {
                        var charId = ResolveCharacterId(c.CharacterId, ctx);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, charId, StringComparison.OrdinalIgnoreCase));
                        if (character != null)
                        {
                            character.Description = ctx.Resolve(c.Description);
                        }
                    }
                    break;

                case CharacterSetDisplayNameCommandData c:
                    {
                        var charId = ResolveCharacterId(c.CharacterId, ctx);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, charId, StringComparison.OrdinalIgnoreCase));
                        if (character != null)
                        {
                            character.Name = ctx.Resolve(c.Name);
                        }
                    }
                    break;

                case RoomDisplayPictureCommandData c:
                    break;

                case RoomSetDescriptionCommandData c:
                    {
                        var roomId = ResolveRoomId(c.RoomId, ctx);
                        var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, roomId, StringComparison.OrdinalIgnoreCase));
                        if (room != null)
                        {
                            room.Description = ctx.Resolve(c.Description);
                        }
                    }
                    break;

                case RoomSetPictureCommandData c:
                    {
                        var roomId = ResolveRoomId(c.RoomId, ctx);
                        var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, roomId, StringComparison.OrdinalIgnoreCase));
                        if (room != null)
                        {
                            var resolvedMedia = ctx.Resolve(c.Picture);
                            var asset = ctx.Game.MediaAssets.Find(m => m.Id == resolvedMedia);
                            room.PortraitImagePath = asset?.RelativePath ?? resolvedMedia;
                        }
                    }
                    break;

                case SetStatusBarVisibleCommandData c:
                    {
                        ctx.SetVariable("ui.statusBarVisible", c.Visible.ToString().ToLower());
                    }
                    break;

                case CharacterSetGenderCommandData c:
                    {
                        var charId = ResolveCharacterId(c.CharacterId, ctx);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, charId, StringComparison.OrdinalIgnoreCase));
                        if (character != null)
                        {
                            character.Properties["Gender"] = ctx.Resolve(c.Gender);
                        }
                    }
                    break;

                case PlayerMoveInventoryToCharacterCommandData c:
                    {
                        var charId = ResolveCharacterId(c.CharacterId, ctx);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, charId, StringComparison.OrdinalIgnoreCase));
                        if (character != null)
                        {
                            var items = new System.Collections.Generic.List<GameObjectData>(ctx.Game.Player.Inventory);
                            foreach (var item in items)
                            {
                                ctx.Game.Player.Inventory.Remove(item);
                                if (!character.Inventory.Contains(item))
                                {
                                    character.Inventory.Add(item);
                                }
                            }
                        }
                    }
                    break;

                case PlayerMoveInventoryToRoomCommandData c:
                    {
                        var roomId = ResolveRoomId(c.RoomId, ctx);
                        var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, roomId, StringComparison.OrdinalIgnoreCase));
                        if (room != null)
                        {
                            var items = new System.Collections.Generic.List<GameObjectData>(ctx.Game.Player.Inventory);
                            foreach (var item in items)
                            {
                                ctx.Game.Player.Inventory.Remove(item);
                                if (!room.ObjectIds.Contains(item.Id))
                                {
                                    room.ObjectIds.Add(item.Id);
                                }
                            }
                        }
                    }
                    break;

                case PlayerMoveToCharacterCommandData c:
                    {
                        var charId = ResolveCharacterId(c.CharacterId, ctx);
                        var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, charId, StringComparison.OrdinalIgnoreCase));
                        if (character != null)
                        {
                            var charRoomVar = ctx.GetVariable($"char.{charId}.currentRoomId")?.Value;
                            var charRoomId = !string.IsNullOrEmpty(charRoomVar) ? charRoomVar : character.StartingRoomId;
                            if (!string.IsNullOrEmpty(charRoomId))
                            {
                                ctx.SetVariable("player.currentRoomId", charRoomId);
                            }
                        }
                    }
                    break;

                case PlayerMoveToObjectCommandData c:
                    {
                        var resolvedObj = ctx.Resolve(c.ObjectId);
                        string currentLocId = resolvedObj;
                        const int maxIterations = 20;
                        int iterations = 0;

                        while (!string.IsNullOrEmpty(currentLocId) && iterations++ < maxIterations)
                        {
                            var room = ctx.Game.Rooms.Find(r => r.ObjectIds.Contains(currentLocId));
                            if (room != null)
                            {
                                ctx.SetVariable("player.currentRoomId", room.Id);
                                return;
                            }

                            var container = ctx.Game.Objects.Find(o => o.ContainedObjectIds != null && o.ContainedObjectIds.Contains(currentLocId));
                            if (container != null)
                            {
                                currentLocId = container.Id;
                                continue;
                            }

                            var character = ctx.Game.Characters.Find(ch => ch.Inventory.Exists(i => string.Equals(i.Id, currentLocId, StringComparison.OrdinalIgnoreCase)));
                            if (character != null)
                            {
                                var charRoomVar = ctx.GetVariable($"char.{character.Id}.currentRoomId")?.Value;
                                var charRoomId = !string.IsNullOrEmpty(charRoomVar) ? charRoomVar : character.StartingRoomId;
                                if (!string.IsNullOrEmpty(charRoomId))
                                {
                                    ctx.SetVariable("player.currentRoomId", charRoomId);
                                }
                                return;
                            }

                            if (ctx.Game.Player.Inventory.Exists(i => string.Equals(i.Id, currentLocId, StringComparison.OrdinalIgnoreCase)))
                            {
                                return;
                            }

                            break;
                        }
                    }
                    break;

                case RoomMoveItemsToPlayerCommandData c:
                    {
                        var roomId = ResolveRoomId(c.RoomId, ctx);
                        var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, roomId, StringComparison.OrdinalIgnoreCase));
                        if (room != null)
                        {
                            var itemsInRoom = ctx.Game.Objects.FindAll(o => room.ObjectIds.Contains(o.Id));
                            foreach (var item in itemsInRoom)
                            {
                                if (item.IsCollectible)
                                {
                                    room.ObjectIds.Remove(item.Id);
                                    if (!ctx.Game.Player.Inventory.Exists(i => i.Id == item.Id))
                                    {
                                        ctx.Game.Player.Inventory.Add(item);
                                    }
                                }
                            }
                        }
                    }
                    break;

                case ShowSplashScreenCommandData c:
                    {
                        var resolvedName = ctx.Resolve(c.SplashScreenName);
                        var splash = ctx.Game.SplashScreens?.Find(s => string.Equals(s.Name, resolvedName, StringComparison.OrdinalIgnoreCase));
                        if (splash != null && UIManager.Instance != null)
                        {
                            UIManager.Instance.PlayCustomSplashScreen(splash);
                        }
                    }
                    break;

                case SwapPlayerCharacterCommandData c:
                    {
                        var resolvedId = ctx.Resolve(c.CharacterId);
                        
                        // 1. Find or create character for the current player
                        var currentPlayerChar = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, ctx.Game.ActivePlayerCharacterId, StringComparison.OrdinalIgnoreCase));
                        if (currentPlayerChar == null)
                        {
                            currentPlayerChar = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, ctx.Game.Player.Id, StringComparison.OrdinalIgnoreCase));
                        }
                        if (currentPlayerChar == null)
                        {
                            currentPlayerChar = new GameObjectData
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
                        foreach (var kvp in ctx.Game.Player.Attributes) currentPlayerChar.Attributes.Add(kvp.Key, kvp.Value);

                        currentPlayerChar.Actions.Clear();
                        foreach (var act in ctx.Game.Player.Actions) currentPlayerChar.Actions.Add(act);

                        currentPlayerChar.Inventory.Clear();
                        foreach (var item in ctx.Game.Player.Inventory) currentPlayerChar.Inventory.Add(item);

                        currentPlayerChar.Name = ctx.Game.Player.Name;
                        currentPlayerChar.Description = ctx.Game.Player.Description;
                        currentPlayerChar.Gender = ctx.Game.Player.Gender;
                        currentPlayerChar.PortraitImagePath = ctx.Game.Player.PortraitImagePath;

                        var currentRoomId = ctx.GetVariable("player.currentRoomId")?.Value;
                        if (!string.IsNullOrEmpty(currentRoomId))
                        {
                            currentPlayerChar.StartingRoomId = currentRoomId;
                            ctx.SetVariable($"char.{currentPlayerChar.Id}.currentRoomId", currentRoomId);
                        }

                        // 3. Find the target character to swap in
                        var targetChar = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, resolvedId, StringComparison.OrdinalIgnoreCase));
                        if (targetChar != null)
                        {
                            // 4. Load targetChar data into Player
                            ctx.Game.Player.Id = targetChar.Id;
                            ctx.Game.Player.Name = targetChar.Name;
                            ctx.Game.Player.Description = targetChar.Description;
                            ctx.Game.Player.Gender = targetChar.Gender;
                            ctx.Game.Player.PortraitImagePath = targetChar.PortraitImagePath;

                            ctx.Game.Player.Attributes.Clear();
                            foreach (var kvp in targetChar.Attributes) ctx.Game.Player.Attributes.Add(kvp.Key, kvp.Value);

                            ctx.Game.Player.Actions.Clear();
                            foreach (var act in targetChar.Actions) ctx.Game.Player.Actions.Add(act);

                            ctx.Game.Player.Inventory.Clear();
                            foreach (var item in targetChar.Inventory) ctx.Game.Player.Inventory.Add(item);

                            string targetRoomId = ctx.GetVariable($"char.{targetChar.Id}.currentRoomId")?.Value;
                            if (string.IsNullOrEmpty(targetRoomId))
                            {
                                targetRoomId = targetChar.StartingRoomId;
                            }

                            if (!string.IsNullOrEmpty(targetRoomId))
                            {
                                ctx.SetVariable("player.currentRoomId", targetRoomId);
                                var room = ctx.Game.Rooms.Find(r => string.Equals(r.Id, targetRoomId, StringComparison.OrdinalIgnoreCase));
                                if (room != null)
                                {
                                    if (GameManager.Instance != null)
                                    {
                                        GameManager.Instance.SetCurrentRoom(room);
                                    }
                                    if (UIManager.Instance != null)
                                    {
                                        UIManager.Instance.OnRoomEntered(room);
                                    }
                                }
                            }

                            ctx.Game.ActivePlayerCharacterId = targetChar.Id;
                            
                            if (UIManager.Instance != null)
                            {
                                UIManager.Instance.OnGameLoaded(ctx.Game);
                            }
                        }
                    }
                    break;

                default:
                    Debug.LogWarning($"[ActionExecutor] Unhandled command type: {cmd.Type}");
                    break;
            }
        }

        // ── Condition Dispatch ────────────────────────────────────────────────

        public static bool EvaluateCondition(ConditionData cond, GameExecutionContext ctx)
        {
            var result = EvaluateConditionInternal(cond, ctx);
            Debug.Log($"[ActionExecutor] EvaluateCondition: Type={cond.GetType().Name}, cond.Type={cond.Type} -> {result}");
            return result;
        }

        private static bool EvaluateVariableEquals(VariableEqualsConditionData c, GameExecutionContext ctx)
        {
            var v = (c.Name.StartsWith("{") && c.Name.EndsWith("}")) ? ctx.Resolve(c.Name) : ctx.GetVariable(c.Name)?.Value;
            var resolvedVal = ctx.Resolve(c.Value);
            var isBool = string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) || 
                         string.Equals(v, "false", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(resolvedVal, "true", StringComparison.OrdinalIgnoreCase) || 
                         string.Equals(resolvedVal, "false", StringComparison.OrdinalIgnoreCase);

            return (c.CaseInsensitive || isBool)
                ? string.Equals(v, resolvedVal, StringComparison.OrdinalIgnoreCase)
                : string.Equals(v, resolvedVal, StringComparison.Ordinal);
        }

        private static bool EvaluateConditionInternal(ConditionData cond, GameExecutionContext ctx)
        {
            return cond switch
            {
                VariableEqualsConditionData c =>
                    EvaluateVariableEquals(c, ctx),

                VariableComparisonConditionData c =>
                    CompareValues(
                        (c.Name.StartsWith("{") && c.Name.EndsWith("}")) ? (ctx.Resolve(c.Name) ?? "") : (ctx.GetVariable(c.Name)?.Value ?? ""),
                        ctx.Resolve(c.Value) ?? "",
                        c.Comparison),

                VariableComparisonToVariableConditionData c =>
                    CompareValues(ctx.GetVariable(c.NameA)?.Value ?? "", ctx.GetVariable(c.NameB)?.Value ?? "", c.Comparison),

                PlayerInRoomConditionData c =>
                    string.Equals(ctx.CurrentRoom?.Id, ctx.Resolve(c.RoomId), StringComparison.OrdinalIgnoreCase),

                RoomHasObjectConditionData c =>
                    ctx.Game.Rooms.Find(r => r.Id == ctx.Resolve(c.RoomId))
                        ?.ObjectIds.Contains(ctx.Resolve(c.ObjectId)) ?? false,

                ItemInRoomConditionData c =>
                    ctx.Game.Rooms.Find(r => r.Id == ctx.Resolve(c.RoomId))
                        ?.ObjectIds.Contains(ctx.Resolve(c.ItemId)) ?? false,

                ItemHeldByPlayerConditionData c =>
                    ctx.Player.Inventory.Exists(i => i.Id == ctx.Resolve(c.ItemId)),

                ItemNotHeldByPlayerConditionData c =>
                    !ctx.Player.Inventory.Exists(i => i.Id == ctx.Resolve(c.ItemId)),

                ItemHeldByCharacterConditionData c =>
                    ctx.Game.Characters.Find(ch => ch.Id == ResolveCharacterId(c.CharacterId, ctx))
                        ?.Inventory.Exists(i => i.Id == ctx.Resolve(c.ItemId)) ?? false,

                PlayerInSameRoomAsConditionData c =>
                    string.Equals(
                        ctx.GetVariable($"char.{ResolveCharacterId(c.CharacterId, ctx)}.currentRoomId")?.Value,
                        ctx.CurrentRoom?.Id, StringComparison.OrdinalIgnoreCase),

                CharacterInRoomConditionData c =>
                    string.Equals(
                        ctx.GetVariable($"char.{ResolveCharacterId(c.CharacterId, ctx)}.currentRoomId")?.Value,
                        ResolveRoomId(c.RoomId, ctx), StringComparison.OrdinalIgnoreCase),

                CharacterGenderConditionData c =>
                    string.Equals(
                        ctx.Game.Characters.Find(ch => ch.Id == ResolveCharacterId(c.CharacterId, ctx))
                            ?.Properties.GetValueOrDefault("Gender", "Male"),
                        c.Gender, StringComparison.OrdinalIgnoreCase),

                PlayerGenderConditionData c =>
                    string.Equals(ctx.Player.Gender, c.Gender, StringComparison.OrdinalIgnoreCase),

                IsRoomExitLockedConditionData c =>
                    HasLockedExit(c, ctx),

                CharacterAttributeCheckConditionData c =>
                    EvaluateAttribute(
                        ctx.Game.Characters.Find(ch => string.Equals(ch.Id, ResolveCharacterId(c.CharacterId, ctx), StringComparison.OrdinalIgnoreCase)) ??
                        ctx.Game.Objects.Find(o => string.Equals(o.Id, ResolveCharacterId(c.CharacterId, ctx), StringComparison.OrdinalIgnoreCase)) as object,
                        ctx.Resolve(c.AttributeName),
                        ctx.Resolve(c.ExpectedValue)),

                ItemAttributeCheckConditionData c =>
                    EvaluateAttribute(
                        ctx.Game.Objects.Find(o => string.Equals(o.Id, ctx.Resolve(c.ItemId), StringComparison.OrdinalIgnoreCase)),
                        ctx.Resolve(c.AttributeName),
                        ctx.Resolve(c.ExpectedValue)),

                PlayerAttributeCheckConditionData c =>
                    EvaluateAttribute(
                        ctx.Player,
                        ctx.Resolve(c.AttributeName),
                        ctx.Resolve(c.ExpectedValue)),

                RoomAttributeCheckConditionData c =>
                    EvaluateAttribute(
                        ctx.Game.Rooms.Find(r => string.Equals(r.Id, ctx.Resolve(c.RoomId), StringComparison.OrdinalIgnoreCase)),
                        ctx.Resolve(c.AttributeName),
                        ctx.Resolve(c.ExpectedValue)),

                TimerActiveConditionData c =>
                    ctx.Game.Timers.Find(t => string.Equals(t.Name, ctx.Resolve(c.TimerId), StringComparison.OrdinalIgnoreCase) || string.Equals(t.Id, ctx.Resolve(c.TimerId), StringComparison.OrdinalIgnoreCase))
                        ?.IsActive ?? false,

                ItemWornConditionData c =>
                    FindGameObject(ctx.Resolve(c.ItemId), ctx.Game)?.IsWorn ?? false,

                ItemCanWearConditionData c =>
                    EvaluateItemCanWear(c, ctx),

                StatusElementVisibleConditionData c =>
                    ctx.Game.StatusBarElements.Find(e => e.Id == ctx.Resolve(c.ElementId) || string.Equals(e.Name, ctx.Resolve(c.ElementId), StringComparison.OrdinalIgnoreCase))
                        ?.IsVisible ?? false,

                ForEachLoopCommandData => true,

                DateTimePartComparisonConditionData or
                DateTimeIsPastConditionData or
                DateTimeIsFutureConditionData or
                DateTimeCompareVariablesConditionData or
                DateTimeCompareDifferenceConditionData or
                DateTimeCompareConstantConditionData or
                DateTimeIsValidConditionData =>
                    EvaluateDateTimeCondition(cond, ctx),

                _ => false
            };
        }

        private static bool EvaluateDateTimeCondition(ConditionData cond, GameExecutionContext ctx)
        {
            switch (cond)
            {
                case DateTimePartComparisonConditionData c:
                    if (string.IsNullOrWhiteSpace(c.VariableName)) return false;
                    var rawVal = ctx.GetVariable(c.VariableName)?.Value;
                    if (string.IsNullOrWhiteSpace(rawVal) || !DateTime.TryParse(rawVal, out var dt)) return false;
                    double actualVal = (c.DateTimeComponent ?? "").ToLowerInvariant() switch
                    {
                        "second" => dt.Second,
                        "hour" => dt.Hour,
                        "day" => dt.Day,
                        "month" => dt.Month,
                        "year" => dt.Year,
                        _ => dt.Minute
                    };
                    return c.Comparison switch
                    {
                        "=" => actualVal == c.ExpectedValue,
                        "!=" => actualVal != c.ExpectedValue,
                        ">" => actualVal > c.ExpectedValue,
                        ">=" => actualVal >= c.ExpectedValue,
                        "<" => actualVal < c.ExpectedValue,
                        "<=" => actualVal <= c.ExpectedValue,
                        _ => false
                    };

                case DateTimeIsPastConditionData c:
                    if (string.IsNullOrWhiteSpace(c.VariableName)) return false;
                    var rawValPast = ctx.GetVariable(c.VariableName)?.Value;
                    if (string.IsNullOrWhiteSpace(rawValPast) || !DateTime.TryParse(rawValPast, out var dtPast)) return false;
                    return dtPast < DateTime.Now;

                case DateTimeIsFutureConditionData c:
                    if (string.IsNullOrWhiteSpace(c.VariableName)) return false;
                    var rawValFuture = ctx.GetVariable(c.VariableName)?.Value;
                    if (string.IsNullOrWhiteSpace(rawValFuture) || !DateTime.TryParse(rawValFuture, out var dtFuture)) return false;
                    return dtFuture > DateTime.Now;

                case DateTimeCompareVariablesConditionData c:
                    if (string.IsNullOrWhiteSpace(c.VariableNameA) || string.IsNullOrWhiteSpace(c.VariableNameB)) return false;
                    var valA = ctx.GetVariable(c.VariableNameA)?.Value;
                    var valB = ctx.GetVariable(c.VariableNameB)?.Value;
                    if (string.IsNullOrWhiteSpace(valA) || !DateTime.TryParse(valA, out var dtA)) return false;
                    if (string.IsNullOrWhiteSpace(valB) || !DateTime.TryParse(valB, out var dtB)) return false;
                    return c.Comparison switch
                    {
                        "=" => dtA == dtB,
                        "!=" => dtA != dtB,
                        ">" => dtA > dtB,
                        ">=" => dtA >= dtB,
                        "<" => dtA < dtB,
                        "<=" => dtA <= dtB,
                        _ => false
                    };

                case DateTimeCompareDifferenceConditionData c:
                    if (string.IsNullOrWhiteSpace(c.VariableNameA) || string.IsNullOrWhiteSpace(c.VariableNameB)) return false;
                    var diffA = ctx.GetVariable(c.VariableNameA)?.Value;
                    var diffB = ctx.GetVariable(c.VariableNameB)?.Value;
                    if (string.IsNullOrWhiteSpace(diffA) || !DateTime.TryParse(diffA, out var dtDiffA)) return false;
                    if (string.IsNullOrWhiteSpace(diffB) || !DateTime.TryParse(diffB, out var dtDiffB)) return false;
                    var resolvedDuration = ctx.Resolve(c.Duration ?? "");
                    var tsOpt = DateTimeHelper.ParseDuration(resolvedDuration);
                    if (!tsOpt.HasValue) return false;
                    var targetSpan = tsOpt.Value;
                    var actualSpan = dtDiffA - dtDiffB;
                    return c.Comparison switch
                    {
                        "=" => actualSpan == targetSpan,
                        "!=" => actualSpan != targetSpan,
                        ">" => actualSpan > targetSpan,
                        ">=" => actualSpan >= targetSpan,
                        "<" => actualSpan < targetSpan,
                        "<=" => actualSpan <= targetSpan,
                        _ => false
                    };

                case DateTimeCompareConstantConditionData c:
                    if (string.IsNullOrWhiteSpace(c.VariableName)) return false;
                    var constRaw = ctx.GetVariable(c.VariableName)?.Value;
                    if (string.IsNullOrWhiteSpace(constRaw) || !DateTime.TryParse(constRaw, out var dtConstVar)) return false;
                    var resolvedConst = ctx.Resolve(c.ConstantValue ?? "");
                    if (!DateTime.TryParse(resolvedConst, out var dtConstVal)) return false;
                    return c.Comparison switch
                    {
                        "=" => dtConstVar == dtConstVal,
                        "!=" => dtConstVar != dtConstVal,
                        ">" => dtConstVar > dtConstVal,
                        ">=" => dtConstVar >= dtConstVal,
                        "<" => dtConstVar < dtConstVal,
                        "<=" => dtConstVar <= dtConstVal,
                        _ => false
                    };

                case DateTimeIsValidConditionData c:
                    if (string.IsNullOrWhiteSpace(c.VariableName)) return false;
                    var validRaw = ctx.GetVariable(c.VariableName)?.Value;
                    if (string.IsNullOrWhiteSpace(validRaw)) return false;
                    return DateTime.TryParse(validRaw, out _);
            }
            return false;
        }

        private static bool EvaluateAttribute(object? entity, string attributeName, string expectedValue)
        {
            if (entity is null) return false;
            System.Collections.Generic.Dictionary<string, string>? attributes = null;
            if (entity is GameObjectData go) attributes = go.Attributes;
            else if (entity is PlayerData pl) attributes = pl.Attributes;
            else if (entity is RoomData rm) attributes = rm.Attributes;

            if (attributes is null) return false;
            foreach (var kvp in attributes)
            {
                if (string.Equals(kvp.Key, attributeName, StringComparison.OrdinalIgnoreCase))
                {
                    return string.Equals(kvp.Value, expectedValue, StringComparison.OrdinalIgnoreCase);
                }
            }
            return string.IsNullOrEmpty(expectedValue);
        }

        private static void RemoveObjectFromEverywhere(string oId, GameExecutionContext ctx)
        {
            foreach (var r in ctx.Game.Rooms)
            {
                r.ObjectIds.Remove(oId);
            }
            foreach (var ch in ctx.Game.Characters)
            {
                ch.Inventory.RemoveAll(i => string.Equals(i.Id, oId, StringComparison.OrdinalIgnoreCase));
            }
            ctx.Player.Inventory.RemoveAll(i => string.Equals(i.Id, oId, StringComparison.OrdinalIgnoreCase));
            
            foreach (var o in ctx.Game.Objects)
            {
                if (o.ContainedObjectIds != null)
                {
                    o.ContainedObjectIds.RemoveAll(id => string.Equals(id, oId, StringComparison.OrdinalIgnoreCase));
                }
            }

            var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, oId, StringComparison.OrdinalIgnoreCase));
            if (obj != null)
            {
                obj.Properties.Remove("ParentContainerId");
            }
        }

        private static bool CompareValues(string a, string b, string op)
        {
            bool result;
            if (double.TryParse(a, out double na) && double.TryParse(b, out double nb))
            {
                result = op switch
                {
                    "="  => na == nb,
                    "!=" => na != nb,
                    ">"  => na > nb,
                    ">=" => na >= nb,
                    "<"  => na < nb,
                    "<=" => na <= nb,
                    _    => false
                };
            }
            else
            {
                result = op switch
                {
                    "="  => string.Equals(a, b, StringComparison.OrdinalIgnoreCase),
                    "!=" => !string.Equals(a, b, StringComparison.OrdinalIgnoreCase),
                    _    => false
                };
            }
            Debug.Log($"[ActionExecutor] CompareValues: a='{a}', b='{b}', op='{op}' -> {result}");
            return result;
        }

        private static GameObjectData FindGameObject(string idOrName, GameData game)
        {
            if (string.IsNullOrEmpty(idOrName)) return null;

            var obj = game.Objects.Find(o => string.Equals(o.Id, idOrName, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, idOrName, StringComparison.OrdinalIgnoreCase));
            if (obj != null) return obj;

            obj = game.Player.Inventory.Find(o => string.Equals(o.Id, idOrName, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, idOrName, StringComparison.OrdinalIgnoreCase));
            if (obj != null) return obj;

            foreach (var ch in game.Characters)
            {
                obj = ch.Inventory.Find(o => string.Equals(o.Id, idOrName, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, idOrName, StringComparison.OrdinalIgnoreCase));
                if (obj != null) return obj;
            }

            return null;
        }

        private static void SetItemWornState(string itemId, bool worn, GameExecutionContext ctx)
        {
            // 1. Search main Objects list
            var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, itemId, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, itemId, StringComparison.OrdinalIgnoreCase));
            if (obj != null)
            {
                obj.IsWorn = worn;
                Debug.Log($"[ActionExecutor] SetItemWornState: objects list item '{obj.Name}' worn set to {worn}");
            }

            // 2. Search Player inventory
            var invItem = ctx.Game.Player.Inventory.Find(o => string.Equals(o.Id, itemId, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, itemId, StringComparison.OrdinalIgnoreCase));
            if (invItem != null)
            {
                invItem.IsWorn = worn;
                Debug.Log($"[ActionExecutor] SetItemWornState: player inventory item '{invItem.Name}' worn set to {worn}");
            }

            // 3. Search Character inventories
            foreach (var ch in ctx.Game.Characters)
            {
                var chItem = ch.Inventory.Find(o => string.Equals(o.Id, itemId, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, itemId, StringComparison.OrdinalIgnoreCase));
                if (chItem != null)
                {
                    chItem.IsWorn = worn;
                    Debug.Log($"[ActionExecutor] SetItemWornState: character '{ch.Name}' inventory item '{chItem.Name}' worn set to {worn}");
                }
            }
        }

        private static bool EvaluateItemCanWear(ItemCanWearConditionData c, GameExecutionContext ctx)
        {
            var resolved = ctx.Resolve(c.ItemId);
            var obj = ctx.Game.Objects.Find(o => string.Equals(o.Id, resolved, StringComparison.OrdinalIgnoreCase) || string.Equals(o.Name, resolved, StringComparison.OrdinalIgnoreCase));
            if (obj == null) return false;
            if (!obj.IsWearable) return false;
            if (string.IsNullOrEmpty(obj.WearSlot)) return true;
            
            var conflict = ctx.Game.Player.Inventory.Find(i => i.IsWorn && string.Equals(i.WearSlot, obj.WearSlot, StringComparison.OrdinalIgnoreCase));
            return conflict == null || string.Equals(conflict.Id, obj.Id, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasLockedExit(IsRoomExitLockedConditionData c, GameExecutionContext ctx)
        {
            var rId = string.IsNullOrWhiteSpace(c.RoomId) ? ctx.CurrentRoom?.Id : ResolveRoomId(c.RoomId, ctx);
            var room = ctx.Game.Rooms.Find(r => r.Id == rId);
            if (room != null && room.LockedExits.TryGetValue(ctx.Resolve(c.Direction), out var isLocked))
            {
                return isLocked;
            }
            return false;
        }

        private static string ResolveCharacterId(string input, GameExecutionContext ctx)
        {
            var resolved = ctx.Resolve(input);
            if (Guid.TryParse(resolved, out _)) return resolved;
            if (string.IsNullOrEmpty(resolved)) return resolved;
            var match = ctx.Game.Characters.Find(c => 
                string.Equals(c.Name, resolved, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(c.Name.Replace(" ", ""), resolved, StringComparison.OrdinalIgnoreCase));
            return match?.Id ?? resolved;
        }

        private static string ResolveRoomId(string input, GameExecutionContext ctx)
        {
            var resolved = ctx.Resolve(input);
            if (Guid.TryParse(resolved, out _)) return resolved;
            if (string.IsNullOrEmpty(resolved)) return resolved;
            var match = ctx.Game.Rooms.Find(r => 
                string.Equals(r.Name, resolved, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r.Name.Replace(" ", ""), resolved, StringComparison.OrdinalIgnoreCase));
            return match?.Id ?? resolved;
        }

        private static void MoveCharacterToRoomHelper(string charId, string targetRoomId, GameExecutionContext ctx)
        {
            var character = ctx.Game.Characters.Find(ch => string.Equals(ch.Id, charId, StringComparison.OrdinalIgnoreCase));
            
            var oldRoomId = ctx.GetVariable($"char.{charId}.currentRoomId")?.Value;
            if (string.IsNullOrEmpty(oldRoomId) && character != null)
            {
                oldRoomId = character.StartingRoomId;
            }
            if (string.IsNullOrEmpty(oldRoomId))
            {
                var containingRoom = ctx.Game.Rooms.Find(r => r.ObjectIds.Contains(charId));
                if (containingRoom != null)
                {
                    oldRoomId = containingRoom.Id;
                }
            }
            
            if (string.Equals(oldRoomId, targetRoomId, StringComparison.OrdinalIgnoreCase))
            {
                ctx.SetVariable($"char.{charId}.currentRoomId", targetRoomId);
                return;
            }
            
            ctx.SetVariable($"char.{charId}.currentRoomId", targetRoomId);
            
            var router = RagNextPlayer.Managers.InteractionController.Instance?.GetComponent<RagNextPlayer.Managers.CommandEffectRouter>();

            var playerRoomId = ctx.GetVariable("player.currentRoomId")?.Value;
            if (string.IsNullOrEmpty(playerRoomId))
            {
                playerRoomId = ctx.Game.Player.StartingRoomId ?? (ctx.Game.Rooms.Count > 0 ? ctx.Game.Rooms[0].Id : null);
            }
            Debug.Log($"[MoveCharacterToRoomHelper] charId={charId}, characterName={character?.Name}, oldRoomId={oldRoomId}, targetRoomId={targetRoomId}, playerRoomId={playerRoomId}");

            // 1. Remove from old room ObjectIds and fire exit triggers
            if (!string.IsNullOrEmpty(oldRoomId))
            {
                var oldRoom = ctx.Game.Rooms.Find(r => string.Equals(r.Id, oldRoomId, StringComparison.OrdinalIgnoreCase));
                if (oldRoom != null)
                {
                    oldRoom.ObjectIds.Remove(charId);
                    
                    // Fire OnCharacterExit triggers only if the player is in the old room
                    if (string.Equals(oldRoomId, playerRoomId, StringComparison.OrdinalIgnoreCase))
                    {
                        // Fire OnCharacterExit on old room
                        if (oldRoom.Actions != null && character != null)
                        {
                            var rCtx = new GameExecutionContext(ctx.Game, oldRoom, character, oldRoom);
                            foreach (var action in oldRoom.Actions)
                            {
                                if (string.Equals(action.Trigger, "OnCharacterExit", StringComparison.OrdinalIgnoreCase))
                                {
                                    ActionExecutor.Execute(action, rCtx, router, isUserInteraction: false);
                                }
                            }
                        }

                        // Fire OnCharacterExit on other game objects in the old room
                        if (character != null)
                        {
                            var itemsInRoom = ctx.Game.Objects.FindAll(o => oldRoom.ObjectIds.Contains(o.Id) && o.Id != charId);
                            foreach (var item in itemsInRoom)
                            {
                                if (item.Actions != null)
                                {
                                    var itemCtx = new GameExecutionContext(ctx.Game, oldRoom, item, item);
                                    foreach (var action in item.Actions)
                                    {
                                        if (string.Equals(action.Trigger, "OnCharacterExit", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ActionExecutor.Execute(action, itemCtx, router, isUserInteraction: false);
                                        }
                                    }
                                }
                            }

                            // Fire OnCharacterExit on other characters in the old room
                            var charsInRoom = ctx.Game.Characters.FindAll(ch => oldRoom.ObjectIds.Contains(ch.Id) && ch.Id != charId);
                            foreach (var ch in charsInRoom)
                            {
                                if (ch.Actions != null)
                                {
                                    var chCtx = new GameExecutionContext(ctx.Game, oldRoom, ch, ch);
                                    foreach (var action in ch.Actions)
                                    {
                                        if (string.Equals(action.Trigger, "OnCharacterExit", StringComparison.OrdinalIgnoreCase))
                                        {
                                            ActionExecutor.Execute(action, chCtx, router, isUserInteraction: false);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (string.Equals(oldRoomId, playerRoomId, StringComparison.OrdinalIgnoreCase))
            {
                // Fire OnCharacterExit on character itself
                if (character != null && character.Actions != null)
                {
                    var oldRoom = string.IsNullOrEmpty(oldRoomId) ? null : ctx.Game.Rooms.Find(r => string.Equals(r.Id, oldRoomId, StringComparison.OrdinalIgnoreCase));
                    var cCtx = new GameExecutionContext(ctx.Game, oldRoom, character, character);
                    foreach (var action in character.Actions)
                    {
                        if (string.Equals(action.Trigger, "OnCharacterExit", StringComparison.OrdinalIgnoreCase))
                        {
                            ActionExecutor.Execute(action, cCtx, router, isUserInteraction: false);
                        }
                    }
                }

                // Fire OnCharacterExit on Player (Global)
                if (character != null && ctx.Game.Player.Actions != null)
                {
                    var oldRoom = string.IsNullOrEmpty(oldRoomId) ? null : ctx.Game.Rooms.Find(r => string.Equals(r.Id, oldRoomId, StringComparison.OrdinalIgnoreCase));
                    var pCtx = new GameExecutionContext(ctx.Game, oldRoom, character, ctx.Player);
                    foreach (var action in ctx.Game.Player.Actions)
                    {
                        if (string.Equals(action.Trigger, "OnCharacterExit", StringComparison.OrdinalIgnoreCase))
                        {
                            ActionExecutor.Execute(action, pCtx, router, isUserInteraction: false);
                        }
                    }
                }
            }

            // 2. Add to target room ObjectIds and fire enter triggers
            var targetRoom = ctx.Game.Rooms.Find(r => string.Equals(r.Id, targetRoomId, StringComparison.OrdinalIgnoreCase));
            if (targetRoom != null)
            {
                if (!targetRoom.ObjectIds.Contains(charId))
                {
                    targetRoom.ObjectIds.Add(charId);
                }

                // Fire OnCharacterEnter triggers only if the player is in the target room
                if (string.Equals(targetRoomId, playerRoomId, StringComparison.OrdinalIgnoreCase))
                {
                    // Fire OnCharacterEnter on target room
                    if (targetRoom.Actions != null && character != null)
                    {
                        var rCtx = new GameExecutionContext(ctx.Game, targetRoom, character, targetRoom);
                        foreach (var action in targetRoom.Actions)
                        {
                            if (string.Equals(action.Trigger, "OnCharacterEnter", StringComparison.OrdinalIgnoreCase))
                            {
                                ActionExecutor.Execute(action, rCtx, router, isUserInteraction: false);
                            }
                        }
                    }

                    // Fire OnCharacterEnter on other game objects in the target room
                    if (character != null)
                    {
                        var itemsInRoom = ctx.Game.Objects.FindAll(o => targetRoom.ObjectIds.Contains(o.Id) && o.Id != charId);
                        foreach (var item in itemsInRoom)
                        {
                            if (item.Actions != null)
                            {
                                var itemCtx = new GameExecutionContext(ctx.Game, targetRoom, item, item);
                                foreach (var action in item.Actions)
                                {
                                    if (string.Equals(action.Trigger, "OnCharacterEnter", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ActionExecutor.Execute(action, itemCtx, router, isUserInteraction: false);
                                    }
                                }
                            }
                        }

                        // Fire OnCharacterEnter on other characters in the target room
                        var charsInRoom = ctx.Game.Characters.FindAll(ch => targetRoom.ObjectIds.Contains(ch.Id) && ch.Id != charId);
                        foreach (var ch in charsInRoom)
                        {
                            if (ch.Actions != null)
                            {
                                var chCtx = new GameExecutionContext(ctx.Game, targetRoom, ch, ch);
                                foreach (var action in ch.Actions)
                                {
                                    if (string.Equals(action.Trigger, "OnCharacterEnter", StringComparison.OrdinalIgnoreCase))
                                    {
                                        ActionExecutor.Execute(action, chCtx, router, isUserInteraction: false);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (string.Equals(targetRoomId, playerRoomId, StringComparison.OrdinalIgnoreCase))
            {
                // Fire OnCharacterEnter on character itself
                if (character != null && character.Actions != null)
                {
                    var cCtx = new GameExecutionContext(ctx.Game, targetRoom, character, character);
                    foreach (var action in character.Actions)
                    {
                        if (string.Equals(action.Trigger, "OnCharacterEnter", StringComparison.OrdinalIgnoreCase))
                        {
                            ActionExecutor.Execute(action, cCtx, router, isUserInteraction: false);
                        }
                    }
                }

                // Fire OnCharacterEnter on Player (Global)
                if (character != null && ctx.Game.Player.Actions != null)
                {
                    var pCtx = new GameExecutionContext(ctx.Game, targetRoom, character, ctx.Player);
                    foreach (var action in ctx.Game.Player.Actions)
                    {
                        if (string.Equals(action.Trigger, "OnCharacterEnter", StringComparison.OrdinalIgnoreCase))
                        {
                            ActionExecutor.Execute(action, pCtx, router, isUserInteraction: false);
                        }
                    }
                }
            }
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
}
