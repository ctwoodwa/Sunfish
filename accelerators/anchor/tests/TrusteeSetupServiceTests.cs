using System.Security.Cryptography;
using Sunfish.Anchor.Services;
using Sunfish.Foundation.Recovery;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;
using Xunit;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#67 PR 5 council R-7 — coverage for the owner-side
/// <see cref="TrusteeSetupService"/> flow.
/// </summary>
public sealed class TrusteeSetupServiceTests
{
    private const string TrusteeAId = "trustee-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string TrusteeBId = "trustee-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task DesignateAndDistributeAsync_HappyPath_PersistsEnvelopeAndReturnsBothFingerprints()
    {
        var harness = new Harness();
        var (edPub, _) = harness.Ed25519.GenerateKeyPair();
        var dhPub = harness.X25519SubkeyDeriv.DeriveX25519PublicKey(
            harness.TrusteeRootSeed, harness.TrusteeTeamId);

        var outcome = await harness.Sut.DesignateAndDistributeAsync(
            TrusteeAId, edPub, dhPub);

        Assert.Equal(TrusteeAId, outcome.TrusteeNodeId);
        Assert.Equal(16, outcome.TrusteeDHFingerprintHex.Length);
        Assert.Equal(16, outcome.TrusteeEd25519FingerprintHex.Length);
        Assert.NotEqual(outcome.TrusteeDHFingerprintHex, outcome.TrusteeEd25519FingerprintHex);

        var envelope = await harness.Coordinator.GetTrusteeEncryptedSeedAsync(TrusteeAId);
        Assert.NotNull(envelope);
        Assert.Equal(TrusteeAttestation.SeedEnvelopeCiphertextLength, envelope!.Ciphertext.Length);
        Assert.Equal(TrusteeAttestation.SeedEnvelopeNonceLength, envelope.Nonce.Length);
    }

    [Fact]
    public async Task DesignateAndDistributeAsync_ReCallForSameTrustee_OverwritesEnvelope()
    {
        var harness = new Harness();
        var (edPub, _) = harness.Ed25519.GenerateKeyPair();
        var dhPub = harness.X25519SubkeyDeriv.DeriveX25519PublicKey(
            harness.TrusteeRootSeed, harness.TrusteeTeamId);

        await harness.Sut.DesignateAndDistributeAsync(TrusteeAId, edPub, dhPub);
        var first = await harness.Coordinator.GetTrusteeEncryptedSeedAsync(TrusteeAId);
        await harness.Sut.DesignateAndDistributeAsync(TrusteeAId, edPub, dhPub);
        var second = await harness.Coordinator.GetTrusteeEncryptedSeedAsync(TrusteeAId);

        Assert.NotNull(first);
        Assert.NotNull(second);
        // Re-call uses a FRESH owner-ephemeral keypair so envelope bytes
        // differ even though plaintext + recipient are identical.
        Assert.NotEqual(first!.Ciphertext, second!.Ciphertext);
    }

    [Fact]
    public async Task DesignateAndDistributeAsync_TwoTrustees_UseFreshOwnerEphemeralKeysPerTrustee()
    {
        var harness = new Harness();
        var (edPubA, _) = harness.Ed25519.GenerateKeyPair();
        var (edPubB, _) = harness.Ed25519.GenerateKeyPair();
        var dhPubA = harness.X25519SubkeyDeriv.DeriveX25519PublicKey(
            harness.TrusteeRootSeed, harness.TrusteeTeamId);
        var dhPubB = harness.X25519SubkeyDeriv.DeriveX25519PublicKey(
            harness.TrusteeRootSeed, "team-other");

        await harness.Sut.DesignateAndDistributeAsync(TrusteeAId, edPubA, dhPubA);
        await harness.Sut.DesignateAndDistributeAsync(TrusteeBId, edPubB, dhPubB);

        var envA = await harness.Coordinator.GetTrusteeEncryptedSeedAsync(TrusteeAId);
        var envB = await harness.Coordinator.GetTrusteeEncryptedSeedAsync(TrusteeBId);
        Assert.NotNull(envA);
        Assert.NotNull(envB);
        Assert.NotEqual(envA!.OwnerEphX25519PublicKey, envB!.OwnerEphX25519PublicKey);
    }

    [Fact]
    public async Task DesignateAndDistributeAsync_WrongLengthDhKey_ThrowsArgumentException()
    {
        var harness = new Harness();
        var (edPub, _) = harness.Ed25519.GenerateKeyPair();
        var shortDh = new byte[TrusteeDesignation.DHPublicKeyLength - 1];

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Sut.DesignateAndDistributeAsync(TrusteeAId, edPub, shortDh));
    }

    [Fact]
    public async Task DesignateAndDistributeAsync_NullArgs_Throw()
    {
        var harness = new Harness();
        var (edPub, _) = harness.Ed25519.GenerateKeyPair();
        var dhPub = harness.X25519SubkeyDeriv.DeriveX25519PublicKey(
            harness.TrusteeRootSeed, harness.TrusteeTeamId);

        await Assert.ThrowsAsync<ArgumentException>(
            () => harness.Sut.DesignateAndDistributeAsync(string.Empty, edPub, dhPub));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => harness.Sut.DesignateAndDistributeAsync(TrusteeAId, null!, dhPub));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => harness.Sut.DesignateAndDistributeAsync(TrusteeAId, edPub, null!));
    }

    // ----- harness ---------------------------------------------------

    private sealed class Harness
    {
        public Ed25519Signer Ed25519 { get; } = new();
        public X25519KeyAgreement KeyAgreement { get; } = new();
        public HkdfX25519SubkeyDerivation X25519SubkeyDeriv { get; } = new();
        public byte[] OwnerRootSeed { get; } = RandomNumberGenerator.GetBytes(KeystoreRootSeedProvider.SeedLength);
        public byte[] TrusteeRootSeed { get; } = RandomNumberGenerator.GetBytes(KeystoreRootSeedProvider.SeedLength);
        public string TrusteeTeamId { get; } = "team-trustee";
        public InMemoryRecoveryStateStore Store { get; } = new();
        public RecoveryCoordinator Coordinator { get; }
        public TrusteeSetupService Sut { get; }

        public Harness()
        {
            var (ownerPub, _) = Ed25519.GenerateKeyPair();
            Coordinator = new RecoveryCoordinator(
                new FixedClock(), Store, Ed25519,
                new FixedDisputerValidator(new[] { ownerPub }),
                new RecoveryCoordinatorOptions { QuorumThreshold = 1, MaxTrustees = 5 });
            Sut = new TrusteeSetupService(
                Coordinator,
                new InMemoryRootSeedProvider(OwnerRootSeed),
                KeyAgreement);
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
}
