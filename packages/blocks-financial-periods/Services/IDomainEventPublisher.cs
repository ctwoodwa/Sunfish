namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Cross-cluster event-publication seam carrying the canonical envelope
/// per <c>_shared/engineering/cross-cluster-event-bus-design.md</c> §1.
/// Local copy in this cluster per
/// <c>xo-ruling-2026-05-16T21-12Z-cob-event-publisher-home.md</c>; when
/// <c>foundation-events</c> ships the canonical
/// <c>Sunfish.Foundation.Events.IDomainEventPublisher</c>, the
/// per-cluster migration sweep re-namespaces consumers to the upstream
/// substrate and deletes this declaration. DI registration shape
/// (<c>TryAddSingleton</c>) stays compatible so the swap is one-line.
/// </summary>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Publish a domain event wrapped in the canonical envelope.
    /// Implementations decide whether to deliver synchronously,
    /// enqueue, persist to an outbox, or no-op.
    /// </summary>
    Task PublishAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op publisher used as the default registration until the canonical
/// foundation-events substrate impl is wired. Consumes the envelope and
/// discards.
/// </summary>
public sealed class NoopDomainEventPublisher : IDomainEventPublisher
{
    /// <inheritdoc />
    public Task PublishAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
