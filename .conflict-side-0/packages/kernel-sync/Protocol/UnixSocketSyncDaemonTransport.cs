using System.Buffers.Binary;
using System.IO.Pipes;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Sunfish.Kernel.Sync.Protocol;

/// <summary>
/// Real OS-level transport for the sync daemon. Uses Unix-domain sockets on
/// POSIX (Linux / macOS / BSD) and Windows named pipes on Windows.
/// </summary>
/// <remarks>
/// <para>
/// <b>Framing:</b> sync-daemon-protocol §2.2 — every frame is a big-endian
/// 4-byte length prefix followed by the CBOR payload. A single frame is
/// capped at 16 MiB (<c>0x01000000</c>); larger messages must be chunked at
/// the application layer (rare — only full bucket snapshots).
/// </para>
/// <para>
/// <b>Platform split:</b> .NET 11's <see cref="UnixDomainSocketEndPoint"/>
/// advertises Windows support, but AF_UNIX is only wired on modern builds
/// and behaves inconsistently in our CI matrix. Per ADR 0029 and the
/// sync-daemon-protocol spec §2.1 table, we use named pipes on Windows.
/// The switch happens in <see cref="CreateListeningHandle"/> and
/// <see cref="ConnectHandle"/>.
/// </para>
/// <para>
/// <b>Scope:</b> this transport is operational but marked provisional for
/// Wave 2.1; peer discovery (mDNS / WireGuard) is Wave 2.2 and a full
/// TCP peer-to-peer path across machines (protocol §2.1 default port 7473)
/// is Wave 2.5. Today the transport targets local-only sockets — the
/// primary gossip-session substrate inside a single machine / team VPN.
/// </para>
/// </remarks>
public sealed class UnixSocketSyncDaemonTransport : ISyncDaemonTransport
{
    private const int MaxFrameBytes = 16 * 1024 * 1024; // 16 MiB, spec §2.2.

    private readonly string _listenEndpoint;
    private readonly IListenerHandle? _listener;
    private bool _disposed;

    /// <summary>
    /// Construct an outbound-only transport (no listener). Use this for a
    /// pure-client daemon that reaches out to peers but does not accept
    /// inbound sessions.
    /// </summary>
    public UnixSocketSyncDaemonTransport()
        : this(listenEndpoint: null)
    {
    }

    /// <summary>
    /// Construct a transport that listens on <paramref name="listenEndpoint"/>.
    /// Path is interpreted per platform — on POSIX an absolute socket path,
    /// on Windows a pipe name (with or without the <c>\\.\pipe\</c> prefix).
    /// </summary>
    public UnixSocketSyncDaemonTransport(string? listenEndpoint)
    {
        _listenEndpoint = listenEndpoint ?? string.Empty;
        _listener = string.IsNullOrEmpty(listenEndpoint)
            ? null
            : CreateListeningHandle(listenEndpoint);
    }

