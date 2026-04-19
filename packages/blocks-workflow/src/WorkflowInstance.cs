namespace Sunfish.Blocks.Workflow;

/// <summary>
/// A snapshot of a running (or completed) workflow instance.
/// Immutable — each transition produces a new record.
/// </summary>
/// <typeparam name="TState">Enum type representing workflow states.</typeparam>
/// <typeparam name="TTrigger">Enum type representing workflow triggers.</typeparam>
/// <typeparam name="TContext">Context object carried by the instance.</typeparam>
/// <param name="Id">Unique identifier for this instance.</param>
/// <param name="CurrentState">The state the instance is in right now.</param>
/// <param name="Context">The context object last written during a transition.</param>
/// <param name="IsTerminal">
///   <see langword="true"/> when <paramref name="CurrentState"/> is one of the definition's
///   <see cref="IWorkflowDefinition{TState,TTrigger,TContext}.TerminalStates"/>.
/// </param>
/// <param name="StartedAtUtc">UTC instant when <c>StartAsync</c> was called.</param>
/// <param name="LastTransitionAtUtc">UTC instant of the most recent state change (equals <paramref name="StartedAtUtc"/> before any triggers have been fired).</param>
/// <param name="TransitionCount">Number of successful state transitions since the instance was started.</param>
public sealed record WorkflowInstance<TState, TTrigger, TContext>(
    WorkflowInstanceId Id,
    TState CurrentState,
    TContext Context,
    bool IsTerminal,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastTransitionAtUtc,
    int TransitionCount)
    where TState : struct, Enum
    where TTrigger : struct, Enum;
