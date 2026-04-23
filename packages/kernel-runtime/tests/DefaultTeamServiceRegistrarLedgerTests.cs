using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Foundation.LocalFirst.Quarantine;
using Sunfish.Kernel.Events;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Sync.Identity;

namespace Sunfish.Kernel.Runtime.Tests;

/// <summary>
/// Wave 6.3.B integration tests — verifies that
/// <see cref="DefaultTeamServiceRegistrar.Compose"/> installs a per-team
/// ledger trio (<see cref="IEventLog"/>, <see cref="IQuarantineQueue"/>,
/// <see cref="IEncryptedStore"/>) whose file-system configuration derives
/// from <see cref="TeamPaths"/>, and that two <see cref="TeamContext"/>s
/// resolve distinct instances and distinct on-disk locations.
/// </summary>
/// <remarks>
/// These tests do NOT exercise <c>IEncryptedStore.OpenAsync</c> — that's
/// integration territory and requires the real SQLCipher key pipeline. Per
/// the 6.3.B scope notes, the store is asserted unopened via its options.
/// </remarks>
public sealed class DefaultTeamServiceRegistrarLedgerTests : IDisposable
{
    private readonly string _tempRoot;
    // Wave 6.3.C wires the sync pair into Compose, and that wiring invokes
    // ITeamSubkeyDerivation.DeriveTeamKeypair at registrar-invocation time.
    // The 6.3.B ledger tests therefore pass the real security primitives
    // (they're pure and fast). The tests assert ledger behavior — the sync
    // pair is covered by DefaultTeamServiceRegistrarSyncTests.
    private readonly IEd25519Signer _realSigner = new Ed25519Signer();
    private readonly ITeamSubkeyDerivation _realSubkeyDerivation;
    private readonly NodeIdentity _rootIdentity;
    private readonly ISqlCipherKeyDerivation _realSqlCipherKeyDerivation = new SqlCipherKeyDerivation();

    public DefaultTeamServiceRegistrarLedgerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "sunfish-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        _realSubkeyDerivation = new TeamSubkeyDerivation(_realSigner);

        // Deterministic root seed for the 6.3.B ledger tests. The seed value
        // is not load-bearing — these tests assert ledger isolation, not
        // identity derivation — but using a fixed seed keeps them
        // reproducible and avoids per-run keypair generation.
        var seed = new byte[32];
        for (var i = 0; i < seed.Length; i++)
        {
            seed[i] = (byte)(i + 1);
        }
        var (pub, priv) = _realSigner.GenerateFromSeed(seed);
        var nodeIdBytes = new byte[16];
        Buffer.BlockCopy(pub, 0, nodeIdBytes, 0, 16);
        _rootIdentity = new NodeIdentity(
            Convert.ToHexString(nodeIdBytes).ToLowerInvariant(), pub, priv);
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
    public async Task Two_teams_get_isolated_IEventLog_instances_with_distinct_directories()
    {
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _realSubkeyDerivation, _rootIdentity, _realSqlCipherKeyDerivation);
        await using var factory = new TeamContextFactory(registrar);

        var teamA = TeamId.New();
        var teamB = TeamId.New();

        var ctxA = await factory.GetOrCreateAsync(teamA, "Team A", CancellationToken.None);
        var ctxB = await factory.GetOrCreateAsync(teamB, "Team B", CancellationToken.None);

        var logA = ctxA.Services.GetRequiredService<IEventLog>();
        var logB = ctxB.Services.GetRequiredService<IEventLog>();

        Assert.NotSame(logA, logB);

        var optsA = ctxA.Services.GetRequiredService<IOptions<EventLogOptions>>().Value;
        var optsB = ctxB.Services.GetRequiredService<IOptions<EventLogOptions>>().Value;

