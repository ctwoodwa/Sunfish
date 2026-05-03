namespace Sunfish.Federation.Common;

/// <summary>
/// Lets a host-process listener (for example an ASP.NET Core endpoint) route an incoming
/// <see cref="SyncEnvelope"/> to the handler registered locally for the envelope's
/// <see cref="SyncEnvelope.ToPeer"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ISyncTransport"/> owns outbound <c>SendAsync</c> and handler registration, but the
/// <c>SendAsync</c> side is only usable for same-process dispatch (see
/// <see cref="InMemorySyncTransport"/>). Over a real network transport, inbound envelopes arrive
/// via the network listener (HTTP POST, WebSocket frame, etc.) and must be routed to the locally
/// registered handler. <see cref="ILocalHandlerDispatcher"/> is that seam: listeners depend on this
/// interface, and every transport implementation exposes it so the same dispatch path works
/// regardless of the underlying wire.
/// </para>
/// <para>
/// Implementations must throw <see cref="InvalidOperationException"/> when no handler is
/// registered for the envelope's target peer, so listeners can translate that into a meaningful
/// protocol error (for example HTTP 404).
/// </para>
/// </remarks>
public interface ILocalHandlerDispatcher
{
    /// <summary>
    /// Dispatches <paramref name="incoming"/> to the handler registered for
    /// <see cref="SyncEnvelope.ToPeer"/> and returns the signed reply.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no handler is registered for the target peer.
    /// </exception>
    ValueTask<SyncEnvelope> DispatchAsync(SyncEnvelope incoming, CancellationToken ct = default);
}
