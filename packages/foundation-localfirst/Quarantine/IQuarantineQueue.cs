namespace Sunfish.Foundation.LocalFirst.Quarantine;

/// <summary>
/// Paper §11.2 Layer 4 quarantine queue. Offline writes are held here pending
/// validation against current team state and policy. Quarantined writes are
/// never discarded — they are reviewed and either promoted (accepted) or
/// explicitly rejected with a recorded reason.
/// </summary>
public interface IQuarantineQueue
{
    /// <summary>Quarantine a new offline write. Returns the assigned record id.</summary>
    Task<Guid> EnqueueAsync(QuarantineEntry entry, CancellationToken ct);

    /// <summary>Stream quarantine records with the given status, in insertion order.</summary>
    IAsyncEnumerable<QuarantineRecord> ReadByStatusAsync(QuarantineStatus status, CancellationToken ct);

    /// <summary>
    /// Promote a <see cref="QuarantineStatus.Pending"/> record to
    /// <see cref="QuarantineStatus.Accepted"/>. Throws
    /// <see cref="InvalidOperationException"/> if the record is not pending or does not exist.
    /// </summary>
    Task PromoteAsync(Guid entryId, string actor, CancellationToken ct);

    /// <summary>
    /// Reject a <see cref="QuarantineStatus.Pending"/> record with a recorded
    /// <paramref name="reason"/>. Throws <see cref="InvalidOperationException"/> if the record is
    /// not pending or does not exist.
    /// </summary>
    Task RejectAsync(Guid entryId, string actor, string reason, CancellationToken ct);

    /// <summary>Lookup a quarantine record by id. Returns <c>null</c> if unknown.</summary>
    Task<QuarantineRecord?> GetAsync(Guid entryId, CancellationToken ct);
}

/// <summary>Lifecycle state of a quarantined write.</summary>
public enum QuarantineStatus
{
    /// <summary>Awaiting review.</summary>
    Pending,
    /// <summary>Promoted into the authoritative stream.</summary>
    Accepted,
    /// <summary>Explicitly rejected with a recorded reason.</summary>
    Rejected,
}

/// <summary>
/// The payload offered for quarantine. Opaque bytes are carried alongside a
/// caller-defined <paramref name="Kind"/> and <paramref name="Stream"/> so reviewers can route
/// records without deserializing.
/// </summary>
/// <param name="Kind">Caller-defined discriminator (e.g. <c>lease.created</c>).</param>
/// <param name="Stream">Logical stream / aggregate the write belongs to.</param>
/// <param name="Payload">Opaque serialized write. The queue does not interpret the bytes.</param>
/// <param name="QueuedAt">Wall-clock time at which the write was quarantined.</param>
/// <param name="QueuedByActor">Identity of the actor whose offline write is being quarantined.</param>
public sealed record QuarantineEntry(
    string Kind,
    string Stream,
    ReadOnlyMemory<byte> Payload,
    DateTimeOffset QueuedAt,
    string QueuedByActor);

/// <summary>A quarantine record: the original <see cref="QuarantineEntry"/> plus its lifecycle state.</summary>
/// <param name="Id">Record identifier assigned at enqueue time.</param>
/// <param name="Entry">The original offered entry.</param>
/// <param name="Status">Current lifecycle state.</param>
/// <param name="StatusChangedAt">Wall-clock time at which <see cref="Status"/> was last changed (enqueue counts as the initial change).</param>
/// <param name="StatusChangedByActor">Actor responsible for the last status transition. Non-null for Accepted / Rejected.</param>
/// <param name="RejectionReason">Non-null iff <see cref="Status"/> is <see cref="QuarantineStatus.Rejected"/>.</param>
public sealed record QuarantineRecord(
    Guid Id,
    QuarantineEntry Entry,
    QuarantineStatus Status,
    DateTimeOffset StatusChangedAt,
    string? StatusChangedByActor,
    string? RejectionReason);
