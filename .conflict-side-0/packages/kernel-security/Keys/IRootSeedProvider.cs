namespace Sunfish.Kernel.Security.Keys;

/// <summary>
/// Supplies the install's 32-byte Ed25519 root seed. On the first launch of a
/// fresh install the seed is generated via
/// <see cref="System.Security.Cryptography.RandomNumberGenerator"/> and stored
/// in the platform keystore (Windows DPAPI today; macOS Keychain + Linux
/// libsecret are Wave 2 stubs). Subsequent launches read the cached value back
/// from the keystore so the same machine always yields the same seed —
/// guaranteeing continuity of Ed25519 + SQLCipher key derivations for the team
/// subkey pipeline (paper §11.2–§11.3).
/// </summary>
/// <remarks>
/// <para>
/// Replaces the Wave 6.3.E.2 / 6.3.F zero-seed carve-outs in
/// <c>apps/local-node-host/LocalNodeRootSeedReader.cs</c> and
/// <c>accelerators/anchor/Services/AnchorRootSeedReader.cs</c>, which both
/// returned a 32-byte zero buffer in Development and threw in Production.
/// Those stubs meant every Anchor install and every Development local-node-host
/// derived the same Ed25519 + SQLCipher keys, trivially breaking per-install
/// isolation. This contract restores per-install independence by making each
/// install's seed a fresh RNG draw persisted to the platform keystore.
/// </para>
/// <para>
/// Prerequisite for: Wave 6.7 Anchor v1→v2 migration (currently bootstraps the
/// migration path with the zero seed); Wave 5.2.C Bridge per-tenant seed
/// derivation (derives per-tenant seeds from a real Bridge-install seed); any
/// production ship of local-node-host or Anchor.
/// </para>
/// </remarks>
public interface IRootSeedProvider
{
    /// <summary>
    /// Returns the 32-byte Ed25519 root seed for this install. On the first
    /// call of the first launch this generates the seed via
    /// <see cref="System.Security.Cryptography.RandomNumberGenerator.GetBytes(int)"/>
    /// and persists it to the platform keystore. Subsequent calls (and
    /// subsequent launches of the same install) return the cached value.
    /// Implementations must be thread-safe — concurrent first callers must
    /// observe a single consistent seed.
    /// </summary>
    /// <param name="ct">Cancellation token for the underlying keystore I/O.</param>
    /// <returns>A 32-byte buffer. Callers must treat the buffer as
    /// secret-material and must not persist it outside the keystore.</returns>
    ValueTask<ReadOnlyMemory<byte>> GetRootSeedAsync(CancellationToken ct);
}
