using Microsoft.Data.Sqlite;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Events.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="EventDispatcherHost"/>
/// background-service drain loop.
/// </summary>
public sealed class EventDispatcherHostTests : IAsyncLifetime
{
    private static readonly TenantId Tenant = new("host-test");

    private SqliteConnection _connection = null!;
    private SqliteDomainEventStore _store = null!;
    private SqliteEventReader _reader = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _store = new SqliteDomainEventStore(_connection);
        await _store.ApplyMigrationsAsync();
        _reader = new SqliteEventReader(_connection, _store, TimeProvider.System);
    }

    public Task DisposeAsync()
    {
        _connection.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ExecuteAsync_StartsDrainLoop_InvokesHandlerOnAppendedEvents()
    {
        var handler = new CountingHandler();
        await _reader.RegisterHandlerAsync("host-handler", null, handler);

        var host = new EventDispatcherHost(
            _reader, tenantId: Tenant, pollInterval: TimeSpan.FromMilliseconds(50));

        using var cts = new CancellationTokenSource();
        var hostTask = host.StartAsync(cts.Token);
        await hostTask;

        // Append an event after the host is running.
        await _store.AppendAsync(NewEnvelope("Test.X", "host-1"));
        await Task.Delay(300); // wait for >= 1 poll tick

        await host.StopAsync(CancellationToken.None);
        cts.Cancel();

        Assert.True(handler.Count >= 1, $"Expected handler to be invoked at least once; got {handler.Count}.");
    }

    [Fact]
    public async Task ExecuteAsync_OnCancellation_ExitsCleanly()
    {
        var host = new EventDispatcherHost(
            _reader, tenantId: Tenant, pollInterval: TimeSpan.FromSeconds(60));
        await host.StartAsync(CancellationToken.None);
        await host.StopAsync(CancellationToken.None);
        // Reaching this line is the assertion — no hang, no throw.
    }

    [Fact]
    public void Constructor_BatchSizeZero_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EventDispatcherHost(_reader, tenantId: Tenant, batchSize: 0));
    }

    [Fact]
    public void Constructor_NullReader_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EventDispatcherHost(null!, tenantId: Tenant));
    }

    // ----- helpers ---------------------------------------------------

    private sealed record TestPayload(string Foo);

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
            Payload              = new TestPayload("x"),
        };

    private sealed class CountingHandler : IEventHandler<RawDomainEvent>
    {
        public int Count;
        public Task HandleAsync(DomainEventEnvelope<RawDomainEvent> envelope, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref Count);
            return Task.CompletedTask;
        }
    }
}
