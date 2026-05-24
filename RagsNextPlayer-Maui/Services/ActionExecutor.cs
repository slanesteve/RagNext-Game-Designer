using System;
using RagsCore.Actions;
using RagsCore.Models;

namespace RagsNextPlayer.Services
{
    /// <summary>
    /// Executes a game action (list of ActionSteps) recursively, propagating
    /// results back to the caller through an optional IGameEventSink.
    /// 
    /// Design principles:
    ///   - RagsCore commands mutate state only (no UI calls).
    ///   - The sink (implemented by MainPage) receives every command/condition
    ///     result and routes it to the appropriate UI update.
    ///   - Conditions recurse fully: TrueBranch and FalseBranch can each contain
    ///     further commands or nested conditions with no depth limit.
    ///   - Exceptions are caught per-node so a single bad command never aborts
    ///     the rest of the action sequence.
    /// </summary>
    public static class ActionExecutor
    {
        /// <summary>
        /// Execute all nodes of an action, optionally notifying a UI sink after each step.
        /// </summary>
        public static void Execute(RagsCore.Models.Action action, ActionContext ctx, IGameEventSink? sink = null)
        {
            if (action is null || ctx is null) return;
            foreach (var node in action.Nodes)
                ExecuteNode(node, ctx, sink);
        }

        /// <summary>
        /// Recursively execute a single ActionStep (command or condition).
        /// </summary>
        private static void ExecuteNode(ActionStep node, ActionContext ctx, IGameEventSink? sink)
        {
            if (node is null) return;

            if (node is GameCommand cmd)
            {
                try
                {
                    cmd.Execute(ctx);
                    sink?.OnCommandExecuted(cmd, ctx);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ActionExecutor] Error executing {node.TypeName}: {ex.Message}");
                }
            }
            else if (node is RagsCore.Actions.Condition cond)
            {
                try
                {
                    bool result = cond.Evaluate(ctx);
                    sink?.OnConditionEvaluated(cond, result, ctx);

                    var branch = result ? cond.TrueBranch : cond.FalseBranch;
                    foreach (var step in branch)
                        ExecuteNode(step, ctx, sink);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[ActionExecutor] Error evaluating {node.TypeName}: {ex.Message}");
                }
            }
        }
    }
}
