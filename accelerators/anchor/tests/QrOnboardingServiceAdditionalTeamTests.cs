using Microsoft.Extensions.DependencyInjection;
using Sunfish.Anchor.Services;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Attestation;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Sync.Identity;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// Wave 6.8 — exercises <see cref="QrOnboardingService"/>'s
/// <c>BeginAdditionalTeamJoinAsync</c> / <c>CompleteAdditionalTeamJoinAsync</c>
/// surface. The existing <see cref="QrOnboardingServiceTests"/> still covers
/// the Wave 3.4 encode/decode surface; this file is scoped to the join-
/// additional-team flow.
/// </summary>
public sealed class QrOnboardingServiceAdditionalTeamTests
{
    private readonly Ed25519Signer _signer = new();
    private readonly StubTimeProvider _clock = new(new DateTimeOffset(2026, 4, 23, 12, 0, 0, TimeSpan.Zero));
    private readonly TeamSubkeyDerivation _subkeyDerivation;
    private readonly NodeIdentity _rootIdentity;

    public QrOnboardingServiceAdditionalTeamTests()
    {
        _subkeyDerivation = new TeamSubkeyDerivation(_signer);
        // Deterministic 32-byte root seed so rerunning tests yields the same
        // root keypair — easier to triage flakes.
        var seed = new byte[32];
        for (int i = 0; i < seed.Length; i++)
        {
            seed[i] = (byte)(i + 1);
        }
        var (rootPub, rootPriv) = _signer.GenerateFromSeed(seed);
        _rootIdentity = new NodeIdentity(
            NodeId: Convert.ToHexString(rootPub.AsSpan(0, 16)).ToLowerInvariant(),
            PublicKey: rootPub,
            PrivateKey: rootPriv);
    }

    private (QrOnboardingService svc, RecordingTeamContextFactory factory, CountingStoreActivator activator)
        MakeService()
    {
        var factory = new RecordingTeamContextFactory();
        var activator = new CountingStoreActivator();
        var issuer = new AttestationIssuer(_signer, _clock);
        var verifier = new AttestationVerifier(_signer);
        var activeTeam = new PassThroughActiveTeamAccessor(factory);
        var svc = new QrOnboardingService(
            signer: _signer,
            activeTeam: activeTeam,
            verifier: verifier,
            issuer: issuer,
            factory: factory,
            storeActivator: activator,
            subkeyDerivation: _subkeyDerivation,
            rootIdentity: _rootIdentity,
            clock: _clock);
        return (svc, factory, activator);
    }

    [Fact]
    public async Task BeginAdditionalTeamJoin_materializes_fresh_TeamContext()
    {
        var (svc, factory, activator) = MakeService();

        var invitation = await svc.BeginAdditionalTeamJoinAsync(CancellationToken.None);

        Assert.NotNull(invitation);
        Assert.NotEmpty(invitation.QrPayload);
        Assert.True(invitation.ExpiresAt > _clock.GetUtcNow());

        // Factory was driven once; activator was driven once for the same id.
        var materialized = Assert.Single(factory.Active);
        Assert.Equal(invitation.TeamId, materialized.TeamId);
        Assert.Equal(new[] { invitation.TeamId }, activator.Activated);
    }

    [Fact]
    public async Task BeginAdditionalTeamJoin_does_not_touch_existing_teams()
    {
        var (svc, factory, activator) = MakeService();

        // Seed a pre-existing team directly through the factory. Its
        // TeamContext is what we want Begin() to leave alone.
        var preexistingId = new TeamId(Guid.Parse("99999999-9999-9999-9999-999999999999"));
        var preexisting = await factory.GetOrCreateAsync(preexistingId, "Existing", CancellationToken.None);
        var preexistingStore = preexisting.Services.GetRequiredService<IEncryptedStore>();

        // Write a sentinel value so we can verify the store isn't re-opened
        // or its contents clobbered by the additional-team flow.
        await preexistingStore.SetAsync("sentinel", new byte[] { 0xAB, 0xCD }, CancellationToken.None);

        var invitation = await svc.BeginAdditionalTeamJoinAsync(CancellationToken.None);

        // Preexisting context is still present, still the same instance, and
        // its sentinel data survived.
        Assert.Contains(factory.Active, t => t.TeamId.Equals(preexistingId));
        Assert.Same(preexisting, factory.Active.First(t => t.TeamId.Equals(preexistingId)));
        var survivor = await preexistingStore.GetAsync("sentinel", CancellationToken.None);
        Assert.NotNull(survivor);
        Assert.Equal(new byte[] { 0xAB, 0xCD }, survivor);

        // The new team is distinct from the preexisting one.
        Assert.NotEqual(preexistingId, invitation.TeamId);
        Assert.DoesNotContain(preexistingId, activator.Activated); // preexisting wasn't (re-)activated.
    }

