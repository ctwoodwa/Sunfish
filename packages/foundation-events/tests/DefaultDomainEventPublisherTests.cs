using Microsoft.Data.Sqlite;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Events.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="DefaultDomainEventPublisher"/>
/// per foundation-events hand-off PR 3 test plan.
/// </summary>
public sealed class DefaultDomainEventPublisherTests : IAsyncLifetime
{
    private static readonly TenantId Tenant = new("publisher-test");

    private SqliteConnection _connection = null!;
    private SqliteDomainEventStore _store = null!;
    private InProcessEventDispatcher _dispatcher = null!;
    private DefaultDomainEventPublisher _sut = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _store = new SqliteDomainEventStore(_connection);
        await _store.ApplyMigrationsAsync();
        _dispatcher = new InProcessEventDispatcher();
        _sut = new DefaultDomainEventPublisher(_store, _dispatcher);
    }

    public Task DisposeAsync()
    {
        _connection.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task PublishAsync_HappyPath_PersistsToStoreAndDispatches()
    {
        var received = new List<RawDomainEvent>();
        _dispatcher.Subscribe((evt, _) =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });

        var envelope = NewEnvelope("Financial.PeriodSoftClosed", "happy-1");

        await _sut.PublishAsync(envelope);

        // Store has the row.
        var stored = await _store.FindByIdempotencyKeyAsync(Tenant, "happy-1");
        Assert.NotNull(stored);
        // Dispatcher saw the event.
        Assert.Single(received);
        Assert.Equal(envelope.EventId, received[0].EventId);
    }

    [Fact]
    public async Task PublishAsync_OnDedup_DoesNotRedispatch()
    {
        var received = new List<RawDomainEvent>();
        _dispatcher.Subscribe((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        var first = NewEnvelope("Financial.PeriodSoftClosed", "dedup-1");
        var second = NewEnvelope("Financial.PeriodSoftClosed", "dedup-1");

        await _sut.PublishAsync(first);
        await _sut.PublishAsync(second);

        Assert.Single(received);
        Assert.Equal(first.EventId, received[0].EventId);
    }

    [Fact]
    public async Task PublishAsync_OnNullEnvelope_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _sut.PublishAsync<TestPayload>(null!));
    }

    [Fact]
    public async Task PublishAsync_PreservesAllEnvelopeFields_InRawDispatch()
    {
        var received = new List<RawDomainEvent>();
        _dispatcher.Subscribe((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        var envelope = NewEnvelope("Work.WorkOrderCreated", "fields-1") with
        {
            CausationId   = "01ABCDEFGH0123456789JKMNPQ",
            CorrelationId = "wf-1",
        };

        await _sut.PublishAsync(envelope);

        var raw = received.Single();
        Assert.Equal(envelope.EventId, raw.EventId);
        Assert.Equal(envelope.EventType, raw.EventType);
        Assert.Equal(envelope.SchemaVersion, raw.SchemaVersion);
        Assert.Equal(envelope.TenantId, raw.TenantId);
        Assert.Equal(envelope.OriginatingReplicaId, raw.OriginatingReplicaId);
        Assert.Equal(envelope.IdempotencyKey, raw.IdempotencyKey);
        Assert.Equal("01ABCDEFGH0123456789JKMNPQ", raw.CausationId);
        Assert.Equal("wf-1", raw.CorrelationId);
        Assert.Equal("work", raw.ProducerCluster);
        Assert.NotNull(raw.PayloadJson);
    }

    [Fact]
    public async Task PublishAsync_OnStoreFailure_BubblesException_AndSkipsDispatch()
    {
        var received = new List<RawDomainEvent>();
        var dispatcher = new InProcessEventDispatcher();
        dispatcher.Subscribe((evt, _) => { received.Add(evt); return Task.CompletedTask; });

        var failingStore = new ThrowingStore();
        var sut = new DefaultDomainEventPublisher(failingStore, dispatcher);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.PublishAsync(NewEnvelope("Test.X", "fail-1")));

        Assert.Empty(received);
    }

    // ----- helpers ---------------------------------------------------

    private sealed record TestPayload(string Foo, int Bar);

    private static DomainEventEnvelope<TestPayload> NewEnvelope(string eventType, string idempotencyKey)
        => new()
        {
            EventId              = EventId.New(),
            EventType            = eventType,
            SchemaVersion        = 1,
            OccurredAt           = DateTimeOffset.UtcNow,
            TenantId             = Tenant,
            OriginatingReplicaId = ReplicaId.System,
            IdempotencyKey       = idempotencyKey,
            Payload              = new TestPayload("x", 1),
        };

    private sealed class ThrowingStore : IDomainEventStore
    {
        public Task<string> AppendAsync<TPayload>(DomainEventEnvelope<TPayload> envelope, CancellationToken ct = default)
            => throw new InvalidOperationException("Simulated store failure.");
        public Task<IReadOnlyList<RawDomainEvent>> GetAfterCursorAsync(TenantId t, string? c, int n, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RawDomainEvent>>(Array.Empty<RawDomainEvent>());
        public Task<RawDomainEvent?> FindByIdempotencyKeyAsync(TenantId t, string k, CancellationToken ct = default)
            => Task.FromResult<RawDomainEvent?>(null);
    }
}
