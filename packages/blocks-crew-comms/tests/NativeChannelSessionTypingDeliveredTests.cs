using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.CrewComms.Crypto;
using Sunfish.Blocks.CrewComms.Protocol;
using Sunfish.Blocks.CrewComms.Session;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Blocks.CrewComms.Tests;

/// <summary>
/// W#45 P4.5 PR 2 — TYPING + DELIVERED round-trip tests on
/// <see cref="NativeChannelSession"/>. Per the addendum hand-off
/// (icm/_state/handoffs/crew-comms-p45-stage06-addendum.md §PR 2):
/// at least 3 cases (TYPING round-trip; DELIVERED round-trip; TYPING
/// drop-oldest under storm).
/// </summary>
public sealed class NativeChannelSessionTypingDeliveredTests
{
    private static readonly TenantId Tenant = new("acme");

    [Fact]
    public async Task TypingIndicator_ReceivesTimestamp()
    {
        var (_, _, sessionA, sessionB, time) = NewPair();
        sessionA.Activate();
        sessionB.Activate();

        var testStart = time.GetUtcNow();
        await sessionA.SendTypingAsync(CancellationToken.None);

        // Drain one item from B's typing stream within a short timeout.
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        DateTimeOffset received = default;
        var found = false;
        await foreach (var ts in sessionB.ReceiveTypingAsync(cts.Token))
        {
            received = ts;
            found = true;
            break;
        }

        Assert.True(found, "Expected to receive at least one TYPING timestamp.");
        Assert.True(received >= testStart, $"Received timestamp {received:O} should be >= test start {testStart:O}.");

        await CleanupAsync(sessionA, sessionB);
    }

    [Fact]
    public async Task DeliveredAck_RoundTrip()
    {
        var (_, _, sessionA, sessionB, _) = NewPair();
        sessionA.Activate();
        sessionB.Activate();

        var messageId = Guid.NewGuid();
        await sessionB.SendDeliveredAsync(messageId, CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        Guid received = default;
        var found = false;
        await foreach (var id in sessionA.ReceiveDeliveredAsync(cts.Token))
        {
            received = id;
            found = true;
            break;
        }

        Assert.True(found, "Expected to receive a DELIVERED message-id.");
        Assert.Equal(messageId, received);

        await CleanupAsync(sessionA, sessionB);
    }

    [Fact]
    public async Task Typing_DropOldest_NoBoundViolation()
    {
        // Send more typing frames than the bounded(8) channel can hold;
        // BoundedChannelFullMode.DropOldest must keep the latest 8 without
        // throwing or blocking the sender.
        var (_, _, sessionA, sessionB, _) = NewPair();
        sessionA.Activate();
        sessionB.Activate();

        for (var i = 0; i < 12; i++)
        {
            await sessionA.SendTypingAsync(CancellationToken.None);
        }

        // Wait briefly for the wire-pair pump to drain the 12 frames into B's
        // bounded stream — the DropOldest policy means B retains at most 8.
        await Task.Delay(150);

        // Cancel the receive enumeration once the channel is empty so the
        // test doesn't hang. We collect by polling the bounded channel via
        // a short-window enumeration with our own cancellation.
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var collected = 0;
        try
        {
            await foreach (var _ in sessionB.ReceiveTypingAsync(cts.Token))
            {
                collected++;
                if (collected >= 8) break;
            }
        }
        catch (OperationCanceledException) { /* expected on the timeout window */ }

        Assert.True(collected <= 8, $"Bounded(8, DropOldest) should retain at most 8; got {collected}.");
        Assert.True(collected >= 1, "Expected at least one TYPING frame delivered after the storm.");

        await CleanupAsync(sessionA, sessionB);
    }

    [Fact]
    public async Task SendTyping_BeforeActivate_Throws()
    {
        var (_, _, sessionA, sessionB, _) = NewPair();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sessionA.SendTypingAsync(CancellationToken.None));

        await CleanupAsync(sessionA, sessionB);
    }

    [Fact]
    public async Task SendDelivered_BeforeActivate_Throws()
    {
        var (_, _, sessionA, sessionB, _) = NewPair();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sessionA.SendDeliveredAsync(Guid.NewGuid(), CancellationToken.None));

        await CleanupAsync(sessionA, sessionB);
    }

    [Fact]
    public async Task DeliveredMessageId_RoundTripsExactBytes()
    {
        // Pin the RFC 4122 BE encoding round-trip: a Guid sent on one side
        // must arrive byte-identical on the other (the canonical risk is
        // that endianness drift in WriteBigEndian ↔ ReadBigEndian re-orders
        // the first three groups).
        var (_, _, sessionA, sessionB, _) = NewPair();
        sessionA.Activate();
        sessionB.Activate();

        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
        foreach (var id in ids)
        {
            await sessionB.SendDeliveredAsync(id, CancellationToken.None);
        }

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var received = new System.Collections.Generic.List<Guid>();
        await foreach (var id in sessionA.ReceiveDeliveredAsync(cts.Token))
        {
            received.Add(id);
            if (received.Count == ids.Count) break;
        }

        Assert.Equal(ids, received);

        await CleanupAsync(sessionA, sessionB);
    }

    private static (KeyPair keyA, KeyPair keyB, NativeChannelSession sessionA, NativeChannelSession sessionB, FakeTimeProvider time) NewPair()
    {
        var keyA = KeyPair.Generate();
        var keyB = KeyPair.Generate();
        var roster = new NativeChannelSessionTests.TestRoster(keyA, keyB);
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

    private static async Task CleanupAsync(NativeChannelSession a, NativeChannelSession b)
    {
        try { await a.CloseAsync(CancellationToken.None); } catch { }
        try { await b.CloseAsync(CancellationToken.None); } catch { }
        await a.DisposeAsync();
        await b.DisposeAsync();
    }
}