    public async Task<ISyncDaemonConnection> ConnectAsync(string peerEndpoint, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(peerEndpoint);
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await ConnectHandle(peerEndpoint, ct).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<ISyncDaemonConnection> ListenAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (_listener is null)
        {
            throw new InvalidOperationException(
                "Transport was not constructed with a listen endpoint; cannot ListenAsync.");
        }

        while (!ct.IsCancellationRequested)
        {
            ISyncDaemonConnection? conn;
            try
            {
                conn = await _listener.AcceptAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }
            if (conn is null)
            {
                yield break;
            }
            yield return conn;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        if (_listener is not null)
        {
            await _listener.DisposeAsync().ConfigureAwait(false);
        }
    }

    // ------------------------------------------------------------------
    // Platform factory methods
    // ------------------------------------------------------------------

    private static IListenerHandle CreateListeningHandle(string endpoint)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return new NamedPipeListenerHandle(NormalizePipeName(endpoint));
        }
        return new UnixSocketListenerHandle(endpoint);
    }

    private static async Task<ISyncDaemonConnection> ConnectHandle(string endpoint, CancellationToken ct)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pipeName = NormalizePipeName(endpoint);
            var client = new NamedPipeClientStream(
                serverName: ".",
                pipeName: pipeName,
                direction: PipeDirection.InOut,
                options: PipeOptions.Asynchronous);
            await client.ConnectAsync(ct).ConfigureAwait(false);
            return new StreamConnection(endpoint, client);
        }
        else
        {
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(endpoint), ct).ConfigureAwait(false);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
            return new StreamConnection(endpoint, new NetworkStream(socket, ownsSocket: true));
        }
    }

    /// <summary>
    /// Windows named pipes are referenced as <c>pipe-name</c>; the full UNC
    /// form <c>\\.\pipe\name</c> is accepted and stripped.
    /// </summary>
    private static string NormalizePipeName(string endpoint)
    {
        const string prefix = "\\\\.\\pipe\\";
        return endpoint.StartsWith(prefix, StringComparison.Ordinal)
            ? endpoint[prefix.Length..]
            : endpoint;
    }

    // ------------------------------------------------------------------
    // Listener abstraction — same Accept shape for Unix and Windows
    // ------------------------------------------------------------------

    private interface IListenerHandle : IAsyncDisposable
    {
        Task<ISyncDaemonConnection?> AcceptAsync(CancellationToken ct);
    }

    private sealed class UnixSocketListenerHandle : IListenerHandle
    {
        private readonly string _path;
        private readonly Socket _listener;
        private bool _disposed;

        public UnixSocketListenerHandle(string path)
        {
            _path = path;
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch { /* bind will surface the real error */ }

            _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            _listener.Bind(new UnixDomainSocketEndPoint(path));
            _listener.Listen(backlog: 32);
        }

        public async Task<ISyncDaemonConnection?> AcceptAsync(CancellationToken ct)
        {
            try
            {
                var accepted = await _listener.AcceptAsync(ct).ConfigureAwait(false);
                return new StreamConnection(_path, new NetworkStream(accepted, ownsSocket: true));
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (ObjectDisposedException)
            {
                return null;
            }
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;
            try { _listener.Dispose(); } catch { /* best-effort */ }
            try { if (File.Exists(_path)) File.Delete(_path); } catch { /* best-effort */ }
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NamedPipeListenerHandle : IListenerHandle
    {
        private readonly string _pipeName;

        public NamedPipeListenerHandle(string pipeName)
        {
            _pipeName = pipeName;
        }

        public async Task<ISyncDaemonConnection?> AcceptAsync(CancellationToken ct)
        {
            // Spin a fresh server instance per accept. NamedPipeServerStream does
            // not support multi-accept on one instance; the standard pattern is
            // one-server-per-client plus a capped max-instances count.
            var server = new NamedPipeServerStream(
                pipeName: _pipeName,
                direction: PipeDirection.InOut,
                maxNumberOfServerInstances: NamedPipeServerStream.MaxAllowedServerInstances,
                transmissionMode: PipeTransmissionMode.Byte,
                options: PipeOptions.Asynchronous);
            try
            {
                await server.WaitForConnectionAsync(ct).ConfigureAwait(false);
            }
            catch
            {
                await server.DisposeAsync().ConfigureAwait(false);
                return null;
            }
            return new StreamConnection(_pipeName, server);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    // ------------------------------------------------------------------
    // Connection — shared between Unix and Windows via Stream base
    // ------------------------------------------------------------------

    private sealed class StreamConnection : ISyncDaemonConnection
    {
        private readonly Stream _stream;
        private readonly SemaphoreSlim _sendGate = new(1, 1);
        private bool _disposed;

        public string RemoteEndpoint { get; }

        public StreamConnection(string remote, Stream stream)
        {
            RemoteEndpoint = remote;
            _stream = stream;
        }

        public async Task SendAsync<TMessage>(TMessage message, CancellationToken ct) where TMessage : class
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var bytes = SyncMessageCodec.Encode(message);
            if (bytes.Length > MaxFrameBytes)
            {
                throw new InvalidOperationException(
                    $"Frame exceeds maximum {MaxFrameBytes} bytes (got {bytes.Length}).");
            }

            await _sendGate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                Span<byte> header = stackalloc byte[4];
                BinaryPrimitives.WriteUInt32BigEndian(header, (uint)bytes.Length);
                await _stream.WriteAsync(header.ToArray().AsMemory(0, 4), ct).ConfigureAwait(false);
                await _stream.WriteAsync(bytes.AsMemory(), ct).ConfigureAwait(false);
                await _stream.FlushAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _sendGate.Release();
            }
        }

        public async Task<object> ReceiveAsync(CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var header = new byte[4];
            await ReadExactAsync(_stream, header, ct).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (length > MaxFrameBytes)
            {
                throw new InvalidOperationException(
                    $"Incoming frame announces {length} bytes — exceeds 16 MiB cap.");
            }

            var payload = new byte[length];
            await ReadExactAsync(_stream, payload, ct).ConfigureAwait(false);
            return SyncMessageCodec.Decode(payload);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            _sendGate.Dispose();
            await _stream.DisposeAsync().ConfigureAwait(false);
        }

        private static async Task ReadExactAsync(Stream s, byte[] buffer, CancellationToken ct)
        {
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = await s.ReadAsync(buffer.AsMemory(offset), ct).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new EndOfStreamException(
                        $"Peer closed after {offset} of {buffer.Length} expected bytes.");
                }
                offset += read;
            }
        }
    }
}