    [Fact]
    public async Task BeginAdditionalTeamJoin_returns_distinct_teamId_per_call()
    {
        var (svc, factory, _) = MakeService();

        var i1 = await svc.BeginAdditionalTeamJoinAsync(CancellationToken.None);
        var i2 = await svc.BeginAdditionalTeamJoinAsync(CancellationToken.None);
        var i3 = await svc.BeginAdditionalTeamJoinAsync(CancellationToken.None);

        Assert.Equal(3, new[] { i1.TeamId, i2.TeamId, i3.TeamId }.Distinct().Count());
        Assert.Equal(3, factory.Active.Count);
    }

    [Fact]
    public async Task CompleteAdditionalTeamJoin_with_invalid_signature_throws()
    {
        var (svc, _, _) = MakeService();
        var invitation = await svc.BeginAdditionalTeamJoinAsync(CancellationToken.None);

        // Build a legitimate founder-signed bundle, then flip a signature bit.
        var founderSigner = new Ed25519Signer();
        var (founderPub, founderPriv) = founderSigner.GenerateKeyPair();
        var issuer = new AttestationIssuer(_signer, _clock);
        var (joinerPub, _) = _signer.GenerateKeyPair();
        var teamIdBytes = invitation.TeamId.Value.ToByteArray();
        var attestation = issuer.Issue(
            teamId: teamIdBytes,
            subjectPublicKey: joinerPub,
            role: "team_member",
            validity: TimeSpan.FromDays(30),
            issuerPrivateKey: founderPriv);
        var badSig = (byte[])attestation.Signature.Clone();
        badSig[0] ^= 0xFF;
        var tampered = attestation with { Signature = badSig };
        var bundle = new AttestationBundle(new[] { tampered });
        var wire = bundle.ToCbor();

        await Assert.ThrowsAsync<InvalidOnboardingPayloadException>(
            () => svc.CompleteAdditionalTeamJoinAsync(invitation.TeamId, wire, CancellationToken.None).AsTask());
    }

    [Fact]
    public async Task CompleteAdditionalTeamJoin_persists_attestation_to_new_teams_store()
    {
        var (svc, factory, _) = MakeService();
        var invitation = await svc.BeginAdditionalTeamJoinAsync(CancellationToken.None);

        // Construct a valid founder-signed team_member attestation against the
        // team id the invitation carries.
        var founderSigner = new Ed25519Signer();
        var (_, founderPriv) = founderSigner.GenerateKeyPair();
        var issuer = new AttestationIssuer(_signer, _clock);
        var (joinerPub, _) = _signer.GenerateKeyPair();
        var teamIdBytes = invitation.TeamId.Value.ToByteArray();
        var attestation = issuer.Issue(
            teamId: teamIdBytes,
            subjectPublicKey: joinerPub,
            role: "team_member",
            validity: TimeSpan.FromDays(30),
            issuerPrivateKey: founderPriv);
        var bundle = new AttestationBundle(new[] { attestation });

        await svc.CompleteAdditionalTeamJoinAsync(
            invitation.TeamId, bundle.ToCbor(), CancellationToken.None);

        // Store for the new team should now carry the attestation bundle.
        var newCtx = factory.Active.First(t => t.TeamId.Equals(invitation.TeamId));
        var store = newCtx.Services.GetRequiredService<IEncryptedStore>();
        var persisted = await store.GetAsync("onboarding.attestation-bundle", CancellationToken.None);
        Assert.NotNull(persisted);
        var round = AttestationBundle.FromCbor(persisted);
        Assert.Single(round.Attestations);
        Assert.Equal("team_member", round.Attestations[0].Role);
    }

    [Fact]
    public async Task CompleteAdditionalTeamJoin_does_not_auto_switch_active_team()
    {
        var (svc, factory, _) = MakeService();

        // Seed a preexisting team and make it the active team.
        var preexistingId = new TeamId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        await factory.GetOrCreateAsync(preexistingId, "Primary", CancellationToken.None);
        var accessor = (PassThroughActiveTeamAccessor)GetFieldPrivate(svc);
        await accessor.SetActiveAsync(preexistingId, CancellationToken.None);
        Assert.Equal(preexistingId, accessor.Active!.TeamId);

        var invitation = await svc.BeginAdditionalTeamJoinAsync(CancellationToken.None);

        // Build + apply a valid response.
        var founderSigner = new Ed25519Signer();
        var (_, founderPriv) = founderSigner.GenerateKeyPair();
        var issuer = new AttestationIssuer(_signer, _clock);
        var (joinerPub, _) = _signer.GenerateKeyPair();
        var attestation = issuer.Issue(
            teamId: invitation.TeamId.Value.ToByteArray(),
            subjectPublicKey: joinerPub,
            role: "team_member",
            validity: TimeSpan.FromDays(30),
            issuerPrivateKey: founderPriv);
        var bundle = new AttestationBundle(new[] { attestation });

        await svc.CompleteAdditionalTeamJoinAsync(
            invitation.TeamId, bundle.ToCbor(), CancellationToken.None);

        // Active team did NOT switch to the new team — still the preexisting one.
        Assert.Equal(preexistingId, accessor.Active!.TeamId);
    }

