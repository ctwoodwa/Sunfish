using Sunfish.Kernel.Security.Crypto;
using Sunfish.Foundation.Recovery;

namespace Sunfish.Foundation.Recovery.Tests;

/// <summary>
/// Coverage for <see cref="RecoveryCoordinator"/> — the Phase 1 G6
/// orchestrator wiring trustee designation, multi-sig attestation
/// quorum (sub-pattern #48a), the 7-day grace window (#48e), the
/// dispute path, and the audit-event emission (#48f) per ADR 0046.
/// </summary>
public sealed class RecoveryCoordinatorTests
{
    // W#67 placeholder fields — protocol-correct byte lengths but
    // zero-filled, since these tests cover coordinator state-machine
    // semantics, not the seed-envelope encryption (that's PR 4+5).
    private static readonly byte[] EphDH       = new byte[RecoveryRequest.EphemeralDHPublicKeyLength];
    private static readonly byte[] TrusteeDH   = new byte[TrusteeAttestation.TrusteeDHPublicKeyLength];
    private static readonly byte[] SeedCT      = new byte[TrusteeAttestation.SeedEnvelopeCiphertextLength];
    private static readonly byte[] SeedNonce   = new byte[TrusteeAttestation.SeedEnvelopeNonceLength];

    private sealed class TestClock : IRecoveryClock
    {
        public DateTimeOffset Now { get; set; } = new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset UtcNow() => Now;
        public void Advance(TimeSpan delta) => Now += delta;
    }

    private sealed record Fixture(
        RecoveryCoordinator Coordinator,
        TestClock Clock,
        Ed25519Signer Signer,
        InMemoryRecoveryStateStore Store,
        FixedDisputerValidator DisputerValidator,
        byte[] OwnerPublicKey,
        byte[] OwnerPrivateKey,
        RecoveryCoordinatorOptions Options);

    private static Fixture NewFixture(RecoveryCoordinatorOptions? options = null)
    {
        var clock = new TestClock();
        var signer = new Ed25519Signer();
        var store = new InMemoryRecoveryStateStore();
        var (ownerPub, ownerPriv) = signer.GenerateKeyPair();
        var disputers = new FixedDisputerValidator(new[] { ownerPub });
        var opts = options ?? new RecoveryCoordinatorOptions();
        var coordinator = new RecoveryCoordinator(clock, store, signer, disputers, opts);
        return new Fixture(coordinator, clock, signer, store, disputers, ownerPub, ownerPriv, opts);
    }

    private static (RecoveryRequest Request, byte[] DevicePub, byte[] DevicePriv) BuildRequest(Ed25519Signer signer, TestClock clock)
    {
        var (pub, priv) = signer.GenerateKeyPair();
        var req = RecoveryRequest.Create(
            requestingNodeId:     "new-device-node",
            ephemeralPublicKey:   pub,
            ephemeralDHPublicKey: EphDH,
            ephemeralPrivateKey:  priv,
            requestedAt:          clock.UtcNow(),
            signer:               signer);
        return (req, pub, priv);
    }

    private static (string NodeId, byte[] Pub, byte[] Priv) NewTrustee(Ed25519Signer signer, int index)
    {
        var (pub, priv) = signer.GenerateKeyPair();
        return ($"trustee-{index}-node", pub, priv);
    }

    [Fact]
    public async Task Designate_AddsTrustee_EmitsTrusteeDesignated()
    {
        var f = NewFixture();
        var trustee = NewTrustee(f.Signer, 1);

        var evt = await f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, TrusteeDH);

