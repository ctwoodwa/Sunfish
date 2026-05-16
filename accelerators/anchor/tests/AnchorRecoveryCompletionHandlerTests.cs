using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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
/// real rekey path, post-security-council amendments
/// (2026-05-16). Verifies happy-path success, divergent-seed abort,
/// quorum enforcement on successful decryptions, envelope-length
/// validation, multi-team detection, ephemeral-key cleanup on every
/// exit path, and the missing-ephemeral-key graceful return.
/// </summary>
/// <remarks>
/// Hand-rolled fakes for <see cref="IX25519KeyAgreement"/> and
/// <see cref="ISqlCipherKeyDerivation"/> — NSubstitute can't mock
/// <c>ReadOnlySpan&lt;byte&gt;</c> parameters.
/// </remarks>
public sealed class AnchorRecoveryCompletionHandlerTests
{
    private const int DefaultQuorum = 3;

    [Fact]
    public async Task HandleAsync_SuccessPath_RestoresSeed_RotatesSqlCipher_ClearsEphemeralKey()
    {
        var seed = new byte[KeystoreRootSeedProvider.SeedLength];
        Array.Fill(seed, (byte)0xCE);
        var sut = NewHandler(out var deps, openBoxResult: seed);
        await deps.EphStore.SetAsync(
            IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName, new byte[32]);

        await sut.HandleAsync(NewCompletionResult(attestationCount: 3), default);

        await deps.RootSeedRestorer.Received(1).RestoreRootSeedAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        await deps.EncryptedStore.Received(1).RotateKeyAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        Assert.Null(await deps.EphStore.GetAsync(IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName));
    }

