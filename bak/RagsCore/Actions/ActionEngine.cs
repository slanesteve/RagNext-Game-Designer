using System.Collections.Generic;
using System.Linq;

namespace RagsCore.Actions
{
    public static class ActionEngine
    {
        // Returns true if executed; false if skipped due to failing conditions.
        public static bool Execute(GameAction action, ActionContext ctx)
        {
            if (action is null || ctx is null) return false;
            if (!action.Conditions.All(c => c.Evaluate(ctx))) return false;

            foreach (var cmd in action.Commands)
                cmd.Execute(ctx);

            return true;
        }

        // Executes all actions; returns count of actions that ran.
        public static int ExecuteAll(IEnumerable<GameAction> actions, ActionContext ctx)
        {
            var count = 0;
            foreach (var a in actions)
                if (Execute(a, ctx)) count++;
            return count;
        }
    }
}