using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Events.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="DomainEventEnvelope{TPayload}"/>
/// canonical shape per
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c> §1.
/// </summary>
public sealed class DomainEventEnvelopeTests
{
    private sealed record SamplePayload(string Foo, int Bar);

    [Fact]
    public void Constructed_AllRequiredFieldsAccessible()
    {
        var envelope = NewSampleEnvelope();

        Assert.NotNull(envelope.EventId);
        Assert.Equal("Sample.Test", envelope.EventType);
        Assert.Equal(1, envelope.SchemaVersion);
        Assert.NotEqual(default, envelope.OccurredAt);
        Assert.Equal(TenantId.System, envelope.TenantId);
        Assert.Equal(ReplicaId.System, envelope.OriginatingReplicaId);
        Assert.Equal("test|key|123", envelope.IdempotencyKey);
        Assert.NotNull(envelope.Payload);
    }

    [Fact]
    public void OptionalFields_DefaultToNull()
    {
        var envelope = NewSampleEnvelope();
        Assert.Null(envelope.CausationId);
        Assert.Null(envelope.CorrelationId);
    }

    [Fact]
    public void OptionalFields_PopulateWhenSet()
    {
        var envelope = NewSampleEnvelope() with
        {
            CausationId   = "01ABCDEFGH0123456789JKMNPQ",
            CorrelationId = "workflow-abc",
        };
        Assert.Equal("01ABCDEFGH0123456789JKMNPQ", envelope.CausationId);
        Assert.Equal("workflow-abc", envelope.CorrelationId);
    }

    [Fact]
    public void Envelope_IsImmutable_RecordEquality()
    {
        var a = NewSampleEnvelope();
        var b = a with { };
        Assert.Equal(a, b);
        Assert.False(ReferenceEquals(a, b));
    }

    [Fact]
    public void Envelope_WithMutatedField_NotEqual()
    {
        var a = NewSampleEnvelope();
        var b = a with { SchemaVersion = 2 };
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Envelope_PayloadIsGeneric_PreservesTypeShape()
    {
        DomainEventEnvelope<SamplePayload> envelope = NewSampleEnvelope();
        SamplePayload payload = envelope.Payload;
        Assert.Equal("hello", payload.Foo);
        Assert.Equal(42, payload.Bar);
    }

    private static DomainEventEnvelope<SamplePayload> NewSampleEnvelope()
        => new()
        {
            EventId              = EventId.New(),
            EventType            = "Sample.Test",
            SchemaVersion        = 1,
            OccurredAt           = DateTimeOffset.UtcNow,
            TenantId             = TenantId.System,
            OriginatingReplicaId = ReplicaId.System,
            IdempotencyKey       = "test|key|123",
            Payload              = new SamplePayload("hello", 42),
        };
}
