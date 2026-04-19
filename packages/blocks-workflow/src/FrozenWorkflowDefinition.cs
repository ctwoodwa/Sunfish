namespace Sunfish.Blocks.Workflow;

/// <summary>
/// Immutable, thread-safe implementation of <see cref="IWorkflowDefinition{TState,TTrigger,TContext}"/>
/// produced by <see cref="WorkflowDefinitionBuilder{TState,TTrigger,TContext}.Build"/>.
/// </summary>
internal sealed class FrozenWorkflowDefinition<TState, TTrigger, TContext>
    : IWorkflowDefinition<TState, TTrigger, TContext>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private readonly IReadOnlyDictionary<(TState From, TTrigger On), TState> _transitions;
    private readonly Func<TState, TState, TTrigger, TContext, CancellationToken, ValueTask>? _onTransition;

    internal FrozenWorkflowDefinition(
        TState initialState,
        HashSet<TState> terminalStates,
        Dictionary<(TState From, TTrigger On), TState> transitions,
        Func<TState, TState, TTrigger, TContext, CancellationToken, ValueTask>? onTransition)
    {
        InitialState = initialState;
        TerminalStates = terminalStates;
        _transitions = transitions;
        _onTransition = onTransition;
    }

    /// <inheritdoc/>
    public TState InitialState { get; }

    /// <inheritdoc/>
    public IReadOnlyCollection<TState> TerminalStates { get; }

    /// <inheritdoc/>
    public TState? Next(TState from, TTrigger trigger)
        => _transitions.TryGetValue((from, trigger), out var to) ? to : null;

    /// <inheritdoc/>
    public ValueTask OnTransitionAsync(
        TState from,
        TState to,
        TTrigger trigger,
        TContext context,
        CancellationToken ct)
        => _onTransition is null
            ? ValueTask.CompletedTask
            : _onTransition(from, to, trigger, context, ct);
}
