using Sunfish.Federation.Common;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Federation.Common.Tests;

public class InMemorySyncTransportTests
{
    private static (PeerId id, PeerDescriptor descriptor) MakePeer(string host)
    {
        using var kp = KeyPair.Generate();
        var id = PeerId.From(kp.PrincipalId);
        return (id, new PeerDescriptor(id, new Uri($"https://{host}")));
    }

    private static SyncEnvelope MakeEnvelope(PeerId from, PeerId to, byte[] payload, SyncMessageKind kind = SyncMessageKind.HealthProbe)
    {
        using var kp = KeyPair.Generate();
        var signer = new Ed25519Signer(kp);
        // Override FromPeer to explicitly-supplied value for test routing (re-sign would bind to kp identity).
        var env = SyncEnvelope.SignAndCreate(signer, to, kind, payload);
        return env with { FromPeer = from };
    }

    [Fact]
    public async Task SendAsync_InvokesRegisteredHandler_ForTargetPeer()
    {
        var transport = new InMemorySyncTransport();
        var (aId, _) = MakePeer("a.example");
        var (bId, bDescriptor) = MakePeer("b.example");

        SyncEnvelope? reply = null;
        using var registration = transport.RegisterHandler(bId, env =>
        {
            reply = env with { Payload = new byte[] { 0xAB } };
            return ValueTask.FromResult(reply);
        });

        var outbound = MakeEnvelope(aId, bId, new byte[] { 1, 2, 3 });
        var result = await transport.SendAsync(bDescriptor, outbound, CancellationToken.None);

        Assert.NotNull(reply);
        Assert.Equal(1, result.Payload.Length);
        Assert.Equal(0xAB, result.Payload.Span[0]);
    }

    [Fact]
    public async Task SendAsync_Throws_WhenTargetNotRegistered()
    {
        var transport = new InMemorySyncTransport();
        var (aId, _) = MakePeer("a.example");
        var (bId, bDescriptor) = MakePeer("b.example");
        var env = MakeEnvelope(aId, bId, new byte[] { 1 });

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await transport.SendAsync(bDescriptor, env, CancellationToken.None));
    }

    [Fact]
    public void RegisterHandler_Twice_ForSamePeer_Throws()
    {
        var transport = new InMemorySyncTransport();
        var (bId, _) = MakePeer("b.example");

        using var first = transport.RegisterHandler(bId, env => ValueTask.FromResult(env));

        Assert.Throws<InvalidOperationException>(() =>
            transport.RegisterHandler(bId, env => ValueTask.FromResult(env)));
    }
}
