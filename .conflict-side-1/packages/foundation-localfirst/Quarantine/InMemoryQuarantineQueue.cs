using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Sunfish.Foundation.LocalFirst.Quarantine;

/// <summary>
/// In-memory reference implementation of <see cref="IQuarantineQueue"/>. Backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> keyed by record id. Suitable for tests and
/// bundled scenarios where durability is provided elsewhere.
/// </summary>
public sealed class InMemoryQuarantineQueue : IQuarantineQueue
{
    private readonly ConcurrentDictionary<Guid, QuarantineRecord> _records = new();

    /// <inheritdoc />
    public Task<Guid> EnqueueAsync(QuarantineEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ct.ThrowIfCancellationRequested();

        var id = Guid.NewGuid();
        var record = new QuarantineRecord(
            id,
            entry,
            QuarantineStatus.Pending,
            entry.QueuedAt,
            entry.QueuedByActor,
            RejectionReason: null);

        if (!_records.TryAdd(id, record))
        {
            // Guid collision is astronomically unlikely; surface it rather than silently overwrite.
            throw new InvalidOperationException($"Quarantine id collision for '{id}'.");
        }

        return Task.FromResult(id);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<QuarantineRecord> ReadByStatusAsync(
        QuarantineStatus status,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var record in _records.Values)
        {
            ct.ThrowIfCancellationRequested();
            if (record.Status == status)
            {
                yield return record;
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task PromoteAsync(Guid entryId, string actor, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(actor);
        ct.ThrowIfCancellationRequested();

        TransitionFromPending(
            entryId,
            current => current with
            {
                Status = QuarantineStatus.Accepted,
                StatusChangedAt = DateTimeOffset.UtcNow,
                StatusChangedByActor = actor,
                RejectionReason = null,
            });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RejectAsync(Guid entryId, string actor, string reason, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(actor);
        ArgumentException.ThrowIfNullOrEmpty(reason);
        ct.ThrowIfCancellationRequested();

        TransitionFromPending(
            entryId,
            current => current with
            {
                Status = QuarantineStatus.Rejected,
                StatusChangedAt = DateTimeOffset.UtcNow,
                StatusChangedByActor = actor,
                RejectionReason = reason,
            });

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<QuarantineRecord?> GetAsync(Guid entryId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _records.TryGetValue(entryId, out var record);
        return Task.FromResult(record);
    }

    private void TransitionFromPending(Guid entryId, Func<QuarantineRecord, QuarantineRecord> transition)
    {
        while (true)
        {
            if (!_records.TryGetValue(entryId, out var current))
            {
                throw new InvalidOperationException($"Quarantine record '{entryId}' does not exist.");
            }

            if (current.Status != QuarantineStatus.Pending)
            {
                throw new InvalidOperationException(
                    $"Quarantine record '{entryId}' is {current.Status}, not Pending.");
            }

            var next = transition(current);
            if (_records.TryUpdate(entryId, next, current))
            {
                return;
            }
            // Lost the race; another thread mutated the record. Retry.
        }
    }
}
