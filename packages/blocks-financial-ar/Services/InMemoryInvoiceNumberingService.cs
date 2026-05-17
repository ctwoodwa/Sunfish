using System.Collections.Concurrent;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialAr.Services;

/// <summary>
/// In-memory <see cref="IInvoiceNumberingService"/>. Counter state lives in
/// a <c>ConcurrentDictionary</c> keyed by <c>(ChartOfAccountsId, ReplicaId)</c>
/// — independent sequences per chart per replica. Restart loses state; a
/// persistence-backed implementation (SQLite, with <c>INSERT … RETURNING</c>
/// for atomic increment) ships in the follow-on substrate hand-off.
/// </summary>
public sealed class InMemoryInvoiceNumberingService : IInvoiceNumberingService
{
    private readonly ConcurrentDictionary<(ChartOfAccountsId Chart, ReplicaId Replica), long> _counters = new();
    private readonly ReplicaId _localReplica;
    private readonly object _gate = new();

    /// <summary>Construct with the local replica's suffix.</summary>
    public InMemoryInvoiceNumberingService(ReplicaId localReplica)
    {
        _localReplica = localReplica;
    }

    /// <inheritdoc />
    public Task<string> NextNumberAsync(
        ChartOfAccountsId chartId,
        DateOnly issueDate,
        CancellationToken cancellationToken = default)
    {
        long next;
        // Lock the increment — ConcurrentDictionary.AddOrUpdate could race
        // with concurrent reads if we computed the next value outside the
        // dictionary's lock. The minted number is what we return, so the
        // caller must see exactly this value.
        lock (_gate)
        {
            next = _counters.AddOrUpdate(
                (chartId, _localReplica),
                addValue: 1L,
                updateValueFactory: (_, current) => current + 1L);
        }

        // Sequence portion: D4 padding minimum, expands beyond for very
        // high-volume replicas (10000+ invoices on a single chart).
        var seqPart = next < 10_000 ? next.ToString("D4") : next.ToString();
        var number = $"INV-{issueDate:yyyy-MM-dd}-{_localReplica.Value}-{seqPart}";
        return Task.FromResult(number);
    }

    /// <inheritdoc />
    public Task<ReplicaId> ResolveCollisionAsync(
        ChartOfAccountsId chartId,
        string conflictingNumber,
        ReplicaId localReplica,
        ReplicaId remoteReplica,
        Instant localReplicaCreatedAt,
        Instant remoteReplicaCreatedAt,
        CancellationToken cancellationToken = default)
    {
        // Older replica wins → returns the replica that must re-key.
        // Tiebreaker: lexicographic on the ReplicaId.Value string;
        // the LARGER value re-keys (smaller wins on tie).
        var localFirst = localReplicaCreatedAt.Value;
        var remoteFirst = remoteReplicaCreatedAt.Value;

        ReplicaId mustRekey;
        if (localFirst < remoteFirst)
            mustRekey = remoteReplica;
        else if (remoteFirst < localFirst)
            mustRekey = localReplica;
        else
            // Equal timestamps → lex compare on the ReplicaId value;
            // larger string re-keys, smaller keeps the number.
            mustRekey = string.CompareOrdinal(localReplica.Value, remoteReplica.Value) > 0
                ? localReplica
                : remoteReplica;

        return Task.FromResult(mustRekey);
    }
}
