using System;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.CrewComms;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Transport;
using Xunit;

namespace Sunfish.Blocks.CrewComms.Tests;

/// <summary>
/// W#45 P4.5 PR 3 — glare-resolution wiring on
/// <see cref="NativeChannelProvider"/>. Per the addendum
/// (icm/_state/handoffs/crew-comms-p45-stage06-addendum.md §PR 3),
/// at least 3 cases covering yielder + winner + no-glare paths.
/// <para>
/// Tests drive the resolution layer via the internal
/// <c>TryResolveGlareAsync</c> helper + direct manipulation of the
/// <c>_pendingOutbounds</c> dictionary (both internal-visible to this
/// test assembly via the existing <c>InternalsVisibleTo</c> on the
/// crew-comms package). End-to-end glare requires bidirectional
/// transport-pair coordination across two providers — that integration
/// surface is exercised manually on the demo path. The unit-level
/// coverage here pins the resolution semantics that an end-to-end test
/// would otherwise be the only safety net for.
/// </para>
/// </summary>
public sealed class GlareResolutionTests
{
    private static readonly TenantId Tenant = new("acme");

    [Fact]
    public async Task NoGlare_NoPendingOutbound_InvitationFallsThrough()
    {
        var provider = NewProvider(out _);
        var invitation = new FakeInvitation(new PeerId("remote-peer-no-glare"));

        var consumed = await provider.TryResolveGlareAsync(invitation, CancellationToken.None);

        Assert.False(consumed); // Caller should yield this invitation to its consumers.
        Assert.False(invitation.AcceptCalled);
        Assert.False(invitation.RejectCalled);
        await provider.DisposeAsync();
    }

    [Fact]
    public async Task LocalYielder_ResolvesPendingTcsWithInboundSession()
    {
        // Construct a provider with a fixed-low local peer id so
        // GlareResolver.IsLocalYielder returns true against any
        // higher-id remote peer.
        var lowKey = KeyPairWithLowId();
        var provider = NewProviderWithPeer(out _, lowKey);
        var localPeer = PeerId.From(lowKey.PrincipalId);

        var remotePeer = new PeerId("zzzz-higher-id-than-local");
        var pendingTcs = new TaskCompletionSource<IChannelSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        provider._pendingOutbounds[remotePeer] = pendingTcs;

        var stubSession = new StubSession(remotePeer);
        var invitation = new FakeInvitation(remotePeer)
        {
            AcceptResult = stubSession,
        };

        var consumed = await provider.TryResolveGlareAsync(invitation, CancellationToken.None);

        Assert.True(consumed);
        Assert.True(invitation.AcceptCalled);
        Assert.False(invitation.RejectCalled);
        Assert.True(pendingTcs.Task.IsCompletedSuccessfully);
        Assert.Same(stubSession, await pendingTcs.Task);
        Assert.NotEqual(default, localPeer);

        await provider.DisposeAsync();
    }

    [Fact]
    public async Task LocalWinner_RejectsInbound_DoesNotResolveTcs()
    {
        // Fixed-high local peer id → IsLocalYielder returns false; the
        // wrapper rejects the inbound and lets the outbound finish.
        var provider = NewProviderWithPeer(out _, KeyPairWithHighId());

        var remotePeer = new PeerId("aaaa-lower-id-than-local");
        var pendingTcs = new TaskCompletionSource<IChannelSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        provider._pendingOutbounds[remotePeer] = pendingTcs;

        var invitation = new FakeInvitation(remotePeer);

        var consumed = await provider.TryResolveGlareAsync(invitation, CancellationToken.None);

        Assert.True(consumed);
        Assert.False(invitation.AcceptCalled);
        Assert.True(invitation.RejectCalled);
        Assert.False(pendingTcs.Task.IsCompleted); // Outbound is still in flight; not our concern here.

        await provider.DisposeAsync();
    }

