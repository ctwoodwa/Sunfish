namespace Sunfish.Kernel.Security.Keys;

/// <summary>
/// Per-team subkey derivation from the install's root Ed25519 device identity.
/// Implements ADR 0032 §Device identity — one root keypair per install, one
/// HKDF-derived Ed25519 subkey per team, so operators of different teams see
/// different public keys and cannot correlate a user's membership across teams.
/// </summary>
/// <remarks>
/// <para>
/// Derivation: <c>HKDF-SHA256(ikm = root_private, salt = empty, info =
/// "sunfish-team-subkey-v1:" + team_id, L = 64)</c>.
/// </para>
/// <para>
/// The first 32 bytes of the 64-byte output are used as the Ed25519 seed
/// (libsodium / NSec raw-private-key convention); the second 32 bytes are
/// reserved for future signing-key-expansion needs (e.g. an X25519 agreement
/// subkey rooted in the same derivation, should that ever be wanted — paper §11.3).
/// </para>
/// <para>
/// This interface is separate from <see cref="IRoleKeyManager"/> because role-key
/// lifecycle (admin-issued symmetric keys, X25519 sealed-box distribution) is a
/// fundamentally different contract from device-identity subkey derivation
/// (no admin, no per-role concept, purely a deterministic per-device transform).
/// Keeping them separate avoids conflating the two key-management flows.
/// </para>
/// </remarks>
public interface ITeamSubkeyDerivation
{
    /// <summary>
    /// Derive a per-team 64-byte subkey from the root Ed25519 private key.
    /// First 32 bytes = Ed25519 seed for the team; last 32 bytes reserved.
    /// </summary>
    /// <param name="rootPrivateKey">The install's root Ed25519 private key (32-byte raw seed).</param>
    /// <param name="teamId">Team identifier (string form; matches
    /// <c>Sunfish.Kernel.Runtime.Teams.TeamId.ToString()</c>).</param>
    /// <returns>64 bytes of derived key material.</returns>
    /// <exception cref="ArgumentException"><paramref name="rootPrivateKey"/> is not 32 bytes,
    /// or <paramref name="teamId"/> is null/empty.</exception>
    byte[] DeriveSubkey(ReadOnlySpan<byte> rootPrivateKey, string teamId);

    /// <summary>
    /// Convenience form: derive the subkey and expand it into a full Ed25519
    /// keypair the team uses for HELLO signing + role-attestation issuance.
    /// Both returned arrays are 32 bytes.
    /// </summary>
    /// <param name="rootPrivateKey">The install's root Ed25519 private key (32-byte raw seed).</param>
    /// <param name="teamId">Team identifier.</param>
    /// <returns>Public + private key pair derived deterministically from root + teamId.</returns>
    (byte[] PublicKey, byte[] PrivateKey) DeriveTeamKeypair(ReadOnlySpan<byte> rootPrivateKey, string teamId);
}
