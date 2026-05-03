using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Sunfish.Kernel.Sync.Protocol;

/// <summary>
/// In-process transport used by tests and the xUnit harness. Simulates
/// a pair of sockets with <see cref="System.Threading.Channels.Channel{T}"/>
/// back-to-back. No framing is performed — messages are passed by reference
/// through the channel — but the public contract is otherwise byte-identical
/// to the real Unix-socket transport.
/// </summary>
/// <remarks>
/// Endpoints are strings registered via <see cref="ListenAsync"/>. A
/// <see cref="ConnectAsync"/> with a matching endpoint resolves a paired
/// <see cref="ISyncDaemonConnection"/>. Multiple transport instances on the
/// same process share a process-wide endpoint registry so tests can wire up
/// two independent daemons that "see" each other.
/// </remarks>
public sealed class InMemorySyncDaemonTransport : ISyncDaemonTransport
{
    // Process-wide endpoint registry. Each entry is an unbounded channel that
    // carries inbound connections to the listener. Concurrent listeners on the
    // same endpoint are not supported — the second AddOrUpdate throws.
    private static readonly ConcurrentDictionary<string, Channel<ISyncDaemonConnection>> _registry = new(StringComparer.Ordinal);

    private readonly string _listenEndpoint;
    private readonly Channel<ISyncDaemonConnection>? _inbound;
    private bool _disposed;

    /// <summary>Create an outbound-only transport (no listener).</summary>
    public InMemorySyncDaemonTransport()
        : this(listenEndpoint: null)
    {
    }

    /// <summary>
    /// Create a transport that also listens on <paramref name="listenEndpoint"/>.
    /// Passing <c>null</c> creates an outbound-only instance.
    /// </summary>
    public InMemorySyncDaemonTransport(string? listenEndpoint)
    {
        _listenEndpoint = listenEndpoint ?? string.Empty;
        if (!string.IsNullOrEmpty(listenEndpoint))
        {
            _inbound = Channel.CreateUnbounded<ISyncDaemonConnection>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            if (!_registry.TryAdd(listenEndpoint, _inbound))
            {
                throw new InvalidOperationException(
                    $"Endpoint '{listenEndpoint}' is already listening on this process.");
            }
        }
    }

    public async Task<ISyncDaemonConnection> ConnectAsync(string peerEndpoint, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(peerEndpoint);
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_registry.TryGetValue(peerEndpoint, out var peerInbound))
        {
            throw new IOException($"No in-memory listener at endpoint '{peerEndpoint}'.");
        }

        var (clientSide, serverSide) = InMemoryConnection.CreatePair(
            clientLabel: peerEndpoint,
            serverLabel: _listenEndpoint);

        // Hand the server side to the listener's inbound channel.
        await peerInbound.Writer.WriteAsync(serverSide, ct).ConfigureAwait(false);
        return clientSide;
    }

    public async IAsyncEnumerable<ISyncDaemonConnection> ListenAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (_inbound is null)
        {
            throw new InvalidOperationException(
                "Transport was not constructed with a listen endpoint; cannot ListenAsync.");
        }

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
            yield return conn;
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return ValueTask.CompletedTask;
        }
        _disposed = true;
        if (_inbound is not null)
        {
            _inbound.Writer.TryComplete();
            _registry.TryRemove(_listenEndpoint, out _);
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// A connection pair. Each side has an outbound and inbound channel; the
    /// two channels are crossed so <c>A.Send</c> lands in <c>B.Receive</c>.
    /// </summary>
    private sealed class InMemoryConnection : ISyncDaemonConnection
    {
        private readonly Channel<object> _outbound;
        private readonly Channel<object> _inbound;
        private bool _disposed;

        public string RemoteEndpoint { get; }

        private InMemoryConnection(string remoteEndpoint, Channel<object> outbound, Channel<object> inbound)
        {
            RemoteEndpoint = remoteEndpoint;
            _outbound = outbound;
            _inbound = inbound;
        }

        public static (InMemoryConnection client, InMemoryConnection server) CreatePair(
            string clientLabel, string serverLabel)
        {
            var a2b = Channel.CreateUnbounded<object>();
            var b2a = Channel.CreateUnbounded<object>();
            var client = new InMemoryConnection(clientLabel, outbound: a2b, inbound: b2a);
            var server = new InMemoryConnection(serverLabel, outbound: b2a, inbound: a2b);
            return (client, server);
        }

        public async Task SendAsync<TMessage>(TMessage message, CancellationToken ct) where TMessage : class
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            // In-memory transport forgoes CBOR framing — we pass the record by
            // reference. Real transports serialize here. The behaviour is
            // otherwise identical (send → one unit delivered to Receive).
            await _outbound.Writer.WriteAsync(message!, ct).ConfigureAwait(false);
        }

        public async Task<object> ReceiveAsync(CancellationToken ct)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return await _inbound.Reader.ReadAsync(ct).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return ValueTask.CompletedTask;
            }
            _disposed = true;
            _outbound.Writer.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
