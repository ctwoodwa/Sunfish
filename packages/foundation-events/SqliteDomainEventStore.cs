using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Events;

/// <summary>
/// SQLite-backed implementation of <see cref="IDomainEventStore"/>.
/// Append-only; idempotent on
/// <c>(<see cref="DomainEventEnvelope{TPayload}.TenantId"/>,
/// <see cref="DomainEventEnvelope{TPayload}.IdempotencyKey"/>)</c>.
/// </summary>
/// <remarks>
/// <para>
/// Per <c>cross-cluster-event-bus-design.md</c> §1 storage shape +
/// the <see cref="ApplyMigrationsAsync"/> migration script
/// (<c>Sql/001-create-domain-events.sql</c> embedded resource).
/// </para>
/// <para>
/// Connection lifetime is owned by the caller — the store does NOT
/// dispose the supplied <see cref="SqliteConnection"/>. The
/// composition root opens the connection (typically a long-lived
/// per-tenant connection) and passes it in.
/// </para>
/// </remarks>
public sealed class SqliteDomainEventStore : IDomainEventStore
{
    private readonly SqliteConnection _connection;
    private readonly TimeProvider _clock;
    private readonly JsonSerializerOptions _jsonOptions;

    public SqliteDomainEventStore(SqliteConnection connection, TimeProvider? clock = null)
    {
        _connection  = connection ?? throw new ArgumentNullException(nameof(connection));
        _clock       = clock ?? TimeProvider.System;
        _jsonOptions = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        };
    }

    /// <summary>
    /// Apply all foundation-events migrations (currently 001 +
    /// 002). Idempotent — safe to call on every host start. The
    /// composition root invokes this once at startup, or
    /// <c>AddFoundationEvents()</c> (PR 6) does it during DI build.
    /// </summary>
    public async Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var resourceName in new[]
                 {
                     "001-create-domain-events.sql",
                     "002-create-handler-cursors.sql",
                 })
        {
            var sql = LoadEmbeddedSql(resourceName);
            await using var cmd = _connection.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task<string> AppendAsync<TPayload>(
        DomainEventEnvelope<TPayload> envelope,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        var producerCluster = DeriveProducerCluster(envelope.EventType);
        var payloadJson     = JsonSerializer.Serialize(envelope.Payload, _jsonOptions);
        var recordedAtUtc   = _clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO domain_events (
                event_id, event_type, schema_version, occurred_at, recorded_at_utc,
                tenant_id, originating_replica_id, idempotency_key,
                causation_id, correlation_id, producer_cluster, payload_json
            ) VALUES (
                $event_id, $event_type, $schema_version, $occurred_at, $recorded_at_utc,
                $tenant_id, $originating_replica_id, $idempotency_key,
                $causation_id, $correlation_id, $producer_cluster, $payload_json
            )
            ON CONFLICT(tenant_id, idempotency_key) DO NOTHING;
            """;
        cmd.Parameters.AddWithValue("$event_id",               envelope.EventId);
        cmd.Parameters.AddWithValue("$event_type",             envelope.EventType);
        cmd.Parameters.AddWithValue("$schema_version",         envelope.SchemaVersion);
        cmd.Parameters.AddWithValue("$occurred_at",            envelope.OccurredAt.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("$recorded_at_utc",        recordedAtUtc);
        cmd.Parameters.AddWithValue("$tenant_id",              envelope.TenantId.Value);
        cmd.Parameters.AddWithValue("$originating_replica_id", envelope.OriginatingReplicaId.Value);
        cmd.Parameters.AddWithValue("$idempotency_key",        envelope.IdempotencyKey);
        cmd.Parameters.AddWithValue("$causation_id",           (object?)envelope.CausationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$correlation_id",         (object?)envelope.CorrelationId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$producer_cluster",       producerCluster);
        cmd.Parameters.AddWithValue("$payload_json",           payloadJson);

        var inserted = await cmd.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (inserted == 1)
            return envelope.EventId;

        // Dedup happened. Look up the existing event_id for the
        // matching (tenant_id, idempotency_key) and return it.
        await using var lookup = _connection.CreateCommand();
        lookup.CommandText = """
            SELECT event_id FROM domain_events
            WHERE tenant_id = $tenant_id AND idempotency_key = $idempotency_key
            LIMIT 1;
            """;
        lookup.Parameters.AddWithValue("$tenant_id",       envelope.TenantId.Value);
        lookup.Parameters.AddWithValue("$idempotency_key", envelope.IdempotencyKey);
        var existing = (string?)await lookup.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return existing ?? throw new InvalidOperationException(
            "Idempotency-key dedup happened but no row found on lookup; "
            + "indicates a concurrent DELETE (forbidden per §4 append-only).");
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RawDomainEvent>> GetAfterCursorAsync(
        TenantId tenantId,
        string? afterEventId,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Must be > 0.");

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = afterEventId is null
            ? """
              SELECT event_id, event_type, schema_version, occurred_at, recorded_at_utc,
                     tenant_id, originating_replica_id, idempotency_key,
                     causation_id, correlation_id, producer_cluster, payload_json
              FROM domain_events
              WHERE tenant_id = $tenant_id
              ORDER BY event_id ASC
              LIMIT $limit;
              """
            : """
              SELECT event_id, event_type, schema_version, occurred_at, recorded_at_utc,
                     tenant_id, originating_replica_id, idempotency_key,
                     causation_id, correlation_id, producer_cluster, payload_json
              FROM domain_events
              WHERE tenant_id = $tenant_id AND event_id > $after_event_id
              ORDER BY event_id ASC
              LIMIT $limit;
              """;
        cmd.Parameters.AddWithValue("$tenant_id", tenantId.Value);
        if (afterEventId is not null)
            cmd.Parameters.AddWithValue("$after_event_id", afterEventId);
        cmd.Parameters.AddWithValue("$limit", batchSize);

        var results = new List<RawDomainEvent>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            results.Add(ReadRow(reader));
        return results;
    }

    /// <inheritdoc />
    public async Task<RawDomainEvent?> FindByIdempotencyKeyAsync(
        TenantId tenantId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(idempotencyKey);

        await using var cmd = _connection.CreateCommand();
        cmd.CommandText = """
            SELECT event_id, event_type, schema_version, occurred_at, recorded_at_utc,
                   tenant_id, originating_replica_id, idempotency_key,
                   causation_id, correlation_id, producer_cluster, payload_json
            FROM domain_events
            WHERE tenant_id = $tenant_id AND idempotency_key = $idempotency_key
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$tenant_id",       tenantId.Value);
        cmd.Parameters.AddWithValue("$idempotency_key", idempotencyKey);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;
        return ReadRow(reader);
    }

    private static string DeriveProducerCluster(string eventType)
    {
        var dotIdx = eventType.IndexOf('.');
        if (dotIdx <= 0)
            throw new ArgumentException(
                $"EventType '{eventType}' is not cluster-qualified. "
                + "Per cross-cluster-event-bus-design.md §2: format is "
                + "<ClusterName-titlecase>.<PascalCaseVerbPastTense>.",
                nameof(eventType));
        return eventType[..dotIdx].ToLowerInvariant();
    }

    private static RawDomainEvent ReadRow(SqliteDataReader reader)
        // Bypass the TenantId / ReplicaId public-ctor reserved-prefix
        // guard via the with-init pattern — the values came from our
        // own append-only store and are trusted by construction. Same
        // pattern as the sentinel factories in
        // foundation/Assets/Common/{TenantId,ReplicaId}.cs.
        => new()
        {
            EventId              = reader.GetString(0),
            EventType            = reader.GetString(1),
            SchemaVersion        = reader.GetInt32(2),
            OccurredAt           = DateTimeOffset.Parse(reader.GetString(3), CultureInfo.InvariantCulture),
            RecordedAtUtc        = DateTimeOffset.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
            TenantId             = new TenantId { Value = reader.GetString(5) },
            OriginatingReplicaId = new ReplicaId { Value = reader.GetString(6) },
            IdempotencyKey       = reader.GetString(7),
            CausationId          = reader.IsDBNull(8) ? null : reader.GetString(8),
            CorrelationId        = reader.IsDBNull(9) ? null : reader.GetString(9),
            ProducerCluster      = reader.GetString(10),
            PayloadJson          = reader.GetString(11),
        };

    private static string LoadEmbeddedSql(string fileName)
    {
        var assembly = typeof(SqliteDomainEventStore).Assembly;
        var resourceName = $"Sunfish.Foundation.Events.Sql.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded migration '{fileName}' not found. "
                + "Verify <EmbeddedResource Include=\"Sql\\*.sql\" /> in csproj.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
