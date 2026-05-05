using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.CrewComms.Crypto;
using Sunfish.Blocks.CrewComms.Presence;
using Sunfish.Blocks.CrewComms.Protocol;
using Sunfish.Blocks.CrewComms.Session;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Transport;
using Xunit;

namespace Sunfish.Blocks.CrewComms.Tests;

public class PresenceBusTests
{
    private static readonly TenantId Tenant = new("acme");

    [Fact]
    public void OnHeartbeatReceived_PopulatesSnapshot()
    {
        using var keyA = KeyPair.Generate();
        using var keyB = KeyPair.Generate();
        var roster = new Roster(keyA, keyB);
        var time = new FakeTimeProvider();

        var bus = new PresenceBus(keyA, roster, Tenant, time);

        var hb = new PresenceHeartbeat
        {
            PeerId = PeerId.From(keyB.PrincipalId).Value,
            TenantId = Tenant.Value,
            Caps = (byte)ChannelCapability.Text,
            Timestamp = time.GetUtcNow().ToUnixTimeMilliseconds(),
            Signature = new byte[64],
        };
        bus.OnHeartbeatReceived(PeerId.From(keyB.PrincipalId), hb, TransportTier.LocalNetwork, "Bob");

        var snapshot = bus.GetSnapshot();
        Assert.Single(snapshot);
        Assert.Equal(PeerId.From(keyB.PrincipalId), snapshot[0].Peer);
        Assert.Equal(TransportTier.LocalNetwork, snapshot[0].Via);
        Assert.Equal("Bob", snapshot[0].DisplayName);
    }

    [Fact]
    public void TtlEviction_RemovesPeerAfter45Seconds()
    {
        using var keyA = KeyPair.Generate();
        using var keyB = KeyPair.Generate();
        var roster = new Roster(keyA, keyB);
        var time = new FakeTimeProvider();
        var bus = new PresenceBus(keyA, roster, Tenant, time);

        var hb = new PresenceHeartbeat
        {
            PeerId = PeerId.From(keyB.PrincipalId).Value,
            TenantId = Tenant.Value,
            Caps = 1,
            Timestamp = time.GetUtcNow().ToUnixTimeMilliseconds(),
            Signature = new byte[64],
        };
        bus.OnHeartbeatReceived(PeerId.From(keyB.PrincipalId), hb, TransportTier.LocalNetwork, "Bob");
        Assert.Single(bus.GetSnapshot());

        // 44 seconds — still alive.
        time.Advance(TimeSpan.FromSeconds(44));
        Assert.Single(bus.GetSnapshot());

        // Past 45 seconds — evicted.
        time.Advance(TimeSpan.FromSeconds(2));
        Assert.Empty(bus.GetSnapshot());
    }

    [Fact]
    public void BuildHeartbeatPayload_VerifiableAgainstLocalIdentity()
    {
        using var keyA = KeyPair.Generate();
        var roster = new Roster(keyA);
        var time = new FakeTimeProvider();
        var bus = new PresenceBus(keyA, roster, Tenant, time);

        var payload = bus.BuildHeartbeatPayload(ChannelCapability.Text);
        var hb = MessagePack.MessagePackSerializer.Deserialize<PresenceHeartbeat>(
            payload, CrewCommsResolver.Options);

        // Round-trip the heartbeat through the static verifier — the
        // identity public key in the embedded peerId field must match.
        Assert.True(EncryptionHandshake.VerifyHeartbeat(hb, keyA.PrincipalId.AsSpan()));
        Assert.Equal(PeerId.From(keyA.PrincipalId).Value, hb.PeerId);
        Assert.Equal(Tenant.Value, hb.TenantId);
        Assert.Equal((byte)ChannelCapability.Text, hb.Caps);
    }

    [Fact]
    public async Task ProbeRelayPeers_RespectsBoundedConcurrency()
    {
        using var keyA = KeyPair.Generate();
        var rosterPeers = Enumerable.Range(0, 10)
            .Select(_ => KeyPair.Generate())
            .ToArray();
        var roster = new Roster(new[] { keyA }.Concat(rosterPeers).ToArray());
        var time = new FakeTimeProvider();
        var bus = new PresenceBus(keyA, roster, Tenant, time);

        var relay = new ConcurrencyTrackingRelay();
        var probed = await bus.ProbeRelayPeersAsync(relay, CancellationToken.None);

        Assert.True(relay.MaxObservedConcurrent <= 3,
            $"Relay probe concurrency was {relay.MaxObservedConcurrent}; expected ≤ 3.");
        Assert.Equal(10, probed); // All 10 non-self roster peers got a probe.
    }

    [Fact]
    public async Task ProbeRelayPeers_RejectsNonRelayTransport()
    {
        using var keyA = KeyPair.Generate();
        var roster = new Roster(keyA);
        var bus = new PresenceBus(keyA, roster, Tenant, new FakeTimeProvider());

        var local = new ConcurrencyTrackingRelay { TierOverride = TransportTier.LocalNetwork };
        await Assert.ThrowsAsync<ArgumentException>(
            () => bus.ProbeRelayPeersAsync(local, CancellationToken.None));
    }

