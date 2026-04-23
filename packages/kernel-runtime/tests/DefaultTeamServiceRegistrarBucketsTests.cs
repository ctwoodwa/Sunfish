using Microsoft.Extensions.DependencyInjection;
using Sunfish.Kernel.Buckets;
using Sunfish.Kernel.Buckets.LazyFetch;
using Sunfish.Kernel.Buckets.Storage;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;

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
    private readonly ITeamSubkeyDerivation _fakeSubkeyDerivation = new ThrowingSubkeyDerivation();
    private readonly IEd25519Signer _fakeRootSigner = new ThrowingEd25519Signer();

    public DefaultTeamServiceRegistrarBucketsTests()
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
    public async Task Two_teams_get_isolated_bucket_registries_populated_from_their_own_manifests()
    {
        var registrar = DefaultTeamServiceRegistrar.Compose(
            _tempRoot, _fakeSubkeyDerivation, _fakeRootSigner);
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
            _tempRoot, _fakeSubkeyDerivation, _fakeRootSigner);
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
            _tempRoot, _fakeSubkeyDerivation, _fakeRootSigner);
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
            _tempRoot, _fakeSubkeyDerivation, _fakeRootSigner);
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

    // --- Throwing stubs for Compose's identity-dependent parameters ---------------
    //
    // Wave 6.3.D only exercises the bucket-registry branch of the composed registrar,
    // which never calls into ITeamSubkeyDerivation or IEd25519Signer. Passing throwing
    // stubs (rather than Moq) keeps the test fast and surfaces any accidental identity
    // dependency in 6.3.D's code path as a loud test failure rather than a silent mock
    // return. Wave 6.3.C's tests exercise these with real implementations.

    private sealed class ThrowingSubkeyDerivation : ITeamSubkeyDerivation
    {
        public byte[] DeriveSubkey(ReadOnlySpan<byte> rootPrivateKey, string teamId) =>
            throw new InvalidOperationException(
                "ITeamSubkeyDerivation was unexpectedly invoked in the 6.3.D bucket-registrar test path.");

        public (byte[] PublicKey, byte[] PrivateKey) DeriveTeamKeypair(ReadOnlySpan<byte> rootPrivateKey, string teamId) =>
            throw new InvalidOperationException(
                "ITeamSubkeyDerivation was unexpectedly invoked in the 6.3.D bucket-registrar test path.");
    }

    private sealed class ThrowingEd25519Signer : IEd25519Signer
    {
        public int PublicKeyLength => 32;

        public int PrivateKeyLength => 32;

        public int SignatureLength => 64;

        public (byte[] PublicKey, byte[] PrivateKey) GenerateKeyPair() =>
            throw new InvalidOperationException(
                "IEd25519Signer was unexpectedly invoked in the 6.3.D bucket-registrar test path.");

        public (byte[] PublicKey, byte[] PrivateKey) GenerateFromSeed(ReadOnlySpan<byte> seed) =>
            throw new InvalidOperationException(
                "IEd25519Signer was unexpectedly invoked in the 6.3.D bucket-registrar test path.");

        public byte[] Sign(ReadOnlySpan<byte> message, ReadOnlySpan<byte> privateKey) =>
            throw new InvalidOperationException(
                "IEd25519Signer was unexpectedly invoked in the 6.3.D bucket-registrar test path.");

        public bool Verify(ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKey) =>
            throw new InvalidOperationException(
                "IEd25519Signer was unexpectedly invoked in the 6.3.D bucket-registrar test path.");
    }
}
