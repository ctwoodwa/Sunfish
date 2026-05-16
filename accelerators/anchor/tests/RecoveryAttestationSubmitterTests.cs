using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Anchor.Services;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Foundation.Recovery;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Security.Session;
using Sunfish.Kernel.Sync.Identity;
using Xunit;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#66 + W#67 PR 5 — `RecoveryAttestationSubmitter` end-to-end contract
/// tests. Uses REAL <see cref="X25519KeyAgreement"/> +
/// <see cref="HkdfX25519SubkeyDerivation"/> +
/// <see cref="InMemoryRecoveryStateStore"/> so the OpenBox + re-Box round-trip
/// is exercised against the production crypto path.
/// </summary>
public sealed class RecoveryAttestationSubmitterTests
{
    private const string TrusteeNodeId = "trustee-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact]
    public async Task SubmitAsync_NoTrusteeEnvelope_ReturnsAcceptedFalse()
    {
        var harness = new Harness();
        var result = await harness.Sut.SubmitAsync(harness.Request);

        Assert.False(result.Accepted);
        Assert.False(result.QuorumReached);
    }

    [Fact]
    public async Task SubmitAsync_EnvelopeOpenBoxFails_ReturnsAcceptedFalse()
    {
        var harness = new Harness();
        await harness.Coordinator.SetupTrusteeAsync(
            TrusteeNodeId,
            new TrusteeEncryptedSeed(
                TrusteeNodeId:           TrusteeNodeId,
                OwnerEphX25519PublicKey: new byte[32],
                Ciphertext:              new byte[TrusteeAttestation.SeedEnvelopeCiphertextLength],
                Nonce:                   new byte[TrusteeAttestation.SeedEnvelopeNonceLength]));

        var result = await harness.Sut.SubmitAsync(harness.Request);

        Assert.False(result.Accepted);
    }

    [Fact]
    public async Task SubmitAsync_OpenBoxAuthTagMismatch_ReturnsAcceptedFalse()
    {
        // Council R-9 — distinct from the all-zero contributory-check
        // case: use a REAL non-zero ownerEph keypair + valid Box() to
        // produce a well-formed envelope, then corrupt the last byte
        // of the ciphertext so OpenBox returns null (auth-tag mismatch
        // path, not CryptographicException path).
        var harness = new Harness();
        var realDhPub = harness.X25519SubkeyDerivation.DeriveX25519PublicKey(
            new byte[KeystoreRootSeedProvider.SeedLength], "team");
        var (ownerEphPub, ownerEphPriv) = harness.KeyAgreement.GenerateKeyPair();
        var (ct, nonce) = harness.KeyAgreement.Box(new byte[32], realDhPub, ownerEphPriv);
        // Corrupt the auth tag (last 16 bytes of ciphertext).
        ct[^1] ^= 0xFF;

        await harness.Coordinator.SetupTrusteeAsync(
            TrusteeNodeId,
            new TrusteeEncryptedSeed(
                TrusteeNodeId:           TrusteeNodeId,
                OwnerEphX25519PublicKey: ownerEphPub,
                Ciphertext:              ct,
                Nonce:                   nonce));

        var result = await harness.Sut.SubmitAsync(harness.Request);

        Assert.False(result.Accepted);
    }

