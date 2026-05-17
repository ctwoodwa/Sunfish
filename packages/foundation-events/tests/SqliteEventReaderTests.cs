using Microsoft.Data.Sqlite;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Events.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="SqliteEventReader"/> per the
/// foundation-events hand-off PR 4 test plan + cross-cluster-event-
/// bus-design.md §5 cursor model + §6 retry backoff.
/// </summary>
public sealed class SqliteEventReaderTests : IAsyncLifetime
{
    private static readonly TenantId Tenant = new("reader-test");
    private static readonly TenantId OtherTenant = new("reader-test-other");
    private static readonly ReplicaId Replica = ReplicaId.System;

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
    public async Task RegisterHandlerAsync_HappyPath_RegistersHandler()
    {
        await _reader.RegisterHandlerAsync("handler-1", null, new CountingHandler());
        // No assertion needed — must not throw.
    }

    [Fact]
    public async Task RegisterHandlerAsync_DuplicateHandlerId_Throws()
    {
        await _reader.RegisterHandlerAsync("dup", null, new CountingHandler());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _reader.RegisterHandlerAsync("dup", null, new CountingHandler()));
    }

    [Fact]
    public async Task RegisterHandlerAsync_NullHandler_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _reader.RegisterHandlerAsync("null-handler", null, null!));
    }

    [Fact]
    public async Task RegisterHandlerAsync_EmptyHandlerId_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _reader.RegisterHandlerAsync("  ", null, new CountingHandler()));
    }

    [Fact]
    public async Task DrainOnceAsync_WithRegisteredHandler_InvokesOnNewEvents()
    {
        var handler = new CountingHandler();
        await _reader.RegisterHandlerAsync("counter", null, handler);

        for (int i = 0; i < 3; i++)
        {
            await _store.AppendAsync(NewEnvelope("Test.X", $"key-{i}"));
            await Task.Delay(2);
        }

        var processed = await _reader.DrainOnceAsync(Tenant, 100);

        Assert.Equal(3, processed);
        Assert.Equal(3, handler.Count);
    }

    [Fact]
    public async Task DrainOnceAsync_FilteredByEventType_OnlyMatchingEventsInvoke()
    {
        var handler = new CountingHandler();
        await _reader.RegisterHandlerAsync("filtered", "Financial.JournalEntryPosted", handler);

        await _store.AppendAsync(NewEnvelope("Financial.JournalEntryPosted", "k1"));
        await _store.AppendAsync(NewEnvelope("Work.WorkOrderCreated", "k2"));
        await _store.AppendAsync(NewEnvelope("Financial.JournalEntryPosted", "k3"));

        await _reader.DrainOnceAsync(Tenant, 100);

        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task DrainOnceAsync_OnHandlerThrow_CursorStaysPinned()
    {
        var handler = new ThrowOnNthHandler(throwOnIndex: 2);
        await _reader.RegisterHandlerAsync("flaky", null, handler);

        for (int i = 0; i < 5; i++)
        {
            await _store.AppendAsync(NewEnvelope("Test.X", $"k-{i}"));
            await Task.Delay(2);
        }

        await _reader.DrainOnceAsync(Tenant, 100);

        // Handler called for events 1 + 2 (event 2 threw); cursor pinned
        // at event 1 (last successful). Next drain re-invokes on
        // event 2.
        Assert.Equal(2, handler.Calls);

        handler.ThrowOnIndex = -1;  // stop throwing
        await _reader.DrainOnceAsync(Tenant, 100);

        // Now drains the remaining: event 2 (retry) + events 3, 4, 5.
        Assert.Equal(2 + 4, handler.Calls);
    }

    [Fact]
    public async Task DrainOnceAsync_OnHandlerThrow_RecordsFailureRow()
    {
        var handler = new ThrowOnNthHandler(throwOnIndex: 1);
        await _reader.RegisterHandlerAsync("recorded-failure", null, handler);
        await _store.AppendAsync(NewEnvelope("Test.X", "fail-1"));

        await _reader.DrainOnceAsync(Tenant, 100);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*), MAX(attempt_number)
            FROM event_handler_failures
            WHERE handler_id = 'recorded-failure';
            """;
        await using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal(1, reader.GetInt32(1));
    }

    [Fact]
    public async Task DrainOnceAsync_AfterMultipleFailures_AttemptNumberIncrements()
    {
        var handler = new AlwaysThrowHandler();
        await _reader.RegisterHandlerAsync("repeating-failure", null, handler);
        await _store.AppendAsync(NewEnvelope("Test.X", "repeat-1"));

        await _reader.DrainOnceAsync(Tenant, 100);
        await _reader.DrainOnceAsync(Tenant, 100);
        await _reader.DrainOnceAsync(Tenant, 100);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT MAX(attempt_number) FROM event_handler_failures
            WHERE handler_id = 'repeating-failure';
            """;
        var maxAttempt = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(3L, maxAttempt);
    }

    [Fact]
    public async Task DrainOnceAsync_AfterFailure_OtherHandlersStillProceed()
    {
        var failing = new ThrowOnNthHandler(throwOnIndex: 1);
        var counting = new CountingHandler();
        await _reader.RegisterHandlerAsync("failing", null, failing);
        await _reader.RegisterHandlerAsync("counting", null, counting);

        await _store.AppendAsync(NewEnvelope("Test.X", "shared-1"));
        await _store.AppendAsync(NewEnvelope("Test.X", "shared-2"));

        await _reader.DrainOnceAsync(Tenant, 100);

        // Counting handler drains both; failing handler stops at first.
        Assert.Equal(2, counting.Count);
        Assert.Equal(1, failing.Calls);
    }

    [Fact]
    public async Task DrainOnceAsync_TenantIsolation_OnlyDrainsCurrentTenant()
    {
        var handler = new CountingHandler();
        await _reader.RegisterHandlerAsync("tenant-iso", null, handler);

        await _store.AppendAsync(NewEnvelope("Test.X", "a-1", Tenant));
        await _store.AppendAsync(NewEnvelope("Test.X", "b-1", OtherTenant));
        await _store.AppendAsync(NewEnvelope("Test.X", "a-2", Tenant));

        await _reader.DrainOnceAsync(Tenant, 100);

        Assert.Equal(2, handler.Count);
    }

    [Fact]
    public async Task DrainOnceAsync_EmptyEvents_NoOpReturnsZero()
    {
        var handler = new CountingHandler();
        await _reader.RegisterHandlerAsync("empty", null, handler);

        var processed = await _reader.DrainOnceAsync(Tenant, 100);

        Assert.Equal(0, processed);
    }

    [Fact]
    public async Task DrainOnceAsync_BatchSizeZero_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _reader.DrainOnceAsync(Tenant, 0));
    }

    [Fact]
    public async Task DrainOnceAsync_CursorAdvances_AcrossDrainCycles()
    {
        var handler = new CountingHandler();
        await _reader.RegisterHandlerAsync("multi-cycle", null, handler);

        // First cycle: 2 events.
        await _store.AppendAsync(NewEnvelope("Test.X", "c1-1"));
        await _store.AppendAsync(NewEnvelope("Test.X", "c1-2"));
        await Task.Delay(5);
        var first = await _reader.DrainOnceAsync(Tenant, 100);
        Assert.Equal(2, first);

        // Second cycle: 1 more event; first 2 NOT re-handled.
        await _store.AppendAsync(NewEnvelope("Test.X", "c2-1"));
        await Task.Delay(5);
        var second = await _reader.DrainOnceAsync(Tenant, 100);
        Assert.Equal(1, second);
        Assert.Equal(3, handler.Count);
    }

    [Fact]
    public void ComputeNextRetry_Attempt1_Returns30s()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var next = SqliteEventReader.ComputeNextRetry(1, now);
        Assert.Equal(now.AddSeconds(30), next);
    }

    [Fact]
    public void ComputeNextRetry_Attempt7_Returns72h()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var next = SqliteEventReader.ComputeNextRetry(7, now);
        Assert.Equal(now.AddHours(72), next);
    }

    [Fact]
    public void ComputeNextRetry_Attempt8_ReturnsNull()
    {
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Null(SqliteEventReader.ComputeNextRetry(8, now));
    }

    // ----- helpers ---------------------------------------------------

    private sealed record TestPayload(string Foo);

    private static DomainEventEnvelope<TestPayload> NewEnvelope(
        string eventType, string idempotencyKey, TenantId? tenantOverride = null)
        => new()
        {
            EventId              = EventId.New(),
            EventType            = eventType,
            SchemaVersion        = 1,
            OccurredAt           = DateTimeOffset.UtcNow,
            TenantId             = tenantOverride ?? Tenant,
            OriginatingReplicaId = Replica,
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

    private sealed class ThrowOnNthHandler : IEventHandler<RawDomainEvent>
    {
        public int Calls;
        public int ThrowOnIndex;
        public ThrowOnNthHandler(int throwOnIndex) { ThrowOnIndex = throwOnIndex; }
        public Task HandleAsync(DomainEventEnvelope<RawDomainEvent> envelope, CancellationToken cancellationToken = default)
        {
            var n = Interlocked.Increment(ref Calls);
            if (n == ThrowOnIndex)
                throw new InvalidOperationException($"Simulated failure on call {n}.");
            return Task.CompletedTask;
        }
    }

    private sealed class AlwaysThrowHandler : IEventHandler<RawDomainEvent>
    {
        public Task HandleAsync(DomainEventEnvelope<RawDomainEvent> envelope, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Simulated permanent failure.");
    }
}
