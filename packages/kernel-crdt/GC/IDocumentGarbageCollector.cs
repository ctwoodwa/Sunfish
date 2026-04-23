using Sunfish.Kernel.Crdt.SnapshotScheduling;

namespace Sunfish.Kernel.Crdt.GarbageCollection;

/// <summary>
/// Convenience facade tying paper §9 sharding + shallow-snapshot mitigations together.
/// </summary>
/// <remarks>
/// Hosts that do not want to orchestrate sharding and snapshot policies directly can
/// register a single <see cref="IDocumentGarbageCollector"/> and call
/// <see cref="CollectAsync"/> on a cadence. The collector picks the right mitigation
/// strategy based on the document's registered policy.
/// </remarks>
public interface IDocumentGarbageCollector
{
    /// <summary>
    /// Evaluate a document for growth-mitigation and apply the appropriate strategy.
    /// Documents must be registered with the underlying <see cref="IShallowSnapshotManager"/>
    /// before they can be collected.
    /// </summary>
    Task<GcResult> CollectAsync(ICrdtDocument doc, CancellationToken ct = default);
}

/// <summary>Outcome of a single <see cref="IDocumentGarbageCollector.CollectAsync"/> call.</summary>
/// <param name="DocumentId">Identifier of the document that was evaluated.</param>
/// <param name="AppliedStrategy">Which mitigation, if any, was applied.</param>
/// <param name="BytesBefore">Serialized byte size before collection.</param>
/// <param name="BytesAfter">Serialized byte size after collection.</param>
/// <param name="At">Timestamp at which the collection completed.</param>
public sealed record GcResult(
    string DocumentId,
    GcStrategy AppliedStrategy,
    ulong BytesBefore,
    ulong BytesAfter,
    DateTimeOffset At);

/// <summary>Growth-mitigation strategy applied by <see cref="IDocumentGarbageCollector"/>.</summary>
public enum GcStrategy
{
    /// <summary>No action required; document is within budget or policy returned <c>false</c>.</summary>
    None,

    /// <summary>A shallow snapshot was taken (paper §9 mitigation 3).</summary>
    ShallowSnapshot,

    /// <summary>
    /// Library-level compaction was triggered (paper §9 mitigation 1). Currently a no-op
    /// on the stub backend; real compaction lands when the Loro/Yjs backend swaps in
    /// (ADR 0028).
    /// </summary>
    LibraryCompaction,
}
