namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Coverage for <see cref="InMemorySyncDaemonTransport"/>. Verifies the
/// connect/listen pairing, send/receive roundtrips, and cancellation.
/// </summary>
public class InMemorySyncDaemonTransportTests
{
    private static string NewEndpoint() => $"inmem-{Guid.NewGuid():N}";

    [Fact]
    public async Task Connect_To_Listening_Transport_Pairs_Two_Connections()
    {
        var endpoint = NewEndpoint();
        await using var listener = new InMemorySyncDaemonTransport(endpoint);
        await using var client = new InMemorySyncDaemonTransport();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var acceptTask = Task.Run(async () =>
        {
            await foreach (var c in listener.ListenAsync(cts.Token))
            {
                return c;
            }
            return null!;
        }, cts.Token);

        var connectTask = client.ConnectAsync(endpoint, cts.Token);
        var clientConn = await connectTask;
        var serverConn = await acceptTask;

        await using var _ = clientConn;
        await using var __ = serverConn;

        Assert.NotNull(clientConn);
        Assert.NotNull(serverConn);
        Assert.Equal(endpoint, clientConn.RemoteEndpoint);
    }

    [Fact]
    public async Task Send_On_Client_Is_Received_On_Server()
    {
        var endpoint = NewEndpoint();
        await using var listener = new InMemorySyncDaemonTransport(endpoint);
        await using var client = new InMemorySyncDaemonTransport();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var serverTask = Task.Run(async () =>
        {
            await foreach (var c in listener.ListenAsync(cts.Token))
            {
                return c;
            }
            throw new InvalidOperationException("no connection accepted");
        }, cts.Token);
        var clientConn = await client.ConnectAsync(endpoint, cts.Token);
        var serverConn = await serverTask;

        var message = new HelloMessage(
            NodeId: new byte[16], SchemaVersion: "1.0.0",
            SupportedVersions: new[] { "1.0.0" }, PublicKey: new byte[32],
            Timestamp: 123456789, Signature: new byte[64]);
        await clientConn.SendAsync(message, cts.Token);

        var received = await serverConn.ReceiveAsync(cts.Token);
        var hello = Assert.IsType<HelloMessage>(received);
        Assert.Equal(123456789ul, hello.Timestamp);

        await clientConn.DisposeAsync();
        await serverConn.DisposeAsync();
    }

    [Fact]
    public async Task Connect_To_Unknown_Endpoint_Throws_IOException()
    {
        await using var client = new InMemorySyncDaemonTransport();
        var ex = await Assert.ThrowsAsync<IOException>(async () =>
            await client.ConnectAsync("not-listening", CancellationToken.None));
        Assert.Contains("not-listening", ex.Message);
    }

    [Fact]
    public async Task Double_Listen_On_Same_Endpoint_Throws()
    {
        var endpoint = NewEndpoint();
        await using var listener1 = new InMemorySyncDaemonTransport(endpoint);
        Assert.Throws<InvalidOperationException>(() => new InMemorySyncDaemonTransport(endpoint));
    }

    [Fact]
    public async Task Bidirectional_Send_Receive_Works_Both_Directions()
    {
        var endpoint = NewEndpoint();
        await using var listener = new InMemorySyncDaemonTransport(endpoint);
        await using var client = new InMemorySyncDaemonTransport();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var acceptTask = Task.Run(async () =>
        {
            await foreach (var c in listener.ListenAsync(cts.Token))
            {
                return c;
            }
            throw new InvalidOperationException("no connection");
        }, cts.Token);

        var clientConn = await client.ConnectAsync(endpoint, cts.Token);
        var serverConn = await acceptTask;

        // Client → Server
        await clientConn.SendAsync(new AckMessage(new[] { "sA" }, Array.Empty<Rejection>()), cts.Token);
        var serverGot = Assert.IsType<AckMessage>(await serverConn.ReceiveAsync(cts.Token));
        Assert.Equal(new[] { "sA" }, serverGot.GrantedSubscriptions);

        // Server → Client
        await serverConn.SendAsync(new AckMessage(new[] { "sB" }, Array.Empty<Rejection>()), cts.Token);
        var clientGot = Assert.IsType<AckMessage>(await clientConn.ReceiveAsync(cts.Token));
        Assert.Equal(new[] { "sB" }, clientGot.GrantedSubscriptions);

        await clientConn.DisposeAsync();
        await serverConn.DisposeAsync();
    }

    [Fact]
    public async Task Listen_Cancellation_Ends_Enumeration_Cleanly()
    {
        var endpoint = NewEndpoint();
        await using var listener = new InMemorySyncDaemonTransport(endpoint);
        using var cts = new CancellationTokenSource();

        var listenTask = Task.Run(async () =>
        {
            var count = 0;
            await foreach (var _ in listener.ListenAsync(cts.Token))
            {
                count++;
            }
            return count;
        });

        // Give the listener a chance to block on ReadAsync, then cancel.
        await Task.Delay(100);
        cts.Cancel();

        var count = await listenTask.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0, count);
    }
}
