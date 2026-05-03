using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Transport;

/// <summary>
/// A connected, full-duplex byte stream between two peers. Implementations
/// are returned by <see cref="IPeerTransport.ConnectAsync"/> and represent
/// a live transport-tier session (mDNS-resolved socket, mesh-VPN tunnel,
/// or Bridge-relay HTTPS pipe).
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="Stream"/> property exposes the underlying duplex stream
/// for callers that want raw <see cref="System.IO.Stream"/> APIs (length-
/// framed parsers, gRPC, etc.). The <see cref="ReadAsync"/> /
/// <see cref="WriteAsync"/> / <see cref="FlushAsync"/> methods are the
/// canonical async entry-points and SHOULD be preferred — they let the
/// implementation back the stream with a non-<see cref="Stream"/> primitive
/// (e.g., a libp2p substream, a QUIC unidirectional stream) without
/// breaking callers.
/// </para>
/// <para>
/// Disposal MUST be idempotent. Implementations release transport-tier
/// resources (mesh-VPN tunnel, HTTPS pipe, raw socket) in
/// <see cref="IAsyncDisposable.DisposeAsync"/>.
/// </para>
/// </remarks>
public interface IDuplexStream : IAsyncDisposable
{
    /// <summary>
    /// The underlying <see cref="System.IO.Stream"/> exposing read + write
    /// halves of the duplex session. Provided for raw-stream parser
    /// interop; prefer the typed async methods on this interface.
    /// </summary>
    Stream Stream { get; }

    /// <summary>
    /// Reads up to <paramref name="buffer"/>.Length bytes from the peer
    /// into <paramref name="buffer"/>. Returns the count read; 0 indicates
    /// the peer half-closed the stream.
    /// </summary>
    Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct);

    /// <summary>
    /// Writes <paramref name="buffer"/> to the peer. Completion does NOT
    /// guarantee the peer has received the bytes — call
    /// <see cref="FlushAsync"/> for that.
    /// </summary>
    Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct);

    /// <summary>Flushes any buffered writes to the underlying transport.</summary>
    Task FlushAsync(CancellationToken ct);
}
