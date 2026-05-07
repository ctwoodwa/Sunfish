using Microsoft.Extensions.DependencyInjection;
using Sunfish.Anchor.Services;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Sync.Discovery;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#59 Phase 2 — closes MVP-demo gap (B): empty roster post-pairing.
/// Verifies <see cref="TeamMembershipCrewRoster"/> wires
/// <see cref="IPeerDiscovery"/> + <see cref="IActiveTeamAccessor"/> into the
/// foundation <see cref="ICrewRoster"/> surface, scoped to the active team.
/// </summary>
public sealed class TeamMembershipCrewRosterTests
{
    private static readonly TeamId TeamA = new(Guid.Parse("00000000-0000-0000-0000-00000000000a"));
    private static readonly TeamId TeamB = new(Guid.Parse("00000000-0000-0000-0000-00000000000b"));
    private static readonly TenantId TenantA = new(TeamA.Value.ToString("D"));
    private static readonly TenantId TenantB = new(TeamB.Value.ToString("D"));

    [Fact]
    public async Task EmptyTeamWithNoActive_ReturnsEmpty()
    {
        var roster = new TeamMembershipCrewRoster(
            new FakePeerDiscovery(),
            new FakeActiveTeamAccessor(active: null));

        var crew = await roster.GetCrewAsync(TenantA, CancellationToken.None);

        Assert.Empty(crew);
    }

    [Fact]
    public async Task SingleSameTeamPeer_ReturnsCrewMember()
    {
        var peer = NewAdvertisement("peer-A1", TeamA);
        var roster = new TeamMembershipCrewRoster(
            new FakePeerDiscovery(peer),
            new FakeActiveTeamAccessor(active: ContextFor(TeamA)));

        var crew = await roster.GetCrewAsync(TenantA, CancellationToken.None);

        Assert.Single(crew);
        Assert.Equal(PeerId.From(Sunfish.Foundation.Crypto.PrincipalId.FromBytes(peer.PublicKey)), crew[0].Peer);
        Assert.False(string.IsNullOrEmpty(crew[0].DisplayName));
    }

    [Fact]
    public async Task MultipleSameTeamPeers_ReturnsAllOfThem()
    {
        var p1 = NewAdvertisement("peer-A1", TeamA);
        var p2 = NewAdvertisement("peer-A2", TeamA);
        var p3 = NewAdvertisement("peer-A3", TeamA);
        var roster = new TeamMembershipCrewRoster(
            new FakePeerDiscovery(p1, p2, p3),
            new FakeActiveTeamAccessor(active: ContextFor(TeamA)));

        var crew = await roster.GetCrewAsync(TenantA, CancellationToken.None);

        Assert.Equal(3, crew.Count);
    }

    [Fact]
    public async Task DifferentTeamPeers_AreFilteredOut_ForTenantIsolation()
    {
        var sameTeamPeer = NewAdvertisement("peer-A1", TeamA);
        var crossTeamPeer = NewAdvertisement("peer-B1", TeamB);
        var roster = new TeamMembershipCrewRoster(
            new FakePeerDiscovery(sameTeamPeer, crossTeamPeer),
            new FakeActiveTeamAccessor(active: ContextFor(TeamA)));

        var crew = await roster.GetCrewAsync(TenantA, CancellationToken.None);

        Assert.Single(crew);
        Assert.Equal(
            PeerId.From(Sunfish.Foundation.Crypto.PrincipalId.FromBytes(sameTeamPeer.PublicKey)),
            crew[0].Peer);
    }

    [Fact]
    public async Task TenantNotMatchingActiveTeam_ReturnsEmpty()
    {
        // Operator is on Team A but the caller asks about Team B's crew —
        // the MVP scope is single-active-team; cross-team queries return
        // empty so a stale chat tab from team B doesn't leak Team A peers.
        var peer = NewAdvertisement("peer-A1", TeamA);
        var roster = new TeamMembershipCrewRoster(
            new FakePeerDiscovery(peer),
            new FakeActiveTeamAccessor(active: ContextFor(TeamA)));

        var crew = await roster.GetCrewAsync(TenantB, CancellationToken.None);

        Assert.Empty(crew);
    }

    [Fact]
    public async Task MalformedPublicKey_IsSilentlyDropped_NotThrown()
    {
        // Defense-in-depth: the discovery layer SHOULD validate keys before
        // surfacing them, but a poisoned advertisement (bad-length public
        // key) must not poison the entire roster. PrincipalId.FromBytes
        // throws on length-mismatch; the roster catches and skips.
        var goodPeer = NewAdvertisement("peer-good", TeamA);
        var malformedPeer = new PeerAdvertisement(
            NodeId: "peer-malformed",
            Endpoint: "tcp://malformed:0",
            PublicKey: new byte[] { 0x00, 0x01, 0x02 }, // not 32 bytes
            TeamId: TeamA.Value.ToString("D"),
            SchemaVersion: "v0",
            Metadata: new Dictionary<string, string>());

        var roster = new TeamMembershipCrewRoster(
            new FakePeerDiscovery(goodPeer, malformedPeer),
            new FakeActiveTeamAccessor(active: ContextFor(TeamA)));

        var crew = await roster.GetCrewAsync(TenantA, CancellationToken.None);

        Assert.Single(crew);
    }

