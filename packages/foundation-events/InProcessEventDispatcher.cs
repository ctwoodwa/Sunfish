using System.Collections.Concurrent;

namespace Sunfish.Foundation.Events;

/// <summary>
/// In-process implementation of <see cref="IEventDispatcher"/>.
/// Subscribers accumulate in a <see cref="ConcurrentBag{T}"/> and are
/// fanned out concurrently on each
/// <see cref="DispatchAsync"/>. Failures in one subscriber are
/// isolated — they do not propagate to the publisher and do not
/// abort sibling subscribers.
/// </summary>
public sealed class InProcessEventDispatcher : IEventDispatcher
{
    private readonly ConcurrentBag<Func<RawDomainEvent, CancellationToken, Task>> _subscribers = new();

    /// <inheritdoc />
    public void Subscribe(Func<RawDomainEvent, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _subscribers.Add(handler);
    }

    /// <inheritdoc />
    public async Task DispatchAsync(RawDomainEvent evt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evt);

        // Fan out concurrently; isolate failures.
        var tasks = _subscribers.Select(sub => SafeInvokeAsync(sub, evt, cancellationToken)).ToArray();
        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task SafeInvokeAsync(
        Func<RawDomainEvent, CancellationToken, Task> handler,
        RawDomainEvent evt,
        CancellationToken cancellationToken)
    {
        try
        {
            await handler(evt, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cooperative cancellation — propagate up so callers see
            // the cancellation. Sibling tasks may still complete.
            throw;
        }
        catch (Exception ex)
        {
            // Best-effort dispatch — failures do NOT propagate to the
            // publisher (durable delivery is the IEventReader cursor
            // walk in PR 4). Log here when an ILogger is wired in via
            // the DI extension in PR 6.
            System.Diagnostics.Debug.WriteLine(
                $"Foundation-events in-process dispatch failure: {ex}");
        }
    }
}
