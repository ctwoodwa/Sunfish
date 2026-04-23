namespace Sunfish.Kernel.Crdt;

/// <summary>
/// CRDT document root contract. Paper §2.2: "AP-class data ... leaderless replicated system;
/// any node can accept writes, convergence is guaranteed by CRDT merge semantics."
/// </summary>
/// <remarks>
/// <para>
/// Per ADR 0028, the concrete backend behind this interface is selected per deployment.
/// The contract intentionally hides wire encoding from callers so that a future engine
/// swap (Loro → Yjs/yrs, or stub → Loro) does not ripple into application code.
/// </para>
/// <para>
/// <b>Identity:</b> <see cref="DocumentId"/> is stable across replicas. Peers exchanging
/// deltas/snapshots for the same logical document MUST share the same <see cref="DocumentId"/>.
/// </para>
/// <para>
/// <b>Disposal:</b> implementations may hold native resources (e.g. an underlying
/// Loro <c>LoroDoc</c> handle). Always dispose asynchronously when finished.
/// </para>
/// </remarks>
public interface ICrdtDocument : IAsyncDisposable
{
    /// <summary>
    /// Stable document identifier. Peers exchanging deltas and snapshots for the same
    /// logical document MUST share this identifier.
    /// </summary>
    string DocumentId { get; }

    /// <summary>
    /// Return (or lazily create) the text container registered under <paramref name="name"/>.
    /// The container is owned by the document; do not dispose it independently.
    /// </summary>
    ICrdtText GetText(string name);

    /// <summary>
    /// Return (or lazily create) the map container registered under <paramref name="name"/>.
    /// The container is owned by the document.
    /// </summary>
    ICrdtMap GetMap(string name);

    /// <summary>
    /// Return (or lazily create) the list container registered under <paramref name="name"/>.
    /// The container is owned by the document.
    /// </summary>
    ICrdtList GetList(string name);

    /// <summary>Serialize current state to a binary snapshot for wire transport.</summary>
    ReadOnlyMemory<byte> ToSnapshot();

    /// <summary>Apply a snapshot from another peer. Snapshots are idempotent; applying the same snapshot twice is a no-op.</summary>
    void ApplySnapshot(ReadOnlyMemory<byte> snapshot);

    /// <summary>
    /// Encode operations not yet seen by the peer with the given vector clock.
    /// Pass <see cref="ReadOnlyMemory{T}.Empty"/> to encode the full history.
    /// </summary>
    ReadOnlyMemory<byte> EncodeDelta(ReadOnlyMemory<byte> peerVectorClock);

    /// <summary>Apply a delta encoding from a peer. Deltas are idempotent.</summary>
    void ApplyDelta(ReadOnlyMemory<byte> delta);

    /// <summary>
    /// Current vector-clock snapshot for capability-negotiation exchange. Paper §6.1.
    /// Opaque to callers — pass it back into <see cref="EncodeDelta"/> on the peer side.
    /// </summary>
    ReadOnlyMemory<byte> VectorClock { get; }
}
