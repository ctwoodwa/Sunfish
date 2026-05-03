using Microsoft.Extensions.DependencyInjection;
using Sunfish.Kernel.Buckets;
using Sunfish.Kernel.Buckets.LazyFetch;
using Sunfish.Kernel.Buckets.Storage;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Sync.Identity;

namespace Sunfish.Kernel.Runtime.Tests;

/// <summary>
/// Wave 6.3.D integration tests — verifies that
/// <see cref="DefaultTeamServiceRegistrar.Compose"/> installs a per-team
/// <see cref="IBucketRegistry"/> (and its four co-registered peers) whose
/// manifest source directory is <see cref="TeamPaths.BucketsDirectory"/>.
/// </summary>
public sealed class DefaultTeamServiceRegistrarBucketsTests : IDisposable
{
    private readonly string _tempRoot;
    // Wave 6.3.C wires ITeamSubkeyDerivation.DeriveTeamKeypair into the
    // registrar body itself, so the bucket tests now thread a real subkey
    // derivation + a deterministic root identity through Compose. Only
    // ISqlCipherKeyDerivation stays a throwing fake — it's captured in the
    // closure but not invoked until 6.3.E's activator hosted service, which
    // these tests don't exercise.
    private readonly IEd25519Signer _realSigner = new Ed25519Signer();
    private readonly ITeamSubkeyDerivation _realSubkeyDerivation;
    private readonly NodeIdentity _rootIdentity;
    private readonly ISqlCipherKeyDerivation _fakeSqlCipherKeyDerivation = new ThrowingSqlCipherKeyDerivation();

    public DefaultTeamServiceRegistrarBucketsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "sunfish-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        _realSubkeyDerivation = new TeamSubkeyDerivation(_realSigner);

        // Deterministic root seed — bucket isolation assertions do not depend
        // on the concrete key bytes, only on the fact that the derivation
        // runs cleanly.
        var seed = new byte[32];
        for (var i = 0; i < seed.Length; i++)
        {
            seed[i] = (byte)(0x10 + i);
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
    public async Task Two_teams_get_isolated_bucket_registries_populated_from_their_own_manifests()
    {
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _realSubkeyDerivation, _rootIdentity, _fakeSqlCipherKeyDerivation);
        await using var factory = new TeamContextFactory(registrar);

        var teamA = TeamId.New();
        var teamB = TeamId.New();

        // Seed each team's buckets directory with a distinguishing manifest *before* context
        // resolution — the DI factory is eager at first resolve, so the directory must exist
        // (with contents) before GetRequiredService<IBucketRegistry>() is called.
        WriteManifest(teamA, "team-a-manifest.yaml", """
            buckets:
              - name: team_a_core
                record_types: [projects, tasks]
                required_attestation: team_member
            """);
        WriteManifest(teamB, "team-b-manifest.yaml", """
            buckets:
              - name: team_b_core
                record_types: [documents]
                required_attestation: financial_role
            """);

        var ctxA = await factory.GetOrCreateAsync(teamA, "Team A", CancellationToken.None);
        var ctxB = await factory.GetOrCreateAsync(teamB, "Team B", CancellationToken.None);

        var registryA = ctxA.Services.GetRequiredService<IBucketRegistry>();
        var registryB = ctxB.Services.GetRequiredService<IBucketRegistry>();

        var namesA = registryA.Definitions.Select(d => d.Name).ToArray();
        var namesB = registryB.Definitions.Select(d => d.Name).ToArray();

        Assert.Equal(new[] { "team_a_core" }, namesA);
        Assert.Equal(new[] { "team_b_core" }, namesB);
        Assert.DoesNotContain("team_b_core", namesA);
        Assert.DoesNotContain("team_a_core", namesB);
    }

    [Fact]
    public async Task All_five_bucket_package_services_are_distinct_instances_across_teams()
    {
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _realSubkeyDerivation, _rootIdentity, _fakeSqlCipherKeyDerivation);
        await using var factory = new TeamContextFactory(registrar);

