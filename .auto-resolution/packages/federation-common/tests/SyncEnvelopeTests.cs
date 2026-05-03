using Sunfish.Federation.Common;
using Sunfish.Foundation.Crypto;
using Xunit;

namespace Sunfish.Federation.Common.Tests;

public class SyncEnvelopeTests
{
    [Fact]
    public void SignAndCreate_ProducesValidEnvelope()
    {
        using var keyPair = KeyPair.Generate();
        var signer = new Ed25519Signer(keyPair);
        using var toKeyPair = KeyPair.Generate();
        var toPeer = PeerId.From(toKeyPair.PrincipalId);
        var payload = new byte[100];
        Random.Shared.NextBytes(payload);

        var env = SyncEnvelope.SignAndCreate(signer, toPeer, SyncMessageKind.EntityHeadsAnnouncement, payload);

        Assert.Equal(Signature.LengthInBytes, env.Signature.AsSpan().Length);
        Assert.Equal(keyPair.PrincipalId.ToBase64Url(), env.FromPeer.Value);
        Assert.Equal(100, env.Payload.Length);
        Assert.Equal(toPeer, env.ToPeer);
        Assert.Equal(SyncMessageKind.EntityHeadsAnnouncement, env.Kind);
    }

    [Fact]
    public void Verify_ReturnsTrue_ForUntamperedEnvelope()
    {
        using var keyPair = KeyPair.Generate();
        var signer = new Ed25519Signer(keyPair);
        var verifier = new Ed25519Verifier();
        using var toKeyPair = KeyPair.Generate();
        var toPeer = PeerId.From(toKeyPair.PrincipalId);
        var payload = new byte[] { 1, 2, 3, 4, 5 };

        var env = SyncEnvelope.SignAndCreate(signer, toPeer, SyncMessageKind.HealthProbe, payload);

        Assert.True(env.Verify(verifier, keyPair.PrincipalId));
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenPayloadTampered()
    {
        using var keyPair = KeyPair.Generate();
        var signer = new Ed25519Signer(keyPair);
        var verifier = new Ed25519Verifier();
        using var toKeyPair = KeyPair.Generate();
        var toPeer = PeerId.From(toKeyPair.PrincipalId);
        var payload = new byte[] { 1, 2, 3, 4, 5 };

        var env = SyncEnvelope.SignAndCreate(signer, toPeer, SyncMessageKind.HealthProbe, payload);

        // Tamper with the payload but keep the original signature.
        var tampered = env with { Payload = new byte[] { 9, 9, 9, 9, 9 } };

        Assert.False(tampered.Verify(verifier, keyPair.PrincipalId));
    }

    [Fact]
    public void Verify_ReturnsFalse_WhenWrongExpectedSigner()
    {
        using var keyA = KeyPair.Generate();
        using var keyB = KeyPair.Generate();
        var signer = new Ed25519Signer(keyA);
        var verifier = new Ed25519Verifier();
        using var toKeyPair = KeyPair.Generate();
        var toPeer = PeerId.From(toKeyPair.PrincipalId);
        var payload = new byte[] { 42 };

        var env = SyncEnvelope.SignAndCreate(signer, toPeer, SyncMessageKind.HealthProbe, payload);

        Assert.False(env.Verify(verifier, keyB.PrincipalId));
    }
}
