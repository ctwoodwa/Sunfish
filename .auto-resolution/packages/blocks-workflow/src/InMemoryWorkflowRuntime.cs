using System.Collections.Concurrent;

namespace Sunfish.Blocks.Workflow;

/// <summary>
/// Pure in-memory workflow runtime with no persistence.
/// State is lost if the process crashes or restarts.
/// Suitable for testing, prototyping, and kitchen-sink demos.
/// </summary>
/// <remarks>
/// <para><strong>Concurrency model:</strong> Each instance has a dedicated
/// <see cref="SemaphoreSlim"/> (1,1) that serializes concurrent
/// <c>FireAsync</c> calls on the same instance. Calls on different instances
/// run fully in parallel with no cross-instance locking.</para>
///
/// <para><strong>Hook-error semantics:</strong>
/// If <see cref="IWorkflowDefinition{TState,TTrigger,TContext}.OnTransitionAsync"/> throws,
/// the state change is already committed to the dictionary before the hook is awaited —
/// the exception propagates to the <c>FireAsync</c> caller but the transition is NOT rolled back.
/// Callers must treat hook exceptions as advisory.</para>
/// </remarks>
public sealed class InMemoryWorkflowRuntime : IWorkflowRuntime
{
    // Stores type-erased wrappers keyed by instance id.
    private readonly ConcurrentDictionary<WorkflowInstanceId, InstanceEntry> _entries = new();

    // ---------------------------------------------------------------------------
    // IWorkflowRuntime
    // ---------------------------------------------------------------------------

    /// <inheritdoc/>
    public ValueTask<WorkflowInstance<TState, TTrigger, TContext>> StartAsync<TState, TTrigger, TContext>(
        IWorkflowDefinition<TState, TTrigger, TContext> definition,
        TContext initialContext,
        CancellationToken ct = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        var id = WorkflowInstanceId.NewId();
        var now = DateTimeOffset.UtcNow;
        var instance = new WorkflowInstance<TState, TTrigger, TContext>(
            Id: id,
            CurrentState: definition.InitialState,
            Context: initialContext,
            IsTerminal: definition.TerminalStates.Contains(definition.InitialState),
            StartedAtUtc: now,
            LastTransitionAtUtc: now,
            TransitionCount: 0);

        var entry = new TypedInstanceEntry<TState, TTrigger, TContext>(definition, instance);
        _entries[id] = entry;

        return ValueTask.FromResult(instance);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowInstance<TState, TTrigger, TContext>> FireAsync<TState, TTrigger, TContext>(
        WorkflowInstanceId id,
        TTrigger trigger,
        CancellationToken ct = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        if (!_entries.TryGetValue(id, out var rawEntry))
            throw new KeyNotFoundException($"No workflow instance with id '{id}' exists.");

        if (rawEntry is not TypedInstanceEntry<TState, TTrigger, TContext> entry)
            throw new InvalidOperationException(
                $"Workflow instance '{id}' exists but its type parameters do not match the requested " +
                $"<{typeof(TState).Name},{typeof(TTrigger).Name},{typeof(TContext).Name}>.");

        await entry.Lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var current = entry.Instance;

            if (current.IsTerminal)
                throw new InvalidOperationException(
                    $"Workflow instance '{id}' is in terminal state '{current.CurrentState}' and cannot accept further triggers.");

            var next = entry.Definition.Next(current.CurrentState, trigger);
            if (next is null)
                throw new InvalidOperationException(
                    $"Trigger '{trigger}' is not permitted from state '{current.CurrentState}' " +
                    $"in workflow instance '{id}'.");

            var updated = current with
            {
                CurrentState = next.Value,
                IsTerminal = entry.Definition.TerminalStates.Contains(next.Value),
                LastTransitionAtUtc = DateTimeOffset.UtcNow,
                TransitionCount = current.TransitionCount + 1
            };

            // Commit the transition before firing the hook (hook-error semantics: transition is final).
            entry.Instance = updated;

            // Invoke the hook outside the dictionary write but inside the per-instance lock.
            await entry.Definition.OnTransitionAsync(
                current.CurrentState, next.Value, trigger, current.Context, ct).ConfigureAwait(false);

            return updated;
        }
        finally
        {
            entry.Lock.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowInstance<TState, TTrigger, TContext>?> GetAsync<TState, TTrigger, TContext>(
        WorkflowInstanceId id,
        CancellationToken ct = default)
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        if (!_entries.TryGetValue(id, out var rawEntry))
            return ValueTask.FromResult<WorkflowInstance<TState, TTrigger, TContext>?>(null);

        if (rawEntry is not TypedInstanceEntry<TState, TTrigger, TContext> entry)
            throw new InvalidOperationException(
                $"Workflow instance '{id}' exists but its type parameters do not match the requested " +
                $"<{typeof(TState).Name},{typeof(TTrigger).Name},{typeof(TContext).Name}>.");

        return ValueTask.FromResult<WorkflowInstance<TState, TTrigger, TContext>?>(entry.Instance);
    }

    // ---------------------------------------------------------------------------
    // Internal storage types
    // ---------------------------------------------------------------------------

    private abstract class InstanceEntry { }

    private sealed class TypedInstanceEntry<TState, TTrigger, TContext> : InstanceEntry
        where TState : struct, Enum
        where TTrigger : struct, Enum
    {
        internal readonly IWorkflowDefinition<TState, TTrigger, TContext> Definition;
        internal volatile WorkflowInstance<TState, TTrigger, TContext> Instance;

        /// <summary>Per-instance semaphore that serializes concurrent FireAsync calls.</summary>
        internal readonly SemaphoreSlim Lock = new(1, 1);

        internal TypedInstanceEntry(
            IWorkflowDefinition<TState, TTrigger, TContext> definition,
            WorkflowInstance<TState, TTrigger, TContext> instance)
        {
            Definition = definition;
            Instance = instance;
        }
    }
}
