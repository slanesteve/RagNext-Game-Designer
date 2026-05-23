using RagsCore.Actions;
using GameCondition = RagsCore.Actions.Condition;

namespace RagsNextPlayer.Services
{
    /// <summary>
    /// Receives notifications after each command or condition is processed by the
    /// ActionExecutor. Implemented by the UI layer (MainPage) to route side-effects
    /// (display text, room changes, sound, etc.) to the correct UI elements without
    /// coupling RagsCore to any MAUI types.
    /// </summary>
    public interface IGameEventSink
    {
        /// <summary>Called immediately after a GameCommand has been executed.</summary>
        void OnCommandExecuted(GameCommand cmd, ActionContext ctx);

        /// <summary>Called after a Condition has been evaluated with its result.</summary>
        void OnConditionEvaluated(RagsCore.Actions.Condition cond, bool result, ActionContext ctx);
    }
}
