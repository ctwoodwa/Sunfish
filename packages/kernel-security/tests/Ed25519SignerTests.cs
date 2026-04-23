using System.Text;
using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Kernel.Security.Tests;

public sealed class Ed25519SignerTests
{
    private readonly Ed25519Signer _signer = new();

    [Fact]
    public void GenerateKeyPair_produces_expected_lengths()
    {
        var (pub, priv) = _signer.GenerateKeyPair();
        Assert.Equal(32, pub.Length);
        Assert.Equal(32, priv.Length);
    }

    [Fact]
    public void Sign_and_Verify_roundtrip()
    {
        var (pub, priv) = _signer.GenerateKeyPair();
        var msg = Encoding.UTF8.GetBytes("sunfish role attestation test");

        var sig = _signer.Sign(msg, priv);
        Assert.Equal(64, sig.Length);
        Assert.True(_signer.Verify(msg, sig, pub));
    }

    [Fact]
    public void Verify_rejects_tampered_message()
    {
        var (pub, priv) = _signer.GenerateKeyPair();
        var msg = Encoding.UTF8.GetBytes("original");
        var sig = _signer.Sign(msg, priv);

        var tampered = Encoding.UTF8.GetBytes("ORIGINAL");
        Assert.False(_signer.Verify(tampered, sig, pub));
    }

    [Fact]
    public void Verify_rejects_wrong_public_key()
    {
        var (_, priv) = _signer.GenerateKeyPair();
        var (otherPub, _) = _signer.GenerateKeyPair();
        var msg = Encoding.UTF8.GetBytes("hello");
        var sig = _signer.Sign(msg, priv);

        Assert.False(_signer.Verify(msg, sig, otherPub));
    }

    [Fact]
    public void Verify_rejects_wrong_signature_length()
    {
        var (pub, _) = _signer.GenerateKeyPair();
        var msg = Encoding.UTF8.GetBytes("hello");
        var badSig = new byte[63];

        Assert.False(_signer.Verify(msg, badSig, pub));
    }

    [Fact]
    public void Ed25519_signatures_are_deterministic()
    {
        // Ed25519 is spec'd to be deterministic: same key + same message ⇒ same signature.
        var (_, priv) = _signer.GenerateKeyPair();
        var msg = Encoding.UTF8.GetBytes("same message");

        var sig1 = _signer.Sign(msg, priv);
        var sig2 = _signer.Sign(msg, priv);

        Assert.Equal(sig1, sig2);
    }

    [Fact]
    public void Sign_rejects_wrong_private_key_length()
    {
        var msg = Encoding.UTF8.GetBytes("hello");
        var badPriv = new byte[16];

        Assert.Throws<ArgumentException>(() => _signer.Sign(msg, badPriv));
    }
}
