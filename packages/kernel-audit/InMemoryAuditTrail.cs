using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Kernel.Audit;

/// <summary>
/// In-memory <see cref="IAuditTrail"/> implementation. Stores audit records
/// in a thread-safe collection; query results are AND-combined per
/// <see cref="AuditQuery"/>.
/// </summary>
/// <remarks>
/// <para>
/// Restart-volatile: process restart loses all stored records. Use for
/// development hosts, test fixtures, and v1 substrates that have
/// audit-emission requirements but no persistent-storage substrate yet
/// (e.g., Bridge v1 per the W#23 P4.5 audit-infrastructure unblock
/// addendum). Persistent <see cref="IAuditTrail"/> implementations
/// (<see cref="EventLogBackedAuditTrail"/> for kernel; future Bridge audit
/// infra per ~ADR 0076) replace this when production durability is
/// required.
/// </para>
/// <para>
/// Unlike <see cref="EventLogBackedAuditTrail"/>, this implementation does
/// NOT verify the payload's <see cref="Sunfish.Foundation.Crypto.SignedOperation{T}"/>
/// envelope — the in-memory variant is for callers that have already
/// signed (or that are running in a test fixture / dev host without a
/// signer). Callers that need verification should use the event-log-backed
/// implementation.
/// </para>
/// </remarks>
public sealed class InMemoryAuditTrail : IAuditTrail
{
    private readonly ConcurrentQueue<AuditRecord> _records = new();

    /// <inheritdoc />
    public ValueTask AppendAsync(AuditRecord record, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (record.TenantId == default)
        {
            throw new ArgumentException(
                "AuditRecord.TenantId must be non-default per IMustHaveTenant.",
                nameof(record));
        }
        ct.ThrowIfCancellationRequested();
        _records.Enqueue(record);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<AuditRecord> QueryAsync(
        AuditQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Snapshot the queue to avoid yielding while iterating; the queue
        // is concurrent so this is a stable read at call time.
        var snapshot = _records.ToArray();
        foreach (var record in snapshot)
        {
            ct.ThrowIfCancellationRequested();
            if (record.TenantId != query.TenantId)
            {
                continue;
            }
            if (query.EventType is { } eventType && !record.EventType.Equals(eventType))
            {
                continue;
            }
            if (query.OccurredAfter is { } after && record.OccurredAt < after)
            {
                continue;
            }
            if (query.OccurredBefore is { } before && record.OccurredAt > before)
            {
                continue;
            }
            if (query.IssuedBy is { } issuer && !record.Payload.IssuerId.Equals(issuer))
            {
                continue;
            }
            yield return record;
            await Task.Yield();
        }
    }

    /// <summary>
    /// Snapshot count of stored records. Useful for test assertions; in
    /// production paths consume <see cref="QueryAsync"/> instead.
    /// </summary>
    public int Count => _records.Count;
}
