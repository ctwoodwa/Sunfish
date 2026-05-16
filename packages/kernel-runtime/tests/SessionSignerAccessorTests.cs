using System.Threading;
using System.Threading.Tasks;
using Sunfish.Kernel.Runtime.Session;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Security.Session;
using Xunit;

namespace Sunfish.Kernel.Runtime.Tests;

/// <summary>
/// W#65 — `DefaultSessionSignerAccessor` contract coverage.
/// Verifies the per-team subkey derivation chain
/// (IRootSeedProvider → ITeamSubkeyDerivation.DeriveTeamKeypair) wires
/// into a key-bound signer without surfacing the raw private key bytes.
/// Uses hand-rolled fakes — kernel-runtime/tests has no mocking framework.
/// </summary>
public sealed class SessionSignerAccessorTests
{
    private static readonly byte[] FixedRootSeed = Enumerable.Range(0, 32)
        .Select(i => (byte)i).ToArray();

    // ── No active team ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetCurrentAsync_WhenNoActiveTeam_ThrowsInvalidOperation()
    {
        var realSigner = new Ed25519Signer();
        var accessor = new DefaultSessionSignerAccessor(
            new FakeRootSeedProvider(FixedRootSeed),
            new TeamSubkeyDerivation(realSigner),
            new FakeActiveTeamAccessor(null),
            realSigner);

        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await accessor.GetCurrentAsync(CancellationToken.None));
    }

    // ── Returns bound signer with the derived public key ────────────────────

    [Fact]
    public async Task GetCurrentAsync_ReturnsSignerWithDerivedPublicKey()
    {
        var realSigner = new Ed25519Signer();
        var derivation = new TeamSubkeyDerivation(realSigner);
        var teamId = new TeamId(Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var expected = derivation.DeriveTeamKeypair(FixedRootSeed, teamId.ToString());

        var bound = await BuildAccessor(derivation, teamId, realSigner)
            .GetCurrentAsync(CancellationToken.None);

        Assert.True(bound.PublicKey.Span.SequenceEqual(expected.PublicKey));
    }

    // ── Sign-then-verify round trip ────────────────────────────────────────

    [Fact]
    public async Task GetCurrentAsync_SignedBytes_VerifyWithPublicKey()
    {
        var realSigner = new Ed25519Signer();
        var teamId = new TeamId(Guid.Parse("00000000-0000-0000-0000-000000000002"));
        var accessor = BuildAccessor(new TeamSubkeyDerivation(realSigner), teamId, realSigner);

        var bound = await accessor.GetCurrentAsync(CancellationToken.None);
        var message = "hello recovery"u8.ToArray();
        var signature = await bound.SignAsync(message);

        Assert.True(realSigner.Verify(message, signature, bound.PublicKey.Span));
    }

    // ── Different teams → different keys ───────────────────────────────────

    [Fact]
    public async Task GetCurrentAsync_DifferentTeams_ReturnDifferentPublicKeys()
    {
        var realSigner = new Ed25519Signer();
        var derivation = new TeamSubkeyDerivation(realSigner);
        var team1 = new TeamId(Guid.Parse("00000000-0000-0000-0000-000000000010"));
        var team2 = new TeamId(Guid.Parse("00000000-0000-0000-0000-000000000020"));

        var sig1 = await BuildAccessor(derivation, team1, realSigner).GetCurrentAsync(CancellationToken.None);
        var sig2 = await BuildAccessor(derivation, team2, realSigner).GetCurrentAsync(CancellationToken.None);

        Assert.False(sig1.PublicKey.Span.SequenceEqual(sig2.PublicKey.Span));
    }

    // ── Ed25519 determinism ────────────────────────────────────────────────

    [Fact]
    public async Task BoundSigner_SignAsync_IsDeterministic()
    {
        var realSigner = new Ed25519Signer();
        var teamId = new TeamId(Guid.Parse("00000000-0000-0000-0000-000000000030"));
        var accessor = BuildAccessor(new TeamSubkeyDerivation(realSigner), teamId, realSigner);

        var bound = await accessor.GetCurrentAsync(CancellationToken.None);
        var message = "deterministic"u8.ToArray();

        var sig1 = await bound.SignAsync(message);
        var sig2 = await bound.SignAsync(message);

        Assert.Equal(sig1, sig2);
    }

    // ── Helpers / fakes ─────────────────────────────────────────────────────

    private static DefaultSessionSignerAccessor BuildAccessor(
        ITeamSubkeyDerivation derivation, TeamId teamId, IEd25519Signer signer)
    {
        var ctx = new TeamContext(teamId, "Test Team", services: new EmptyServiceProvider());
        return new DefaultSessionSignerAccessor(
            new FakeRootSeedProvider(FixedRootSeed),
            derivation,
            new FakeActiveTeamAccessor(ctx),
            signer);
    }

    private sealed class FakeRootSeedProvider : IRootSeedProvider
    {
        private readonly byte[] _seed;
        public FakeRootSeedProvider(byte[] seed) => _seed = seed;
        public ValueTask<ReadOnlyMemory<byte>> GetRootSeedAsync(CancellationToken ct)
            => ValueTask.FromResult<ReadOnlyMemory<byte>>(_seed);
    }

    private sealed class FakeActiveTeamAccessor : IActiveTeamAccessor
    {
        public FakeActiveTeamAccessor(TeamContext? active) => Active = active;
        public TeamContext? Active { get; }
        public event EventHandler<ActiveTeamChangedEventArgs>? ActiveChanged
        {
            add { } remove { }
        }
        public Task SetActiveAsync(TeamId teamId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