    /// <summary>Reflection helper: QrOnboardingService keeps _activeTeam private;
    /// the test needs to assert against it without exposing a public accessor.</summary>
    private static object GetFieldPrivate(QrOnboardingService svc)
    {
        var field = typeof(QrOnboardingService).GetField(
            "_activeTeam",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("QrOnboardingService._activeTeam field missing — test is out of sync.");
        return field.GetValue(svc)
            ?? throw new InvalidOperationException("QrOnboardingService._activeTeam was null.");
    }

    private sealed class StubTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public StubTimeProvider(DateTimeOffset now) => _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }

    /// <summary>
    /// Minimal <see cref="ITeamContextFactory"/> — each GetOrCreate builds a
    /// fresh child <see cref="IServiceProvider"/> with an in-memory
    /// <see cref="IEncryptedStore"/>, so per-team stores are genuinely
    /// isolated (the Part-2 "does not touch existing teams" test pins the
    /// isolation on this fake).
    /// </summary>
    private sealed class RecordingTeamContextFactory : ITeamContextFactory
    {
        private readonly Dictionary<TeamId, TeamContext> _contexts = new();

        public IReadOnlyCollection<TeamContext> Active => _contexts.Values.ToList();

        public Task<TeamContext> GetOrCreateAsync(TeamId teamId, string displayName, CancellationToken ct)
        {
            if (_contexts.TryGetValue(teamId, out var existing))
            {
                return Task.FromResult(existing);
            }

            var services = new ServiceCollection();
            services.AddSingleton<IEncryptedStore, InMemoryEncryptedStore>();
            var provider = services.BuildServiceProvider();
            var ctx = new TeamContext(teamId, displayName, provider);
            _contexts[teamId] = ctx;
            return Task.FromResult(ctx);
        }

        public Task RemoveAsync(TeamId teamId, CancellationToken ct)
        {
            _contexts.Remove(teamId);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// <see cref="ITeamStoreActivator"/> fake — records every activation call
    /// so tests can assert Begin() drives the activator exactly once for the
    /// new team and does not re-activate preexisting teams.
    /// </summary>
    private sealed class CountingStoreActivator : ITeamStoreActivator
    {
        public List<TeamId> Activated { get; } = new();

        public ValueTask ActivateAsync(TeamId teamId, CancellationToken ct)
        {
            Activated.Add(teamId);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Active-team accessor that binds to the fake factory's live set so
    /// tests can drive SetActiveAsync without wiring through
    /// <see cref="ActiveTeamAccessor"/>'s full contract.
    /// </summary>
    private sealed class PassThroughActiveTeamAccessor : IActiveTeamAccessor
    {
        private readonly RecordingTeamContextFactory _factory;
        private TeamContext? _active;

        public PassThroughActiveTeamAccessor(RecordingTeamContextFactory factory) => _factory = factory;

        public TeamContext? Active => _active;

        public Task SetActiveAsync(TeamId teamId, CancellationToken ct)
        {
            _active = _factory.Active.First(t => t.TeamId.Equals(teamId));
            ActiveChanged?.Invoke(this, new ActiveTeamChangedEventArgs(null, _active));
            return Task.CompletedTask;
        }

        public event EventHandler<ActiveTeamChangedEventArgs>? ActiveChanged;
    }

    /// <summary>
    /// In-memory <see cref="IEncryptedStore"/> fake. Trivial KV over a
    /// dictionary — the test surface only needs round-trip of the
    /// attestation bundle plus a sentinel write.
    /// </summary>
    private sealed class InMemoryEncryptedStore : IEncryptedStore
    {
        private readonly Dictionary<string, byte[]> _data = new();

        public Task OpenAsync(string databasePath, ReadOnlyMemory<byte> key, CancellationToken ct) => Task.CompletedTask;
        public Task<byte[]?> GetAsync(string key, CancellationToken ct)
            => Task.FromResult(_data.TryGetValue(key, out var v) ? v : null);
        public Task SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken ct)
        {
            _data[key] = value.ToArray();
            return Task.CompletedTask;
        }
        public Task DeleteAsync(string key, CancellationToken ct)
        {
            _data.Remove(key);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct)
        {
            IReadOnlyList<string> matches = _data.Keys
                .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
                .OrderBy(k => k, StringComparer.Ordinal)
                .ToList();
            return Task.FromResult(matches);
        }
        public Task CloseAsync() => Task.CompletedTask;
    }
}
