namespace Sunfish.Foundation.Events;

/// <summary>
/// Canonical cross-cluster event publisher per
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c>.
/// Replaces the per-cluster local <c>IDomainEventPublisher</c> stubs
/// that ship today in <c>blocks-financial-periods</c>,
/// <c>blocks-financial-tax</c>, etc. — those packages migrate to
/// this canonical home via a DI-swap sweep PR when
/// <c>foundation-events</c> ships in full (PR 6).
/// </summary>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Publish a domain event wrapped in the canonical envelope.
    /// Implementations decide whether to deliver synchronously,
    /// enqueue, persist to an outbox, or no-op (see
    /// <see cref="NoopDomainEventPublisher"/>).
    /// </summary>
    /// <typeparam name="TPayload">The cluster-specific payload type.</typeparam>
    /// <param name="envelope">The canonical envelope wrapping the payload.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task PublishAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op publisher. Consumes the envelope and discards. Useful as a
/// default DI registration during composition before the real
/// publisher is wired, and for unit tests that don't care about
/// downstream delivery.
/// </summary>
public sealed class NoopDomainEventPublisher : IDomainEventPublisher
{
    /// <inheritdoc />
    public Task PublishAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