        Assert.Equal(RecoveryEventType.TrusteeDesignated, evt.Type);
        Assert.Equal(trustee.NodeId, evt.ActorNodeId);
        Assert.Null(evt.PreviousEventHash);
    }

    [Fact]
    public async Task Designate_RejectsBeyondMaxTrustees()
    {
        var f = NewFixture(new RecoveryCoordinatorOptions { MaxTrustees = 2, QuorumThreshold = 2 });
        await f.Coordinator.DesignateTrusteeAsync(NewTrustee(f.Signer, 1).NodeId, NewTrustee(f.Signer, 1).Pub, TrusteeDH);
        await f.Coordinator.DesignateTrusteeAsync(NewTrustee(f.Signer, 2).NodeId, NewTrustee(f.Signer, 2).Pub, TrusteeDH);

        var third = NewTrustee(f.Signer, 3);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Coordinator.DesignateTrusteeAsync(third.NodeId, third.Pub, TrusteeDH));
    }

    [Fact]
    public async Task Designate_AcceptsRepeatWithSameKeys()
    {
        // W#67 PR 5 council R-5: same-keys re-call is now idempotent
        // (was throw). This test renamed from Designate_RejectsDuplicate
        // and inverted to assert the new contract. The throw-on-different-keys
        // variant lives in DesignateTrustee_RejectsExistingNodeIdWithDifferentKeys.
        var f = NewFixture();
        var trustee = NewTrustee(f.Signer, 1);
        var first = await f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, TrusteeDH);
        var second = await f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, TrusteeDH);

        Assert.Equal(RecoveryEventType.TrusteeDesignated, first.Type);
        Assert.Equal(RecoveryEventType.TrusteeDesignated, second.Type);
    }

    [Fact]
    public async Task Revoke_RemovesTrustee_EmitsTrusteeRevoked()
    {
        var f = NewFixture();
        var trustee = NewTrustee(f.Signer, 1);
        await f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, TrusteeDH);

        var evt = await f.Coordinator.RevokeTrusteeAsync(trustee.NodeId);

        Assert.Equal(RecoveryEventType.TrusteeRevoked, evt.Type);
        Assert.NotNull(evt.PreviousEventHash); // Chained to the prior Designate.
    }

    [Fact]
    public async Task Revoke_RejectsUnknownTrustee()
    {
        var f = NewFixture();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Coordinator.RevokeTrusteeAsync("never-designated"));
    }

    [Fact]
    public async Task InitiateRecovery_StoresPendingRequest_EmitsEvent()
    {
        var f = NewFixture();
        var (request, _, _) = BuildRequest(f.Signer, f.Clock);

        var evt = await f.Coordinator.InitiateRecoveryAsync(request);

        Assert.Equal(RecoveryEventType.RecoveryInitiated, evt.Type);
        Assert.Equal(request.RequestingNodeId, evt.ActorNodeId);
        var status = await f.Coordinator.GetStatusAsync();
        Assert.Equal(RecoveryStatusKind.AwaitingAttestations, status.Kind);
        Assert.Same(request, status.PendingRequest);
    }

    [Fact]
    public async Task InitiateRecovery_RejectsInvalidSignature()
    {
        var f = NewFixture();
        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        var corrupted = (byte[])request.Signature.Clone();
        corrupted[^1] ^= 0xFF;
        var bad = request with { Signature = corrupted };

        await Assert.ThrowsAsync<ArgumentException>(
            () => f.Coordinator.InitiateRecoveryAsync(bad));
    }

    [Fact]
    public async Task InitiateRecovery_RejectsConcurrentRequest()
    {
        var f = NewFixture();
        var (first, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(first);

        f.Clock.Advance(TimeSpan.FromMinutes(1));
        var (second, _, _) = BuildRequest(f.Signer, f.Clock);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Coordinator.InitiateRecoveryAsync(second));
    }

    [Fact]
    public async Task InitiateRecovery_AfterCompletion_AllowsNewRequest()
    {
        var f = NewFixture(new RecoveryCoordinatorOptions
        {
            QuorumThreshold = 1,
            MaxTrustees = 1,
            GracePeriod = TimeSpan.FromHours(1),
        });

        var trustee = NewTrustee(f.Signer, 1);
        await f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, TrusteeDH);
        var (req1, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(req1);
        var attestation = TrusteeAttestation.Create(
            req1, trustee.NodeId, trustee.Pub, trustee.Priv, f.Clock.UtcNow(), f.Signer,
            TrusteeDH, SeedCT, SeedNonce);
        await f.Coordinator.SubmitAttestationAsync(attestation);
        f.Clock.Advance(TimeSpan.FromHours(2));
        await f.Coordinator.EvaluateGracePeriodAsync();

        var (req2, _, _) = BuildRequest(f.Signer, f.Clock);
        var evt = await f.Coordinator.InitiateRecoveryAsync(req2);
        Assert.Equal(RecoveryEventType.RecoveryInitiated, evt.Type);
        var status = await f.Coordinator.GetStatusAsync();
        Assert.Equal(RecoveryStatusKind.AwaitingAttestations, status.Kind);
    }

    [Fact]
    public async Task SubmitAttestation_FromUnknownTrustee_Dropped()
    {
        var f = NewFixture();
        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(request);

        var stranger = NewTrustee(f.Signer, 99);
        var attestation = TrusteeAttestation.Create(
            request, stranger.NodeId, stranger.Pub, stranger.Priv, f.Clock.UtcNow(), f.Signer,
            TrusteeDH, SeedCT, SeedNonce);

        var outcome = await f.Coordinator.SubmitAttestationAsync(attestation);

        Assert.False(outcome.Accepted);
        Assert.Empty(outcome.Events);
    }

    [Fact]
    public async Task DesignateTrustee_RejectsWrongLengthDHKey()
    {
        var f = NewFixture();
        var trustee = NewTrustee(f.Signer, 1);
        var tooShortDh = new byte[TrusteeDesignation.DHPublicKeyLength - 1];

        await Assert.ThrowsAsync<ArgumentException>(
            () => f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, tooShortDh));
    }

    [Fact]
    public async Task GetTrusteeEncryptedSeedAsync_ReturnsNullForUnknownTrustee()
    {
        var f = NewFixture();
        var result = await f.Coordinator.GetTrusteeEncryptedSeedAsync("never-designated-node");
        Assert.Null(result);
    }

    [Fact]
    public async Task RevokeTrustee_WipesPersistedSeedEnvelope()
    {
        // Council MAJOR-2: orphan seed envelopes after revocation
        // must NOT remain on disk — a future compromise of the revoked
        // trustee's DH key would otherwise decrypt the owner's root seed.
        var f = NewFixture();
        var trustee = NewTrustee(f.Signer, 1);
        await f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, TrusteeDH);
        await f.Coordinator.SetupTrusteeAsync(trustee.NodeId,
            new TrusteeEncryptedSeed(
                TrusteeNodeId:           trustee.NodeId,
                OwnerEphX25519PublicKey: new byte[32],
                Ciphertext:              SeedCT,
                Nonce:                   SeedNonce));
        Assert.NotNull(await f.Coordinator.GetTrusteeEncryptedSeedAsync(trustee.NodeId));

        await f.Coordinator.RevokeTrusteeAsync(trustee.NodeId);

        var afterRevoke = await f.Coordinator.GetTrusteeEncryptedSeedAsync(trustee.NodeId);
        Assert.Null(afterRevoke);
    }

    [Fact]
    public async Task SubmitAttestation_DropsWhenTrusteeDHKeyLengthMismatch()
    {
        // Council R-10: a TrusteeAttestation with a wrong-length DH key
        // must be silently dropped — same outcome as a key-value
        // mismatch. TrusteeAttestation's positional ctor doesn't
        // length-check, so we construct directly.
        var f = NewFixture(new RecoveryCoordinatorOptions
        {
            QuorumThreshold = 1, MaxTrustees = 1, GracePeriod = TimeSpan.FromDays(7),
        });
        var trustee = NewTrustee(f.Signer, 1);
        var dh = new byte[TrusteeDesignation.DHPublicKeyLength];
        Array.Fill(dh, (byte)0x55);
        await f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, dh);

        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(request);

        // Construct an attestation with a 33-byte DH key (length mismatch).
        var attHash = TrusteeAttestation.HashOf(request);
        var canonical = TrusteeAttestation.CanonicalBytesForSigning(
            trustee.NodeId, attHash, f.Clock.UtcNow(),
            new byte[33], SeedCT, SeedNonce);
        var attSig = f.Signer.Sign(canonical, trustee.Priv);
        var att = new TrusteeAttestation(
            TrusteeNodeId:                    trustee.NodeId,
            TrusteePublicKey:                 trustee.Pub,
            RecoveryRequestHash:              attHash,
            AttestedAt:                       f.Clock.UtcNow(),
            Signature:                        attSig,
            TrusteeDHPublicKey:               new byte[33],
            EncryptedSeedEnvelopeCiphertext:  SeedCT,
            EncryptedSeedEnvelopeNonce:       SeedNonce);

        var outcome = await f.Coordinator.SubmitAttestationAsync(att);

        Assert.False(outcome.Accepted);
        Assert.Empty(outcome.Events);
    }

    [Fact]
    public async Task DesignateTrustee_IsIdempotentOnSameKeys()
    {
        // Council R-5: re-running DesignateTrusteeAsync with the SAME
        // keys for the same NodeId must succeed (not throw) — allows
        // the owner to retry after a Phase 2 SetupTrusteeAsync failure.
        var f = NewFixture();
        var trustee = NewTrustee(f.Signer, 1);
        var dh = new byte[TrusteeDesignation.DHPublicKeyLength];
        Array.Fill(dh, (byte)0x77);

        var first = await f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, dh);
        var second = await f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, dh);

        Assert.Equal(RecoveryEventType.TrusteeDesignated, first.Type);
        Assert.Equal(RecoveryEventType.TrusteeDesignated, second.Type);
        // Second emit records the idempotent marker in detail.
        Assert.True(second.Detail.ContainsKey("trustee.designation.idempotent"));
    }

    [Fact]
    public async Task DesignateTrustee_RejectsExistingNodeIdWithDifferentKeys()
    {
        // Council R-5 — only same-keys are idempotent. Different keys
        // for an existing NodeId must throw, since it would otherwise
        // silently overwrite the designation (NodeId-collision attack).
        var f = NewFixture();
        var trustee = NewTrustee(f.Signer, 1);
        var dh1 = new byte[TrusteeDesignation.DHPublicKeyLength];
        Array.Fill(dh1, (byte)0x11);
        var dh2 = new byte[TrusteeDesignation.DHPublicKeyLength];
        Array.Fill(dh2, (byte)0x22);

        await f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, dh1);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, dh2));
    }

    [Fact]
    public async Task EvaluateGracePeriodAsync_WipesTrusteeEncryptedSeedsOnCompletion()
    {
        // Council R-4: after RecoveryCompleted fires, the persisted
        // trustee seed envelopes must be wiped to give forward-secrecy
        // against a future trustee-DH-key compromise.
        var f = NewFixture(new RecoveryCoordinatorOptions
        {
            QuorumThreshold = 1, MaxTrustees = 1, GracePeriod = TimeSpan.FromMinutes(1),
        });
        var trustee = NewTrustee(f.Signer, 1);
        // Designate with TrusteeDH (matches what MakeAttestation
        // constructs); the wipe-on-completion behaviour is what's
        // under test, not the DH-key-mismatch path.
        await f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, TrusteeDH);
        await f.Coordinator.SetupTrusteeAsync(trustee.NodeId,
            new TrusteeEncryptedSeed(trustee.NodeId, new byte[32], SeedCT, SeedNonce));
        Assert.NotNull(await f.Coordinator.GetTrusteeEncryptedSeedAsync(trustee.NodeId));

        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(request);
        await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustee));
        f.Clock.Advance(TimeSpan.FromMinutes(2));
        var result = await f.Coordinator.EvaluateGracePeriodAsync();

        Assert.NotNull(result);
        var afterCompletion = await f.Coordinator.GetTrusteeEncryptedSeedAsync(trustee.NodeId);
        Assert.Null(afterCompletion);
    }

    [Fact]
    public async Task SubmitAttestation_DropsWhenTrusteeDHKeyMismatch()
    {
        // W#67 PR 5 MAJOR-2 binding — designate the trustee with one
        // DH key, then attempt to submit an attestation carrying a
        // different DH key. Coordinator must drop the attestation
        // silently (no event emitted).
        var f = NewFixture(new RecoveryCoordinatorOptions
        {
            QuorumThreshold = 1,
            MaxTrustees = 1,
            GracePeriod = TimeSpan.FromDays(7),
        });
        var trustee = NewTrustee(f.Signer, 1);
        var designatedDh = new byte[TrusteeDesignation.DHPublicKeyLength];
        Array.Fill(designatedDh, (byte)0x11);
        await f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, designatedDh);

        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(request);

        // Attestation carries a DIFFERENT DH key — simulates an
        // attacker who learned the trustee's NodeId + Ed25519 pubkey
        // but uses an attacker-controlled DH key for the envelope.
        var attackerDh = new byte[TrusteeDesignation.DHPublicKeyLength];
        Array.Fill(attackerDh, (byte)0x22);
        var attestation = TrusteeAttestation.Create(
            request, trustee.NodeId, trustee.Pub, trustee.Priv, f.Clock.UtcNow(), f.Signer,
            attackerDh, SeedCT, SeedNonce);

        var outcome = await f.Coordinator.SubmitAttestationAsync(attestation);

        Assert.False(outcome.Accepted);
        Assert.Empty(outcome.Events);
    }

    [Fact]
    public async Task SubmitAttestation_DuplicateFromTrustee_Dropped()
    {
        var f = NewFixture();
        var trustee = NewTrustee(f.Signer, 1);
        await f.Coordinator.DesignateTrusteeAsync(trustee.NodeId, trustee.Pub, TrusteeDH);
        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(request);

        var att1 = TrusteeAttestation.Create(
            request, trustee.NodeId, trustee.Pub, trustee.Priv, f.Clock.UtcNow(), f.Signer,
            TrusteeDH, SeedCT, SeedNonce);
        var first = await f.Coordinator.SubmitAttestationAsync(att1);
        Assert.True(first.Accepted);

        var att2 = TrusteeAttestation.Create(
            request, trustee.NodeId, trustee.Pub, trustee.Priv, f.Clock.UtcNow().AddSeconds(1), f.Signer,
            TrusteeDH, SeedCT, SeedNonce);
        var second = await f.Coordinator.SubmitAttestationAsync(att2);
        Assert.False(second.Accepted);
    }

    [Fact]
    public async Task SubmitAttestation_AtQuorum_StartsGracePeriod()
    {
        var f = NewFixture(new RecoveryCoordinatorOptions
        {
            QuorumThreshold = 3,
            MaxTrustees = 5,
            GracePeriod = TimeSpan.FromDays(7),
        });

        var trustees = Enumerable.Range(1, 5).Select(i => NewTrustee(f.Signer, i)).ToList();
        foreach (var t in trustees)
        {
            await f.Coordinator.DesignateTrusteeAsync(t.NodeId, t.Pub, TrusteeDH);
        }

        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(request);

        var first = await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[0]));
        var second = await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[1]));
        var third = await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[2]));

        Assert.Single(first.Events);
        Assert.Single(second.Events);
        Assert.Equal(2, third.Events.Count);
        Assert.Equal(RecoveryEventType.AttestationReceived, third.Events[0].Type);
        Assert.Equal(RecoveryEventType.GracePeriodStarted, third.Events[1].Type);

        var status = await f.Coordinator.GetStatusAsync();
        Assert.Equal(RecoveryStatusKind.GracePeriodActive, status.Kind);
        Assert.Equal(3, status.AttestationsReceived);
        Assert.Equal(f.Clock.Now, status.GracePeriodStartedAt);
        Assert.Equal(f.Clock.Now + TimeSpan.FromDays(7), status.GracePeriodElapsesAt);
    }

    [Fact]
    public async Task SubmitAttestation_PostQuorum_AcceptedNoNewGraceEvent()
    {
        var f = NewFixture(new RecoveryCoordinatorOptions
        {
            QuorumThreshold = 3,
            MaxTrustees = 5,
            GracePeriod = TimeSpan.FromDays(7),
        });

        var trustees = Enumerable.Range(1, 5).Select(i => NewTrustee(f.Signer, i)).ToList();
        foreach (var t in trustees) await f.Coordinator.DesignateTrusteeAsync(t.NodeId, t.Pub, TrusteeDH);
        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(request);

        for (var i = 0; i < 3; i++)
        {
            await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[i]));
        }

        var fourth = await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[3]));

        Assert.True(fourth.Accepted);
        Assert.Single(fourth.Events);
        Assert.Equal(RecoveryEventType.AttestationReceived, fourth.Events[0].Type);
    }

    [Fact]
    public async Task Dispute_DuringGrace_AbortsRecovery()
    {
        var f = NewFixture(new RecoveryCoordinatorOptions { QuorumThreshold = 2, MaxTrustees = 3 });
        var trustees = Enumerable.Range(1, 3).Select(i => NewTrustee(f.Signer, i)).ToList();
        foreach (var t in trustees) await f.Coordinator.DesignateTrusteeAsync(t.NodeId, t.Pub, TrusteeDH);
        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(request);
        await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[0]));
        await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[1]));

        f.Clock.Advance(TimeSpan.FromMinutes(5));
        var dispute = RecoveryDispute.Create(
            request,
            disputingNodeId: "owner-laptop",
            disputingPublicKey: f.OwnerPublicKey,
            disputingPrivateKey: f.OwnerPrivateKey,
            disputedAt: f.Clock.UtcNow(),
            reason: "I still have my keys.",
            signer: f.Signer);

        var evt = await f.Coordinator.DisputeRecoveryAsync(dispute);

        Assert.Equal(RecoveryEventType.RecoveryDisputed, evt.Type);
        var status = await f.Coordinator.GetStatusAsync();
        Assert.Equal(RecoveryStatusKind.Disputed, status.Kind);
    }

    [Fact]
    public async Task Dispute_FromUnauthorizedKey_Throws()
    {
        var f = NewFixture(new RecoveryCoordinatorOptions { QuorumThreshold = 2, MaxTrustees = 3 });
        var trustees = Enumerable.Range(1, 3).Select(i => NewTrustee(f.Signer, i)).ToList();
        foreach (var t in trustees) await f.Coordinator.DesignateTrusteeAsync(t.NodeId, t.Pub, TrusteeDH);
        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(request);
        await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[0]));
        await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[1]));

        var (strangerPub, strangerPriv) = f.Signer.GenerateKeyPair();
        var dispute = RecoveryDispute.Create(
            request, "stranger-node", strangerPub, strangerPriv,
            f.Clock.UtcNow(), "objection", f.Signer);

        await Assert.ThrowsAsync<ArgumentException>(
            () => f.Coordinator.DisputeRecoveryAsync(dispute));
    }

    [Fact]
    public async Task Dispute_BeforeQuorum_Throws()
    {
        var f = NewFixture(new RecoveryCoordinatorOptions { QuorumThreshold = 3, MaxTrustees = 5 });
        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(request);

        var dispute = RecoveryDispute.Create(
            request, "owner", f.OwnerPublicKey, f.OwnerPrivateKey,
            f.Clock.UtcNow(), "early objection", f.Signer);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => f.Coordinator.DisputeRecoveryAsync(dispute));
    }

    [Fact]
    public async Task EvaluateGracePeriod_BeforeElapsed_NoEvent()
    {
        var f = NewFixture(new RecoveryCoordinatorOptions
        {
            QuorumThreshold = 2,
            MaxTrustees = 3,
            GracePeriod = TimeSpan.FromHours(1),
        });
        var trustees = Enumerable.Range(1, 3).Select(i => NewTrustee(f.Signer, i)).ToList();
        foreach (var t in trustees) await f.Coordinator.DesignateTrusteeAsync(t.NodeId, t.Pub, TrusteeDH);
        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(request);
        await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[0]));
        await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[1]));

        f.Clock.Advance(TimeSpan.FromMinutes(30));
        var evt = await f.Coordinator.EvaluateGracePeriodAsync();

        Assert.Null(evt);
        var status = await f.Coordinator.GetStatusAsync();
        Assert.Equal(RecoveryStatusKind.GracePeriodActive, status.Kind);
    }

    [Fact]
    public async Task EvaluateGracePeriod_AfterElapsed_EmitsCompleted()
    {
        var f = NewFixture(new RecoveryCoordinatorOptions
        {
            QuorumThreshold = 2,
            MaxTrustees = 3,
            GracePeriod = TimeSpan.FromHours(1),
        });
        var trustees = Enumerable.Range(1, 3).Select(i => NewTrustee(f.Signer, i)).ToList();
        foreach (var t in trustees) await f.Coordinator.DesignateTrusteeAsync(t.NodeId, t.Pub, TrusteeDH);
        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(request);
        await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[0]));
        await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[1]));

        f.Clock.Advance(TimeSpan.FromHours(2));
        var result = await f.Coordinator.EvaluateGracePeriodAsync();

        Assert.NotNull(result);
        Assert.Equal(RecoveryEventType.RecoveryCompleted, result!.Event.Type);
        Assert.Equal(2, result.Attestations.Count);
        var status = await f.Coordinator.GetStatusAsync();
        Assert.Equal(RecoveryStatusKind.Completed, status.Kind);
    }

    [Fact]
    public async Task EvaluateGracePeriod_AfterDispute_NoEvent()
    {
        var f = NewFixture(new RecoveryCoordinatorOptions
        {
            QuorumThreshold = 2,
            MaxTrustees = 3,
            GracePeriod = TimeSpan.FromHours(1),
        });
        var trustees = Enumerable.Range(1, 3).Select(i => NewTrustee(f.Signer, i)).ToList();
        foreach (var t in trustees) await f.Coordinator.DesignateTrusteeAsync(t.NodeId, t.Pub, TrusteeDH);
        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        await f.Coordinator.InitiateRecoveryAsync(request);
        await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[0]));
        await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[1]));

        var dispute = RecoveryDispute.Create(
            request, "owner", f.OwnerPublicKey, f.OwnerPrivateKey,
            f.Clock.UtcNow(), "objection", f.Signer);
        await f.Coordinator.DisputeRecoveryAsync(dispute);

        f.Clock.Advance(TimeSpan.FromHours(2));
        var evt = await f.Coordinator.EvaluateGracePeriodAsync();

        Assert.Null(evt);
        var status = await f.Coordinator.GetStatusAsync();
        Assert.Equal(RecoveryStatusKind.Disputed, status.Kind);
    }

    [Fact]
    public async Task Recovery_3of5Trustees_7DayGrace_Completion_Canonical()
    {
        // Per the Phase 1 plan §G6 acceptance criterion:
        //   "Recovery_3of5Trustees_GracePeriod_KeyReissue test simulates 5 trustees,
        //    original device offline, new device initiates request, 3 trustees
        //    approve, advance simulated clock 7 days, verify new device gets reissued
        //    key + audit log entry signed by all 3 attesting trustees + new device."
        // This test covers the orchestration deliverable; the host wires the actual
        // SQLCipher rekey to the RecoveryCompleted event in a follow-up.
        var f = NewFixture(); // defaults: 3-of-5, 7-day grace.

        var trustees = Enumerable.Range(1, 5).Select(i => NewTrustee(f.Signer, i)).ToList();
        var designations = new List<RecoveryEvent>();
        foreach (var t in trustees)
        {
            designations.Add(await f.Coordinator.DesignateTrusteeAsync(t.NodeId, t.Pub, TrusteeDH));
        }
        Assert.Equal(5, designations.Count);

        var (request, _, _) = BuildRequest(f.Signer, f.Clock);
        var initiated = await f.Coordinator.InitiateRecoveryAsync(request);
        Assert.Equal(RecoveryEventType.RecoveryInitiated, initiated.Type);

        var att1 = await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[0]));
        var att2 = await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[1]));
        var att3 = await f.Coordinator.SubmitAttestationAsync(MakeAttestation(f, request, trustees[2]));

        Assert.True(att1.Accepted && att2.Accepted && att3.Accepted);
        Assert.Equal(RecoveryEventType.GracePeriodStarted, att3.Events[1].Type);

        // Advance the clock past the 7-day grace window.
        f.Clock.Advance(TimeSpan.FromDays(7) + TimeSpan.FromMinutes(1));
        var completed = await f.Coordinator.EvaluateGracePeriodAsync();

        Assert.NotNull(completed);
        Assert.Equal(RecoveryEventType.RecoveryCompleted, completed!.Event.Type);

        var status = await f.Coordinator.GetStatusAsync();
        Assert.Equal(RecoveryStatusKind.Completed, status.Kind);
        Assert.Equal(3, status.AttestationsReceived);

        // Audit chain: every event after the first carries a non-null PreviousEventHash.
        var allEvents = new List<RecoveryEvent>();
        allEvents.AddRange(designations);
        allEvents.Add(initiated);
        allEvents.AddRange(att1.Events);
        allEvents.AddRange(att2.Events);
        allEvents.AddRange(att3.Events);
        allEvents.Add(completed.Event);
        for (var i = 1; i < allEvents.Count; i++)
        {
            Assert.NotNull(allEvents[i].PreviousEventHash);
            var expected = RecoveryCoordinator.ChainHashOf(allEvents[i - 1]);
            Assert.Equal(expected, allEvents[i].PreviousEventHash!);
        }
    }

    [Fact]
    public async Task Persistence_RoundTrip_AcrossCoordinatorInstances()
    {
        // Simulate process restart: write state via coordinator A, instantiate
        // coordinator B against the same store, verify B sees the in-flight state
        // and can finalize the grace window. Mirrors the Phase 1 plan's
        // "7-day grace must survive device restart" requirement.
        var clock = new TestClock();
        var signer = new Ed25519Signer();
        var store = new InMemoryRecoveryStateStore();
        var (ownerPub, ownerPriv) = signer.GenerateKeyPair();
        var disputers = new FixedDisputerValidator(new[] { ownerPub });
        var options = new RecoveryCoordinatorOptions
        {
            QuorumThreshold = 2,
            MaxTrustees = 3,
            GracePeriod = TimeSpan.FromHours(1),
        };

        var coordinatorA = new RecoveryCoordinator(clock, store, signer, disputers, options);

        var trustees = Enumerable.Range(1, 3).Select(i => NewTrustee(signer, i)).ToList();
        foreach (var t in trustees) await coordinatorA.DesignateTrusteeAsync(t.NodeId, t.Pub, TrusteeDH);
        var (request, _, _) = BuildRequest(signer, clock);
        await coordinatorA.InitiateRecoveryAsync(request);

        var att1 = TrusteeAttestation.Create(request, trustees[0].NodeId, trustees[0].Pub, trustees[0].Priv, clock.UtcNow(), signer, TrusteeDH, SeedCT, SeedNonce);
        var att2 = TrusteeAttestation.Create(request, trustees[1].NodeId, trustees[1].Pub, trustees[1].Priv, clock.UtcNow(), signer, TrusteeDH, SeedCT, SeedNonce);
        await coordinatorA.SubmitAttestationAsync(att1);
        await coordinatorA.SubmitAttestationAsync(att2);

        // "Restart" — drop coordinator A, instantiate coordinator B against the same store.
        var coordinatorB = new RecoveryCoordinator(clock, store, signer, disputers, options);
        var statusB = await coordinatorB.GetStatusAsync();
        Assert.Equal(RecoveryStatusKind.GracePeriodActive, statusB.Kind);
        Assert.Equal(2, statusB.AttestationsReceived);

        clock.Advance(TimeSpan.FromHours(2));
        var completed = await coordinatorB.EvaluateGracePeriodAsync();
        Assert.NotNull(completed);
        Assert.Equal(RecoveryEventType.RecoveryCompleted, completed!.Event.Type);
    }

    [Fact]
    public void ChainHashOf_IsDeterministic_AndDetectsTamper()
    {
        var detail = new Dictionary<string, string> { ["k"] = "v" };
        var evt = new RecoveryEvent(
            Type: RecoveryEventType.RecoveryInitiated,
            ActorNodeId: "node-a",
            TargetNodeId: "node-a",
            OccurredAt: new DateTimeOffset(2026, 5, 1, 12, 0, 0, TimeSpan.Zero),
            PreviousEventHash: null,
            Detail: detail);

        var hash1 = RecoveryCoordinator.ChainHashOf(evt);
        var hash2 = RecoveryCoordinator.ChainHashOf(evt);
        Assert.Equal(hash1, hash2);

        var tampered = evt with { ActorNodeId = "node-b" };
        Assert.NotEqual(hash1, RecoveryCoordinator.ChainHashOf(tampered));
    }

    private static TrusteeAttestation MakeAttestation(Fixture f, RecoveryRequest request, (string NodeId, byte[] Pub, byte[] Priv) trustee)
    {
        return TrusteeAttestation.Create(
            request, trustee.NodeId, trustee.Pub, trustee.Priv, f.Clock.UtcNow(), f.Signer,
            TrusteeDH, SeedCT, SeedNonce);
    }
}
