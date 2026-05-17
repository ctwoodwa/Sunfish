using Microsoft.Data.Sqlite;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Foundation.Events.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="SqliteDomainEventStore"/> per
/// the foundation-events hand-off PR 2 test plan + cross-cluster-
/// event-bus-design.md §1 storage shape + §4 idempotency contract.
/// </summary>
public sealed class SqliteDomainEventStoreTests : IAsyncLifetime
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");
    private static readonly ReplicaId Replica = ReplicaId.System;

    private SqliteConnection _connection = null!;
    private SqliteDomainEventStore _store = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        _store = new SqliteDomainEventStore(_connection, TimeProvider.System);
        await _store.ApplyMigrationsAsync();
    }

    public Task DisposeAsync()
    {
        _connection.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ApplyMigrationsAsync_OnFreshDb_CreatesDomainEventsTable()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='domain_events';";
        var name = (string?)await cmd.ExecuteScalarAsync();
        Assert.Equal("domain_events", name);
    }

    [Fact]
    public async Task ApplyMigrationsAsync_RepeatedCall_IsIdempotent()
    {
        // Already applied in InitializeAsync; calling again must succeed.
        await _store.ApplyMigrationsAsync();
        await _store.ApplyMigrationsAsync();
        // No assertion needed — must not throw.
    }

    [Fact]
    public async Task AppendAsync_HappyPath_PersistsAndReturnsEventId()
    {
        var envelope = NewEnvelope(TenantA, "Financial.JournalEntryPosted", "key-1");

        var returnedId = await _store.AppendAsync(envelope);

        Assert.Equal(envelope.EventId, returnedId);
        var found = await _store.FindByIdempotencyKeyAsync(TenantA, "key-1");
        Assert.NotNull(found);
        Assert.Equal(envelope.EventId, found!.EventId);
    }

    [Fact]
    public async Task AppendAsync_OnDuplicateIdempotencyKey_ReturnsExistingEventId()
    {
        var first = NewEnvelope(TenantA, "Financial.PeriodSoftClosed", "key-dup");
        var second = NewEnvelope(TenantA, "Financial.PeriodSoftClosed", "key-dup");

        var firstId = await _store.AppendAsync(first);
        var secondId = await _store.AppendAsync(second);

        Assert.Equal(firstId, secondId);
        Assert.NotEqual(second.EventId, secondId);
    }

    [Fact]
    public async Task AppendAsync_DifferentTenantsSameKey_BothPersist()
    {
        var a = NewEnvelope(TenantA, "Financial.PeriodSoftClosed", "shared-key");
        var b = NewEnvelope(TenantB, "Financial.PeriodSoftClosed", "shared-key");

        await _store.AppendAsync(a);
        await _store.AppendAsync(b);

        var foundA = await _store.FindByIdempotencyKeyAsync(TenantA, "shared-key");
        var foundB = await _store.FindByIdempotencyKeyAsync(TenantB, "shared-key");
        Assert.NotNull(foundA);
        Assert.NotNull(foundB);
        Assert.NotEqual(foundA!.EventId, foundB!.EventId);
    }

    [Fact]
    public async Task AppendAsync_PersistsAllEnvelopeFields()
    {
        var envelope = NewEnvelope(TenantA, "Work.WorkOrderCreated", "all-fields") with
        {
            CausationId   = "01ABCDEFGH0123456789JKMNPQ",
            CorrelationId = "workflow-abc-123",
        };

        await _store.AppendAsync(envelope);

        var found = await _store.FindByIdempotencyKeyAsync(TenantA, "all-fields");
        Assert.NotNull(found);
        Assert.Equal(envelope.EventId, found!.EventId);
        Assert.Equal("Work.WorkOrderCreated", found.EventType);
        Assert.Equal(envelope.SchemaVersion, found.SchemaVersion);
        Assert.Equal(envelope.TenantId, found.TenantId);
        Assert.Equal(envelope.OriginatingReplicaId, found.OriginatingReplicaId);
        Assert.Equal("all-fields", found.IdempotencyKey);
        Assert.Equal("01ABCDEFGH0123456789JKMNPQ", found.CausationId);
        Assert.Equal("workflow-abc-123", found.CorrelationId);
        Assert.NotNull(found.PayloadJson);
    }

    [Fact]
    public async Task AppendAsync_DerivesProducerClusterFromEventType()
    {
        var envelope = NewEnvelope(TenantA, "Financial.JournalEntryPosted", "cluster-test");
        await _store.AppendAsync(envelope);

        var found = await _store.FindByIdempotencyKeyAsync(TenantA, "cluster-test");
        Assert.NotNull(found);
        Assert.Equal("financial", found!.ProducerCluster);
    }

    [Fact]
    public async Task AppendAsync_RejectsEventTypeWithoutDotPrefix()
    {
        var envelope = NewEnvelope(TenantA, "BadEventTypeNoDot", "rejected-key");

        await Assert.ThrowsAsync<ArgumentException>(() => _store.AppendAsync(envelope));
    }

    [Fact]
    public async Task AppendAsync_RecordedAtUtc_IsSetToClockNow()
    {
        var fixedInstant = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        var fakeClock = new FixedTimeProvider(fixedInstant);
        var store = new SqliteDomainEventStore(_connection, fakeClock);
        var envelope = NewEnvelope(TenantA, "Test.Event", "clock-test");

        await store.AppendAsync(envelope);

        var found = await store.FindByIdempotencyKeyAsync(TenantA, "clock-test");
        Assert.NotNull(found);
        Assert.Equal(fixedInstant, found!.RecordedAtUtc);
    }

    [Fact]
    public async Task GetAfterCursorAsync_WithNullCursor_ReturnsAllEventsInOrder()
    {
        var ids = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var env = NewEnvelope(TenantA, "Test.Event", $"key-{i}");
            ids.Add(await _store.AppendAsync(env));
            await Task.Delay(2); // ensure ULID timestamps differ
        }

        var events = await _store.GetAfterCursorAsync(TenantA, null, 100);

        Assert.Equal(5, events.Count);
        Assert.Equal(ids.OrderBy(id => id, StringComparer.Ordinal).ToList(),
                     events.Select(e => e.EventId).ToList());
    }

    [Fact]
    public async Task GetAfterCursorAsync_WithCursor_ReturnsOnlyAfter()
    {
        var ids = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            ids.Add(await _store.AppendAsync(NewEnvelope(TenantA, "Test.Event", $"key-{i}")));
            await Task.Delay(2);
        }
        var sorted = ids.OrderBy(id => id, StringComparer.Ordinal).ToList();

        var events = await _store.GetAfterCursorAsync(TenantA, sorted[2], 100);

        Assert.Equal(2, events.Count);
        Assert.Equal(sorted[3], events[0].EventId);
        Assert.Equal(sorted[4], events[1].EventId);
    }

    [Fact]
    public async Task GetAfterCursorAsync_RespectsBatchSize()
    {
        for (int i = 0; i < 10; i++)
        {
            await _store.AppendAsync(NewEnvelope(TenantA, "Test.Event", $"k-{i}"));
            await Task.Delay(2);
        }

        var events = await _store.GetAfterCursorAsync(TenantA, null, 3);
        Assert.Equal(3, events.Count);
    }

    [Fact]
    public async Task GetAfterCursorAsync_TenantIsolation_OnlyReturnsCurrentTenant()
    {
        await _store.AppendAsync(NewEnvelope(TenantA, "Test.Event", "a-1"));
        await _store.AppendAsync(NewEnvelope(TenantB, "Test.Event", "b-1"));
        await _store.AppendAsync(NewEnvelope(TenantA, "Test.Event", "a-2"));

        var aEvents = await _store.GetAfterCursorAsync(TenantA, null, 100);
        Assert.Equal(2, aEvents.Count);
        Assert.All(aEvents, e => Assert.Equal(TenantA, e.TenantId));
    }

    [Fact]
    public async Task GetAfterCursorAsync_BatchSizeZero_Throws()
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => _store.GetAfterCursorAsync(TenantA, null, 0));
    }

    [Fact]
    public async Task FindByIdempotencyKeyAsync_MissingKey_ReturnsNull()
    {
        var result = await _store.FindByIdempotencyKeyAsync(TenantA, "nonexistent-key");
        Assert.Null(result);
    }

    [Fact]
    public async Task FindByIdempotencyKeyAsync_EmptyKey_Throws()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _store.FindByIdempotencyKeyAsync(TenantA, string.Empty));
    }

    [Fact]
    public async Task Schema_DomainEventsTable_HasUniqueIndexOnTenantAndIdempotencyKey()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT sql FROM sqlite_master WHERE type='index' AND name='idx_domain_events_idempotency';";
        var sql = (string?)await cmd.ExecuteScalarAsync();
        Assert.NotNull(sql);
        Assert.Contains("UNIQUE", sql!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("tenant_id", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("idempotency_key", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Schema_DomainEventsTable_HasCorrelationIndex()
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='index' AND name='idx_domain_events_correlation';";
        var name = (string?)await cmd.ExecuteScalarAsync();
        Assert.Equal("idx_domain_events_correlation", name);
    }

    [Fact]
    public async Task AppendAsync_PayloadJson_RoundTrips()
    {
        var envelope = NewEnvelope(TenantA, "Test.SomePayload", "payload-test");
        await _store.AppendAsync(envelope);

        var found = await _store.FindByIdempotencyKeyAsync(TenantA, "payload-test");
        Assert.NotNull(found);
        Assert.Contains("\"Foo\":\"sample\"", found!.PayloadJson);
        Assert.Contains("\"Bar\":42", found.PayloadJson);
    }

    // ----- helpers ---------------------------------------------------

    private sealed record TestPayload(string Foo, int Bar);

    private static DomainEventEnvelope<TestPayload> NewEnvelope(
        TenantId tenantId, string eventType, string idempotencyKey)
        => new()
        {
            EventId              = EventId.New(),
            EventType            = eventType,
            SchemaVersion        = 1,
            OccurredAt           = DateTimeOffset.UtcNow,
            TenantId             = tenantId,
            OriginatingReplicaId = Replica,
            IdempotencyKey       = idempotencyKey,
            Payload              = new TestPayload("sample", 42),
        };

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FixedTimeProvider(DateTimeOffset now) { _now = now; }
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
