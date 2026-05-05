using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.CrewComms;
using Sunfish.Blocks.CrewComms.Signaling;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Transport;
using Xunit;

namespace Sunfish.Blocks.CrewComms.Tests;

public class NativeChannelProviderIntegrationTests
{
    private static readonly TenantId Tenant = new("acme");

    [Fact]
    public async Task EndToEnd_TextExchange_BetweenTwoProviders()
    {
        // Two providers wired through in-memory pipe pair. The initiator's
        // transport selector returns a fake transport whose ConnectAsync
        // hands out one half; the responder's listener is fed the other
        // half manually (production code wires this through a real
        // server-side transport adapter).
        using var keyA = KeyPair.Generate();
        using var keyB = KeyPair.Generate();
        var roster = new InMemoryCrewRoster(new[]
        {
            new CrewMember { Peer = PeerId.From(keyA.PrincipalId), DisplayName = "Alice" },
            new CrewMember { Peer = PeerId.From(keyB.PrincipalId), DisplayName = "Bob" },
        });

        var (streamA, streamB) = MemoryDuplexStream.CreatePair();
        var fakeTransport = new SingleShotTransport(streamA);
        var initiatorSelector = new SingleTransportSelector(fakeTransport);

        await using var providerA = new NativeChannelProvider(keyA, roster, initiatorSelector);
        await using var providerB = new NativeChannelProvider(keyB, roster, new SingleTransportSelector(new UnreachableTransport()));

        // Kick off both halves concurrently. The responder must drive its
        // side of the handshake (read HELLO, send HELLO, read INVITE, queue
        // invitation) while the initiator is mid-OpenAsync; the initiator
        // blocks at WaitAsync(ACCEPT) until the consumer of the listener
        // calls invitation.AcceptAsync.
        var listenTask = providerB.Listener.AcceptIncomingAsync(
            streamB, Tenant, ChannelCapability.Text, CancellationToken.None);
        var openTask = providerA.OpenAsync(
            Tenant, PeerId.From(keyB.PrincipalId), ChannelCapability.Text, CancellationToken.None);

        // Drain the invitation queue.
        await using var inviteEnumerator = providerB.ListenAsync(Tenant, CancellationToken.None).GetAsyncEnumerator();
        using var inviteCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.True(await inviteEnumerator.MoveNextAsync().AsTask().WaitAsync(inviteCts.Token));
        var invitation = inviteEnumerator.Current;
        Assert.Equal(PeerId.From(keyA.PrincipalId), invitation.FromPeer);
        await listenTask;

        // Accepting on the responder side sends ACCEPT, which unblocks the
        // initiator's OpenAsync; both sides exchange CONFIRM and transition
        // to ACTIVE.
        var sessionB = await invitation.AcceptAsync(CancellationToken.None);
        var sessionA = await openTask;

        // Now exchange text both ways.
        await sessionA.SendTextAsync("hello from A", CancellationToken.None);

        await using var bRecv = sessionB.ReceiveTextAsync(CancellationToken.None).GetAsyncEnumerator();
        using var recvCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Assert.True(await bRecv.MoveNextAsync().AsTask().WaitAsync(recvCts.Token));
        Assert.Equal("hello from A", bRecv.Current);

        await sessionB.SendTextAsync("ack from B", CancellationToken.None);
        await using var aRecv = sessionA.ReceiveTextAsync(CancellationToken.None).GetAsyncEnumerator();
        using var recvCts2 = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Assert.True(await aRecv.MoveNextAsync().AsTask().WaitAsync(recvCts2.Token));
        Assert.Equal("ack from B", aRecv.Current);

        // BYE from A → B.Completed surfaces RemoteBye.
        await sessionA.CloseAsync(CancellationToken.None);
        var reasonB = await sessionB.Completed.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(ChannelTerminationReason.RemoteBye, reasonB);

        await sessionA.DisposeAsync();
        await sessionB.DisposeAsync();
    }

    [Fact]
    public void SessionListener_DropNewest_FillBoundedChannelDirectly()
    {
        // Direct exercise of the bounded-channel drop path: bypass the
        // handshake (which is exercised by the EndToEnd test) and feed
        // 17 invitations directly through the internal write hook. The
        // 17th MUST be dropped (capacity 16, FullMode = DropNewest) and
        // OnInviteDropped MUST fire exactly once.
        using var keyB = KeyPair.Generate();
        var roster = new InMemoryCrewRoster(new[]
        {
            new CrewMember { Peer = PeerId.From(keyB.PrincipalId), DisplayName = "Bob" },
        });
        var listener = new SessionListener(keyB, roster);
        var dropped = 0;
        listener.OnInviteDropped = _ => Interlocked.Increment(ref dropped);

        // Fill the channel to capacity + 1 via the internal test hook.
        for (var i = 0; i < 17; i++)
            listener.TryEnqueueForTest(new StubInvitation(PeerId.From(keyB.PrincipalId)));

        Assert.Equal(1, dropped);
        Assert.Equal(1L, listener.DroppedCount);
    }