        var teamA = TeamId.New();
        var teamB = TeamId.New();

        // Both teams need at least one manifest so IBucketRegistry resolves through the
        // directory-backed factory branch (the same factory branch 6.3.D wires).
        WriteManifest(teamA, "a.yaml", """
            buckets:
              - name: a_only
                record_types: [x]
                required_attestation: team_member
            """);
        WriteManifest(teamB, "b.yaml", """
            buckets:
              - name: b_only
                record_types: [y]
                required_attestation: team_member
            """);

        var ctxA = await factory.GetOrCreateAsync(teamA, "Team A", CancellationToken.None);
        var ctxB = await factory.GetOrCreateAsync(teamB, "Team B", CancellationToken.None);

        Assert.NotSame(
            ctxA.Services.GetRequiredService<IBucketRegistry>(),
            ctxB.Services.GetRequiredService<IBucketRegistry>());
        Assert.NotSame(
            ctxA.Services.GetRequiredService<IBucketYamlLoader>(),
            ctxB.Services.GetRequiredService<IBucketYamlLoader>());
        Assert.NotSame(
            ctxA.Services.GetRequiredService<IBucketFilterEvaluator>(),
            ctxB.Services.GetRequiredService<IBucketFilterEvaluator>());
        Assert.NotSame(
            ctxA.Services.GetRequiredService<IBucketStubStore>(),
            ctxB.Services.GetRequiredService<IBucketStubStore>());
        Assert.NotSame(
            ctxA.Services.GetRequiredService<IStorageBudgetManager>(),
            ctxB.Services.GetRequiredService<IStorageBudgetManager>());
    }

    [Fact]
    public async Task Team_with_empty_buckets_directory_produces_empty_registry_without_crashing()
    {
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _realSubkeyDerivation, _rootIdentity, _fakeSqlCipherKeyDerivation);
        await using var factory = new TeamContextFactory(registrar);

        var teamId = TeamId.New();

        // Create the directory but leave it empty.
        Directory.CreateDirectory(TeamPaths.BucketsDirectory(_tempRoot, teamId));

        var ctx = await factory.GetOrCreateAsync(teamId, "Empty Team", CancellationToken.None);
        var registry = ctx.Services.GetRequiredService<IBucketRegistry>();

        Assert.Empty(registry.Definitions);
    }

    [Fact]
    public async Task Team_with_missing_buckets_directory_produces_empty_registry_without_crashing()
    {
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _realSubkeyDerivation, _rootIdentity, _fakeSqlCipherKeyDerivation);
        await using var factory = new TeamContextFactory(registrar);

        var teamId = TeamId.New();
        // Deliberately do NOT create the team's buckets directory — the on-disk layout is
        // created lazily by other subsystems and must not gate bucket-registry resolution.

        var ctx = await factory.GetOrCreateAsync(teamId, "Missing Dir Team", CancellationToken.None);
        var registry = ctx.Services.GetRequiredService<IBucketRegistry>();

        Assert.Empty(registry.Definitions);
    }

    private void WriteManifest(TeamId teamId, string filename, string yaml)
    {
        var dir = TeamPaths.BucketsDirectory(_tempRoot, teamId);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, filename), yaml);
    }

    // ISqlCipherKeyDerivation is still a throwing fake — it is captured in
    // the registrar closure for Wave 6.3.E's activator but never invoked
    // during the bucket-registration branch these tests exercise. The
    // ITeamSubkeyDerivation and IEd25519Signer fakes were removed in 6.3.C
    // (the real primitives are threaded through the fixture) because
    // DeriveTeamKeypair now runs inside the registrar body.

    private sealed class ThrowingSqlCipherKeyDerivation : ISqlCipherKeyDerivation
    {
        public byte[] DeriveSqlCipherKey(ReadOnlySpan<byte> rootSeed, string teamId) =>
            throw new InvalidOperationException(
                "ISqlCipherKeyDerivation was unexpectedly invoked in the 6.3.D bucket-registrar test path.");
    }
}
