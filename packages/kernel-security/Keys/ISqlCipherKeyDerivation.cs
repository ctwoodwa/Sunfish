namespace Sunfish.Kernel.Security.Keys;

/// <summary>
/// Per-team SQLCipher key derivation from the install's root Ed25519 seed.
/// Produces a 32-byte symmetric key that is cryptographically separate from
/// the team's Ed25519 signing subkey (see <see cref="ITeamSubkeyDerivation"/>)
/// so that a compromise of one never mechanically implies compromise of the
/// other.
/// </summary>
/// <remarks>
/// <para>
/// Derivation: <c>HKDF-Expand(prk = root_seed, info =
/// "sunfish:sqlcipher:v1:" + team_id, L = 32)</c>. The root seed already carries
/// 256 bits of entropy from the install-root generation (see
/// <c>IEd25519Signer.GenerateKeyPair</c>), so an Argon2id stretching pass is
/// unnecessary — a second HKDF call with a distinct info label gives the
/// SQLCipher key its own domain-separated key schedule.
/// </para>
/// <para>
/// Wave 6.3.B stop-work resolution: the decomposition plan
/// (<c>_shared/product/wave-6.3-decomposition.md</c> §6.3.B) flagged that
/// <see cref="Sunfish.Foundation.LocalFirst.Encryption.IEncryptedStore.OpenAsync"/>
/// requires a 32-byte key distinct from the team's signing subkey. This
/// interface is that key bridge — it is NOT used during 6.3.B's registrar wiring
/// (the store is registered unopened), but it IS the component a later hosted
/// service (Wave 6.3.E <c>ITeamStoreActivator</c>) will call on first team
/// activation.
/// </para>
/// <para>
/// This interface is separate from <see cref="ITeamSubkeyDerivation"/> on
/// purpose: the two produce keys for different cryptographic purposes
/// (signing vs. symmetric encryption) and the separation makes it impossible
/// for a caller to accidentally use the wrong key in the wrong place — the
/// type system carries the domain separation.
/// </para>
/// </remarks>
public interface ISqlCipherKeyDerivation
{
    /// <summary>
    /// Derive the 32-byte SQLCipher key for a team from the install's root
    /// Ed25519 seed. Uses HKDF-SHA256 with info label
    /// <c>"sunfish:sqlcipher:v1:" + teamId</c> to ensure the SQLCipher key
    /// is cryptographically separate from the team's signing subkey.
    /// </summary>
    /// <param name="rootSeed">The install's root Ed25519 32-byte seed (as
    /// returned by <c>IEd25519Signer.GenerateKeyPair</c>'s private-key half).</param>
    /// <param name="teamId">Team identifier string form — matches
    /// <c>Sunfish.Kernel.Runtime.Teams.TeamId.Value.ToString("D")</c>, i.e. the
    /// 36-character hyphenated GUID rendering used throughout the per-team
    /// path conventions (see <c>TeamPaths</c>).</param>
    /// <returns>A freshly allocated 32-byte array suitable for
    /// <see cref="Sunfish.Foundation.LocalFirst.Encryption.IEncryptedStore.OpenAsync"/>.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="rootSeed"/> is not exactly 32 bytes, or
    /// <paramref name="teamId"/> is null or empty.
    /// </exception>
    byte[] DeriveSqlCipherKey(ReadOnlySpan<byte> rootSeed, string teamId);
}
