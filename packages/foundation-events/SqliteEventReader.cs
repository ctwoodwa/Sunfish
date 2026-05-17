using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Data.Sqlite;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Events;

/// <summary>
/// SQLite-backed <see cref="IEventReader"/>. Drains the
/// <c>domain_events</c> table on demand via
/// <see cref="DrainOnceAsync"/> (driven by the
/// <see cref="EventDispatcherHost"/> polling loop). Each registered
/// handler has its own cursor in <c>event_handler_cursors</c>;
/// failures are recorded in <c>event_handler_failures</c> with a
/// 7-step exponential-ish backoff per
/// <c>cross-cluster-event-bus-design.md</c> §6.
/// </summary>
/// <remarks>
/// On handler failure the cursor is NOT advanced — the handler revisits
/// the same event on the next drain. The reader stops processing
/// further events for that handler in the current cycle so the failure
/// doesn't cascade.
/// </remarks>
public sealed class SqliteEventReader : IEventReader
{
    private readonly SqliteConnection _connection;
    private readonly IDomainEventStore _store;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<string, HandlerRegistration> _handlers = new();

    public SqliteEventReader(
        SqliteConnection connection,
        IDomainEventStore store,
        TimeProvider? clock = null)
    {
        _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        _store      = store      ?? throw new ArgumentNullException(nameof(store));
        _clock      = clock ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RawDomainEvent>> ReadAsync(
        TenantId tenantId,
        string eventType,
        string? afterEventId,
        int maxBatchSize,
        CancellationToken cancellationToken = default)
    {
        var all = await _store.GetAfterCursorAsync(tenantId, afterEventId, maxBatchSize, cancellationToken)
            .ConfigureAwait(false);
        return all.Where(e => e.EventType == eventType).ToList();
    }

    /// <inheritdoc />
    public Task RegisterHandlerAsync(
        string handlerId,
        string? eventType,
        IEventHandler<RawDomainEvent> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(handlerId);
        ArgumentNullException.ThrowIfNull(handler);

        var added = _handlers.TryAdd(handlerId, new HandlerRegistration(handlerId, eventType, handler));
        if (!added)
            throw new InvalidOperationException(
                $"Handler '{handlerId}' is already registered. "
                + "Per cross-cluster-event-bus-design.md §5: handler ids are "
                + "stable + unique per process.");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Drain one batch of events to all registered handlers. The
    /// <see cref="EventDispatcherHost"/> calls this on its polling
    /// interval. Returns the count of successfully-handled
    /// invocations across all handlers.
    /// </summary>
    public async Task<int> DrainOnceAsync(
        TenantId tenantId,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Must be > 0.");

        var totalProcessed = 0;
        foreach (var reg in _handlers.Values)
        {
            var cursor = await GetCursorAsync(reg.HandlerId, tenantId, cancellationToken).ConfigureAwait(false);
            var events = await _store.GetAfterCursorAsync(tenantId, cursor, batchSize, cancellationToken)
                .ConfigureAwait(false);
            foreach (var evt in events)
            {
                if (reg.EventTypeFilter is not null && evt.EventType != reg.EventTypeFilter)
                {
                    // Filter mismatch — advance cursor past this event
                    // so the handler doesn't keep re-evaluating it.
                    await SetCursorAsync(reg.HandlerId, tenantId, evt.EventId, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                try
                {
                    var envelope = ProjectToEnvelope(evt);
                    await reg.Handler.HandleAsync(envelope, cancellationToken).ConfigureAwait(false);
                    await SetCursorAsync(reg.HandlerId, tenantId, evt.EventId, cancellationToken)
                        .ConfigureAwait(false);
                    totalProcessed++;
                }
                catch (Exception ex)
                {
                    await RecordFailureAsync(reg.HandlerId, evt, tenantId, ex, cancellationToken)
                        .ConfigureAwait(false);
                    // Cursor NOT advanced. Stop this handler's cycle —
                    // it revisits the same event on the next drain.
                    break;
                }
            }
        }
        return totalProcessed;
    }

    private async Task<string?> GetCursorAsync(
        string handlerId, TenantId tenantId, CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT last_handled_event_id FROM event_handler_cursors
            WHERE handler_id = $handler_id AND tenant_id = $tenant_id;
            """;
        cmd.Parameters.AddWithValue("$handler_id", handlerId);
        cmd.Parameters.AddWithValue("$tenant_id", tenantId.Value);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return result switch { string s => s, _ => null };
    }

    private async Task SetCursorAsync(
        string handlerId, TenantId tenantId, string eventId, CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO event_handler_cursors
                (handler_id, tenant_id, last_handled_event_id, last_handled_at)
            VALUES ($handler_id, $tenant_id, $event_id, $now)
            ON CONFLICT(handler_id, tenant_id) DO UPDATE
                SET last_handled_event_id = $event_id,
                    last_handled_at = $now;
            """;
        cmd.Parameters.AddWithValue("$handler_id", handlerId);
        cmd.Parameters.AddWithValue("$tenant_id", tenantId.Value);
        cmd.Parameters.AddWithValue("$event_id", eventId);
        cmd.Parameters.AddWithValue("$now", _clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task RecordFailureAsync(
        string handlerId, RawDomainEvent evt, TenantId tenantId,
        Exception ex, CancellationToken ct)
    {
        var attempt = await GetAttemptCountAsync(handlerId, evt.EventId, ct).ConfigureAwait(false) + 1;
        var nextRetry = ComputeNextRetry(attempt, _clock.GetUtcNow());

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO event_handler_failures
                (id, handler_id, event_id, tenant_id, attempt_number, failed_at, error_message, next_retry_at)
            VALUES
                ($id, $handler_id, $event_id, $tenant_id, $attempt, $failed_at, $error_message, $next_retry_at);
            """;
        cmd.Parameters.AddWithValue("$id", EventId.New());
        cmd.Parameters.AddWithValue("$handler_id", handlerId);
        cmd.Parameters.AddWithValue("$event_id", evt.EventId);
        cmd.Parameters.AddWithValue("$tenant_id", tenantId.Value);
        cmd.Parameters.AddWithValue("$attempt", attempt);
        cmd.Parameters.AddWithValue("$failed_at", _clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$error_message", ex.Message);
        cmd.Parameters.AddWithValue("$next_retry_at",
            (object?)nextRetry?.ToString("O", CultureInfo.InvariantCulture) ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }

    private async Task<int> GetAttemptCountAsync(string handlerId, string eventId, CancellationToken ct)
    {
        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM event_handler_failures
            WHERE handler_id = $handler_id AND event_id = $event_id;
            """;
        cmd.Parameters.AddWithValue("$handler_id", handlerId);
        cmd.Parameters.AddWithValue("$event_id", eventId);
        var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
        return Convert.ToInt32(result, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Backoff schedule per <c>cross-cluster-event-bus-design.md</c>
    /// §6: 30s / 2m / 10m / 1h / 6h / 24h / 72h. Returns
    /// <c>null</c> after 7 attempts (retry exhausted; manual
    /// intervention).
    /// </summary>
    internal static DateTimeOffset? ComputeNextRetry(int attempt, DateTimeOffset now)
        => attempt switch
        {
            1 => now.AddSeconds(30),
            2 => now.AddMinutes(2),
            3 => now.AddMinutes(10),
            4 => now.AddHours(1),
            5 => now.AddHours(6),
            6 => now.AddHours(24),
            7 => now.AddHours(72),
            _ => null,
        };

    /// <summary>
    /// Project the untyped <see cref="RawDomainEvent"/> back to a
    /// strongly-typed <see cref="DomainEventEnvelope{TPayload}"/>
    /// where <c>TPayload</c> is <see cref="RawDomainEvent"/> itself.
    /// Handlers that need the typed payload deserialize
    /// <see cref="RawDomainEvent.PayloadJson"/> themselves.
    /// </summary>
    private static DomainEventEnvelope<RawDomainEvent> ProjectToEnvelope(RawDomainEvent evt)
        => new()
        {
            EventId              = evt.EventId,
            EventType            = evt.EventType,
            SchemaVersion        = evt.SchemaVersion,
            OccurredAt           = evt.OccurredAt,
            TenantId             = evt.TenantId,
            OriginatingReplicaId = evt.OriginatingReplicaId,
            IdempotencyKey       = evt.IdempotencyKey,
            CausationId          = evt.CausationId,
            CorrelationId        = evt.CorrelationId,
            Payload              = evt,
        };

    private sealed record HandlerRegistration(
        string HandlerId,
        string? EventTypeFilter,
        IEventHandler<RawDomainEvent> Handler);
}
