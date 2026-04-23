namespace Sunfish.Kernel.Security.Crypto;

/// <summary>
/// Ed25519 detached-signature primitive. Thin adapter over <c>NSec.Cryptography</c>
/// so the rest of the kernel-security package can sign/verify without touching
/// NSec directly.
/// </summary>
/// <remarks>
/// The related package <c>Sunfish.Foundation.Crypto</c> exposes a higher-level
/// <c>IOperationSigner</c> that binds a signer to a specific principal via the
/// NSec <c>KeyPair</c> abstraction. This interface is deliberately lower-level:
/// callers pass raw key material and receive raw signatures, which is what the
/// role-attestation issuance flow (paper §11.3) needs.
/// </remarks>
public interface IEd25519Signer
{
    /// <summary>Ed25519 public-key length in bytes (32).</summary>
    int PublicKeyLength { get; }

    /// <summary>Ed25519 private-key length in bytes (32 — the raw seed).</summary>
    int PrivateKeyLength { get; }

    /// <summary>Ed25519 signature length in bytes (64).</summary>
    int SignatureLength { get; }

    /// <summary>
    /// Generates a new random Ed25519 key pair. The private key is the raw 32-byte seed
    /// in NSec terminology — convertible back into an <c>NSec.Cryptography.Key</c> via
    /// <c>Key.Import(SignatureAlgorithm.Ed25519, seed, KeyBlobFormat.RawPrivateKey)</c>.
    /// </summary>
    (byte[] PublicKey, byte[] PrivateKey) GenerateKeyPair();

    /// <summary>
    /// Deterministically derives an Ed25519 keypair from a 32-byte seed (the "raw
    /// private key" form NSec uses for Ed25519). Same seed → same keypair. Used by
    /// per-team subkey derivation in <c>ITeamSubkeyDerivation</c> (ADR 0032).
    /// </summary>
    /// <param name="seed">32-byte seed. The private key returned is a copy of this seed
    /// (Ed25519's raw-private-key form).</param>
    /// <exception cref="ArgumentException"><paramref name="seed"/> is not 32 bytes.</exception>
    (byte[] PublicKey, byte[] PrivateKey) GenerateFromSeed(ReadOnlySpan<byte> seed);

    /// <summary>Signs <paramref name="message"/> with the 32-byte raw Ed25519 seed.</summary>
    /// <exception cref="ArgumentException">The private key is not 32 bytes.</exception>
    byte[] Sign(ReadOnlySpan<byte> message, ReadOnlySpan<byte> privateKey);

    /// <summary>Verifies a detached Ed25519 signature. Returns <c>false</c> on any invalid input.</summary>
    bool Verify(ReadOnlySpan<byte> message, ReadOnlySpan<byte> signature, ReadOnlySpan<byte> publicKey);
}
