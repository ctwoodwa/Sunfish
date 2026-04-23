namespace Sunfish.Kernel.Crdt.SnapshotScheduling;

/// <summary>
/// The conservative default policy documented in paper §9:
/// <i>"full history is retained, relying on library-level compaction."</i>
/// Shallow snapshots are opt-in per document type.
/// </summary>
public sealed class NeverShallowSnapshotPolicy : IShallowSnapshotPolicy
{
    /// <inheritdoc />
    public bool ShouldTakeShallowSnapshot(ICrdtDocument doc, DocumentStatistics stats, DateTimeOffset now) => false;
}
