using RagNextPlayer.Runtime.Models;

namespace RagNextPlayer.Runtime
{
    /// <summary>
    /// Receives notifications after each command or condition is processed by
    /// ActionExecutor. Implemented by CommandEffectRouter (Unity MonoBehaviour)
    /// to route side-effects to UIManager, AudioManager, and GameManager without
    /// coupling the execution engine to any Unity types.
    ///
    /// Direct port of IGameEventSink from RagsNextPlayer.Services.
    /// </summary>
    public interface IGameEventSink
    {
        /// <summary>Called immediately after a command has been executed.</summary>
        void OnCommandExecuted(CommandData cmd, GameExecutionContext ctx);

        /// <summary>Called after a condition has been evaluated.</summary>
        void OnConditionEvaluated(ConditionData cond, bool result, GameExecutionContext ctx);
    }
}
