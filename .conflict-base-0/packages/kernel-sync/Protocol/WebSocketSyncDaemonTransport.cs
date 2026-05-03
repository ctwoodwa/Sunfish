using System.Buffers;
using System.Net.WebSockets;

using Microsoft.Extensions.Logging;

namespace Sunfish.Kernel.Sync.Protocol;

/// <summary>
/// Wave 5.3.C WebSocket-framed variant of the sync-daemon transport. Exposes
/// the same <see cref="ISyncDaemonTransport"/> / <see cref="ISyncDaemonConnection"/>
/// contract as <see cref="UnixSocketSyncDaemonTransport"/>, but wraps an
/// already-upgraded <see cref="WebSocket"/> instead of a raw byte stream.
/// </summary>
/// <remarks>
/// <para>
/// <b>Framing.</b> One WebSocket binary message carries exactly one CBOR
/// envelope. Because the WebSocket layer already frames each message with a
/// length + end-of-message bit, the transport intentionally omits the 4-byte
/// big-endian length prefix used by <see cref="UnixSocketSyncDaemonTransport"/>
/// (sync-daemon-protocol §2.2). The application-level CBOR envelope is
/// unchanged — the two transports are interchangeable for the handshake
/// ladder and gossip loops once a connection is established.
/// </para>
/// <para>
/// <b>Server-side accept-only.</b> This transport does not initiate outbound
/// connections today. Bridge's SaaS reverse-proxy opens the outbound
/// <see cref="ClientWebSocket"/> to the tenant child; the child's Kestrel
/// performs <c>AcceptWebSocketAsync</c> and hands the resulting WebSocket to
/// <see cref="Accept(WebSocket)"/>, which yields it via
/// <see cref="ListenAsync"/>. <see cref="ConnectAsync"/> throws — use
/// <see cref="UnixSocketSyncDaemonTransport"/> (or the in-memory variant in
/// tests) for outbound connections.
/// </para>
/// <para>
/// <b>Message-size cap.</b> Inbound messages larger than
/// <see cref="MaxMessageBytes"/> are rejected with a clean
/// <see cref="WebSocketCloseStatus.MessageTooBig"/> close — the sender is
/// expected to chunk at the application layer (rare — only full bucket
/// snapshots).
/// </para>
/// </remarks>
public sealed class WebSocketSyncDaemonTransport : ISyncDaemonTransport
{
    /// <summary>
    /// Default inbound message cap. Matches the <c>BrowserWebSocketOptions</c>
    /// default shipped in Wave 5.3.C and keeps memory pressure predictable
    /// for a single tenant child.
    /// </summary>
    public const int DefaultMaxMessageBytes = 4 * 1024 * 1024;

