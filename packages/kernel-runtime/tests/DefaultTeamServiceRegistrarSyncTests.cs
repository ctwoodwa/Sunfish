using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Lease;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Sync.Gossip;
using Sunfish.Kernel.Sync.Identity;
using Sunfish.Kernel.Sync.Protocol;

namespace Sunfish.Kernel.Runtime.Tests;

/// <summary>
/// Wave 6.3.C integration tests — verifies that
/// <see cref="DefaultTeamServiceRegistrar.Compose"/> installs a per-team
/// <see cref="INodeIdentityProvider"/> (derived via
/// <see cref="TeamScopedNodeIdentity.Derive(NodeIdentity, string, ITeamSubkeyDerivation)"/>),
/// a per-team <see cref="ISyncDaemonTransport"/> bound to a
/// <see cref="TeamPaths.TransportEndpoint"/>, and per-team
/// <see cref="IGossipDaemon"/> + <see cref="ILeaseCoordinator"/> instances
/// whose identity + endpoint derive deterministically from the root seed +
/// team id.
/// </summary>
/// <remarks>
/// These tests do NOT start the gossip daemon's round scheduler and do NOT
/// attempt a cross-team gossip exchange — that's integration territory and
/// would require real socket binds (Unix) or live named pipes (Windows). The
/// 6.3.C scope is DI composition; end-to-end multi-team gossip traffic is
/// covered by 6.5/6.6 test harnesses.
/// </remarks>
public sealed class DefaultTeamServiceRegistrarSyncTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly IEd25519Signer _signer = new Ed25519Signer();
    private readonly ITeamSubkeyDerivation _subkeyDerivation;
    private readonly ISqlCipherKeyDerivation _sqlCipherKeyDerivation = new SqlCipherKeyDerivation();
    private readonly NodeIdentity _rootIdentity;

    public DefaultTeamServiceRegistrarSyncTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "sunfish-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        _subkeyDerivation = new TeamSubkeyDerivation(_signer);
        _rootIdentity = BuildRootIdentity(_signer, fillByte: 0x42);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; filesystem races on Windows are not a test failure.
        }
    }

    [Fact]
    public async Task Two_teams_get_distinct_gossip_daemons()
    {
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _subkeyDerivation, _rootIdentity, _sqlCipherKeyDerivation);
        await using var factory = new TeamContextFactory(registrar);

        var teamA = TeamId.New();
        var teamB = TeamId.New();

        var ctxA = await factory.GetOrCreateAsync(teamA, "Team A", CancellationToken.None);
        var ctxB = await factory.GetOrCreateAsync(teamB, "Team B", CancellationToken.None);

        var gossipA = ctxA.Services.GetRequiredService<IGossipDaemon>();
        var gossipB = ctxB.Services.GetRequiredService<IGossipDaemon>();

        Assert.NotSame(gossipA, gossipB);

        // VectorClock is per-team too — sync-daemon state cannot leak across
        // teams, so each team's daemon must own its own clock instance.
        var clockA = ctxA.Services.GetRequiredService<VectorClock>();
        var clockB = ctxB.Services.GetRequiredService<VectorClock>();
        Assert.NotSame(clockA, clockB);
    }

    [Fact]
    public async Task Two_teams_get_distinct_node_identities_with_distinct_public_keys()
    {
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _subkeyDerivation, _rootIdentity, _sqlCipherKeyDerivation);
        await using var factory = new TeamContextFactory(registrar);

        var teamA = TeamId.New();
        var teamB = TeamId.New();

        var ctxA = await factory.GetOrCreateAsync(teamA, "Team A", CancellationToken.None);
        var ctxB = await factory.GetOrCreateAsync(teamB, "Team B", CancellationToken.None);

        var idA = ctxA.Services.GetRequiredService<INodeIdentityProvider>().Current;
        var idB = ctxB.Services.GetRequiredService<INodeIdentityProvider>().Current;

        // The HKDF info string includes the team id, so two teams produce
        // two independent keypairs. Anyone observing both teams' HELLO
        // messages cannot correlate membership by public key — ADR 0032
        // §Device identity.
        Assert.False(idA.PublicKey.AsSpan().SequenceEqual(idB.PublicKey),
            "Team-scoped public keys must differ across teams.");
        Assert.False(idA.PrivateKey.AsSpan().SequenceEqual(idB.PrivateKey),
            "Team-scoped private keys must differ across teams.");

        // Neither team's key may equal the root — the point of subkey
        // derivation is that operators who compromise a team-scope key
        // cannot recover the root, and that the root never appears on the
        // wire.
        Assert.False(idA.PublicKey.AsSpan().SequenceEqual(_rootIdentity.PublicKey),
            "Team A's public key must NOT equal the root public key.");
        Assert.False(idB.PublicKey.AsSpan().SequenceEqual(_rootIdentity.PublicKey),
            "Team B's public key must NOT equal the root public key.");
    }

    [Fact]
    public async Task Two_teams_get_distinct_lease_coordinators_with_distinct_localNodeIds()
    {
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _subkeyDerivation, _rootIdentity, _sqlCipherKeyDerivation);
        await using var factory = new TeamContextFactory(registrar);

        var teamA = TeamId.New();
        var teamB = TeamId.New();

        var ctxA = await factory.GetOrCreateAsync(teamA, "Team A", CancellationToken.None);
        var ctxB = await factory.GetOrCreateAsync(teamB, "Team B", CancellationToken.None);

        var leaseA = ctxA.Services.GetRequiredService<ILeaseCoordinator>();
        var leaseB = ctxB.Services.GetRequiredService<ILeaseCoordinator>();

        Assert.NotSame(leaseA, leaseB);

        // _localNodeId is private state on FleaseLeaseCoordinator. Reflection
        // is the cleanest way to pin the derivation without exposing it
        // publicly just for tests — FleaseLeaseCoordinator is marked sealed
        // and lives in Sunfish.Kernel.Lease.
        var localA = ReadLocalNodeId(leaseA);
        var localB = ReadLocalNodeId(leaseB);

        Assert.NotEqual(localA, localB);
        // Derivation contract: 32 hex chars = 16 bytes, lowercase, no
        // separators. Matches the first-16-bytes-of-public-key scheme.
        Assert.Equal(32, localA.Length);
        Assert.Equal(32, localB.Length);
        Assert.Matches("^[0-9a-f]{32}$", localA);
        Assert.Matches("^[0-9a-f]{32}$", localB);
    }

    [Fact]
    public async Task Deterministic_localNodeId_roundtrip_across_registrars()
    {
        // Two separately-composed registrars built against the same root
        // seed must produce identical localNodeIds for the same team id.
        // This pins the "deterministic derivation" contract from ADR 0032 —
        // two processes on two machines holding the same root keystore
        // recover the same team identity on their own without coordination.
        var registrar1 = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _subkeyDerivation, _rootIdentity, _sqlCipherKeyDerivation);
        var registrar2 = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _subkeyDerivation, _rootIdentity, _sqlCipherKeyDerivation);

        await using var factory1 = new TeamContextFactory(registrar1);
        await using var factory2 = new TeamContextFactory(registrar2);

        var teamId = TeamId.New();

        var ctx1 = await factory1.GetOrCreateAsync(teamId, "Team", CancellationToken.None);
        var ctx2 = await factory2.GetOrCreateAsync(teamId, "Team", CancellationToken.None);

        var local1 = ReadLocalNodeId(ctx1.Services.GetRequiredService<ILeaseCoordinator>());
        var local2 = ReadLocalNodeId(ctx2.Services.GetRequiredService<ILeaseCoordinator>());

        Assert.Equal(local1, local2);

        // Identity provider must agree too — the keypair is the upstream of
        // the localNodeId derivation, so if identity drifts the lease does.
        var idPubKey1 = ctx1.Services.GetRequiredService<INodeIdentityProvider>().Current.PublicKey;
        var idPubKey2 = ctx2.Services.GetRequiredService<INodeIdentityProvider>().Current.PublicKey;
        Assert.True(idPubKey1.AsSpan().SequenceEqual(idPubKey2),
            "Team identity must be deterministic across independently-composed registrars.");
    }

    [Fact]
    public async Task Transport_endpoints_are_per_team()
    {
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _subkeyDerivation, _rootIdentity, _sqlCipherKeyDerivation);
        await using var factory = new TeamContextFactory(registrar);

        var teamA = TeamId.New();
        var teamB = TeamId.New();

        var ctxA = await factory.GetOrCreateAsync(teamA, "Team A", CancellationToken.None);
        var ctxB = await factory.GetOrCreateAsync(teamB, "Team B", CancellationToken.None);

        var transportA = ctxA.Services.GetRequiredService<ISyncDaemonTransport>();
        var transportB = ctxB.Services.GetRequiredService<ISyncDaemonTransport>();

        Assert.NotSame(transportA, transportB);
        // Both must be the concrete UnixSocketSyncDaemonTransport — the
        // registrar overrides the TryAddSingleton default with a listening
        // endpoint. If the override ever regressed we'd get the no-endpoint
        // fallback and this cast would fail.
        Assert.IsType<UnixSocketSyncDaemonTransport>(transportA);
        Assert.IsType<UnixSocketSyncDaemonTransport>(transportB);

        // Endpoints are per-team strings — the registrar computes them from
        // TeamPaths.TransportEndpoint so two teams route on different
        // sockets / named pipes. Reach into the private _listenEndpoint
        // field (the transport does not expose it publicly).
        var endpointA = ReadTransportEndpoint(transportA);
        var endpointB = ReadTransportEndpoint(transportB);

        Assert.NotEqual(endpointA, endpointB);
        Assert.Equal(TeamPaths.TransportEndpoint(_tempRoot, teamA), endpointA);
        Assert.Equal(TeamPaths.TransportEndpoint(_tempRoot, teamB), endpointB);
    }

    [Fact]
    public async Task Gossip_daemon_sees_team_scoped_identity_not_autogenerated_fallback()
    {
        // Regression guard for 6.3.C risk note "Root/team identity conflation":
        // AddSunfishKernelSync registers a fallback INodeIdentityProvider via
        // TryAddSingleton that generates a fresh keypair. The registrar must
        // ensure our team-scoped provider wins. We assert by comparing the
        // INodeIdentityProvider returned from the team provider to a
        // re-derived team identity — they must be identical.
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _subkeyDerivation, _rootIdentity, _sqlCipherKeyDerivation);
        await using var factory = new TeamContextFactory(registrar);

        var teamId = TeamId.New();
        var ctx = await factory.GetOrCreateAsync(teamId, "Team", CancellationToken.None);

        var provider = ctx.Services.GetRequiredService<INodeIdentityProvider>();
        var expected = TeamScopedNodeIdentity.Derive(
            _rootIdentity, teamId.Value.ToString("D"), _subkeyDerivation);

        Assert.True(provider.Current.PublicKey.AsSpan().SequenceEqual(expected.PublicKey),
            "Team provider must expose the TeamScopedNodeIdentity-derived keypair, not the AddSunfishKernelSync fallback.");
    }

    // --- Helpers ------------------------------------------------------------

    private static NodeIdentity BuildRootIdentity(IEd25519Signer signer, byte fillByte)
    {
        // Deterministic seed keeps tests reproducible across runs. The
        // actual byte pattern is not load-bearing — only that it's fixed.
        var seed = new byte[32];
        Array.Fill(seed, fillByte);
        var (pub, priv) = signer.GenerateFromSeed(seed);
        var nodeIdBytes = new byte[16];
        Buffer.BlockCopy(pub, 0, nodeIdBytes, 0, 16);
        return new NodeIdentity(
            Convert.ToHexString(nodeIdBytes).ToLowerInvariant(), pub, priv);
    }

    private static string ReadLocalNodeId(ILeaseCoordinator coordinator)
    {
        // FleaseLeaseCoordinator._localNodeId is private. Read it reflectively
        // so tests can pin the derivation without broadening the public API.
        var field = coordinator.GetType().GetField(
            "_localNodeId",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        var value = field!.GetValue(coordinator);
        Assert.IsType<string>(value);
        return (string)value!;
    }

    private static string ReadTransportEndpoint(ISyncDaemonTransport transport)
    {
        // UnixSocketSyncDaemonTransport._listenEndpoint is private. Same
        // reflection approach as ReadLocalNodeId — the test pins a
        // composition contract that the public API doesn't surface.
        var field = transport.GetType().GetField(
            "_listenEndpoint",
            BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        var value = field!.GetValue(transport);
        Assert.IsType<string>(value);
        return (string)value!;
    }
}
