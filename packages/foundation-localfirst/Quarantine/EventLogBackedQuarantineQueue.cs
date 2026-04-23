using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Events;

namespace Sunfish.Foundation.LocalFirst.Quarantine;

/// <summary>
/// <see cref="IQuarantineQueue"/> persisted as an append-only stream of events on an
/// <see cref="IEventLog"/>. Every enqueue / promote / reject is written as a
/// <see cref="KernelEvent"/> with kind <c>quarantine-enqueued</c>, <c>quarantine-promoted</c>, or
/// <c>quarantine-rejected</c>. Current state is materialized into an in-memory index that is
/// rebuilt from a full replay when the queue is constructed — per paper §2.5 the log is the
/// source of truth and the index is a rebuildable projection.
/// </summary>
public sealed class EventLogBackedQuarantineQueue : IQuarantineQueue
{
    internal const string KindEnqueued = "quarantine-enqueued";
    internal const string KindPromoted = "quarantine-promoted";
    internal const string KindRejected = "quarantine-rejected";

    private const string StreamAuthority = "foundation-localfirst";
    private const string StreamLocalPart = "quarantine";
    private const string PayloadKey = "body";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IEventLog _log;
    private readonly ConcurrentDictionary<Guid, QuarantineRecord> _index = new();
    private readonly object _writeGate = new();

    /// <summary>
    /// Construct a queue against <paramref name="log"/>. The log is replayed synchronously to
    /// rebuild the in-memory index before the constructor returns.
    /// </summary>
    public EventLogBackedQuarantineQueue(IEventLog log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
        RehydrateFromLog();
    }