    [Fact]
    public async Task CrossTeamSwitch_ReturnsNewTeamCrewWithoutCache()
    {
        // The roster is a thin pass-through (no internal cache). After
        // operator switches active team, a subsequent GetCrewAsync with
        // the new tenant resolves to the new team's peers. No restart.
        var teamAPeer = NewAdvertisement("peer-A1", TeamA);
        var teamBPeer = NewAdvertisement("peer-B1", TeamB);
        var activeAccessor = new FakeActiveTeamAccessor(active: ContextFor(TeamA));
        var roster = new TeamMembershipCrewRoster(
            new FakePeerDiscovery(teamAPeer, teamBPeer),
            activeAccessor);

        var teamACrew = await roster.GetCrewAsync(TenantA, CancellationToken.None);
        Assert.Single(teamACrew);

        activeAccessor.SetActiveImmediate(ContextFor(TeamB));

        var teamBCrew = await roster.GetCrewAsync(TenantB, CancellationToken.None);
        Assert.Single(teamBCrew);
        Assert.NotEqual(teamACrew[0].Peer, teamBCrew[0].Peer);
    }

    [Fact]
    public async Task DisplayNameMetadata_IsPreferredOverNodeIdSuffix()
    {
        var peer = NewAdvertisement(
            "very-long-node-id-from-mdns",
            TeamA,
            displayName: "Bridge Alpha");
        var roster = new TeamMembershipCrewRoster(
            new FakePeerDiscovery(peer),
            new FakeActiveTeamAccessor(active: ContextFor(TeamA)));

        var crew = await roster.GetCrewAsync(TenantA, CancellationToken.None);

        Assert.Single(crew);
        Assert.Equal("Bridge Alpha", crew[0].DisplayName);
    }

    [Fact]
    public async Task NoDisplayNameMetadata_FallsBackToNodeIdSuffix()
    {
        var peer = NewAdvertisement(
            "very-long-node-id-from-mdns",
            TeamA,
            displayName: null);
        var roster = new TeamMembershipCrewRoster(
            new FakePeerDiscovery(peer),
            new FakeActiveTeamAccessor(active: ContextFor(TeamA)));

        var crew = await roster.GetCrewAsync(TenantA, CancellationToken.None);

        Assert.Single(crew);
        // 8-char prefix matches the Anchor sync-pipe naming convention
        // (sunfish-anchor-{nodeId[..8]}).
        Assert.Equal("very-lon", crew[0].DisplayName);
    }

    [Fact]
    public async Task NullArguments_ThrowOnConstruct()
    {
        var disco = new FakePeerDiscovery();
        var acc = new FakeActiveTeamAccessor(active: null);

        Assert.Throws<ArgumentNullException>(() =>
            new TeamMembershipCrewRoster(null!, acc));
        Assert.Throws<ArgumentNullException>(() =>
            new TeamMembershipCrewRoster(disco, null!));

        // Cancellation is honored (smoke check).
        var cts = new CancellationTokenSource();
        cts.Cancel();
        var roster = new TeamMembershipCrewRoster(disco, acc);
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => roster.GetCrewAsync(TenantA, cts.Token));
    }

    private static TeamContext ContextFor(TeamId teamId)
    {
        // TeamContext requires a non-null IServiceProvider in its ctor; an
        // empty one is sufficient for the tests since we only read TeamId.
        return new TeamContext(
            teamId,
            displayName: $"Team-{teamId.Value:N}",
            services: new ServiceCollection().BuildServiceProvider());
    }

    private static PeerAdvertisement NewAdvertisement(
        string nodeId,
        TeamId teamId,
        string? displayName = null)
    {
        var publicKey = new byte[32];
        // Seed unique keys per nodeId so PeerId.From distinguishes peers.
        var seed = System.Text.Encoding.UTF8.GetBytes(nodeId);
        Array.Copy(seed, publicKey, Math.Min(seed.Length, publicKey.Length));

        var metadata = new Dictionary<string, string>();
        if (displayName is not null)
        {
            metadata["display-name"] = displayName;
        }

        return new PeerAdvertisement(
            NodeId: nodeId,
            Endpoint: $"tcp://{nodeId}:0",
            PublicKey: publicKey,
            TeamId: teamId.Value.ToString("D"),
            SchemaVersion: "v0",
            Metadata: metadata);
    }

    private sealed class FakePeerDiscovery : IPeerDiscovery
    {
        private readonly List<PeerAdvertisement> _known;

        public FakePeerDiscovery(params PeerAdvertisement[] peers)
        {
            _known = peers.ToList();
        }

        public IReadOnlyCollection<PeerAdvertisement> KnownPeers => _known;

        public event EventHandler<PeerDiscoveredEventArgs>? PeerDiscovered;
        public event EventHandler<PeerLostEventArgs>? PeerLost;

        public Task StartAsync(PeerAdvertisement self, CancellationToken ct)
        {
            _ = PeerDiscovered;
            _ = PeerLost;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeActiveTeamAccessor : IActiveTeamAccessor
    {
        public FakeActiveTeamAccessor(TeamContext? active)
        {
            Active = active;
        }

        public TeamContext? Active { get; private set; }

        public event EventHandler<ActiveTeamChangedEventArgs>? ActiveChanged;

        public Task SetActiveAsync(TeamId teamId, CancellationToken ct)
        {
            // Tests use SetActiveImmediate to mutate without a factory dep.
            return Task.CompletedTask;
        }

        public void SetActiveImmediate(TeamContext? next)
        {
            var prev = Active;
            Active = next;
            ActiveChanged?.Invoke(this, new ActiveTeamChangedEventArgs(prev, next));
        }
    }
}
