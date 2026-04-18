namespace Sunfish.Federation.Common;

/// <summary>
/// Abstraction over the wire transport used to exchange <see cref="SyncEnvelope"/> messages between
/// federation peers. Implementations include <see cref="InMemorySyncTransport"/> (testing / same-process)
/// and an HTTP+JSON transport (Task D-3).
/// </summary>
public interface ISyncTransport
{
    /// <summary>
    /// Delivers <paramref name="envelope"/> to <paramref name="target"/> and returns the peer's reply.
    /// Request/reply semantics follow the message kind — some kinds expect a substantive reply,
    /// others a simple acknowledgement.
    /// </summary>
    ValueTask<SyncEnvelope> SendAsync(PeerDescriptor target, SyncEnvelope envelope, CancellationToken ct);

    /// <summary>
    /// Registers a handler for envelopes addressed to <paramref name="local"/>. The returned
    /// <see cref="IDisposable"/> unregisters the handler when disposed. A transport may only have
    /// one handler per peer id at a time.
    /// </summary>
    IDisposable RegisterHandler(PeerId local, Func<SyncEnvelope, ValueTask<SyncEnvelope>> handler);
}