    [Fact]
    public async Task SubmitAsync_HappyPath_SubmitsAttestationWithDerivedDhKeyAndReEncryptedEnvelope()
    {
        // End-to-end OpenBox + re-Box flow through real X25519
        // crypto. Uses a substitute coordinator to capture the
        // submitted TrusteeAttestation directly and confirm the
        // envelope round-trips back to the original owner seed.
        var ed25519 = new Ed25519Signer();
        var keyAgreement = new X25519KeyAgreement();
        var x25519SubkeyDeriv = new HkdfX25519SubkeyDerivation();
        var teamSubkeyDeriv = new TeamSubkeyDerivation(ed25519);
        var trusteeRootSeed = RandomNumberGenerator.GetBytes(KeystoreRootSeedProvider.SeedLength);
        var teamIdValue = TeamId.New();
        var teamId = teamIdValue.Value.ToString("D");

        // Owner side: Box ownerRootSeed against the trustee's derived DH pub.
        var ownerRootSeed = RandomNumberGenerator.GetBytes(32);
        var trusteeDhPub = x25519SubkeyDeriv.DeriveX25519PublicKey(trusteeRootSeed, teamId);
        var (ownerEphPub, ownerEphPriv) = keyAgreement.GenerateKeyPair();
        var (ct, nonce) = keyAgreement.Box(ownerRootSeed, trusteeDhPub, ownerEphPriv);
        var envelope = new TrusteeEncryptedSeed(
            TrusteeNodeId:           TrusteeNodeId,
            OwnerEphX25519PublicKey: ownerEphPub,
            Ciphertext:              ct,
            Nonce:                   nonce);

        // Recovering device: ephemeral DH keypair.
        var (recoveringEphPub, recoveringEphPriv) = keyAgreement.GenerateKeyPair();
        var (reqEdPub, reqEdPriv) = ed25519.GenerateKeyPair();
        var request = RecoveryRequest.Create(
            "node-recovering", reqEdPub, recoveringEphPub, reqEdPriv,
            DateTimeOffset.UtcNow, ed25519);

        // Coordinator: substitute. Captures the submitted attestation.
        TrusteeAttestation? submitted = null;
        var coord = Substitute.For<IRecoveryCoordinator>();
        coord.GetTrusteeEncryptedSeedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
             .Returns(Task.FromResult<TrusteeEncryptedSeed?>(envelope));
        coord.SubmitAttestationAsync(Arg.Any<TrusteeAttestation>(), Arg.Any<CancellationToken>())
             .Returns(ci =>
             {
                 submitted = (TrusteeAttestation)ci[0]!;
                 var graceStarted = new RecoveryEvent(
                     RecoveryEventType.GracePeriodStarted,
                     submitted.TrusteeNodeId, submitted.TrusteeNodeId,
                     DateTimeOffset.UtcNow, null, new Dictionary<string, string>());
                 return Task.FromResult(new RecoveryAttestationOutcome(true, new[] { graceStarted }));
             });

        var rootSeedProvider = new InMemoryRootSeedProvider(trusteeRootSeed);
        var nodeIdentity = new InMemoryNodeIdentityProvider(
            new NodeIdentity(TrusteeNodeId, new byte[32], new byte[32]));
        var sessionSignerAccessor = new FakeSessionSignerAccessor(
            ed25519, trusteeRootSeed, teamId, teamSubkeyDeriv);
        var activeTeam = Substitute.For<IActiveTeamAccessor>();
        var sp = new ServiceCollection().BuildServiceProvider();
        activeTeam.Active.Returns(new TeamContext(teamIdValue, "t", sp));

        var sut = new RecoveryAttestationSubmitter(
            coord, sessionSignerAccessor, nodeIdentity, x25519SubkeyDeriv,
            rootSeedProvider, activeTeam, keyAgreement, TimeProvider.System);

        var result = await sut.SubmitAsync(request);

        Assert.True(result.Accepted);
        Assert.True(result.QuorumReached);
        Assert.NotNull(submitted);

        // The submitted TrusteeDHPublicKey must match the derived key
        // (so the coordinator's MAJOR-2 binding check passes upstream).
        Assert.Equal(trusteeDhPub, submitted!.TrusteeDHPublicKey);

        // The re-encrypted envelope must OpenBox back to the owner's
        // original seed when opened with the recovering device's
        // ephemeral DH private key.
        var openedBack = keyAgreement.OpenBox(
            ciphertext:           submitted.EncryptedSeedEnvelopeCiphertext,
            nonce:                submitted.EncryptedSeedEnvelopeNonce,
            senderPublicKey:      submitted.TrusteeDHPublicKey,
            recipientPrivateKey:  recoveringEphPriv);
        Assert.NotNull(openedBack);
        Assert.Equal(ownerRootSeed, openedBack);
    }

    // ----- harness ---------------------------------------------------

    private sealed class Harness
    {
        public Ed25519Signer Ed25519 { get; } = new();
        public X25519KeyAgreement KeyAgreement { get; } = new();
        public HkdfX25519SubkeyDerivation X25519SubkeyDerivation { get; } = new();
        public TeamSubkeyDerivation TeamSubkeyDerivation { get; }
        public InMemoryRecoveryStateStore Store { get; } = new();
        public RecoveryCoordinator Coordinator { get; }
        public byte[] RootSeed { get; } = RandomNumberGenerator.GetBytes(KeystoreRootSeedProvider.SeedLength);
        public byte[] OwnerEd25519Pub { get; }
        public byte[] OwnerEd25519Priv { get; }
        public TeamId TeamId { get; } = TeamId.New();
        public RecoveryRequest Request { get; }
        public byte[] EphX25519Pub { get; }
        public byte[] EphX25519Priv { get; }
        public RecoveryAttestationSubmitter Sut { get; }

