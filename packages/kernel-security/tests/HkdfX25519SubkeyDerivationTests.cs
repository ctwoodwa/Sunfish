using System.Security.Cryptography;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Kernel.Security.Tests;

/// <summary>
/// W#67 / ADR 0046-A6 — <see cref="HkdfX25519SubkeyDerivation"/> contract tests.
/// Verifies determinism, per-team isolation, domain separation from the
/// Ed25519 + SQLCipher derivations, and that the derived keys round-trip
/// through <see cref="IX25519KeyAgreement"/>.
/// </summary>
public sealed class HkdfX25519SubkeyDerivationTests
{
    private readonly HkdfX25519SubkeyDerivation _sut = new();

    [Fact]
    public void DeriveX25519PrivateKey_is_deterministic_for_same_seed_and_team()
    {
        var seed = RandomSeed();
        const string team = "team-abc";

        var a = _sut.DeriveX25519PrivateKey(seed, team);
        var b = _sut.DeriveX25519PrivateKey(seed, team);

        Assert.Equal(HkdfX25519SubkeyDerivation.KeyLength, a.Length);
        Assert.Equal(a, b);
    }

    [Fact]
    public void DeriveX25519PrivateKey_differs_across_team_ids()
    {
        var seed = RandomSeed();
        var keyA = _sut.DeriveX25519PrivateKey(seed, "team-A");
        var keyB = _sut.DeriveX25519PrivateKey(seed, "team-B");

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void DeriveX25519PrivateKey_domain_separated_from_Ed25519_subkey()
    {
        // Same root, same team — but Ed25519 and X25519 derivations use
        // distinct HKDF info prefixes, so the bytes must differ. Without
        // domain separation, key-reuse attacks across protocols would be
        // possible (paper §11.3 + ADR 0046-A6 §security analysis).
        var seed = RandomSeed();
        const string team = "team-domain-sep";

        var x25519Priv = _sut.DeriveX25519PrivateKey(seed, team);

        // Ed25519 subkey: TeamSubkeyDerivation uses ROOT_PRIVATE_KEY (32B
        // Ed25519 seed), not arbitrary seed bytes. We re-run the same HKDF
        // shape with the Ed25519 prefix to verify the prefix really is
        // what produces different output.
        var ed25519PrefixHkdf = HkdfWith(
            seed.Span,
            TeamSubkeyDerivation.InfoPrefix + team,
            HkdfX25519SubkeyDerivation.KeyLength);

        Assert.NotEqual(x25519Priv, ed25519PrefixHkdf);
        Assert.NotEqual(
            HkdfX25519SubkeyDerivation.InfoPrefix,
            TeamSubkeyDerivation.InfoPrefix);
    }

    [Fact]
    public void DeriveX25519PublicKey_matches_NSec_scalar_mult()
    {
        // Derive the public key two ways and assert they match: (a) via the
        // SUT's DeriveX25519PublicKey, (b) via NSec directly off the SUT's
        // private-key bytes. This pins the SUT's "private → public" path to
        // NSec's canonical scalar mult and catches accidental clamping
        // changes.
        var seed = RandomSeed();
        const string team = "team-pub";

        var pubFromSut    = _sut.DeriveX25519PublicKey(seed, team);
        var privFromSut   = _sut.DeriveX25519PrivateKey(seed, team);
        using var nsecKey = NSec.Cryptography.Key.Import(
            NSec.Cryptography.KeyAgreementAlgorithm.X25519,
            privFromSut,
            NSec.Cryptography.KeyBlobFormat.RawPrivateKey,
            new NSec.Cryptography.KeyCreationParameters
            {
                ExportPolicy = NSec.Cryptography.KeyExportPolicies.AllowPlaintextExport,
            });
        var pubFromNsec = nsecKey.Export(NSec.Cryptography.KeyBlobFormat.RawPublicKey);

        Assert.Equal(32, pubFromSut.Length);
        Assert.Equal(pubFromNsec, pubFromSut);
    }

    [Fact]
    public void Derived_keys_round_trip_through_IX25519KeyAgreement()
    {
        // Integration check: derive a sender keypair + a recipient keypair
        // for two distinct teams from the same root seed, then verify that
        // Box / OpenBox round-trip with the derived keys behaves the same
        // as with NSec-generated keys (since they ARE NSec-generated, just
        // seeded from HKDF rather than from RandomNumberGenerator).
        var seed = RandomSeed();
        var senderPriv    = _sut.DeriveX25519PrivateKey(seed, "team-sender");
        var senderPub     = _sut.DeriveX25519PublicKey(seed, "team-sender");
        var recipientPriv = _sut.DeriveX25519PrivateKey(seed, "team-recipient");
        var recipientPub  = _sut.DeriveX25519PublicKey(seed, "team-recipient");

        var agreement = new X25519KeyAgreement();
        var plaintext = System.Text.Encoding.UTF8.GetBytes("recovery seed envelope contents");
        var (ciphertext, nonce) = agreement.Box(plaintext, recipientPub, senderPriv);

        var decrypted = agreement.OpenBox(ciphertext, nonce, senderPub, recipientPriv);
        Assert.NotNull(decrypted);
        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void DeriveX25519PrivateKey_rejects_empty_team_id()
    {
        var seed = RandomSeed();
        Assert.Throws<ArgumentException>(() => _sut.DeriveX25519PrivateKey(seed, string.Empty));
    }

    [Fact]
    public void DeriveX25519PrivateKey_rejects_empty_root_seed()
    {
        Assert.Throws<ArgumentException>(
            () => _sut.DeriveX25519PrivateKey(ReadOnlyMemory<byte>.Empty, "team-x"));
    }

    private static ReadOnlyMemory<byte> RandomSeed()
        => RandomNumberGenerator.GetBytes(KeystoreRootSeedProvider.SeedLength);

    private static byte[] HkdfWith(ReadOnlySpan<byte> ikm, string infoString, int length)
    {
        var info = System.Text.Encoding.UTF8.GetBytes(infoString);
        var output = new byte[length];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, output, ReadOnlySpan<byte>.Empty, info);
        return output;
    }
}