    [Fact]
    public async Task LocalYielder_AcceptThrows_PropagatesToTcs()
    {
        // Defense path: if AcceptAsync fails (transport error, handshake
        // mismatch, etc.), the exception MUST propagate to the awaiting
        // OpenAsync caller via TrySetException. Otherwise the caller
        // hangs forever on the TCS.
        var provider = NewProviderWithPeer(out _, KeyPairWithLowId());

        var remotePeer = new PeerId("zzzz-higher");
        var pendingTcs = new TaskCompletionSource<IChannelSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        provider._pendingOutbounds[remotePeer] = pendingTcs;

        var failure = new InvalidOperationException("simulated handshake failure");
        var invitation = new FakeInvitation(remotePeer)
        {
            AcceptThrows = failure,
        };

        var consumed = await provider.TryResolveGlareAsync(invitation, CancellationToken.None);

        Assert.True(consumed);
        Assert.True(invitation.AcceptCalled);
        Assert.True(pendingTcs.Task.IsFaulted);
        var thrown = pendingTcs.Task.Exception?.InnerException;
        Assert.Same(failure, thrown);

        await provider.DisposeAsync();
    }

    [Fact]
    public async Task LocalYielder_OutboundWonRace_StaleInboundIsClosed()
    {
        // Race condition: by the time AcceptAsync resolves, the outbound
        // OpenAsync may already have completed and the TCS may have been
        // marked cancelled. The wrapper must dispose the stale inbound
        // session rather than leaking transport resources.
        var provider = NewProviderWithPeer(out _, KeyPairWithLowId());

        var remotePeer = new PeerId("zzzz-higher");
        var pendingTcs = new TaskCompletionSource<IChannelSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        // Pre-cancel the TCS to simulate "outbound won the race."
        pendingTcs.TrySetCanceled();
        provider._pendingOutbounds[remotePeer] = pendingTcs;

        var staleSession = new StubSession(remotePeer);
        var invitation = new FakeInvitation(remotePeer)
        {
            AcceptResult = staleSession,
        };

        var consumed = await provider.TryResolveGlareAsync(invitation, CancellationToken.None);

        Assert.True(consumed);
        Assert.True(staleSession.CloseCalled);
        Assert.True(staleSession.DisposeCalled);

        await provider.DisposeAsync();
    }

