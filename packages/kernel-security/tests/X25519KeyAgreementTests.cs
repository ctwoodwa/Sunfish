using System.Text;
using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Kernel.Security.Tests;

public sealed class X25519KeyAgreementTests
{
    private readonly X25519KeyAgreement _kem = new();

    [Fact]
    public void GenerateKeyPair_produces_expected_lengths()
    {
        var (pub, priv) = _kem.GenerateKeyPair();
        Assert.Equal(32, pub.Length);
        Assert.Equal(32, priv.Length);
    }

    [Fact]
    public void Box_and_OpenBox_roundtrip()
    {
        var (senderPub, senderPriv) = _kem.GenerateKeyPair();
        var (recipPub, recipPriv) = _kem.GenerateKeyPair();
        var plaintext = Encoding.UTF8.GetBytes("a role key would go here");

        var (ct, nonce) = _kem.Box(plaintext, recipPub, senderPriv);
        Assert.Equal(24, nonce.Length);
        Assert.Equal(plaintext.Length + 16, ct.Length);

        var recovered = _kem.OpenBox(ct, nonce, senderPub, recipPriv);
        Assert.NotNull(recovered);
        Assert.Equal(plaintext, recovered);
    }

    [Fact]
    public void OpenBox_rejects_wrong_recipient_private_key()
    {
        var (senderPub, senderPriv) = _kem.GenerateKeyPair();
        var (recipPub, _) = _kem.GenerateKeyPair();
        var (_, wrongPriv) = _kem.GenerateKeyPair();
        var plaintext = Encoding.UTF8.GetBytes("secret");

        var (ct, nonce) = _kem.Box(plaintext, recipPub, senderPriv);
        var result = _kem.OpenBox(ct, nonce, senderPub, wrongPriv);

        Assert.Null(result);
    }

    [Fact]
    public void OpenBox_rejects_tampered_ciphertext()
    {
        var (senderPub, senderPriv) = _kem.GenerateKeyPair();
        var (recipPub, recipPriv) = _kem.GenerateKeyPair();
        var plaintext = Encoding.UTF8.GetBytes("secret");

        var (ct, nonce) = _kem.Box(plaintext, recipPub, senderPriv);
        ct[0] ^= 0x01; // flip a bit

        var result = _kem.OpenBox(ct, nonce, senderPub, recipPriv);
        Assert.Null(result);
    }

    [Fact]
    public void Box_of_same_plaintext_twice_produces_different_ciphertexts()
    {
        var (_, senderPriv) = _kem.GenerateKeyPair();
        var (recipPub, _) = _kem.GenerateKeyPair();
        var plaintext = Encoding.UTF8.GetBytes("identical plaintext");

        var (ct1, nonce1) = _kem.Box(plaintext, recipPub, senderPriv);
        var (ct2, nonce2) = _kem.Box(plaintext, recipPub, senderPriv);

        Assert.NotEqual(nonce1, nonce2);
        Assert.NotEqual(ct1, ct2);
    }

    [Fact]
    public void OpenBox_throws_on_wrong_nonce_length()
    {
        var (senderPub, _) = _kem.GenerateKeyPair();
        var (_, recipPriv) = _kem.GenerateKeyPair();

        var ct = new byte[48];
        var badNonce = new byte[12];

        Assert.Throws<ArgumentException>(() => _kem.OpenBox(ct, badNonce, senderPub, recipPriv));
    }
}
