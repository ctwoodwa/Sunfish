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

    [Theory]
    // W#67 PR 5 council R-8 — RFC 7748 §6 canonical low-order points.
    // X25519 must reject these via contributory-check so any
    // implementation that silently returns all-zeros on shared-secret
    // derivation breaks loudly here. Vectors from RFC 7748 §6.1
    // (Curve25519 low-order subgroup order 1, 2, 4, 8 points).
    [InlineData(new byte[] {
        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00 })]
    [InlineData(new byte[] {
        0x01,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,
        0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00 })]
    [InlineData(new byte[] {
        0xe0,0xeb,0x7a,0x7c,0x3b,0x41,0xb8,0xae,0x16,0x56,0xe3,0xfa,0xf1,0x9f,0xc4,0x6a,
        0xda,0x09,0x8d,0xeb,0x9c,0x32,0xb1,0xfd,0x86,0x62,0x05,0x16,0x5f,0x49,0xb8,0x00 })]
    public void OpenBox_RejectsLowOrderPeerPublicKey(byte[] lowOrderPeerKey)
    {
        // Construct a real ciphertext via Box() then call OpenBox with
        // the low-order key as senderPublicKey. NSec / .NET BCL
        // X25519 implementations reject low-order points by throwing
        // CryptographicException at scalar-mult time (RFC 7748
        // contributory check), NOT returning a zero shared secret.
        // This pin guards against a future library swap that breaks
        // that contract.
        var (pub, priv) = _kem.GenerateKeyPair();
        var (ct, nonce) = _kem.Box(new byte[] { 1, 2, 3, 4 }, pub, priv);

        Assert.Throws<System.Security.Cryptography.CryptographicException>(
            () => _kem.OpenBox(ct, nonce, lowOrderPeerKey, priv));
    }
}