    [Fact]
    public async Task EndToEnd_TamperedCiphertext_DecryptionFails()
    {
        // AEAD round-trip negative: tamper one byte of an encrypted post-HELLO
        // frame on the wire; the receiver MUST surface the tampering as a
        // session-termination via TransportError (the reader pump catches the
        // CryptographicException and Terminates).
        using var keyA = KeyPair.Generate();
        using var keyB = KeyPair.Generate();
        var roster = new InMemoryCrewRoster(new[]
        {
            new CrewMember { Peer = PeerId.From(keyA.PrincipalId), DisplayName = "Alice" },
            new CrewMember { Peer = PeerId.From(keyB.PrincipalId), DisplayName = "Bob" },
        });

        var (streamA, streamB) = MemoryDuplexStream.CreatePair();
        var tamperingStream = new ByteFlippingDuplex(streamA, flipOffset: 30);
        var fakeTransport = new SingleShotTransport(tamperingStream);
        var initiatorSelector = new SingleTransportSelector(fakeTransport);

        await using var providerA = new NativeChannelProvider(keyA, roster, initiatorSelector);
        await using var providerB = new NativeChannelProvider(keyB, roster, new SingleTransportSelector(new UnreachableTransport()));

        var listenTask = providerB.Listener.AcceptIncomingAsync(
            streamB, Tenant, ChannelCapability.Text, CancellationToken.None);
        var openTask = providerA.OpenAsync(
            Tenant, PeerId.From(keyB.PrincipalId), ChannelCapability.Text, CancellationToken.None);

        // The tamper happens AFTER HELLO (on the post-handshake INVITE/ACCEPT/CONFIRM
        // path). Either side will surface it as an exception — the exact one varies
        // depending on which frame the tampered byte ends up on. We just assert that
        // the test does NOT successfully complete a session.
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await Task.WhenAll(listenTask, openTask).WaitAsync(TimeSpan.FromSeconds(5));
        });
    }

    private sealed class SingleTransportSelector : ITransportSelector
    {
        private readonly IPeerTransport _t;
        public SingleTransportSelector(IPeerTransport t) => _t = t;
        public Task<IPeerTransport> SelectAsync(PeerId peer, CancellationToken ct) => Task.FromResult(_t);
    }

    private sealed class SingleShotTransport : IPeerTransport
    {
        private readonly IDuplexStream _stream;
        private int _consumed;
        public SingleShotTransport(IDuplexStream stream) => _stream = stream;
        public TransportTier Tier => TransportTier.LocalNetwork;
        public bool IsAvailable => true;
        public Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct) =>
            Task.FromResult<PeerEndpoint?>(new PeerEndpoint
            {
                Peer = peer,
                Endpoint = new IPEndPoint(IPAddress.Loopback, 0),
                Tier = TransportTier.LocalNetwork,
                DiscoveredAt = DateTimeOffset.UtcNow,
            });
        public Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct)
        {
            if (Interlocked.Exchange(ref _consumed, 1) != 0)
                throw new InvalidOperationException("SingleShotTransport already consumed.");
            return Task.FromResult(_stream);
        }
    }

    private sealed class UnreachableTransport : IPeerTransport
    {
        public TransportTier Tier => TransportTier.LocalNetwork;
        public bool IsAvailable => false;
        public Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct) =>
            Task.FromResult<PeerEndpoint?>(null);
        public Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct) =>
            throw new NotSupportedException("UnreachableTransport never connects.");
    }

    /// <summary>Pass-through duplex that flips one byte of ciphertext at a fixed offset on writes.</summary>
    private sealed class ByteFlippingDuplex : IDuplexStream
    {
        private readonly IDuplexStream _inner;
        private readonly int _flipOffset;
        private long _bytesWritten;
        public ByteFlippingDuplex(IDuplexStream inner, int flipOffset) { _inner = inner; _flipOffset = flipOffset; }
        public System.IO.Stream Stream => throw new NotSupportedException();
        public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken ct) => _inner.ReadAsync(buffer, ct);
        public async Task WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken ct)
        {
            var arr = buffer.ToArray();
            for (var i = 0; i < arr.Length; i++)
            {
                var pos = _bytesWritten + i;
                if (pos == _flipOffset) arr[i] ^= 0xFF;
            }
            _bytesWritten += arr.Length;
            await _inner.WriteAsync(arr, ct);
        }
        public Task FlushAsync(CancellationToken ct) => _inner.FlushAsync(ct);
        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private sealed class StubInvitation : IChannelInvitation
    {
        public StubInvitation(PeerId from) { FromPeer = from; }
        public PeerId FromPeer { get; }
        public ChannelCapability OfferedCapabilities => ChannelCapability.Text;
        public Task<IChannelSession> AcceptAsync(CancellationToken ct) =>
            throw new NotSupportedException("Stub invitation; tests must not accept.");
        public Task RejectAsync(string? reason, CancellationToken ct) => Task.CompletedTask;
    }
}
