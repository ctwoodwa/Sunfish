namespace Sunfish.Blocks.Workflow;

/// <summary>
/// Fluent builder for constructing an immutable <see cref="IWorkflowDefinition{TState,TTrigger,TContext}"/>.
/// Call <see cref="Build"/> to freeze the definition and validate it.
/// </summary>
/// <typeparam name="TState">Enum type representing workflow states.</typeparam>
/// <typeparam name="TTrigger">Enum type representing workflow triggers.</typeparam>
/// <typeparam name="TContext">Context object carried by instances at runtime.</typeparam>
public sealed class WorkflowDefinitionBuilder<TState, TTrigger, TContext>
    where TState : struct, Enum
    where TTrigger : struct, Enum
{
    private TState? _initialState;
    private readonly HashSet<TState> _terminalStates = new();
    private readonly Dictionary<(TState From, TTrigger On), TState> _transitions = new();
    private Func<TState, TState, TTrigger, TContext, CancellationToken, ValueTask>? _onTransition;

    /// <summary>Sets the initial state for new workflow instances.</summary>
    public WorkflowDefinitionBuilder<TState, TTrigger, TContext> StartAt(TState state)
    {
        _initialState = state;
        return this;
    }

    /// <summary>Registers an allowed state transition.</summary>
    /// <exception cref="ArgumentException">
    ///   Thrown immediately if an edge <c>(from, on)</c> is registered more than once.
    /// </exception>
    public WorkflowDefinitionBuilder<TState, TTrigger, TContext> Transition(TState from, TTrigger on, TState to)
    {
        var key = (from, on);
        if (!_transitions.TryAdd(key, to))
            throw new ArgumentException(
                $"Duplicate transition edge: ({from}, {on}). " +
                $"A transition from state '{from}' on trigger '{on}' is already registered to '{_transitions[key]}'.");
        return this;
    }

    /// <summary>Marks one or more states as terminal (no further transitions allowed).</summary>
    public WorkflowDefinitionBuilder<TState, TTrigger, TContext> Terminal(params TState[] states)
    {
        foreach (var s in states)
            _terminalStates.Add(s);
        return this;
    }

    /// <summary>Registers a side-effect hook invoked after each committed transition.</summary>
    public WorkflowDefinitionBuilder<TState, TTrigger, TContext> OnTransition(
        Func<TState, TState, TTrigger, TContext, CancellationToken, ValueTask> handler)
    {
        _onTransition = handler;
        return this;
    }

    /// <summary>
    /// Freezes and validates the definition.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///   Thrown if <see cref="StartAt"/> was never called, or if reachability validation
    ///   discovers states that are neither terminal nor have outgoing transitions.
    /// </exception>
    public IWorkflowDefinition<TState, TTrigger, TContext> Build()
    {
        if (_initialState is null)
            throw new InvalidOperationException(
                "WorkflowDefinitionBuilder.Build() requires at least one call to StartAt().");

        ValidateReachability(_initialState.Value);

        return new FrozenWorkflowDefinition<TState, TTrigger, TContext>(
            _initialState.Value,
            _terminalStates.ToHashSet(),
            _transitions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            _onTransition);
    }

    // ---------------------------------------------------------------------------
    // Reachability validation
    // ---------------------------------------------------------------------------

    private void ValidateReachability(TState initial)
    {
        // BFS from initial state; every visited state must be terminal OR have outgoing edges.
        var visited = new HashSet<TState>();
        var queue = new Queue<TState>();
        queue.Enqueue(initial);
        visited.Add(initial);

        while (queue.Count > 0)
        {
            var state = queue.Dequeue();

            bool isTerminal = _terminalStates.Contains(state);
            var outgoing = _transitions
                .Where(kvp => EqualityComparer<TState>.Default.Equals(kvp.Key.From, state))
                .ToList();

            if (!isTerminal && outgoing.Count == 0)
                throw new InvalidOperationException(
                    $"State '{state}' is reachable from the initial state '{initial}' but is neither " +
                    $"marked as terminal nor has any outgoing transitions. " +
                    $"Either add transitions from '{state}' or mark it Terminal(...).");

            foreach (var kvp in outgoing)
            {
                if (visited.Add(kvp.Value))
                    queue.Enqueue(kvp.Value);
            }
        }
    }
}
