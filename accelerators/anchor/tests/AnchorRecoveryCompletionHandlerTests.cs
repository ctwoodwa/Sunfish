using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Sunfish.Anchor.Services;
using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Foundation.Recovery;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#67 / ADR 0046-A6 — coverage for <see cref="AnchorRecoveryCompletionHandler"/>'s
/// real rekey path. Verifies happy-path success, divergent-seed abort,
/// zero-decryption abort, missing-ephemeral-key graceful return, and
/// SecureStorage clearing on success.
/// </summary>
/// <remarks>
/// Uses hand-rolled fakes for <see cref="IX25519KeyAgreement"/> and
/// <see cref="ISqlCipherKeyDerivation"/> because NSubstitute cannot mock
/// methods whose parameters are <c>ReadOnlySpan&lt;byte&gt;</c> (ref
/// structs are disallowed as generic type arguments). Mirrors the
/// hand-rolled-fake pattern in <c>SessionSignerAccessorTests</c>.
/// </remarks>
public sealed class AnchorRecoveryCompletionHandlerTests
{
    [Fact]
    public async Task HandleAsync_SuccessPath_RestoresSeed_RotatesSqlCipher_ClearsEphemeralKey()
    {
        var seed = new byte[32]; Array.Fill(seed, (byte)0xCE);
        var sut = NewHandler(out var deps, openBoxResult: seed);
        await deps.EphStore.SetAsync(
            IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName, new byte[32]);

        await sut.HandleAsync(NewCompletionResult(attestationCount: 3), default);

        await deps.RootSeedRestorer.Received(1).RestoreRootSeedAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        await deps.EncryptedStore.Received(1).RotateKeyAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        var afterCleanup = await deps.EphStore.GetAsync(
            IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName);
        Assert.Null(afterCleanup);
    }

