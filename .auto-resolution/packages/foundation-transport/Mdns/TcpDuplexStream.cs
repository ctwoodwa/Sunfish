using System;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Transport.Mdns;

/// <summary>
/// <see cref="IDuplexStream"/> backed by a connected
/// <see cref="System.Net.Sockets.TcpClient"/>. The transport tier
/// owns the socket lifetime; <see cref="DisposeAsync"/> closes it.
/// </summary>
internal sealed class TcpDuplexStream : IDuplexStream
{
    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private bool _disposed;

    public TcpDuplexStream(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
    }

    public Stream Stream => _stream;

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct) =>
        await _stream.ReadAsync(buffer, ct).ConfigureAwait(false);

    public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct) =>
        await _stream.WriteAsync(buffer, ct).ConfigureAwait(false);

    public Task FlushAsync(CancellationToken ct) => _stream.FlushAsync(ct);

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        try { _stream.Dispose(); } catch { /* best-effort close */ }
        try { _client.Dispose(); } catch { /* best-effort close */ }
        return ValueTask.CompletedTask;
    }
}
