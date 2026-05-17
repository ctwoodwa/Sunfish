using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Events.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="NoopDomainEventPublisher"/>.
/// </summary>
public sealed class NoopDomainEventPublisherTests
{
    private sealed record SamplePayload(string X);

    [Fact]
    public async Task PublishAsync_AcceptsEnvelope_DoesNotThrow()
    {
        var publisher = new NoopDomainEventPublisher();
        var envelope = new DomainEventEnvelope<SamplePayload>
        {
            EventId              = EventId.New(),
            EventType            = "Sample.Test",
            SchemaVersion        = 1,
            OccurredAt           = DateTimeOffset.UtcNow,
            TenantId             = TenantId.System,
            OriginatingReplicaId = ReplicaId.System,
            IdempotencyKey       = "key",
            Payload              = new SamplePayload("x"),
        };

        await publisher.PublishAsync(envelope);
        // Reaching here is the assertion — Noop must not throw.
    }

    [Fact]
    public async Task PublishAsync_NullCancellationToken_AcceptsAndCompletes()
    {
        var publisher = new NoopDomainEventPublisher();
        var envelope = new DomainEventEnvelope<SamplePayload>
        {
            EventId              = EventId.New(),
            EventType            = "Sample.Test",
            SchemaVersion        = 1,
            OccurredAt           = DateTimeOffset.UtcNow,
            TenantId             = TenantId.System,
            OriginatingReplicaId = ReplicaId.System,
            IdempotencyKey       = "key",
            Payload              = new SamplePayload("x"),
        };

        await publisher.PublishAsync(envelope, CancellationToken.None);
    }
}
