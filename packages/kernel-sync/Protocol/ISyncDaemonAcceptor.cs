using System.Net.WebSockets;

namespace Sunfish.Kernel.Sync.Protocol;

/// <summary>
/// Wave 5.3.C accept-side handoff for pre-upgraded transports. The Bridge
/// tenant-child's Kestrel performs the WebSocket upgrade (and equivalent
/// platform-specific moves on other substrates) and then hands the resulting
/// <see cref="WebSocket"/> handle to an implementation of this interface,
/// which wraps it in a transport-specific connection object and feeds it into
/// the sync daemon's session-handling pipeline.
/// </summary>
/// <remarks>
/// <para>
/// Wave 5.3.C ships only the transport + the accept surface. The real session
/// pipeline (HELLO handshake → DELTA_STREAM pump) lands in 5.3.D, which will
/// replace <see cref="LoggingSyncDaemonAcceptor"/> with the production
/// implementation that wires accepted connections into the gossip scheduler.
/// </para>
/// <para>
/// Kept intentionally narrow: the implementation is responsible for wrapping
/// the <see cref="WebSocket"/> in a <see cref="WebSocketSyncDaemonTransport"/>
/// (or equivalent) and for driving the connection's lifetime. The caller
/// (e.g. <c>HostedWebSocketEndpoint</c>) awaits <see cref="AcceptAsync"/> for
/// the duration of the WebSocket — returning from <c>AcceptAsync</c> signals
/// that the handshake or session has ended and the host-side middleware may
/// complete the request.
/// </para>
/// </remarks>
public interface ISyncDaemonAcceptor
{
    /// <summary>
    /// Accept an inbound sync-daemon connection carried over a pre-upgraded
    /// WebSocket. The transport implementation wraps the raw handle and hands
    /// it to the sync daemon's session-handling pipeline. Returns once the
    /// session has terminated cleanly or been cancelled; the WebSocket is
    /// closed by the implementation before return.
    /// </summary>
    /// <param name="ws">An already-upgraded <see cref="WebSocket"/>.</param>
    /// <param name="ct">Cancellation token — fires on host shutdown.</param>
    Task AcceptAsync(WebSocket ws, CancellationToken ct);
}
