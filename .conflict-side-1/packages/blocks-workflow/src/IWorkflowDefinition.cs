namespace Sunfish.Blocks.Workflow;

/// <summary>
/// Declarative, immutable state-machine definition.
/// Frozen at build time via <see cref="WorkflowDefinitionBuilder{TState,TTrigger,TContext}"/>;
/// safe to share across threads without synchronization.
/// </summary>
/// <typeparam name="TState">Enum type representing workflow states.</typeparam>
/// <typeparam name="TTrigger">Enum type representing workflow triggers (events/commands).</typeparam>
/// <typeparam name="TContext">Mutable context object carried by each running instance.</typeparam>
public interface IWorkflowDefinition<TState, TTrigger, TContext>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    /// <summary>The state assigned to new instances at creation.</summary>
    TState InitialState { get; }

    /// <summary>
    /// States that represent a completed workflow.
    /// Instances in a terminal state reject further triggers.
    /// </summary>
    IReadOnlyCollection<TState> TerminalStates { get; }

    /// <summary>
    /// Returns the target state when <paramref name="trigger"/> is fired from <paramref name="from"/>,
    /// or <see langword="null"/> if the trigger is not permitted from that state.
    /// </summary>
    TState? Next(TState from, TTrigger trigger);

    /// <summary>
    /// Optional side-effect hook invoked after a transition is committed.
    /// Exceptions propagate to the caller of
    /// <see cref="IWorkflowRuntime.FireAsync{TState,TTrigger,TContext}"/>; the
    /// transition is considered committed regardless (see documentation for hook-error semantics).
    /// </summary>
    ValueTask OnTransitionAsync(
        TState from,
        TState to,
        TTrigger trigger,
        TContext context,
        CancellationToken ct);
}
