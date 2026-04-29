using Sunfish.Kernel.Security.Crypto;
using Sunfish.Foundation.Recovery;

namespace Sunfish.Foundation.Recovery.Tests;

/// <summary>
/// Phase 1 G6 sub-pattern #48a (multi-sig social) — coverage for the
/// signed message envelopes that flow between a recovering device and
/// its trustees. Verifies signature round-trips, replay protection
/// via the request-hash binding, and tampering detection.
/// </summary>
public sealed class TrusteeRecordTests
{
    [Fact]
    public void RecoveryRequest_signed_then_verified_round_trips()
    {
        var signer = new Ed25519Signer();
        var (pub, priv) = signer.GenerateKeyPair();

        var request = RecoveryRequest.Create(
            requestingNodeId: "node-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            ephemeralPublicKey: pub,
            ephemeralPrivateKey: priv,
            requestedAt: DateTimeOffset.UtcNow,
            signer: signer);

        Assert.True(request.VerifySignature(signer));
        Assert.Equal(RecoveryRequest.SignatureLength, request.Signature.Length);
        Assert.Equal(RecoveryRequest.EphemeralPublicKeyLength, request.EphemeralPublicKey.Length);
    }

    [Fact]
    public void RecoveryRequest_VerifySignature_fails_on_tampered_NodeId()
    {
        var signer = new Ed25519Signer();
        var (pub, priv) = signer.GenerateKeyPair();
        var legit = RecoveryRequest.Create(
            "node-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            pub, priv, DateTimeOffset.UtcNow, signer);

        // Forge a request that claims a different NodeId but reuses the
        // legit request's signature.
        var tampered = legit with { RequestingNodeId = "node-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb" };

        Assert.False(tampered.VerifySignature(signer));
    }

    [Fact]
    public void RecoveryRequest_VerifySignature_fails_on_tampered_timestamp()
    {
        var signer = new Ed25519Signer();
        var (pub, priv) = signer.GenerateKeyPair();
        var legit = RecoveryRequest.Create(
            "node-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            pub, priv, DateTimeOffset.UtcNow, signer);

        var tampered = legit with { RequestedAt = legit.RequestedAt.AddDays(7) };

        Assert.False(tampered.VerifySignature(signer));
    }

    [Fact]
    public void RecoveryRequest_VerifySignature_fails_on_corrupted_signature()
    {
        var signer = new Ed25519Signer();
        var (pub, priv) = signer.GenerateKeyPair();
        var legit = RecoveryRequest.Create(
            "node-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            pub, priv, DateTimeOffset.UtcNow, signer);

        var corruptedSig = (byte[])legit.Signature.Clone();
        corruptedSig[0] ^= 0xFF;
        var tampered = legit with { Signature = corruptedSig };

        Assert.False(tampered.VerifySignature(signer));
    }

    [Fact]
    public void TrusteeAttestation_signed_then_verified_round_trips()
    {
        var signer = new Ed25519Signer();
        var (devicePub, devicePriv) = signer.GenerateKeyPair();
        var request = RecoveryRequest.Create(
            "new-device-node",
            devicePub, devicePriv, DateTimeOffset.UtcNow, signer);

        var (trusteePub, trusteePriv) = signer.GenerateKeyPair();
        var attestation = TrusteeAttestation.Create(
            request,
            trusteeNodeId: "trustee-1-node",
            trusteePublicKey: trusteePub,
            trusteePrivateKey: trusteePriv,
            attestedAt: DateTimeOffset.UtcNow.AddSeconds(30),
            signer: signer);

        Assert.True(attestation.Verify(request, signer));
        Assert.Equal(TrusteeAttestation.RequestHashLength, attestation.RecoveryRequestHash.Length);
    }

    [Fact]
    public void TrusteeAttestation_Verify_fails_when_replayed_against_different_request()
    {
        // Trustee attests to request A; attacker tries to replay the attestation
        // against a different request B. The hash binding catches it.
        var signer = new Ed25519Signer();
        var (devicePubA, devicePrivA) = signer.GenerateKeyPair();
        var (devicePubB, devicePrivB) = signer.GenerateKeyPair();
        var requestA = RecoveryRequest.Create(
            "device-a", devicePubA, devicePrivA, DateTimeOffset.UtcNow, signer);
        var requestB = RecoveryRequest.Create(
            "device-b", devicePubB, devicePrivB, DateTimeOffset.UtcNow, signer);

        var (trusteePub, trusteePriv) = signer.GenerateKeyPair();
        var attestationForA = TrusteeAttestation.Create(
            requestA, "trustee-1-node", trusteePub, trusteePriv,
            DateTimeOffset.UtcNow, signer);

        // Same attestation bytes, different request reference at verify time.
        Assert.True(attestationForA.Verify(requestA, signer));
        Assert.False(attestationForA.Verify(requestB, signer));
    }

    [Fact]
    public void TrusteeAttestation_Verify_fails_on_corrupted_signature()
    {
        var signer = new Ed25519Signer();
        var (devicePub, devicePriv) = signer.GenerateKeyPair();
        var request = RecoveryRequest.Create(
            "new-device-node",
            devicePub, devicePriv, DateTimeOffset.UtcNow, signer);

        var (trusteePub, trusteePriv) = signer.GenerateKeyPair();
        var attestation = TrusteeAttestation.Create(
            request, "trustee-1", trusteePub, trusteePriv,
            DateTimeOffset.UtcNow, signer);

        var corrupted = (byte[])attestation.Signature.Clone();
        corrupted[^1] ^= 0xFF;
        var tampered = attestation with { Signature = corrupted };

        Assert.False(tampered.Verify(request, signer));
    }

    [Fact]
    public void TrusteeAttestation_HashOf_is_deterministic_for_same_request()
    {
        var signer = new Ed25519Signer();
        var (pub, priv) = signer.GenerateKeyPair();
        var ts = DateTimeOffset.UtcNow;
        var requestA = RecoveryRequest.Create("device-a", pub, priv, ts, signer);
        var requestB = RecoveryRequest.Create("device-a", pub, priv, ts, signer);

        // Same NodeId + pubkey + timestamp -> same canonical bytes -> same hash.
        var hashA = TrusteeAttestation.HashOf(requestA);
        var hashB = TrusteeAttestation.HashOf(requestB);

        Assert.Equal(hashA, hashB);
        Assert.Equal(TrusteeAttestation.RequestHashLength, hashA.Length);
    }

    [Fact]
    public void RecoveryEvent_record_carries_chain_pointer_and_detail_metadata()
    {
        // Smoke test the record contract — orchestrator reads/writes these
        // so the structural fields need to round-trip cleanly through pattern
        // matching and `with` expressions.
        var detail = new Dictionary<string, string>
        {
            ["trustee.nodeId"] = "trustee-1-node",
            ["attestation.signature.hex"] = "abcd1234",
        };
        var prevHash = new byte[32];

        var evt = new RecoveryEvent(
            Type: RecoveryEventType.AttestationReceived,
            ActorNodeId: "trustee-1-node",
            TargetNodeId: "recovering-device-node",
            OccurredAt: DateTimeOffset.UtcNow,
            PreviousEventHash: prevHash,
            Detail: detail);

        Assert.Equal(RecoveryEventType.AttestationReceived, evt.Type);
        Assert.Equal("trustee-1-node", evt.ActorNodeId);
        Assert.Equal(2, evt.Detail.Count);
        Assert.Equal(prevHash, evt.PreviousEventHash);

        // `with` produces a new value preserving the rest.
        var completed = evt with { Type = RecoveryEventType.RecoveryCompleted };
        Assert.Equal(RecoveryEventType.RecoveryCompleted, completed.Type);
        Assert.Equal(evt.ActorNodeId, completed.ActorNodeId);
    }
}
