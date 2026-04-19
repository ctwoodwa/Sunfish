namespace Sunfish.Blocks.Workflow;

/// <summary>
/// Manages the lifecycle of workflow instances: starting, advancing state via triggers,
/// and querying current state.
/// </summary>
public interface IWorkflowRuntime
{
    /// <summary>
    /// Creates a new workflow instance in the initial state and stores it in the runtime.
    /// </summary>
    /// <typeparam name="TState">Enum type representing workflow states.</typeparam>
    /// <typeparam name="TTrigger">Enum type representing workflow triggers.</typeparam>
    /// <typeparam name="TContext">Context object type.</typeparam>
    /// <param name="definition">The frozen workflow definition.</param>
    /// <param name="initialContext">Context object for the new instance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The newly created <see cref="WorkflowInstance{TState,TTrigger,TContext}"/>.</returns>
    ValueTask<WorkflowInstance<TState, TTrigger, TContext>> StartAsync<TState, TTrigger, TContext>(
        IWorkflowDefinition<TState, TTrigger, TContext> definition,
        TContext initialContext,
        CancellationToken ct = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum;

    /// <summary>
    /// Fires <paramref name="trigger"/> against the instance identified by <paramref name="id"/>,
    /// advancing its state if the trigger is valid.
    /// </summary>
    /// <typeparam name="TState">Enum type representing workflow states.</typeparam>
    /// <typeparam name="TTrigger">Enum type representing workflow triggers.</typeparam>
    /// <typeparam name="TContext">Context object type.</typeparam>
    /// <param name="id">The instance to advance.</param>
    /// <param name="trigger">The trigger to fire.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The updated <see cref="WorkflowInstance{TState,TTrigger,TContext}"/>.</returns>
    /// <exception cref="KeyNotFoundException">
    ///   Thrown when no instance with the given <paramref name="id"/> exists.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    ///   Thrown when the instance is in a terminal state, or when the trigger is not
    ///   permitted from the current state.
    /// </exception>
    ValueTask<WorkflowInstance<TState, TTrigger, TContext>> FireAsync<TState, TTrigger, TContext>(
        WorkflowInstanceId id,
        TTrigger trigger,
        CancellationToken ct = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum;

    /// <summary>
    /// Retrieves the current snapshot of a workflow instance, or <see langword="null"/>
    /// if no instance with that id exists.
    /// </summary>
    /// <typeparam name="TState">Enum type representing workflow states.</typeparam>
    /// <typeparam name="TTrigger">Enum type representing workflow triggers.</typeparam>
    /// <typeparam name="TContext">Context object type.</typeparam>
    /// <param name="id">The instance to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///   The instance snapshot, or <see langword="null"/> if unknown.
    /// </returns>
    ValueTask<WorkflowInstance<TState, TTrigger, TContext>?> GetAsync<TState, TTrigger, TContext>(
        WorkflowInstanceId id,
        CancellationToken ct = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum;
}
