namespace Sunfish.Kernel.Crdt.Sharding;

/// <summary>
/// Paper §9 mitigation 2 — <b>Application-level document sharding</b>.
/// </summary>
/// <remarks>
/// <para>
/// A logical document composed of sub-documents registered under a map key. Retiring
/// or archiving a section becomes a key deletion, allowing the CRDT engine to
/// garbage-collect that section's content without affecting the rest of the document
/// (paper §9).
/// </para>
/// <para>
/// Implementations are expected to hold a parent <see cref="ICrdtDocument"/> whose root
/// is a map (<see cref="Shards"/>) of shard-keys to serialized sub-document snapshots.
/// Nested sub-documents are materialized lazily on <see cref="GetOrCreateShard"/>,
/// and their state is persisted back into the parent map on disposal of the
/// sharded document.
/// </para>
/// </remarks>
public interface IShardedDocument : IAsyncDisposable
{
    /// <summary>Stable identifier of the parent (logical) document.</summary>
    string DocumentId { get; }

    /// <summary>
    /// The shard-index map: keys are shard identifiers, values are serialized
    /// sub-document snapshots (opaque bytes owned by the CRDT engine).
    /// </summary>
    ICrdtMap Shards { get; }

    /// <summary>
    /// Return (or lazily create) the sub-document registered under <paramref name="shardKey"/>.
    /// Repeated calls with the same key return the same instance for the lifetime of this
    /// <see cref="IShardedDocument"/>.
    /// </summary>
    ICrdtDocument GetOrCreateShard(string shardKey);

    /// <summary>
    /// Retire (archive) the shard identified by <paramref name="shardKey"/>. The shard is
    /// removed from <see cref="Shards"/>, allowing the CRDT engine to garbage-collect its
    /// content. Returns <c>true</c> if the shard existed, <c>false</c> otherwise.
    /// </summary>
    bool RetireShard(string shardKey);

    /// <summary>Snapshot of active shard-keys at call time.</summary>
    IReadOnlyList<string> ActiveShardKeys { get; }

    /// <summary>Serialize all active shards + the sharding map into a single snapshot.</summary>
    ReadOnlyMemory<byte> ToSnapshot();

    /// <summary>
    /// Restore sharding state from a snapshot previously produced by <see cref="ToSnapshot"/>.
    /// Idempotent: applying the same snapshot twice is a no-op.
    /// </summary>
    void ApplySnapshot(ReadOnlyMemory<byte> snapshot);
}
