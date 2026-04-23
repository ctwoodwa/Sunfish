using System.Security.Cryptography;
using System.Text;
using Sunfish.Kernel.Security.Crypto;

namespace Sunfish.Kernel.Security.Keys;

/// <summary>
/// Default <see cref="ITeamSubkeyDerivation"/>. Implements ADR 0032 §Device identity
/// via <see cref="HKDF"/>-SHA256 + <see cref="IEd25519Signer.GenerateFromSeed"/>.
/// Stateless; one instance can be shared across the process.
/// </summary>
public sealed class TeamSubkeyDerivation : ITeamSubkeyDerivation
{
    /// <summary>HKDF info-string prefix. Version-stamped so a future v2 derivation
    /// can coexist with already-deployed v1 installs during migration.</summary>
    public const string InfoPrefix = "sunfish-team-subkey-v1:";

    /// <summary>Ed25519 root-private-key length in bytes.</summary>
    public const int RootPrivateKeyLength = 32;

    /// <summary>Derived-subkey output length in bytes (32-byte Ed25519 seed + 32 reserved).</summary>
    public const int SubkeyLength = 64;

    private readonly IEd25519Signer _signer;

    /// <summary>Constructs a derivation bound to an Ed25519 signer.</summary>
    public TeamSubkeyDerivation(IEd25519Signer signer)
    {
        _signer = signer ?? throw new ArgumentNullException(nameof(signer));
    }

    /// <inheritdoc />
    public byte[] DeriveSubkey(ReadOnlySpan<byte> rootPrivateKey, string teamId)
    {
        ArgumentException.ThrowIfNullOrEmpty(teamId);
        if (rootPrivateKey.Length != RootPrivateKeyLength)
        {
            throw new ArgumentException(
                $"Root private key must be {RootPrivateKeyLength} bytes (was {rootPrivateKey.Length}).",
                nameof(rootPrivateKey));
        }

        // info = "sunfish-team-subkey-v1:" + UTF-8(team_id)
        var prefix = Encoding.UTF8.GetBytes(InfoPrefix);
        var teamBytes = Encoding.UTF8.GetBytes(teamId);
        var info = new byte[prefix.Length + teamBytes.Length];
        Buffer.BlockCopy(prefix, 0, info, 0, prefix.Length);
        Buffer.BlockCopy(teamBytes, 0, info, prefix.Length, teamBytes.Length);

        // HKDF-SHA256(ikm = root_private, salt = empty, info, L = 64).
        var output = new byte[SubkeyLength];
        HKDF.DeriveKey(HashAlgorithmName.SHA256, rootPrivateKey, output, salt: ReadOnlySpan<byte>.Empty, info);
        return output;
    }

    /// <inheritdoc />
    public (byte[] PublicKey, byte[] PrivateKey) DeriveTeamKeypair(
        ReadOnlySpan<byte> rootPrivateKey, string teamId)
    {
        var subkey = DeriveSubkey(rootPrivateKey, teamId);
        // First 32 bytes = Ed25519 seed. Ed25519's private-key form IS the seed;
        // the signer's GenerateFromSeed returns (publicKey, seed-as-private).
        var seed = new ReadOnlySpan<byte>(subkey, 0, 32);
        return _signer.GenerateFromSeed(seed);
    }
}
