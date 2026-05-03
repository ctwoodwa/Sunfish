namespace Sunfish.Kernel.Sync.Protocol;

/// <summary>
/// Transport substrate for the sync-daemon wire protocol. Abstracts the
/// Unix-domain-socket / named-pipe / in-memory channel plumbing so the
/// gossip scheduler and handshake ladder remain substrate-agnostic.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are responsible for length-prefixed framing (4-byte
/// big-endian u32 + CBOR payload, sync-daemon-protocol §2.2) and for tearing
/// down sockets cleanly on dispose. They are NOT responsible for the HELLO /
/// CAPABILITY_NEG / ACK handshake — that's the <c>HandshakeProtocol</c>'s job.
/// </para>
/// <para>
/// <b>Concurrency:</b> <see cref="ConnectAsync"/> and <see cref="ListenAsync"/>
/// may run concurrently on the same transport instance. Individual connections
/// returned from either path are single-owner — do not share one
/// <see cref="ISyncDaemonConnection"/> across threads.
/// </para>
/// </remarks>
public interface ISyncDaemonTransport : IAsyncDisposable
{
    /// <summary>
    /// Open an outgoing connection to the named peer.
    /// </summary>
    /// <param name="peerEndpoint">
    /// Transport-specific endpoint string. For Unix sockets, an absolute socket
    /// path. For named pipes, the pipe name. For the in-memory transport, the
    /// peer's registered name.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<ISyncDaemonConnection> ConnectAsync(string peerEndpoint, CancellationToken ct);

    /// <summary>
    /// Accept incoming connections. The returned async sequence yields one
    /// <see cref="ISyncDaemonConnection"/> per accepted peer. Cancelling
    /// <paramref name="ct"/> ends the enumeration cleanly.
    /// </summary>
    IAsyncEnumerable<ISyncDaemonConnection> ListenAsync(CancellationToken ct);
}

/// <summary>
/// One end of a sync-daemon session. Hand-in to the handshake protocol, then
/// used by the gossip/delta loops. Disposing closes the underlying socket.
/// </summary>
public interface ISyncDaemonConnection : IAsyncDisposable
{
    /// <summary>Transport-specific identifier for the remote end (for logging and tests).</summary>
    string RemoteEndpoint { get; }

    /// <summary>
    /// Serialize <paramref name="message"/> to CBOR and write one
    /// length-prefixed frame to the socket. Thread-safety is implementation
    /// defined; callers SHOULD serialize sends on a single task.
    /// </summary>
    Task SendAsync<TMessage>(TMessage message, CancellationToken ct) where TMessage : class;

    /// <summary>
    /// Read one length-prefixed frame and return the decoded message record.
    /// Returned object is one of the <c>*Message</c> record types declared in
    /// the protocol namespace. Callers pattern-match on the runtime type.
    /// </summary>
    Task<object> ReceiveAsync(CancellationToken ct);
}
