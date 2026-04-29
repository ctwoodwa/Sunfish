using Sunfish.Kernel.Security.Crypto;
using Sunfish.Foundation.Recovery;

namespace Sunfish.Foundation.Recovery.Tests;

/// <summary>
/// Coverage for the <see cref="RecoveryDispute"/> signed-message
/// envelope. Mirrors <see cref="TrusteeRecordTests"/> with the dispute-
/// specific tampering vectors.
/// </summary>
public sealed class RecoveryDisputeTests
{
    private static RecoveryRequest BuildRequest(
        Ed25519Signer signer,
        out byte[] devicePub,
        out byte[] devicePriv)
    {
        (devicePub, devicePriv) = signer.GenerateKeyPair();
        return RecoveryRequest.Create(
            requestingNodeId: "new-device-node",
            ephemeralPublicKey: devicePub,
            ephemeralPrivateKey: devicePriv,
            requestedAt: DateTimeOffset.UtcNow,
            signer: signer);
    }

    [Fact]
    public void Dispute_signed_then_verified_round_trips()
    {
        var signer = new Ed25519Signer();
        var request = BuildRequest(signer, out _, out _);
        var (ownerPub, ownerPriv) = signer.GenerateKeyPair();

        var dispute = RecoveryDispute.Create(
            request,
            disputingNodeId: "owner-laptop",
            disputingPublicKey: ownerPub,
            disputingPrivateKey: ownerPriv,
            disputedAt: DateTimeOffset.UtcNow.AddSeconds(30),
            reason: "I still have my keys; this recovery is unauthorized.",
            signer: signer);

        Assert.True(dispute.Verify(request, signer));
        Assert.Equal(RecoveryRequest.SignatureLength, dispute.Signature.Length);
    }

    [Fact]
    public void Dispute_Verify_fails_when_replayed_against_different_request()
    {
        var signer = new Ed25519Signer();
        var requestA = BuildRequest(signer, out _, out _);
        var requestB = BuildRequest(signer, out _, out _);
        var (ownerPub, ownerPriv) = signer.GenerateKeyPair();

        var disputeForA = RecoveryDispute.Create(
            requestA, "owner-laptop", ownerPub, ownerPriv,
            DateTimeOffset.UtcNow, "objection", signer);

        Assert.True(disputeForA.Verify(requestA, signer));
        Assert.False(disputeForA.Verify(requestB, signer));
    }

    [Fact]
    public void Dispute_Verify_fails_on_tampered_reason()
    {
        var signer = new Ed25519Signer();
        var request = BuildRequest(signer, out _, out _);
        var (ownerPub, ownerPriv) = signer.GenerateKeyPair();

        var legit = RecoveryDispute.Create(
            request, "owner-laptop", ownerPub, ownerPriv,
            DateTimeOffset.UtcNow, "original reason", signer);

        var tampered = legit with { Reason = "fabricated reason" };

        Assert.False(tampered.Verify(request, signer));
    }

    [Fact]
    public void Dispute_Verify_fails_on_tampered_timestamp()
    {
        var signer = new Ed25519Signer();
        var request = BuildRequest(signer, out _, out _);
        var (ownerPub, ownerPriv) = signer.GenerateKeyPair();

        var legit = RecoveryDispute.Create(
            request, "owner-laptop", ownerPub, ownerPriv,
            DateTimeOffset.UtcNow, "objection", signer);

        var tampered = legit with { DisputedAt = legit.DisputedAt.AddDays(1) };

        Assert.False(tampered.Verify(request, signer));
    }

    [Fact]
    public void Dispute_Verify_fails_on_corrupted_signature()
    {
        var signer = new Ed25519Signer();
        var request = BuildRequest(signer, out _, out _);
        var (ownerPub, ownerPriv) = signer.GenerateKeyPair();

        var legit = RecoveryDispute.Create(
            request, "owner-laptop", ownerPub, ownerPriv,
            DateTimeOffset.UtcNow, "objection", signer);

        var corrupted = (byte[])legit.Signature.Clone();
        corrupted[0] ^= 0xFF;
        var tampered = legit with { Signature = corrupted };

        Assert.False(tampered.Verify(request, signer));
    }
}
