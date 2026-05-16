using System.Security.Cryptography;
using System.Text;
using NSec.Cryptography;

namespace Sunfish.Kernel.Security.Keys;

/// <summary>
/// Default <see cref="IX25519SubkeyDerivation"/>. HKDF-Expand-SHA256 over
/// the install root seed produces a 32-byte raw X25519 private key; NSec
/// imports that with <c>KeyBlobFormat.RawPrivateKey</c> (applying RFC 7748
/// clamping internally) and exports the matching public key as
/// <c>KeyBlobFormat.RawPublicKey</c>. Stateless; one instance can be
/// shared across the process.
/// </summary>
/// <remarks>
/// Mirrors the shape of <see cref="TeamSubkeyDerivation"/> (the Ed25519
/// counterpart) — same HKDF-Expand pattern, distinct info prefix.
/// </remarks>
public sealed class HkdfX25519SubkeyDerivation : IX25519SubkeyDerivation
{
    /// <summary>HKDF info-string prefix. Version-stamped per ADR 0046-A6
    /// so a future v2 derivation can coexist with deployed v1 installs.</summary>
    public const string InfoPrefix = "sunfish-x25519-team-v1:";

    /// <summary>Length of an X25519 private (and public) key in bytes.</summary>
    public const int KeyLength = 32;

    /// <inheritdoc />
    public byte[] DeriveX25519PrivateKey(ReadOnlyMemory<byte> rootSeed, string teamId)
    {
        ArgumentException.ThrowIfNullOrEmpty(teamId);
        if (rootSeed.Length == 0)
        {
            throw new ArgumentException("Root seed must be non-empty.", nameof(rootSeed));
        }

        var prefix = Encoding.UTF8.GetBytes(InfoPrefix);
        var teamBytes = Encoding.UTF8.GetBytes(teamId);
        var info = new byte[prefix.Length + teamBytes.Length];
        Buffer.BlockCopy(prefix, 0, info, 0, prefix.Length);
        Buffer.BlockCopy(teamBytes, 0, info, prefix.Length, teamBytes.Length);

        var output = new byte[KeyLength];
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm:  rootSeed.Span,
            output: output,
            salt: ReadOnlySpan<byte>.Empty,
            info: info);
        return output;
    }

    /// <inheritdoc />
    public byte[] DeriveX25519PublicKey(ReadOnlyMemory<byte> rootSeed, string teamId)
    {
        var raw = DeriveX25519PrivateKey(rootSeed, teamId);
        try
        {
            // NSec applies RFC 7748 clamping inside Key.Import; the raw
            // bytes we pass in are accepted directly. Export the
            // matching public key via Curve25519 scalar mult against
            // the base point.
            using var key = Key.Import(
                KeyAgreementAlgorithm.X25519,
                raw,
                KeyBlobFormat.RawPrivateKey,
                new KeyCreationParameters
                {
                    ExportPolicy = KeyExportPolicies.AllowPlaintextExport,
                });
            return key.Export(KeyBlobFormat.RawPublicKey);
        }
        finally
        {
            // W#67 PR 5 council R-1: zero the intermediate private-key
            // buffer so it does not sit on the GC heap until collection.
            CryptographicOperations.ZeroMemory(raw);
        }
    }
}
