using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Sunfish.Foundation.Transport.Relay;

/// <summary>
/// <see cref="IDuplexStream"/> backed by a connected
/// <see cref="ClientWebSocket"/>. Writes go out as binary
/// <see cref="WebSocketMessageType.Binary"/> frames; reads return
/// concatenated frame payloads. The <see cref="Stream"/> property
/// returns a thin <see cref="System.IO.Stream"/> adapter for callers
/// that need raw-stream interop, but the typed async methods on
/// <see cref="IDuplexStream"/> are the canonical entry points and
/// SHOULD be preferred (per the contract on <see cref="IDuplexStream"/>).
/// </summary>
internal sealed class WebSocketDuplexStream : IDuplexStream
{
    private readonly ClientWebSocket _ws;
    private readonly WebSocketBackedStream _streamView;
    private bool _disposed;

    public WebSocketDuplexStream(ClientWebSocket ws)
    {
        _ws = ws;
        _streamView = new WebSocketBackedStream(this);
    }

    public Stream Stream => _streamView;

    public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct)
    {
        var result = await _ws.ReceiveAsync(buffer, ct).ConfigureAwait(false);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            return 0;
        }
        return result.Count;
    }

    public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct) =>
        await _ws.SendAsync(buffer, WebSocketMessageType.Binary, endOfMessage: true, ct).ConfigureAwait(false);

    public Task FlushAsync(CancellationToken ct) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
            {
                await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "transport-disposed", CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch { /* best-effort close */ }
        _ws.Dispose();
    }

    private sealed class WebSocketBackedStream : Stream
    {
        private readonly WebSocketDuplexStream _outer;
        public WebSocketBackedStream(WebSocketDuplexStream outer) => _outer = outer;

        public override bool CanRead => true;
        public override bool CanWrite => true;
        public override bool CanSeek => false;
        public override long Length => throw new NotSupportedException("WebSocket streams have no fixed length.");
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) =>
            ReadAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            await _outer.ReadAsync(buffer.AsMemory(offset, count), ct).ConfigureAwait(false);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct) =>
            await _outer.ReadAsync(buffer, ct).ConfigureAwait(false);
        public override void Write(byte[] buffer, int offset, int count) =>
            WriteAsync(buffer, offset, count, CancellationToken.None).GetAwaiter().GetResult();
        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken ct) =>
            await _outer.WriteAsync(buffer.AsMemory(offset, count), ct).ConfigureAwait(false);
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct) =>
            await _outer.WriteAsync(buffer, ct).ConfigureAwait(false);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
