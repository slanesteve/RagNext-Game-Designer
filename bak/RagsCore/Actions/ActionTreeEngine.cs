using System.Linq;

namespace RagsCore.Actions
{
    public static class ActionTreeEngine
    {
        // Executes DefaultCommands, then all matching branches depth-first.
        public static bool Execute(ActionTree tree, ActionContext ctx)
        {
            if (tree is null || ctx is null) return false;
            var ran = false;

            foreach (var cmd in tree.DefaultCommands)
            {
                cmd.Execute(ctx);
                ran = true;
            }

            foreach (var branch in tree.Branches)
                if (ExecuteBranch(branch, ctx)) ran = true;

            return ran;
        }

        private static bool ExecuteBranch(ActionBranch branch, ActionContext ctx)
        {
            if (branch.Condition is not null && !branch.Condition.Evaluate(ctx))
                return false;

            foreach (var cmd in branch.Commands)
                cmd.Execute(ctx);

            var any = true; // branch ran because commands executed
            foreach (var child in branch.Children)
                ExecuteBranch(child, ctx);

            return any;
        }
    }
}