    [Fact]
    public async Task HandleAsync_NoEphemeralKey_ReturnsWithoutRekey()
    {
        var sut = NewHandler(out var deps, openBoxResult: new byte[32]);

        await sut.HandleAsync(NewCompletionResult(attestationCount: 3), default);

        await deps.RootSeedRestorer.DidNotReceive().RestoreRootSeedAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        await deps.EncryptedStore.DidNotReceive().RotateKeyAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_AllEnvelopesFailToDecrypt_AbortsWithoutRekey()
    {
        var sut = NewHandler(out var deps, openBoxResult: null);
        await deps.EphStore.SetAsync(
            IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName, new byte[32]);

        await sut.HandleAsync(NewCompletionResult(attestationCount: 3), default);

        await deps.RootSeedRestorer.DidNotReceive().RestoreRootSeedAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        await deps.EncryptedStore.DidNotReceive().RotateKeyAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DivergentSeeds_AbortsWithoutRekey()
    {
        var seedA = new byte[32]; Array.Fill(seedA, (byte)0xAA);
        var seedB = new byte[32]; Array.Fill(seedB, (byte)0xBB);

        var fakeKeyAgreement = new FakeKeyAgreement();
        var callCount = 0;
        fakeKeyAgreement.OpenBoxFunc = () => (++callCount) <= 2 ? seedA : seedB;

        var sut = NewHandlerCore(fakeKeyAgreement, withActiveTeam: true, out var deps);
        await deps.EphStore.SetAsync(
            IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName, new byte[32]);

        await sut.HandleAsync(NewCompletionResult(attestationCount: 3), default);

        await deps.RootSeedRestorer.DidNotReceive().RestoreRootSeedAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        await deps.EncryptedStore.DidNotReceive().RotateKeyAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NoActiveTeam_RestoresSeedButSkipsRekey()
    {
        var sut = NewHandler(out var deps, openBoxResult: new byte[32], withActiveTeam: false);
        await deps.EphStore.SetAsync(
            IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName, new byte[32]);

        await sut.HandleAsync(NewCompletionResult(attestationCount: 1), default);

        await deps.RootSeedRestorer.Received(1).RestoreRootSeedAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        await deps.EncryptedStore.DidNotReceive().RotateKeyAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    // ----- helpers ---------------------------------------------------

    private sealed record Deps(
        IRootSeedRestorer RootSeedRestorer,
        IEncryptedStore EncryptedStore,
        IEphemeralRecoveryKeyStore EphStore);

    private static AnchorRecoveryCompletionHandler NewHandler(
        out Deps deps,
        byte[]? openBoxResult,
        bool withActiveTeam = true)
    {
        var fake = new FakeKeyAgreement { OpenBoxFunc = () => openBoxResult };
        return NewHandlerCore(fake, withActiveTeam, out deps);
    }

    private static AnchorRecoveryCompletionHandler NewHandlerCore(
        FakeKeyAgreement keyAgreement,
        bool withActiveTeam,
        out Deps deps)
    {
        var rootSeedRestorer = Substitute.For<IRootSeedRestorer>();
        var sqlCipherDeriv   = new FakeSqlCipherKeyDerivation();
        var encryptedStore   = Substitute.For<IEncryptedStore>();
        var ephStore         = new InMemoryEphemeralRecoveryKeyStore();
        var activeTeam       = Substitute.For<IActiveTeamAccessor>();

        if (withActiveTeam)
        {
            var sp = new ServiceCollection()
                .AddSingleton<IEncryptedStore>(encryptedStore)
                .BuildServiceProvider();
            var ctx = new TeamContext(TeamId.New(), "team", sp);
            activeTeam.Active.Returns(ctx);
        }
        else
        {
            activeTeam.Active.Returns((TeamContext?)null);
        }

        deps = new Deps(rootSeedRestorer, encryptedStore, ephStore);
        return new AnchorRecoveryCompletionHandler(
            keyAgreement, rootSeedRestorer, sqlCipherDeriv, ephStore, activeTeam,
            NullLogger<AnchorRecoveryCompletionHandler>.Instance);
    }

    private static RecoveryCompletionResult NewCompletionResult(int attestationCount)
    {
        var evt = new RecoveryEvent(
            Type:               RecoveryEventType.RecoveryCompleted,
            ActorNodeId:        "node-target",
            TargetNodeId:       "node-target",
            OccurredAt:         DateTimeOffset.UnixEpoch,
            PreviousEventHash:  null,
            Detail:             new Dictionary<string, string>());
        var attestations = new List<TrusteeAttestation>();
        for (var i = 0; i < attestationCount; i++)
        {
            attestations.Add(new TrusteeAttestation(
                TrusteeNodeId:                    $"trustee-{i}",
                TrusteePublicKey:                 new byte[32],
                RecoveryRequestHash:              new byte[TrusteeAttestation.RequestHashLength],
                AttestedAt:                       DateTimeOffset.UnixEpoch,
                Signature:                        new byte[64],
                TrusteeDHPublicKey:               new byte[TrusteeAttestation.TrusteeDHPublicKeyLength],
                EncryptedSeedEnvelopeCiphertext:  new byte[TrusteeAttestation.SeedEnvelopeCiphertextLength],
                EncryptedSeedEnvelopeNonce:       new byte[TrusteeAttestation.SeedEnvelopeNonceLength]));
        }
        return new RecoveryCompletionResult(evt, attestations);
    }

    /// <summary>
    /// Hand-rolled fake for <see cref="IX25519KeyAgreement"/>. NSubstitute
    /// cannot mock <c>ReadOnlySpan&lt;byte&gt;</c> parameters; this fake
    /// returns whatever <see cref="OpenBoxFunc"/> produces per call.
    /// </summary>
    private sealed class FakeKeyAgreement : IX25519KeyAgreement
    {
        public Func<byte[]?>? OpenBoxFunc { get; set; }
        public int PublicKeyLength => 32;
        public int PrivateKeyLength => 32;
        public int NonceLength => 24;
        public (byte[] PublicKey, byte[] PrivateKey) GenerateKeyPair()
            => (new byte[32], new byte[32]);
        public (byte[] Ciphertext, byte[] Nonce) Box(
            ReadOnlySpan<byte> plaintext,
            ReadOnlySpan<byte> recipientPublicKey,
            ReadOnlySpan<byte> senderPrivateKey)
            => (new byte[plaintext.Length + 16], new byte[24]);
        public byte[]? OpenBox(
            ReadOnlySpan<byte> ciphertext,
            ReadOnlySpan<byte> nonce,
            ReadOnlySpan<byte> senderPublicKey,
            ReadOnlySpan<byte> recipientPrivateKey)
            => OpenBoxFunc?.Invoke();
    }

    /// <summary>
    /// Hand-rolled fake for <see cref="ISqlCipherKeyDerivation"/> — returns
    /// a deterministic 32-byte key for any input. Captures the teamId
    /// arg for assertions if needed.
    /// </summary>
    private sealed class FakeSqlCipherKeyDerivation : ISqlCipherKeyDerivation
    {
        public string? LastTeamId { get; private set; }
        public byte[] DeriveSqlCipherKey(ReadOnlySpan<byte> rootSeed, string teamId)
        {
            LastTeamId = teamId;
            var key = new byte[32];
            Array.Fill(key, (byte)0xAA);
            return key;
        }
    }
}