        public Harness()
        {
            TeamSubkeyDerivation = new TeamSubkeyDerivation(Ed25519);
            (OwnerEd25519Pub, OwnerEd25519Priv) = Ed25519.GenerateKeyPair();
            Coordinator = new RecoveryCoordinator(
                new FixedClock(), Store, Ed25519,
                new FixedDisputerValidator(new[] { OwnerEd25519Pub }),
                new RecoveryCoordinatorOptions { QuorumThreshold = 1, MaxTrustees = 5 });

            (EphX25519Pub, EphX25519Priv) = KeyAgreement.GenerateKeyPair();
            var (reqPub, reqPriv) = Ed25519.GenerateKeyPair();
            Request = RecoveryRequest.Create(
                requestingNodeId:     "node-recovering",
                ephemeralPublicKey:   reqPub,
                ephemeralDHPublicKey: EphX25519Pub,
                ephemeralPrivateKey:  reqPriv,
                requestedAt:          DateTimeOffset.UtcNow,
                signer:               Ed25519);

            var rootSeedProvider = new InMemoryRootSeedProvider(RootSeed);
            var nodeIdentity = new InMemoryNodeIdentityProvider(
                new NodeIdentity(TrusteeNodeId, new byte[32], new byte[32]));
            var sessionSignerAccessor = new FakeSessionSignerAccessor(
                Ed25519, RootSeed, TeamId.Value.ToString("D"), TeamSubkeyDerivation);
            var activeTeam = Substitute.For<IActiveTeamAccessor>();
            var sp = new ServiceCollection().BuildServiceProvider();
            activeTeam.Active.Returns(new TeamContext(TeamId, "team-active", sp));

            Sut = new RecoveryAttestationSubmitter(
                recovery:               Coordinator,
                signerAccessor:         sessionSignerAccessor,
                nodeIdentity:           nodeIdentity,
                x25519SubkeyDerivation: X25519SubkeyDerivation,
                rootSeedProvider:       rootSeedProvider,
                activeTeam:             activeTeam,
                keyAgreement:           KeyAgreement,
                time:                   TimeProvider.System);
        }
    }

    private sealed class RealOwnerBoxer
    {
        private readonly Harness _h;
        private readonly byte[] _ownerRootSeed = RandomNumberGenerator.GetBytes(32);

        public RealOwnerBoxer(Harness h) { _h = h; }

        public async Task SetupAsync()
        {
            var trusteeDhPub = _h.X25519SubkeyDerivation.DeriveX25519PublicKey(
                _h.RootSeed, _h.TeamId.Value.ToString("D"));
            var (trusteeEdPub, _) = _h.TeamSubkeyDerivation.DeriveTeamKeypair(
                _h.RootSeed, _h.TeamId.Value.ToString("D"));
            await _h.Coordinator.DesignateTrusteeAsync(
                TrusteeNodeId, trusteeEdPub, trusteeDhPub);

            var (ownerEphPub, ownerEphPriv) = _h.KeyAgreement.GenerateKeyPair();
            var (ct, nonce) = _h.KeyAgreement.Box(_ownerRootSeed, trusteeDhPub, ownerEphPriv);
            await _h.Coordinator.SetupTrusteeAsync(TrusteeNodeId,
                new TrusteeEncryptedSeed(
                    TrusteeNodeId:           TrusteeNodeId,
                    OwnerEphX25519PublicKey: ownerEphPub,
                    Ciphertext:              ct,
                    Nonce:                   nonce));

            await _h.Coordinator.InitiateRecoveryAsync(_h.Request);
        }
    }

    private sealed class FixedClock : IRecoveryClock
    {
        public DateTimeOffset UtcNow() => new(2026, 5, 16, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class InMemoryRootSeedProvider : IRootSeedProvider
    {
        private readonly byte[] _seed;
        public InMemoryRootSeedProvider(byte[] seed) { _seed = seed; }
        public ValueTask<ReadOnlyMemory<byte>> GetRootSeedAsync(CancellationToken ct)
            => new(_seed);
    }

    private sealed class FakeSessionSignerAccessor : ISessionSignerAccessor
    {
        private readonly IEd25519Signer _signer;
        private readonly TeamSubkeyDerivation _teamDeriv;
        private readonly byte[] _rootSeed;
        private readonly string _teamId;

        public FakeSessionSignerAccessor(
            IEd25519Signer signer, byte[] rootSeed, string teamId, TeamSubkeyDerivation teamDeriv)
        {
            _signer   = signer;
            _rootSeed = rootSeed;
            _teamId   = teamId;
            _teamDeriv = teamDeriv;
        }

        public ValueTask<IBoundEd25519Signer> GetCurrentAsync(CancellationToken ct = default)
        {
            var (pub, priv) = _teamDeriv.DeriveTeamKeypair(_rootSeed, _teamId);
            return new ValueTask<IBoundEd25519Signer>(
                new DefaultBoundEd25519Signer(_signer, priv, pub));
        }
    }
}
