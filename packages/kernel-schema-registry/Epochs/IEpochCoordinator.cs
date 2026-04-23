namespace Sunfish.Kernel.SchemaRegistry.Epochs;

/// <summary>
/// Schema-epoch coordinator per paper §7.4. Announces and manages epoch transitions
/// for truly breaking schema changes.
/// </summary>
/// <remarks>
/// <para>
/// Paper §7.4: <i>"A new epoch is announced and agreed by quorum. A background
/// copy-transform job reads the existing log, applies lenses and upcasters, and
/// writes to a new epoch stream. Nodes cut over to the new epoch as they upgrade."</i>
/// </para>
/// <para>
/// <b>Lease note:</b> real epoch transitions require distributed lease quorum (see
/// <c>packages/kernel-lease</c>, Wave 2.3). This coordinator currently uses a local,
/// single-process stand-in — sufficient for reasoning about the state-machine shape and
/// for unit/integration tests of the schema-registry surface. Wave 2.3 replaces the
/// local stand-in with a Flease-backed distributed lease.
/// </para>
/// </remarks>
public interface IEpochCoordinator
{
    /// <summary>Identifier of the current (latest non-frozen) epoch.</summary>
    string CurrentEpochId { get; }

    /// <summary>All epochs known to this coordinator, oldest first.</summary>
    IReadOnlyList<EpochRecord> Epochs { get; }

    /// <summary>
    /// Announce a new epoch transition. The new epoch enters
    /// <see cref="EpochStatus.Announced"/> status and the previous epoch remains active
    /// until every active node records a cutover (see <see cref="RecordNodeCutoverAsync"/>)
    /// and a caller invokes <see cref="FreezeEpochAsync"/>.
    /// </summary>
    /// <param name="reasonSummary">Human-readable reason for the epoch bump (shown in audit logs).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The id of the newly-announced epoch.</returns>
    Task<string> AnnounceEpochAsync(string reasonSummary, CancellationToken ct);

    /// <summary>
    /// Record that <paramref name="nodeId"/> has completed its upgrade and is now
    /// operating against <paramref name="epochId"/>. Duplicate calls are idempotent.
    /// </summary>
    /// <param name="nodeId">Identifier of the node that cut over.</param>
    /// <param name="epochId">The epoch the node has adopted — must be a known epoch.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RecordNodeCutoverAsync(string nodeId, string epochId, CancellationToken ct);

    /// <summary>
    /// Mark <paramref name="epochId"/> as <see cref="EpochStatus.Frozen"/> — read-only.
    /// Callers should only freeze an epoch once every active node has cut over to a
    /// successor (paper §7.4).
    /// </summary>
    /// <param name="epochId">The epoch to freeze. Must be a known epoch and must not already be frozen.</param>
    /// <param name="ct">Cancellation token.</param>
    Task FreezeEpochAsync(string epochId, CancellationToken ct);

    /// <summary>Raised after a new epoch is announced and stored.</summary>
    event EventHandler<EpochAnnouncedEventArgs>? EpochAnnounced;

    /// <summary>Raised after an epoch transitions to <see cref="EpochStatus.Frozen"/>.</summary>
    event EventHandler<EpochFrozenEventArgs>? EpochFrozen;
}

/// <summary>Status of a schema epoch.</summary>
public enum EpochStatus
{
    /// <summary>Freshly announced; not yet the current epoch.</summary>
    Announced,

    /// <summary>Accepting new writes; current epoch.</summary>
    Active,

    /// <summary>Read-only. No new writes accepted; retained for audit / rehydration of older snapshots.</summary>
    Frozen,
}

/// <summary>Record of a schema epoch as stored by <see cref="IEpochCoordinator"/>.</summary>
/// <param name="Id">Unique epoch id (typically <c>epoch-1</c>, <c>epoch-2</c>, …).</param>
/// <param name="PreviousId">Id of the predecessor epoch; empty string for the genesis epoch.</param>
/// <param name="AnnouncedAt">Wall-clock time the epoch was announced (or instantiated, for the genesis epoch).</param>
/// <param name="Status">Current status — <see cref="EpochStatus.Announced"/>, <see cref="EpochStatus.Active"/>, or <see cref="EpochStatus.Frozen"/>.</param>
/// <param name="CutoverNodes">Identifiers of nodes that have reported cutover to this epoch.</param>
/// <param name="ReasonSummary">Human-readable reason provided at announcement time.</param>
public sealed record EpochRecord(
    string Id,
    string PreviousId,
    DateTimeOffset AnnouncedAt,
    EpochStatus Status,
    IReadOnlyList<string> CutoverNodes,
    string ReasonSummary);

/// <summary>Event args for <see cref="IEpochCoordinator.EpochAnnounced"/>.</summary>
public sealed class EpochAnnouncedEventArgs : EventArgs
{
    /// <summary>The newly-announced epoch.</summary>
    public EpochRecord Epoch { get; }

    /// <summary>Create args wrapping <paramref name="epoch"/>.</summary>
    public EpochAnnouncedEventArgs(EpochRecord epoch)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        Epoch = epoch;
    }
}

/// <summary>Event args for <see cref="IEpochCoordinator.EpochFrozen"/>.</summary>
public sealed class EpochFrozenEventArgs : EventArgs
{
    /// <summary>The frozen epoch.</summary>
    public EpochRecord Epoch { get; }

    /// <summary>Create args wrapping <paramref name="epoch"/>.</summary>
    public EpochFrozenEventArgs(EpochRecord epoch)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        Epoch = epoch;
    }
}
