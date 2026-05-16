namespace Sunfish.Blocks.FinancialPeriods.Services;

/// <summary>
/// Cross-cluster event-publication seam. Minimal local interface per the
/// hand-off "ship a local interface if the foundation/kernel-events home
/// isn't ratified yet" fallback. When the canonical home lands, this
/// interface relocates and consumers of
/// <see cref="DependencyInjection.ServiceCollectionExtensions"/>
/// re-wire to the upstream package.
/// </summary>
public interface IDomainEventPublisher
{
    /// <summary>
    /// Publish a domain event payload. Implementations decide whether to
    /// deliver synchronously, enqueue, or no-op.
    /// </summary>
    Task PublishAsync<TPayload>(TPayload payload, CancellationToken cancellationToken = default);
}

/// <summary>
/// No-op publisher used as the default registration until the canonical
/// event-bus home is wired. Consumers can substitute via DI.
/// </summary>
public sealed class NoopDomainEventPublisher : IDomainEventPublisher
{
    /// <inheritdoc />
    public Task PublishAsync<TPayload>(TPayload payload, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