    [Fact]
    public async Task DuplicateOpenAsync_ToSamePeer_ThrowsInvalidOperation()
    {
        // First-write-wins guard — the second concurrent OpenAsync to
        // the same peer must throw rather than silently overriding the
        // pending TCS. This pins the contract that the glare-wiring
        // does NOT collapse two outbounds into one entry.
        var provider = NewProvider(out _);
        var peer = new PeerId("conflict-peer");

        // Simulate an already-pending entry by injecting one directly.
        var firstTcs = new TaskCompletionSource<IChannelSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        provider._pendingOutbounds[peer] = firstTcs;

        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.OpenAsync(Tenant, peer, ChannelCapability.Text, CancellationToken.None));
        }
        finally
        {
            firstTcs.TrySetCanceled();
            provider._pendingOutbounds.TryRemove(peer, out _);
            await provider.DisposeAsync();
        }
    }

    private static NativeChannelProvider NewProvider(out KeyPair identity)
    {
        identity = KeyPair.Generate();
        var roster = new EmptyRoster();
        var selector = new SingleTransportSelector(new UnreachableTransport());
        return new NativeChannelProvider(identity, roster, selector);
    }

    private static NativeChannelProvider NewProviderWithPeer(out KeyPair identity, KeyPair fixedKey)
    {
        identity = fixedKey;
        var roster = new EmptyRoster();
        var selector = new SingleTransportSelector(new UnreachableTransport());
        return new NativeChannelProvider(identity, roster, selector);
    }

    /// <summary>
    /// Produces a KeyPair whose PeerId.Value (base64url of the public
    /// key) sorts low compared to typical random keys — sufficient for
    /// the test to assert IsLocalYielder against a deliberately-high
    /// remote peer-id literal.
    /// </summary>
    private static KeyPair KeyPairWithLowId()
    {
        // KeyPair.Generate uses random bytes; we can't fix the byte
        // pattern without reaching into NSec internals. Generate a
        // few candidates and pick one whose base64url sorts lower
        // than a high-id literal (we use "zzzz..." as the comparison
        // anchor). Cap iterations so the test is deterministic.
        for (var i = 0; i < 16; i++)
        {
            var k = KeyPair.Generate();
            var pid = PeerId.From(k.PrincipalId);
            if (string.CompareOrdinal(pid.Value, "zzzz") < 0)
            {
                return k;
            }
            k.Dispose();
        }
        // base64url-encoded Ed25519 public keys never start above 'z'
        // in practice; the loop is bounded for safety.
        throw new InvalidOperationException("Could not generate a low-id key pair after 16 tries.");
    }

    private static KeyPair KeyPairWithHighId()
    {
        for (var i = 0; i < 16; i++)
        {
            var k = KeyPair.Generate();
            var pid = PeerId.From(k.PrincipalId);
            if (string.CompareOrdinal(pid.Value, "aaaa") > 0)
            {
                return k;
            }
            k.Dispose();
        }
        throw new InvalidOperationException("Could not generate a high-id key pair after 16 tries.");
    }

    private sealed class EmptyRoster : ICrewRoster
    {
        public Task<System.Collections.Generic.IReadOnlyList<CrewMember>> GetCrewAsync(TenantId tenant, CancellationToken ct)
            => Task.FromResult<System.Collections.Generic.IReadOnlyList<CrewMember>>(Array.Empty<CrewMember>());
    }

    private sealed class FakeInvitation : IChannelInvitation
    {
        public FakeInvitation(PeerId fromPeer)
        {
            FromPeer = fromPeer;
        }

        public PeerId FromPeer { get; }
        public ChannelCapability OfferedCapabilities { get; init; } = ChannelCapability.Text;

        public IChannelSession? AcceptResult { get; set; }
        public Exception? AcceptThrows { get; set; }
        public bool AcceptCalled { get; private set; }
        public bool RejectCalled { get; private set; }

        public Task<IChannelSession> AcceptAsync(CancellationToken ct)
        {
            AcceptCalled = true;
            if (AcceptThrows is not null)
            {
                return Task.FromException<IChannelSession>(AcceptThrows);
            }
            return Task.FromResult(AcceptResult ?? throw new InvalidOperationException("AcceptResult unset."));
        }

        public Task RejectAsync(string? reason, CancellationToken ct)
        {
            RejectCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSession : IChannelSession
    {
        public StubSession(PeerId peer) { Peer = peer; }

        public PeerId Peer { get; }
        public ChannelCapability Capability { get; } = ChannelCapability.Text;
        public ChannelSessionState State { get; private set; } = ChannelSessionState.Active;
        public Task<ChannelTerminationReason> Completed { get; } =
            new TaskCompletionSource<ChannelTerminationReason>().Task;

        public bool CloseCalled { get; private set; }
        public bool DisposeCalled { get; private set; }

        public Task SendTextAsync(string message, CancellationToken ct) => Task.CompletedTask;
        public IAsyncEnumerable<string> ReceiveTextAsync(CancellationToken ct) => EmptyAsync<string>();
        public Task SendAudioFrameAsync(ReadOnlyMemory<byte> opusFrame, CancellationToken ct)
            => throw new NotSupportedException();
        public IAsyncEnumerable<ReadOnlyMemory<byte>> ReceiveAudioFramesAsync(CancellationToken ct)
            => EmptyAsync<ReadOnlyMemory<byte>>();
        public Task SendTypingAsync(CancellationToken ct) => Task.CompletedTask;
        public Task SendDeliveredAsync(Guid messageId, CancellationToken ct) => Task.CompletedTask;
        public IAsyncEnumerable<DateTimeOffset> ReceiveTypingAsync(CancellationToken ct) => EmptyAsync<DateTimeOffset>();
        public IAsyncEnumerable<Guid> ReceiveDeliveredAsync(CancellationToken ct) => EmptyAsync<Guid>();

        public Task CloseAsync(CancellationToken ct)
        {
            CloseCalled = true;
            State = ChannelSessionState.Terminated;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCalled = true;
            return ValueTask.CompletedTask;
        }

        private static async IAsyncEnumerable<T> EmptyAsync<T>()
        {
            await Task.Yield();
            yield break;
        }
    }

    private sealed class UnreachableTransport : IPeerTransport
    {
        public TransportTier Tier => TransportTier.LocalNetwork;
        public bool IsAvailable => false;
        public Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct) =>
            Task.FromResult<PeerEndpoint?>(null);
        public Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct) =>
            throw new NotSupportedException("UnreachableTransport never connects (test stub).");
    }

    private sealed class SingleTransportSelector : ITransportSelector
    {
        private readonly IPeerTransport _t;
        public SingleTransportSelector(IPeerTransport t) { _t = t; }
        public Task<IPeerTransport> SelectAsync(PeerId peer, CancellationToken ct) => Task.FromResult(_t);
    }
}
