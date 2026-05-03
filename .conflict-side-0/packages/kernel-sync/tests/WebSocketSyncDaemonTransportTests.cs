using System.IO;
using System.Net.WebSockets;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Wave 5.3.C coverage for <see cref="WebSocketSyncDaemonTransport"/>. Uses a
/// loopback Kestrel host with a minimal <c>/ws</c> endpoint and a
/// <see cref="ClientWebSocket"/> to round-trip real WebSocket frames — the
/// in-memory testing approach wouldn't exercise the frame-assembly /
/// message-size / close-status code paths that matter.
/// </summary>
public class WebSocketSyncDaemonTransportTests
{
    /// <summary>
    /// Spin up a Kestrel-backed app that hands every inbound <c>/ws</c>
    /// WebSocket to the supplied transport via <see cref="WebSocketSyncDaemonTransport.Accept"/>.
    /// Returns the bound <c>ws://…/ws</c> URI and an <see cref="IAsyncDisposable"/>
    /// that stops the host on teardown.
    /// </summary>
    private static async Task<(Uri Uri, IAsyncDisposable Host)> StartServerAsync(
        WebSocketSyncDaemonTransport transport,
        CancellationToken ct = default)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.UseWebSockets();
        app.Map("/ws", async (HttpContext ctx) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            // Tie the HTTP request lifetime to the WebSocket lifetime so the
            // request doesn't complete before the test's assertions run.
            await transport.Accept(ws).ConfigureAwait(false);
        });
        await app.StartAsync(ct).ConfigureAwait(false);
        var serverAddresses = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!;
        var httpUrl = serverAddresses.Addresses.First();
        var wsUri = new Uri(httpUrl.Replace("http://", "ws://", StringComparison.Ordinal) + "/ws");
        return (wsUri, new AppHandle(app));
    }

    private sealed class AppHandle : IAsyncDisposable
    {
        private readonly WebApplication _app;
        public AppHandle(WebApplication app) => _app = app;
        public async ValueTask DisposeAsync()
        {
            try
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _app.StopAsync(stopCts.Token).ConfigureAwait(false);
            }
            catch { /* best-effort */ }
            await ((IAsyncDisposable)_app).DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<ISyncDaemonConnection> AcceptOneAsync(
        WebSocketSyncDaemonTransport transport,
        CancellationToken ct)
    {
        await foreach (var c in transport.ListenAsync(ct))
        {
            return c;
        }
        throw new InvalidOperationException("listener terminated before accepting a connection");
    }

    private static HelloMessage NewHello(ulong ts = 123) => new(
        NodeId: new byte[16],
        SchemaVersion: "1.0.0",
        SupportedVersions: new[] { "1.0.0" },
        PublicKey: new byte[32],
        Timestamp: ts,
        Signature: new byte[64]);

    [Fact]
    public async Task SendAsync_writes_single_WS_binary_message_per_CBOR_frame()
    {
        await using var transport = new WebSocketSyncDaemonTransport();
        var (uri, host) = await StartServerAsync(transport);
        await using var _ = host;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new ClientWebSocket();
        await client.ConnectAsync(uri, cts.Token);

        var serverConn = await AcceptOneAsync(transport, cts.Token);

        await serverConn.SendAsync(NewHello(42), cts.Token);

        // The ClientWebSocket must observe exactly one binary frame with
        // endOfMessage=true — that's the Wave 5.3.C framing contract.
        var buffer = new byte[16 * 1024];
        var result = await client.ReceiveAsync(buffer, cts.Token);
        Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
        Assert.True(result.EndOfMessage, "CBOR frame must fit in a single WS message");

        var payload = buffer.AsSpan(0, result.Count).ToArray();
        var decoded = Assert.IsType<HelloMessage>(SyncMessageCodec.Decode(payload));
        Assert.Equal(42ul, decoded.Timestamp);

        // Dispose the server-side connection first so its TryCloseAsync sends
        // the close frame, then the client observes the close reply via its
        // own CloseAsync. The order avoids a deadlock where CloseAsync waits
        // on a server-side close that never comes because the accept pump is
        // still awaiting the connection's Completion.
        await serverConn.DisposeAsync();
        try { await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token); }
        catch { /* server may have already torn down */ }
    }

    [Fact]
    public async Task ReceiveAsync_reads_single_CBOR_frame_from_binary_message()
    {
        await using var transport = new WebSocketSyncDaemonTransport();
        var (uri, host) = await StartServerAsync(transport);
        await using var _ = host;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new ClientWebSocket();
        await client.ConnectAsync(uri, cts.Token);

        var serverConn = await AcceptOneAsync(transport, cts.Token);

        var bytes = NewHello(9001).ToCbor();
        await client.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            cts.Token);

        var received = await serverConn.ReceiveAsync(cts.Token);
        var hello = Assert.IsType<HelloMessage>(received);
        Assert.Equal(9001ul, hello.Timestamp);

        await serverConn.DisposeAsync();
        try { await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token); }
        catch { /* server may have already torn down */ }
    }

    [Fact]
    public async Task Large_frame_within_MaxMessageBytes_roundtrips()
    {
        // MaxMessageBytes deliberately chosen so the HELLO payload (plus CBOR
        // overhead) fits comfortably while still stretching across multiple
        // ReceiveAsync chunks (default pooled buffer is 8 KiB).
        await using var transport = new WebSocketSyncDaemonTransport(maxMessageBytes: 512 * 1024);
        var (uri, host) = await StartServerAsync(transport);
        await using var _ = host;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new ClientWebSocket();
        await client.ConnectAsync(uri, cts.Token);

        var serverConn = await AcceptOneAsync(transport, cts.Token);

        // DeltaStreamMessage carries a byte[] payload — easiest way to
        // generate a frame that is large but still a valid CBOR envelope.
        var payload = new byte[128 * 1024];
        new Random(42).NextBytes(payload);
        var delta = new DeltaStreamMessage("stream-big", 7, payload);
        await serverConn.SendAsync(delta, cts.Token);

        // Read all fragments on the client side.
        var ms = new MemoryStream();
        var buffer = new byte[16 * 1024];
        while (true)
        {
            var result = await client.ReceiveAsync(buffer, cts.Token);
            ms.Write(buffer, 0, result.Count);
            if (result.EndOfMessage)
            {
                Assert.Equal(WebSocketMessageType.Binary, result.MessageType);
                break;
            }
        }

        var decoded = Assert.IsType<DeltaStreamMessage>(SyncMessageCodec.Decode(ms.ToArray()));
        Assert.Equal("stream-big", decoded.StreamId);
        Assert.Equal(7ul, decoded.OpSequence);
        Assert.Equal(payload, decoded.CrdtOps);

        await serverConn.DisposeAsync();
        try { await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "done", cts.Token); }
        catch { /* server may have already torn down */ }
    }

    [Fact]
    public async Task Oversized_frame_rejected_with_clean_close()
    {
        // Cap tight enough to trip when we send 8 KiB of payload.
        await using var transport = new WebSocketSyncDaemonTransport(maxMessageBytes: 4 * 1024);
        var (uri, host) = await StartServerAsync(transport);
        await using var _ = host;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new ClientWebSocket();
        await client.ConnectAsync(uri, cts.Token);

        var serverConn = await AcceptOneAsync(transport, cts.Token);

        // Fire the transport's ReceiveAsync on a background task so we can
        // simultaneously push the oversized payload from the client.
        var receiveTask = Task.Run(async () =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await serverConn.ReceiveAsync(cts.Token));
        });

        var oversized = new byte[8 * 1024];
        await client.SendAsync(
            new ArraySegment<byte>(oversized),
            WebSocketMessageType.Binary,
            endOfMessage: true,
            cts.Token);

        await receiveTask;

        // The server should have closed the socket with MessageTooBig.
        var buffer = new byte[256];
        var closeResult = await client.ReceiveAsync(buffer, cts.Token);
        Assert.Equal(WebSocketMessageType.Close, closeResult.MessageType);
        Assert.Equal(WebSocketCloseStatus.MessageTooBig, closeResult.CloseStatus);

        await serverConn.DisposeAsync();
    }

    [Fact]
    public async Task Client_close_propagates_cancellation()
    {
        await using var transport = new WebSocketSyncDaemonTransport();
        var (uri, host) = await StartServerAsync(transport);
        await using var _ = host;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var client = new ClientWebSocket();
        await client.ConnectAsync(uri, cts.Token);

        var serverConn = await AcceptOneAsync(transport, cts.Token);

        var receiveTask = Task.Run(async () =>
        {
            await Assert.ThrowsAsync<EndOfStreamException>(async () =>
                await serverConn.ReceiveAsync(cts.Token));
        });

        await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", cts.Token);
        await receiveTask;
        await serverConn.DisposeAsync();
    }
}
