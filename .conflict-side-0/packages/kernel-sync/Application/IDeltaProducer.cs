namespace Sunfish.Kernel.Sync.Application;

/// <summary>
/// Wave 2.5 — produces the outbound CRDT-delta payload that <see cref="Gossip.GossipDaemon"/>
/// sends in a <c>DELTA_STREAM</c> frame after the GOSSIP_PING vector-clock exchange.
/// </summary>
/// <remarks>
/// <para>
/// Implementations resolve the local CRDT engine's view of a logical document
/// and call <c>ICrdtDocument.EncodeDelta(peerVectorClock)</c> to produce the
/// minimum byte sequence the peer hasn't seen yet. Returning <see langword="null"/>
/// (or an empty payload) tells the daemon "nothing to send this round" — common
/// when there are no local mutations since the last sync with this peer.
/// </para>
/// <para>
/// The contract is intentionally per-document. Phase 1 / single-active-team
/// callers can hard-wire a single document id; multi-team / multi-document
/// callers fan out across documents in the application layer (one
/// <see cref="IDeltaProducer"/> per logical document, or a composite that
/// iterates internally).
/// </para>
/// <para>
/// <b>Default registration:</b> <see cref="DependencyInjection.ServiceCollectionExtensions.AddSunfishKernelSync"/>
/// registers <see cref="NoopDeltaProducer"/> when no caller-supplied implementation
/// is present, so the gossip daemon's round loop continues to operate as
/// PING-only — preserving Wave 2.1–2.4 behavior. Anchor / local-node-host
/// register a real producer through <c>TryAddSingleton</c> before
/// <c>AddSunfishKernelSync</c>.
/// </para>
/// </remarks>
public interface IDeltaProducer
{
    /// <summary>
    /// Encode operations not yet seen by the peer with the given vector clock.
    /// </summary>
    /// <param name="documentId">Logical document identifier the peer announced
    /// during CAPABILITY_NEG. Phase 1 single-document callers may use a fixed
    /// well-known value (e.g. <c>"default"</c>).</param>
    /// <param name="peerVectorClock">The peer's CRDT vector-clock snapshot
    /// (opaque bytes — passed straight through from the inbound GOSSIP_PING
    /// payload). Empty means "encode the full history."</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The delta payload, or <see langword="null"/> if the producer
    /// has nothing to send this round.</returns>
    ValueTask<ReadOnlyMemory<byte>?> EncodeOutboundDeltaAsync(
        string documentId,
        ReadOnlyMemory<byte> peerVectorClock,
        CancellationToken ct);
}

/// <summary>
/// No-op <see cref="IDeltaProducer"/>. Returns <see langword="null"/> for every
/// call — the round loop interprets this as "no DELTA_STREAM to send this round."
/// Registered by default so existing PING-only deployments behave unchanged.
/// </summary>
public sealed class NoopDeltaProducer : IDeltaProducer
{
    /// <inheritdoc />
    public ValueTask<ReadOnlyMemory<byte>?> EncodeOutboundDeltaAsync(
        string documentId,
        ReadOnlyMemory<byte> peerVectorClock,
        CancellationToken ct) => ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);
}
