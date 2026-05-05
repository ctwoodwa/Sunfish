using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MessagePack;
using Sunfish.Blocks.CrewComms.Crypto;
using Sunfish.Blocks.CrewComms.Protocol;
using Sunfish.Blocks.CrewComms.Session;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Transport;
using Xunit;

namespace Sunfish.Blocks.CrewComms.Tests;

public class NativeChannelSessionTests
{
    private static readonly TenantId Tenant = new("acme");

    [Fact]
    public async Task SendTextAsync_BeforeActivate_ThrowsInvalidOperation()
    {
        var (_, _, session, _, _) = NewPair();
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => session.SendTextAsync("nope", CancellationToken.None));
        }
        finally
        {
            await session.DisposeAsync();
        }
    }

    [Fact]
    public async Task SendReceive_TextRoundTripsBetweenPeers()
    {
        var (initiator, responder, sessionA, sessionB, _) = NewPair();
        sessionA.Activate();
        sessionB.Activate();

        await sessionA.SendTextAsync("ahoy", CancellationToken.None);

        await using var enumerator = sessionB.ReceiveTextAsync(CancellationToken.None).GetAsyncEnumerator();
        using var readCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var moved = await enumerator.MoveNextAsync().AsTask().WaitAsync(readCts.Token);
        Assert.True(moved);
        Assert.Equal("ahoy", enumerator.Current);

        await sessionA.DisposeAsync();
        await sessionB.DisposeAsync();
    }

    [Fact]
    public async Task ReceiveTextAsync_SecondConsumerThrowsInvalidOperation()
    {
        var (_, _, sessionA, sessionB, _) = NewPair();
        sessionA.Activate();
        sessionB.Activate();

        // First enumerator must enter `await foreach` so that the second call's
        // counter increment triggers the rejection path. Use a cancellable
        // token so the first one can be unwound at end-of-test.
        using var firstCts = new CancellationTokenSource();
        var first = sessionB.ReceiveTextAsync(firstCts.Token).GetAsyncEnumerator();
        var firstMove = first.MoveNextAsync().AsTask();
        // Yield once so the first enumerator's body has incremented the counter.
        await Task.Delay(50);

        var second = sessionB.ReceiveTextAsync(CancellationToken.None).GetAsyncEnumerator();
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await second.MoveNextAsync());

        firstCts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async () => await firstMove);
        await first.DisposeAsync();
        await sessionA.DisposeAsync();
        await sessionB.DisposeAsync();
    }

    [Fact]
    public async Task RemoteBye_TerminatesSessionWithRemoteByeReason()
    {
        var (_, _, sessionA, sessionB, _) = NewPair();
        sessionA.Activate();
        sessionB.Activate();

        await sessionA.CloseAsync(CancellationToken.None);
        var reason = await sessionB.Completed.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(ChannelTerminationReason.RemoteBye, reason);

        await sessionA.DisposeAsync();
        await sessionB.DisposeAsync();
    }

    [Fact]
    public async Task LocalCloseAsync_TerminatesSessionWithLocalByeReason()
    {
        var (_, _, sessionA, sessionB, _) = NewPair();
        sessionA.Activate();
        sessionB.Activate();

        await sessionA.CloseAsync(CancellationToken.None);
        var reason = await sessionA.Completed.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(ChannelTerminationReason.LocalBye, reason);

        await sessionA.DisposeAsync();
        await sessionB.DisposeAsync();
    }

    [Fact]
    public async Task SendAudioFrameAsync_ThrowsNotSupportedWhenCapabilityNotNegotiated()
    {
        var (_, _, sessionA, _, _) = NewPair();
        sessionA.Activate();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => sessionA.SendAudioFrameAsync(new byte[] { 0xFF }, CancellationToken.None));
        await sessionA.DisposeAsync();
    }

    [Fact]
    public async Task State_TransitionsThroughIdleConnectingActiveTerminated()
    {
        var (_, _, sessionA, sessionB, _) = NewPair();
        Assert.Equal(ChannelSessionState.Connecting, sessionA.State);
        sessionA.Activate();
        sessionB.Activate();
        Assert.Equal(ChannelSessionState.Active, sessionA.State);

        await sessionA.CloseAsync(CancellationToken.None);
        Assert.Equal(ChannelSessionState.Terminated, sessionA.State);

        await sessionA.DisposeAsync();
        await sessionB.DisposeAsync();
    }

    private static (KeyPair keyA, KeyPair keyB, NativeChannelSession sessionA, NativeChannelSession sessionB, FakeTimeProvider time) NewPair()
    {
        var keyA = KeyPair.Generate();
        var keyB = KeyPair.Generate();
        var roster = new TestRoster(keyA, keyB);
        var time = new FakeTimeProvider();

        var (streamA, streamB) = MemoryDuplexStream.CreatePair();
        var framesA = new FrameProtocol(streamA);
        var framesB = new FrameProtocol(streamB);

        var hsA = new EncryptionHandshake(keyA, roster, Tenant);
        var hsB = new EncryptionHandshake(keyB, roster, Tenant);

        var sessionA = new NativeChannelSession(framesA, hsA, PeerId.From(keyB.PrincipalId), ChannelCapability.Text, time);
        var sessionB = new NativeChannelSession(framesB, hsB, PeerId.From(keyA.PrincipalId), ChannelCapability.Text, time);
        return (keyA, keyB, sessionA, sessionB, time);
    }

    internal sealed class TestRoster : ICrewRoster
    {
        private readonly System.Collections.Generic.IReadOnlyList<CrewMember> _members;
        public TestRoster(params KeyPair[] keys)
        {
            var list = new System.Collections.Generic.List<CrewMember>();
            var i = 0;
            foreach (var k in keys)
                list.Add(new CrewMember { Peer = PeerId.From(k.PrincipalId), DisplayName = $"member-{i++}" });
            _members = list;
        }
        public Task<System.Collections.Generic.IReadOnlyList<CrewMember>> GetCrewAsync(TenantId tenant, CancellationToken ct)
            => Task.FromResult(_members);
    }
}

internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = new(2026, 5, 5, 0, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        => new FakeTimer(callback, state, this);
    private sealed class FakeTimer : ITimer
    {
        private readonly TimerCallback _cb;
        private readonly object? _state;
        public FakeTimer(TimerCallback cb, object? state, FakeTimeProvider _) { _cb = cb; _state = state; }
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Fire() => _cb(_state);
    }
}
