namespace Sunfish.Kernel.Sync.Application;

/// <summary>
/// Wave 2.5 — applies an inbound CRDT-delta payload received via
/// <c>DELTA_STREAM</c>. <see cref="Gossip.GossipDaemon"/> dispatches each
/// inbound delta to the registered sink after the per-peer rate limiter
/// admits the frame.
/// </summary>
/// <remarks>
/// <para>
/// Implementations resolve the local CRDT engine's view of a logical document
/// and call <c>ICrdtDocument.ApplyDelta(payload)</c>. CRDT semantics guarantee
/// idempotence (applying the same delta twice is a no-op) and commutativity
/// (delta ordering across peers does not matter), so the sink does not need
/// to coordinate with concurrent producers.
/// </para>
/// <para>
/// <b>Error policy:</b> implementations should treat malformed deltas as
/// recoverable — log + drop the frame and return rather than throwing.
/// Throwing here aborts the round and triggers per-peer dead-peer backoff,
/// which over-penalizes a peer for a single bad frame.
/// </para>
/// <para>
/// <b>Default registration:</b> <see cref="DependencyInjection.ServiceCollectionExtensions.AddSunfishKernelSync"/>
/// registers <see cref="NoopDeltaSink"/> when no caller-supplied implementation
/// is present. Anchor / local-node-host register a real sink through
/// <c>TryAddSingleton</c> before <c>AddSunfishKernelSync</c>.
/// </para>
/// </remarks>
public interface IDeltaSink
{
    /// <summary>
    /// Apply a delta payload received from a peer to the local CRDT document.
    /// </summary>
    /// <param name="documentId">Logical document identifier carried in the
    /// inbound <c>DELTA_STREAM</c> envelope's <c>StreamId</c> field.</param>
    /// <param name="opSequence">Monotonic per-stream sequence the peer assigned
    /// to this frame. Sinks may use it for replay detection or back-pressure
    /// observability; the CRDT layer does not require it.</param>
    /// <param name="delta">The delta payload as encoded by the peer's
    /// <see cref="IDeltaProducer"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    ValueTask ApplyInboundDeltaAsync(
        string documentId,
        ulong opSequence,
        ReadOnlyMemory<byte> delta,
        CancellationToken ct);
}

/// <summary>
/// No-op <see cref="IDeltaSink"/>. Discards every inbound delta — the round
/// loop continues without applying anything to a local CRDT. Registered by
/// default so existing PING-only deployments behave unchanged.
/// </summary>
public sealed class NoopDeltaSink : IDeltaSink
{
    /// <inheritdoc />
    public ValueTask ApplyInboundDeltaAsync(
        string documentId,
        ulong opSequence,
        ReadOnlyMemory<byte> delta,
        CancellationToken ct) => ValueTask.CompletedTask;
}