    [Fact]
    public async Task HandleAsync_NoEphemeralKey_ReturnsWithoutRekey()
    {
        var sut = NewHandler(out var deps, openBoxResult: new byte[KeystoreRootSeedProvider.SeedLength]);
        // (Do NOT seed the ephemeral key store.)

        await sut.HandleAsync(NewCompletionResult(attestationCount: 3), default);

        await deps.RootSeedRestorer.DidNotReceive().RestoreRootSeedAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        await deps.EncryptedStore.DidNotReceive().RotateKeyAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_BelowQuorumDecryptions_AbortsAndClearsEphemeralKey()
    {
        // 3 attestations, but only 1 decrypts → below quorum (3). Even
        // though that one decryption is internally-consistent, accepting
        // it would let a single rogue trustee install a rogue seed.
        var fake = new FakeKeyAgreement();
        var calls = 0;
        fake.OpenBoxFunc = () => (++calls) == 1 ? new byte[KeystoreRootSeedProvider.SeedLength] : null;
        var sut = NewHandlerCore(fake, withActiveTeam: true, materializedTeamsExtra: 0, out var deps);
        await deps.EphStore.SetAsync(
            IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName, new byte[32]);

        await sut.HandleAsync(NewCompletionResult(attestationCount: 3), default);

        await deps.RootSeedRestorer.DidNotReceive().RestoreRootSeedAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        await deps.EncryptedStore.DidNotReceive().RotateKeyAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        // Ephemeral key MUST be cleared on the abort path
        // (council BLOCKING-3).
        Assert.Null(await deps.EphStore.GetAsync(IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName));
    }

    [Fact]
    public async Task HandleAsync_DivergentSeeds_AbortsAndClearsEphemeralKey()
    {
        var seedA = new byte[KeystoreRootSeedProvider.SeedLength]; Array.Fill(seedA, (byte)0xAA);
        var seedB = new byte[KeystoreRootSeedProvider.SeedLength]; Array.Fill(seedB, (byte)0xBB);
        var fake = new FakeKeyAgreement();
        var calls = 0;
        fake.OpenBoxFunc = () => (++calls) <= 2 ? seedA : seedB;
        var sut = NewHandlerCore(fake, withActiveTeam: true, materializedTeamsExtra: 0, out var deps);
        await deps.EphStore.SetAsync(
            IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName, new byte[32]);

        await sut.HandleAsync(NewCompletionResult(attestationCount: 3), default);

        await deps.RootSeedRestorer.DidNotReceive().RestoreRootSeedAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        Assert.Null(await deps.EphStore.GetAsync(IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName));
    }

    [Fact]
    public async Task HandleAsync_MultiTeam_AbortsAndClearsEphemeralKey()
    {
        // 2 materialized teams → multi-team rekey not implemented yet;
        // handler must refuse (council BLOCKING-1).
        var sut = NewHandler(out var deps, openBoxResult: new byte[KeystoreRootSeedProvider.SeedLength],
            materializedTeamsExtra: 1);
        await deps.EphStore.SetAsync(
            IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName, new byte[32]);

        await sut.HandleAsync(NewCompletionResult(attestationCount: 3), default);

        await deps.RootSeedRestorer.DidNotReceive().RestoreRootSeedAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        await deps.EncryptedStore.DidNotReceive().RotateKeyAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        Assert.Null(await deps.EphStore.GetAsync(IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName));
    }

    [Fact]
    public async Task HandleAsync_MalformedEnvelopeLength_AbortsAndClearsEphemeralKey()
    {
        // Council MAJOR-3 — envelope length not enforced. Now it is.
        var sut = NewHandler(out var deps, openBoxResult: new byte[KeystoreRootSeedProvider.SeedLength]);
        await deps.EphStore.SetAsync(
            IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName, new byte[32]);

        // One attestation has a wrong-length nonce (23 vs 24).
        var result = NewCompletionResult(attestationCount: 3,
            mutator: (i, att) => i == 0
                ? att with { EncryptedSeedEnvelopeNonce = new byte[23] }
                : att);

        await sut.HandleAsync(result, default);

        await deps.RootSeedRestorer.DidNotReceive().RestoreRootSeedAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        Assert.Null(await deps.EphStore.GetAsync(IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName));
    }

    [Fact]
    public async Task HandleAsync_NoActiveTeam_RestoresSeedSkipsRekeyClearsEphemeralKey()
    {
        var sut = NewHandler(out var deps,
            openBoxResult: new byte[KeystoreRootSeedProvider.SeedLength],
            withActiveTeam: false);
        await deps.EphStore.SetAsync(
            IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName, new byte[32]);

        await sut.HandleAsync(NewCompletionResult(attestationCount: 3), default);

        await deps.RootSeedRestorer.Received(1).RestoreRootSeedAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        await deps.EncryptedStore.DidNotReceive().RotateKeyAsync(
            Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>());
        Assert.Null(await deps.EphStore.GetAsync(IEphemeralRecoveryKeyStore.RecoveryDhPrivateKeyName));
    }

    // ----- helpers ---------------------------------------------------

    private sealed record Deps(
        IRootSeedRestorer RootSeedRestorer,
        IEncryptedStore EncryptedStore,
        IEphemeralRecoveryKeyStore EphStore);

    private static AnchorRecoveryCompletionHandler NewHandler(
        out Deps deps,
        byte[]? openBoxResult,
        bool withActiveTeam = true,
        int materializedTeamsExtra = 0)
    {
        var fake = new FakeKeyAgreement { OpenBoxFunc = () => openBoxResult };
        return NewHandlerCore(fake, withActiveTeam, materializedTeamsExtra, out deps);
    }

    private static AnchorRecoveryCompletionHandler NewHandlerCore(
        FakeKeyAgreement keyAgreement,
        bool withActiveTeam,
        int materializedTeamsExtra,
        out Deps deps)
    {
        var rootSeedRestorer = Substitute.For<IRootSeedRestorer>();
        var sqlCipherDeriv   = new FakeSqlCipherKeyDerivation();
        var encryptedStore   = Substitute.For<IEncryptedStore>();
        var ephStore         = new InMemoryEphemeralRecoveryKeyStore();
        var activeTeam       = Substitute.For<IActiveTeamAccessor>();
        var teamFactory      = Substitute.For<ITeamContextFactory>();
        var options          = Options.Create(new RecoveryCoordinatorOptions { QuorumThreshold = DefaultQuorum });

        var materialized = new List<TeamContext>();
        if (withActiveTeam)
        {
            var sp = new ServiceCollection()
                .AddSingleton<IEncryptedStore>(encryptedStore)
                .BuildServiceProvider();
            var ctx = new TeamContext(TeamId.New(), "team-active", sp);
            activeTeam.Active.Returns(ctx);
            materialized.Add(ctx);
        }
        else
        {
            activeTeam.Active.Returns((TeamContext?)null);
        }
        for (var i = 0; i < materializedTeamsExtra; i++)
        {
            var sp = new ServiceCollection().BuildServiceProvider();
            materialized.Add(new TeamContext(TeamId.New(), $"team-extra-{i}", sp));
        }
        teamFactory.Active.Returns(materialized.AsReadOnly());

        deps = new Deps(rootSeedRestorer, encryptedStore, ephStore);
        return new AnchorRecoveryCompletionHandler(
            keyAgreement, rootSeedRestorer, sqlCipherDeriv, ephStore, activeTeam, teamFactory,
            options, NullLogger<AnchorRecoveryCompletionHandler>.Instance);
    }

    private static RecoveryCompletionResult NewCompletionResult(
        int attestationCount,
        Func<int, TrusteeAttestation, TrusteeAttestation>? mutator = null)
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
            var att = new TrusteeAttestation(
                TrusteeNodeId:                    $"trustee-{i}",
                TrusteePublicKey:                 new byte[32],
                RecoveryRequestHash:              new byte[TrusteeAttestation.RequestHashLength],
                AttestedAt:                       DateTimeOffset.UnixEpoch,
                Signature:                        new byte[64],
                TrusteeDHPublicKey:               new byte[TrusteeAttestation.TrusteeDHPublicKeyLength],
                EncryptedSeedEnvelopeCiphertext:  new byte[TrusteeAttestation.SeedEnvelopeCiphertextLength],
                EncryptedSeedEnvelopeNonce:       new byte[TrusteeAttestation.SeedEnvelopeNonceLength]);
            attestations.Add(mutator is not null ? mutator(i, att) : att);
        }
        return new RecoveryCompletionResult(evt, attestations);
    }

    private sealed class FakeKeyAgreement : IX25519KeyAgreement
    {
        public Func<byte[]?>? OpenBoxFunc { get; set; }
        public int PublicKeyLength => 32;
        public int PrivateKeyLength => 32;
        public int NonceLength => 24;
        public (byte[] PublicKey, byte[] PrivateKey) GenerateKeyPair() => (new byte[32], new byte[32]);
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

    private sealed class FakeSqlCipherKeyDerivation : ISqlCipherKeyDerivation
    {
        public byte[] DeriveSqlCipherKey(ReadOnlySpan<byte> rootSeed, string teamId)
        {
            var key = new byte[32];
            Array.Fill(key, (byte)0xAA);
            return key;
        }
    }
}