    private readonly System.Threading.Channels.Channel<ISyncDaemonConnection> _inbound =
        System.Threading.Channels.Channel.CreateUnbounded<ISyncDaemonConnection>(
            new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });

    private bool _disposed;

    /// <summary>
    /// Maximum inbound message size in bytes. Frames larger than this cap are
    /// rejected with a <see cref="WebSocketCloseStatus.MessageTooBig"/> close
    /// and surface as an <see cref="InvalidOperationException"/> on the
    /// <see cref="ISyncDaemonConnection.ReceiveAsync"/> side.
    /// </summary>
    public int MaxMessageBytes { get; }

    /// <summary>
    /// Construct an accept-only WebSocket transport. <see cref="Accept(WebSocket)"/>
    /// feeds inbound connections that <see cref="ListenAsync"/> yields.
    /// </summary>
    /// <param name="maxMessageBytes">
    /// Optional inbound frame cap; defaults to <see cref="DefaultMaxMessageBytes"/>.
    /// </param>
    public WebSocketSyncDaemonTransport(int maxMessageBytes = DefaultMaxMessageBytes)
    {
        if (maxMessageBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxMessageBytes),
                maxMessageBytes,
                "MaxMessageBytes must be positive.");
        }
        MaxMessageBytes = maxMessageBytes;
    }

    /// <summary>
    /// Feed an already-upgraded WebSocket into the transport's inbound queue.
    /// Called by the hosted endpoint (<c>HostedWebSocketEndpoint</c> in
    /// local-node-host) once Kestrel completes <c>AcceptWebSocketAsync</c>.
    /// </summary>
    /// <remarks>
    /// Returns a <see cref="Task"/> that completes when the wrapping
    /// <see cref="ISyncDaemonConnection"/> is disposed — the caller may await
    /// it to tie the HTTP request lifetime to the WebSocket lifetime.
    /// </remarks>
    public Task Accept(WebSocket webSocket)
    {
        ArgumentNullException.ThrowIfNull(webSocket);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var conn = new WebSocketSyncDaemonTransportConnection(webSocket, MaxMessageBytes);
        if (!_inbound.Writer.TryWrite(conn))
        {
            // Writer is completed (transport disposed between checks).
            throw new ObjectDisposedException(nameof(WebSocketSyncDaemonTransport));
        }
        return conn.Completion;
    }

    /// <inheritdoc />
    public Task<ISyncDaemonConnection> ConnectAsync(string peerEndpoint, CancellationToken ct)
    {
        throw new NotSupportedException(
            "WebSocketSyncDaemonTransport is accept-only. Use UnixSocketSyncDaemonTransport " +
            "for outbound connections; the Bridge reverse-proxy uses ClientWebSocket directly.");
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<ISyncDaemonConnection> ListenAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            ISyncDaemonConnection conn;
            try
            {
                conn = await _inbound.Reader.ReadAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            catch (System.Threading.Channels.ChannelClosedException)
            {
                yield break;
            }
            yield return conn;
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }
        _disposed = true;
        _inbound.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Per-connection wrapper around a single pre-upgraded
    /// <see cref="WebSocket"/>. One WebSocket binary message per CBOR frame.
    /// </summary>
    internal sealed class WebSocketSyncDaemonTransportConnection : ISyncDaemonConnection
    {
        private readonly WebSocket _ws;
        private readonly int _maxMessageBytes;
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private readonly TaskCompletionSource<object?> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private bool _disposed;

        public string RemoteEndpoint { get; }

        internal Task Completion => _completion.Task;

        public WebSocketSyncDaemonTransportConnection(WebSocket ws, int maxMessageBytes)
        {
            _ws = ws;
            _maxMessageBytes = maxMessageBytes;
            RemoteEndpoint = "ws://" + (ws.SubProtocol ?? "sync-daemon");
        }

        /// <inheritdoc />
        public async Task SendAsync<TMessage>(TMessage message, CancellationToken ct)
            where TMessage : class
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var bytes = SyncMessageCodec.Encode(message);
            if (bytes.Length > _maxMessageBytes)
            {
                throw new InvalidOperationException(
                    $"Frame exceeds maximum {_maxMessageBytes} bytes (got {bytes.Length}).");
            }

            await _sendGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                // One WS binary message == one CBOR envelope. endOfMessage:true
                // is critical — the peer relies on it to frame the payload.
                await _ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Binary,
                    endOfMessage: true,
                    ct).ConfigureAwait(false);
            }
            finally
            {
                _sendGate.Release();
            }
        }

        /// <inheritdoc />
        public async Task<object> ReceiveAsync(CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            // Stream chunks into a pooled buffer until endOfMessage=true,
            // enforcing the MaxMessageBytes cap as we go. Using a rented
            // array pool buffer keeps hot-path allocations down.
            var pool = ArrayPool<byte>.Shared;
            var buffer = pool.Rent(8 * 1024);
            var ms = new MemoryStream();
            try
            {
                while (true)
                {
                    var result = await _ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer), ct).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        // Peer initiated a clean close before we observed a full
                        // frame; surface as EndOfStream for symmetry with the
                        // UnixSocketSyncDaemonTransport path.
                        await TryCloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "peer closed",
                            CancellationToken.None).ConfigureAwait(false);
                        throw new EndOfStreamException(
                            "Peer closed the WebSocket before delivering a complete frame.");
                    }

                    // We only accept binary frames — text frames indicate a
                    // protocol violation on a sync-daemon channel.
                    if (result.MessageType != WebSocketMessageType.Binary)
                    {
                        await TryCloseAsync(
                            WebSocketCloseStatus.InvalidMessageType,
                            "sync-daemon requires binary frames",
                            CancellationToken.None).ConfigureAwait(false);
                        throw new InvalidOperationException(
                            $"Unexpected WebSocket message type '{result.MessageType}'.");
                    }

                    if (ms.Length + result.Count > _maxMessageBytes)
                    {
                        await TryCloseAsync(
                            WebSocketCloseStatus.MessageTooBig,
                            $"frame exceeds {_maxMessageBytes} bytes",
                            CancellationToken.None).ConfigureAwait(false);
                        throw new InvalidOperationException(
                            $"Incoming WebSocket message exceeds {_maxMessageBytes}-byte cap.");
                    }

                    ms.Write(buffer, 0, result.Count);
                    if (result.EndOfMessage)
                    {
                        break;
                    }
                }
            }
            finally
            {
                pool.Return(buffer);
            }

            return SyncMessageCodec.Decode(ms.ToArray());
        }

        /// <inheritdoc />
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }
            _disposed = true;

            await TryCloseAsync(
                WebSocketCloseStatus.NormalClosure,
                "sync-daemon session closed",
                CancellationToken.None).ConfigureAwait(false);

            try { _sendGate.Dispose(); } catch { /* best-effort */ }
            try { _ws.Dispose(); } catch { /* best-effort */ }
            _completion.TrySetResult(null);
        }

        private async Task TryCloseAsync(
            WebSocketCloseStatus status,
            string description,
            CancellationToken ct)
        {
            // CloseOutputAsync is intentionally one-way: send the close frame
            // and don't wait for the peer's reply. This matters when the
            // connection is being torn down mid-session — the CBOR reader may
            // never read another frame and the bidirectional
            // CloseAsync deadlocks waiting for a close reply that never
            // comes. The caller (handshake / gossip loop) is responsible for
            // draining any remaining frames + Dispose.
            if (_ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                try
                {
                    await _ws.CloseOutputAsync(status, description, ct).ConfigureAwait(false);
                }
                catch
                {
                    // Socket already torn down — ignore.
                }
            }
        }
    }
}

/// <summary>
/// Stub <see cref="ISyncDaemonAcceptor"/> shipped with Wave 5.3.C. Logs the
/// inbound WebSocket connection and closes it cleanly. Wave 5.3.D replaces
/// this with the real session pipeline (accept → HELLO handshake →
/// DELTA_STREAM pump).
/// </summary>
public sealed class LoggingSyncDaemonAcceptor : ISyncDaemonAcceptor
{
    private readonly ILogger<LoggingSyncDaemonAcceptor> _logger;

    public LoggingSyncDaemonAcceptor(ILogger<LoggingSyncDaemonAcceptor> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AcceptAsync(WebSocket ws, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ws);

        _logger.LogInformation(
            "Wave 5.3.C stub acceptor: received WS connection, CBOR-reading not yet wired. " +
            "Closing with NormalClosure until 5.3.D lands the session pipeline.");

        try
        {
            if (ws.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await ws.CloseOutputAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Wave 5.3.C stub acceptor: session pipeline lands in 5.3.D",
                    ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Host shutting down — fine.
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug(ex, "WebSocket close from stub acceptor threw (ignored).");
        }
    }
}