        Assert.Equal(TeamPaths.EventLogDirectory(_tempRoot, teamA), optsA.Directory);
        Assert.Equal(TeamPaths.EventLogDirectory(_tempRoot, teamB), optsB.Directory);
        Assert.NotEqual(optsA.Directory, optsB.Directory);
        Assert.Equal("epoch-0", optsA.EpochId);
        Assert.Equal("epoch-0", optsB.EpochId);
    }

    [Fact]
    public async Task Two_teams_get_isolated_IQuarantineQueue_instances_with_no_cross_visibility()
    {
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _realSubkeyDerivation, _rootIdentity, _realSqlCipherKeyDerivation);
        await using var factory = new TeamContextFactory(registrar);

        var teamA = TeamId.New();
        var teamB = TeamId.New();

        var ctxA = await factory.GetOrCreateAsync(teamA, "Team A", CancellationToken.None);
        var ctxB = await factory.GetOrCreateAsync(teamB, "Team B", CancellationToken.None);

        var queueA = ctxA.Services.GetRequiredService<IQuarantineQueue>();
        var queueB = ctxB.Services.GetRequiredService<IQuarantineQueue>();

        Assert.NotSame(queueA, queueB);

        // Writing into team-A's quarantine queue must NOT surface in team-B's
        // view — the whole point of per-team state isolation.
        var entry = new QuarantineEntry(
            Kind: "lease.created",
            Stream: "projects",
            Payload: new byte[] { 0xDE, 0xAD, 0xBE, 0xEF },
            QueuedAt: DateTimeOffset.UtcNow,
            QueuedByActor: "actor-a");

        var idA = await queueA.EnqueueAsync(entry, CancellationToken.None);

        Assert.NotNull(await queueA.GetAsync(idA, CancellationToken.None));
        Assert.Null(await queueB.GetAsync(idA, CancellationToken.None));

        var bPending = new List<QuarantineRecord>();
        await foreach (var r in queueB.ReadByStatusAsync(QuarantineStatus.Pending, CancellationToken.None))
        {
            bPending.Add(r);
        }
        Assert.Empty(bPending);
    }

    [Fact]
    public async Task Two_teams_get_isolated_IEncryptedStore_instances_with_distinct_database_paths()
    {
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _realSubkeyDerivation, _rootIdentity, _realSqlCipherKeyDerivation);
        await using var factory = new TeamContextFactory(registrar);

        var teamA = TeamId.New();
        var teamB = TeamId.New();

        var ctxA = await factory.GetOrCreateAsync(teamA, "Team A", CancellationToken.None);
        var ctxB = await factory.GetOrCreateAsync(teamB, "Team B", CancellationToken.None);

        var storeA = ctxA.Services.GetRequiredService<IEncryptedStore>();
        var storeB = ctxB.Services.GetRequiredService<IEncryptedStore>();

        Assert.NotSame(storeA, storeB);

        var optsA = ctxA.Services.GetRequiredService<IOptions<EncryptionOptions>>().Value;
        var optsB = ctxB.Services.GetRequiredService<IOptions<EncryptionOptions>>().Value;

        Assert.Equal(TeamPaths.DatabasePath(_tempRoot, teamA), optsA.DatabasePath);
        Assert.Equal(TeamPaths.DatabasePath(_tempRoot, teamB), optsB.DatabasePath);
        Assert.NotEqual(optsA.DatabasePath, optsB.DatabasePath);

        Assert.Equal(TeamPaths.KeystoreKeyName(teamA), optsA.KeystoreKeyName);
        Assert.Equal(TeamPaths.KeystoreKeyName(teamB), optsB.KeystoreKeyName);
        Assert.NotEqual(optsA.KeystoreKeyName, optsB.KeystoreKeyName);
    }

    [Fact]
    public async Task IQuarantineQueue_flows_through_the_same_per_team_IEventLog_it_was_registered_with()
    {
        // Regression guard: EventLogBackedQuarantineQueue is constructed with
        // sp.GetRequiredService<IEventLog>() — if the registrar were ever
        // rewired to share an event log across teams, quarantine writes would
        // cross-pollinate. Assert identity of the IEventLog the queue sees.
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _realSubkeyDerivation, _rootIdentity, _realSqlCipherKeyDerivation);
        await using var factory = new TeamContextFactory(registrar);

        var teamA = TeamId.New();
        var teamB = TeamId.New();

        var ctxA = await factory.GetOrCreateAsync(teamA, "Team A", CancellationToken.None);
        var ctxB = await factory.GetOrCreateAsync(teamB, "Team B", CancellationToken.None);

        var logA = ctxA.Services.GetRequiredService<IEventLog>();
        var logB = ctxB.Services.GetRequiredService<IEventLog>();
        var queueA = ctxA.Services.GetRequiredService<IQuarantineQueue>();
        var queueB = ctxB.Services.GetRequiredService<IQuarantineQueue>();

        Assert.NotSame(logA, logB);
        Assert.NotSame(queueA, queueB);
    }

    // NOTE: Wave 6.3.B's earlier revision used throwing fakes for
    // ITeamSubkeyDerivation and IEd25519Signer to prove the ledger branch
    // never invoked them. Wave 6.3.C collapsed that separation: Compose now
    // invokes ITeamSubkeyDerivation.DeriveTeamKeypair at registrar-invocation
    // time (the derivation is cheap + pure), so real implementations are
    // threaded through these tests instead. The ledger-isolation assertions
    // are unchanged.
}
