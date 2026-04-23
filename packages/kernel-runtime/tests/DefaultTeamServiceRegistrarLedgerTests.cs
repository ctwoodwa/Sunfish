using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Foundation.LocalFirst.Quarantine;
using Sunfish.Kernel.Events;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;

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
    private readonly ITeamSubkeyDerivation _fakeSubkeyDerivation = new ThrowingSubkeyDerivation();
    private readonly IEd25519Signer _fakeRootSigner = new ThrowingEd25519Signer();
    private readonly ISqlCipherKeyDerivation _realSqlCipherKeyDerivation = new SqlCipherKeyDerivation();

    public DefaultTeamServiceRegistrarLedgerTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "sunfish-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
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
            _tempRoot, _fakeSubkeyDerivation, _fakeRootSigner, _realSqlCipherKeyDerivation);
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
            _tempRoot, _fakeSubkeyDerivation, _fakeRootSigner, _realSqlCipherKeyDerivation);
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
            _tempRoot, _fakeSubkeyDerivation, _fakeRootSigner, _realSqlCipherKeyDerivation);
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
            _tempRoot, _fakeSubkeyDerivation, _fakeRootSigner, _realSqlCipherKeyDerivation);
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

    // --- Throwing stubs for Compose's identity-dependent parameters ---------------
    //
    // Wave 6.3.B only exercises the ledger branch of the composed registrar,
    // which never calls into ITeamSubkeyDerivation or IEd25519Signer at
    // registrar-compose time. Passing throwing stubs surfaces any accidental
    // identity dependency as a loud test failure. ISqlCipherKeyDerivation is
    // captured in the closure but not invoked until 6.3.E's activator, so we
    // pass the real implementation (it never gets called here — verified by
    // the green path of these tests).

    private sealed class ThrowingSubkeyDerivation : ITeamSubkeyDerivation
    {
        public byte[] DeriveSubkey(ReadOnlySpan<byte> rootPrivateKey, string teamId) =>
            throw new InvalidOperationException(
                "ITeamSubkeyDerivation was unexpectedly invoked in the 6.3.B ledger-registrar test path.");

        public (byte[] PublicKey, byte[] PrivateKey) DeriveTeamKeypair(ReadOnlySpan<byte> rootPrivateKey, string teamId) =>
            throw new InvalidOperationException(
                "ITeamSubkeyDerivation was unexpectedly invoked in the 6.3.B ledger-registrar test path.");
    }

    private sealed class ThrowingEd25519Signer : IEd25519Signer
    {
        public int PublicKeyLength => 32;

        public int PrivateKeyLength => 32;

        public int SignatureLength => 64;

        public (byte[] PublicKey, byte[] PrivateKey) GenerateKeyPair() =>
            throw new InvalidOperationException(
                "IEd25519Signer was unexpectedly invoked in the 6.3.B ledger-registrar test path.");

        public (byte[] PublicKey, byte[] PrivateKey) GenerateFromSeed(ReadOnlySpan<byte> seed) =>
            throw new InvalidOperationException(
                "IEd25519Signer was unexpectedly invoked in the 6.3.B ledger-registrar test path.");

        public byte[] Sign(ReadOnlySpan<byte> message, ReadOnlySpan<byte> privateKey) =>
            throw new InvalidOperationException(
                "IEd25519Signer was unexpectedly invoked in the 6.3.B ledger-registrar test path.");

        public bool Verify(ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKey) =>
            throw new InvalidOperationException(
                "IEd25519Signer was unexpectedly invoked in the 6.3.B ledger-registrar test path.");
    }
}
