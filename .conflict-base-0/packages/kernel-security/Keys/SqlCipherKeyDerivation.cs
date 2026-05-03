using System.Security.Cryptography;
using System.Text;

namespace Sunfish.Kernel.Security.Keys;

/// <summary>
/// Default <see cref="ISqlCipherKeyDerivation"/>. Implements the Wave 6.3.B
/// stop-work resolution: a second <see cref="HKDF"/>-Expand call over the same
/// root seed, using a different info label from
/// <see cref="TeamSubkeyDerivation"/>, so that the SQLCipher key is
/// domain-separated from the team's Ed25519 signing subkey.
/// </summary>
/// <remarks>
/// Stateless; a single instance may be shared across the entire process.
/// </remarks>
public sealed class SqlCipherKeyDerivation : ISqlCipherKeyDerivation
{
    /// <summary>HKDF info-string prefix. Version-stamped so a future v2 derivation
    /// (e.g. an AEAD re-keying pass) can coexist with already-provisioned v1
    /// databases during a migration window.</summary>
    public const string InfoPrefix = "sunfish:sqlcipher:v1:";

    /// <summary>Ed25519 root-seed length in bytes — matches
    /// <see cref="TeamSubkeyDerivation.RootPrivateKeyLength"/>.</summary>
    public const int RootSeedLength = 32;

    /// <summary>SQLCipher key length in bytes. 32 bytes = 256 bits, the
    /// size <c>sqlcipher</c> expects when the raw-key form is used.</summary>
    public const int SqlCipherKeyLength = 32;

    /// <inheritdoc />
    public byte[] DeriveSqlCipherKey(ReadOnlySpan<byte> rootSeed, string teamId)
    {
        ArgumentException.ThrowIfNullOrEmpty(teamId);
        if (rootSeed.Length != RootSeedLength)
        {
            throw new ArgumentException(
                $"Root seed must be {RootSeedLength} bytes (was {rootSeed.Length}).",
                nameof(rootSeed));
        }

        // info = "sunfish:sqlcipher:v1:" + UTF-8(team_id)
        var prefix = Encoding.UTF8.GetBytes(InfoPrefix);
        var teamBytes = Encoding.UTF8.GetBytes(teamId);
        var info = new byte[prefix.Length + teamBytes.Length];
        Buffer.BlockCopy(prefix, 0, info, 0, prefix.Length);
        Buffer.BlockCopy(teamBytes, 0, info, prefix.Length, teamBytes.Length);

        // HKDF-Expand directly — the root seed already carries 256 bits of
        // entropy (see type remarks on ISqlCipherKeyDerivation), so the HKDF-Extract
        // step would be a no-op beyond shape-normalization. Using Expand keeps
        // the derivation cheap and explicit about the input's entropy budget.
        var output = new byte[SqlCipherKeyLength];
        HKDF.Expand(HashAlgorithmName.SHA256, rootSeed, output, info);
        return output;
    }
}