    [Fact]
    public async Task InSessionKeepalive_FiresAfter20SecondsOfSilence()
    {
        // Wires PresenceBus → NativeChannelSession → in-memory pipe pair.
        // Verifies that MaybeSendKeepaliveAsync (called by the bus tick)
        // emits a HEARTBEAT frame on the active stream when the session
        // has been silent ≥ 20s, and skips the emission otherwise.
        using var keyA = KeyPair.Generate();
        using var keyB = KeyPair.Generate();
        var roster = new Roster(keyA, keyB);
        var time = new FakeTimeProvider();

        var (streamA, streamB) = MemoryDuplexStream.CreatePair();
        var framesA = new FrameProtocol(streamA);
        var framesB = new FrameProtocol(streamB);
        var hsA = new EncryptionHandshake(keyA, roster, Tenant);
        var hsB = new EncryptionHandshake(keyB, roster, Tenant);
        await using var sessionA = new NativeChannelSession(
            framesA, hsA, PeerId.From(keyB.PrincipalId), ChannelCapability.Text, time);
        await using var sessionB = new NativeChannelSession(
            framesB, hsB, PeerId.From(keyA.PrincipalId), ChannelCapability.Text, time);
        sessionA.Activate();
        sessionB.Activate();

        var bus = new PresenceBus(keyA, roster, Tenant, time);
        var hbPayload = bus.BuildHeartbeatPayload(ChannelCapability.Text);

        // 19 seconds of silence — keepalive does NOT fire.
        time.Advance(TimeSpan.FromSeconds(19));
        var fired19 = await sessionA.MaybeSendKeepaliveAsync(hbPayload, CancellationToken.None);
        Assert.False(fired19, "Keepalive should not fire before 20s silence threshold.");

        // Advance to 21s of silence — keepalive DOES fire.
        time.Advance(TimeSpan.FromSeconds(2));
        var fired21 = await sessionA.MaybeSendKeepaliveAsync(hbPayload, CancellationToken.None);
        Assert.True(fired21, "Keepalive should fire after 20s silence threshold.");
    }

    [Fact]
    public void GetSnapshot_FiltersStaleEntriesEachCall()
    {
        using var keyA = KeyPair.Generate();
        using var keyB = KeyPair.Generate();
        using var keyC = KeyPair.Generate();
        var roster = new Roster(keyA, keyB, keyC);
        var time = new FakeTimeProvider();
        var bus = new PresenceBus(keyA, roster, Tenant, time);

        bus.OnHeartbeatReceived(PeerId.From(keyB.PrincipalId), new PresenceHeartbeat
        {
            PeerId = PeerId.From(keyB.PrincipalId).Value,
            TenantId = Tenant.Value,
            Caps = 1,
            Timestamp = time.GetUtcNow().ToUnixTimeMilliseconds(),
            Signature = new byte[64],
        }, TransportTier.LocalNetwork, "Bob");

        time.Advance(TimeSpan.FromSeconds(30));

        bus.OnHeartbeatReceived(PeerId.From(keyC.PrincipalId), new PresenceHeartbeat
        {
            PeerId = PeerId.From(keyC.PrincipalId).Value,
            TenantId = Tenant.Value,
            Caps = 1,
            Timestamp = time.GetUtcNow().ToUnixTimeMilliseconds(),
            Signature = new byte[64],
        }, TransportTier.LocalNetwork, "Carol");

        time.Advance(TimeSpan.FromSeconds(20)); // Bob: 50s old (evicted); Carol: 20s old (alive).
        var snap = bus.GetSnapshot();
        Assert.Single(snap);
        Assert.Equal(PeerId.From(keyC.PrincipalId), snap[0].Peer);
    }

    private sealed class Roster : ICrewRoster
    {
        private readonly IReadOnlyList<CrewMember> _members;
        public Roster(params KeyPair[] keys)
        {
            var list = new List<CrewMember>();
            var i = 0;
            foreach (var k in keys)
                list.Add(new CrewMember { Peer = PeerId.From(k.PrincipalId), DisplayName = $"member-{i++}" });
            _members = list;
        }
        public Task<IReadOnlyList<CrewMember>> GetCrewAsync(TenantId tenant, CancellationToken ct)
            => Task.FromResult(_members);
    }

    private sealed class ConcurrencyTrackingRelay : IPeerTransport
    {
        private int _inFlight;
        public int MaxObservedConcurrent { get; private set; }
        public TransportTier TierOverride { get; set; } = TransportTier.ManagedRelay;
        public TransportTier Tier => TierOverride;
        public bool IsAvailable => true;
        public async Task<PeerEndpoint?> ResolvePeerAsync(PeerId peer, CancellationToken ct)
        {
            var current = Interlocked.Increment(ref _inFlight);
            try
            {
                if (current > MaxObservedConcurrent) MaxObservedConcurrent = current;
                await Task.Delay(50, ct);
                return new PeerEndpoint
                {
                    Peer = peer,
                    Endpoint = new System.Net.IPEndPoint(System.Net.IPAddress.Loopback, 9001),
                    Tier = Tier,
                    DiscoveredAt = DateTimeOffset.UtcNow,
                };
            }
            finally
            {
                Interlocked.Decrement(ref _inFlight);
            }
        }
        public Task<IDuplexStream> ConnectAsync(PeerId peer, CancellationToken ct)
            => throw new NotSupportedException();
    }
}
