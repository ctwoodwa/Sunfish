namespace Sunfish.Kernel.Crdt.SnapshotScheduling;

/// <summary>
/// Paper §9 mitigation 3 — <b>Periodic shallow snapshots</b>.
/// </summary>
/// <remarks>
/// A shallow snapshot discards op history, keeping only current state + recent vector
/// clocks. It is reserved for well-understood document types where long-term mergeability
/// is less critical than bounded storage (paper §9).
/// </remarks>
public interface IShallowSnapshotPolicy
{
    /// <summary>
    /// Decide whether the document should have a shallow snapshot taken now.
    /// </summary>
    /// <param name="doc">The document under evaluation. Implementations may inspect its
    /// <see cref="ICrdtDocument.DocumentId"/> to apply per-document tuning.</param>
    /// <param name="stats">Growth statistics observed by the caller.</param>
    /// <param name="now">The current time, injected for testability.</param>
    bool ShouldTakeShallowSnapshot(ICrdtDocument doc, DocumentStatistics stats, DateTimeOffset now);
}

/// <summary>Observed growth statistics for a document, supplied to an <see cref="IShallowSnapshotPolicy"/>.</summary>
/// <param name="OperationCount">Number of operations observed since document creation (or last snapshot).</param>
/// <param name="ByteSize">Current serialized snapshot size in bytes.</param>
/// <param name="LastOperationAt">Timestamp of the most recent operation applied to the document.</param>
/// <param name="LastShallowSnapshotAt">Timestamp of the most recent shallow snapshot, or <c>null</c> if none has ever been taken.</param>
public sealed record DocumentStatistics(
    ulong OperationCount,
    ulong ByteSize,
    DateTimeOffset LastOperationAt,
    DateTimeOffset? LastShallowSnapshotAt);