    /// <inheritdoc />
    public async Task<Guid> EnqueueAsync(QuarantineEntry entry, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var id = Guid.NewGuid();
        var envelope = new EnqueuedEnvelope(
            id,
            entry.Kind,
            entry.Stream,
            Convert.ToBase64String(entry.Payload.Span),
            entry.QueuedAt,
            entry.QueuedByActor);

        await AppendEventAsync(id, KindEnqueued, envelope, ct).ConfigureAwait(false);

        var record = new QuarantineRecord(
            id,
            entry,
            QuarantineStatus.Pending,
            entry.QueuedAt,
            entry.QueuedByActor,
            RejectionReason: null);

        _index[id] = record;
        return id;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<QuarantineRecord> ReadByStatusAsync(
        QuarantineStatus status,
        [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var record in _index.Values)
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
    public async Task PromoteAsync(Guid entryId, string actor, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(actor);
        var current = RequirePending(entryId);

        var timestamp = DateTimeOffset.UtcNow;
        var envelope = new StatusChangeEnvelope(entryId, actor, timestamp, Reason: null);
        await AppendEventAsync(entryId, KindPromoted, envelope, ct).ConfigureAwait(false);

        _index[entryId] = current with
        {
            Status = QuarantineStatus.Accepted,
            StatusChangedAt = timestamp,
            StatusChangedByActor = actor,
            RejectionReason = null,
        };
    }

    /// <inheritdoc />
    public async Task RejectAsync(Guid entryId, string actor, string reason, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(actor);
        ArgumentException.ThrowIfNullOrEmpty(reason);
        var current = RequirePending(entryId);

        var timestamp = DateTimeOffset.UtcNow;
        var envelope = new StatusChangeEnvelope(entryId, actor, timestamp, reason);
        await AppendEventAsync(entryId, KindRejected, envelope, ct).ConfigureAwait(false);

        _index[entryId] = current with
        {
            Status = QuarantineStatus.Rejected,
            StatusChangedAt = timestamp,
            StatusChangedByActor = actor,
            RejectionReason = reason,
        };
    }

    /// <inheritdoc />
    public Task<QuarantineRecord?> GetAsync(Guid entryId, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        _index.TryGetValue(entryId, out var record);
        return Task.FromResult(record);
    }

    private QuarantineRecord RequirePending(Guid entryId)
    {
        if (!_index.TryGetValue(entryId, out var current))
        {
            throw new InvalidOperationException($"Quarantine record '{entryId}' does not exist.");
        }

        if (current.Status != QuarantineStatus.Pending)
        {
            throw new InvalidOperationException(
                $"Quarantine record '{entryId}' is {current.Status}, not Pending.");
        }

        return current;
    }

    private Task AppendEventAsync<T>(Guid entryId, string kind, T envelope, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(envelope, JsonOptions);
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            [PayloadKey] = Convert.ToBase64String(bytes),
        };

        var evt = new KernelEvent(
            EventId.NewId(),
            new EntityId("quarantine", StreamAuthority, entryId.ToString("D")),
            kind,
            DateTimeOffset.UtcNow,
            payload);

        // Serialize writes so replay order matches logical order even under concurrent callers.
        Task<ulong> append;
        lock (_writeGate)
        {
            append = _log.AppendAsync(evt, ct);
        }

        return append;
    }

    private void RehydrateFromLog()
    {
        _index.Clear();
        var enumerator = _log.ReadAfterAsync(0UL, CancellationToken.None).GetAsyncEnumerator();
        try
        {
            while (WaitFor(enumerator.MoveNextAsync()))
            {
                Apply(enumerator.Current.Event);
            }
        }
        finally
        {
            WaitFor(enumerator.DisposeAsync());
        }
    }

    private void Apply(KernelEvent evt)
    {
        if (!evt.Payload.TryGetValue(PayloadKey, out var raw) || raw is not string base64)
        {
            return;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return;
        }

        switch (evt.Kind)
        {
            case KindEnqueued:
            {
                var body = JsonSerializer.Deserialize<EnqueuedEnvelope>(bytes, JsonOptions);
                if (body is null) return;
                var payload = Convert.FromBase64String(body.PayloadBase64);
                var entry = new QuarantineEntry(body.Kind, body.Stream, payload, body.QueuedAt, body.QueuedByActor);
                _index[body.Id] = new QuarantineRecord(
                    body.Id,
                    entry,
                    QuarantineStatus.Pending,
                    body.QueuedAt,
                    body.QueuedByActor,
                    RejectionReason: null);
                break;
            }

            case KindPromoted:
            {
                var body = JsonSerializer.Deserialize<StatusChangeEnvelope>(bytes, JsonOptions);
                if (body is null || !_index.TryGetValue(body.EntryId, out var current)) return;
                _index[body.EntryId] = current with
                {
                    Status = QuarantineStatus.Accepted,
                    StatusChangedAt = body.ChangedAt,
                    StatusChangedByActor = body.Actor,
                    RejectionReason = null,
                };
                break;
            }

            case KindRejected:
            {
                var body = JsonSerializer.Deserialize<StatusChangeEnvelope>(bytes, JsonOptions);
                if (body is null || !_index.TryGetValue(body.EntryId, out var current)) return;
                _index[body.EntryId] = current with
                {
                    Status = QuarantineStatus.Rejected,
                    StatusChangedAt = body.ChangedAt,
                    StatusChangedByActor = body.Actor,
                    RejectionReason = body.Reason,
                };
                break;
            }
        }
    }

    private static bool WaitFor(ValueTask<bool> task)
    {
        if (task.IsCompletedSuccessfully)
        {
            return task.Result;
        }
        return task.AsTask().GetAwaiter().GetResult();
    }

    private static void WaitFor(ValueTask task)
    {
        if (task.IsCompletedSuccessfully)
        {
            return;
        }
        task.AsTask().GetAwaiter().GetResult();
    }

    private sealed record EnqueuedEnvelope(
        Guid Id,
        string Kind,
        string Stream,
        string PayloadBase64,
        DateTimeOffset QueuedAt,
        string QueuedByActor);

    private sealed record StatusChangeEnvelope(
        Guid EntryId,
        string Actor,
        DateTimeOffset ChangedAt,
        string? Reason);
}
