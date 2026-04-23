namespace Sunfish.Kernel.Events;

/// <summary>
/// Append-only, event-sourced log. Paper §2.5: "never modifies past records — only appends new events.
/// Current aggregate state is derived by replaying the event log from a known snapshot." Paper §8 adds:
/// snapshots are a performance optimization; the log is the source of truth.
/// </summary>
/// <remarks>
/// <para>
/// <b>Contract vs. bus:</b> <see cref="IEventBus"/> delivers live events with per-entity ordering
/// and idempotent publish. <see cref="IEventLog"/> is the durable append-only substrate that an
/// event bus (or projections, or rehydration pipelines) reads from and writes to. The two can
/// compose — a future persistent event-bus backend appends to an <see cref="IEventLog"/> as part
/// of its publish path — but the log is deliberately a standalone primitive so snapshots and
/// replay work without a bus.
/// </para>
/// <para>
/// <b>Sequence numbers:</b> monotonically increasing within an epoch, starting at 1. A value of
/// zero is reserved to mean "before the first event" — pass it to
/// <see cref="ReadAfterAsync"/> to read the whole log.
/// </para>
/// <para>
/// <b>Snapshot semantics:</b> snapshots are keyed by
/// <c>(AggregateId, EpochId, SchemaVersion)</c>; the newest (by <see cref="Snapshot.CreatedAt"/>)
/// wins on read. They can be safely deleted and regenerated without affecting correctness; the log
/// remains authoritative.
/// </para>
/// </remarks>
public interface IEventLog
{
    /// <summary>Append an event. Returns its assigned sequence number.</summary>
    /// <param name="evt">The event to persist. The log does not verify signatures or other envelope properties — those are the publisher's responsibility.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The sequence number assigned to <paramref name="evt"/>. Always strictly greater than any previously-returned sequence number on the same log.</returns>
    Task<ulong> AppendAsync(KernelEvent evt, CancellationToken ct);

    /// <summary>Read events after the given sequence (exclusive). Use <see cref="ulong.MinValue"/> for all.</summary>
    /// <param name="afterSeq">Exclusive lower bound. Zero yields every event.</param>
    /// <param name="ct">Cancellation token. Cancelling ends the enumeration cleanly.</param>
    IAsyncEnumerable<LogEntry> ReadAfterAsync(ulong afterSeq, CancellationToken ct);

    /// <summary>Read a specific range of events, inclusive on both ends.</summary>
    /// <param name="fromSeq">Inclusive lower bound.</param>
    /// <param name="toSeqInclusive">Inclusive upper bound.</param>
    /// <param name="ct">Cancellation token.</param>
    IAsyncEnumerable<LogEntry> ReadRangeAsync(ulong fromSeq, ulong toSeqInclusive, CancellationToken ct);

    /// <summary>Current highest sequence number. Zero if log is empty.</summary>
    ulong CurrentSequence { get; }

    /// <summary>
    /// Write a snapshot for the given aggregate. Per paper §8, snapshots are a performance
    /// optimization — the log is the source of truth. Multiple snapshots for the same
    /// <c>(AggregateId, EpochId, SchemaVersion)</c> tuple are retained; the newest wins on
    /// read but callers are free to prune older ones.
    /// </summary>
    Task WriteSnapshotAsync(Snapshot snapshot, CancellationToken ct);

    /// <summary>Read the most recent snapshot for an aggregate; returns null if none exists.</summary>
    Task<Snapshot?> ReadLatestSnapshotAsync(string aggregateId, string epochId, string schemaVersion, CancellationToken ct);
}

/// <summary>An event paired with its assigned sequence number on the log.</summary>
/// <param name="Sequence">Monotonic sequence number assigned by <see cref="IEventLog.AppendAsync"/>.</param>
/// <param name="Event">The event itself.</param>
public sealed record LogEntry(ulong Sequence, KernelEvent Event);

/// <summary>
/// A point-in-time snapshot of an aggregate's derived state, scoped to a specific epoch and schema
/// version. Paper §8.2.
/// </summary>
/// <param name="AggregateId">Caller-chosen aggregate identifier (often the string form of an <c>EntityId</c>, but the log does not require that).</param>
/// <param name="EpochId">Epoch the snapshot was produced under. Snapshots from other epochs are discarded on rehydration.</param>
/// <param name="SchemaVersion">Schema version the <paramref name="Payload"/> was serialized against. Snapshots for other schema versions are discarded on rehydration.</param>
/// <param name="LastEventSeq">The last <see cref="LogEntry.Sequence"/> that had been applied when this snapshot was produced.</param>
/// <param name="Payload">Caller-serialized aggregate state. The log does not interpret the bytes.</param>
/// <param name="CreatedAt">Wall-clock time at which the snapshot was written. Used as the newest-wins tiebreaker on read.</param>
public sealed record Snapshot(
    string AggregateId,
    string EpochId,
    string SchemaVersion,
    ulong LastEventSeq,
    byte[] Payload,
    DateTimeOffset CreatedAt);
