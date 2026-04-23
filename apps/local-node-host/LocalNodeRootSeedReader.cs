using Microsoft.Extensions.Hosting;

namespace Sunfish.LocalNodeHost;

/// <summary>
/// Wave 6.3.E.2 carve-out: the real root-seed loading path goes through the
/// install's keystore facade, which Wave 6.7 will deliver. For now, the
/// reader yields a deterministic zero-filled seed in development and throws
/// in production so we do not accidentally ship a zero-key install.
/// </summary>
/// <remarks>
/// <para>
/// The Ed25519 + SQLCipher key derivations downstream of this seed are pure
/// HKDF-based functions of <c>(seed, team_id)</c>. A deterministic zero seed
/// across dev machines yields identical derived keys, which is fine for the
/// test/dev loop but MUST NOT be used against real SQLCipher databases in a
/// production deployment.
/// </para>
/// <para>
/// TODO (Wave 6.7): replace with a proper <c>IKeystore</c>-backed loader that
/// reads the install's root seed from the platform keystore (DPAPI /
/// Keychain / libsecret) and falls back to <c>{DataDirectory}/root.seed</c>
/// only when keystore APIs are unavailable (CI / sandboxed test hosts).
/// </para>
/// </remarks>
internal static class LocalNodeRootSeedReader
{
    /// <summary>Length of an Ed25519 root seed (bytes).</summary>
    public const int SeedLength = 32;

    /// <summary>
    /// Read the install's 32-byte root seed. In development environments,
    /// returns a deterministic zero-filled buffer. In other environments,
    /// throws — Wave 6.7 replaces this with a keystore-backed loader.
    /// </summary>
    /// <param name="hostEnvironment">The host environment used to gate the
    /// dev-only zero-seed stub.</param>
    /// <returns>The 32-byte root seed.</returns>
    /// <exception cref="InvalidOperationException">The environment is not
    /// Development and no real keystore-backed loader is wired yet.</exception>
    public static byte[] Read(IHostEnvironment hostEnvironment)
    {
        ArgumentNullException.ThrowIfNull(hostEnvironment);

        if (hostEnvironment.IsDevelopment())
        {
            // Deterministic zero seed for the dev/test loop. Wave 6.7 replaces
            // this with a real keystore-backed loader.
            return new byte[SeedLength];
        }

        throw new InvalidOperationException(
            "LocalNodeRootSeedReader: no production keystore loader wired yet. " +
            "Wave 6.7 (keystore-backed root seed) is a prerequisite for non-dev " +
            "deployments. See the Wave 6.3.E.2 carve-out in CLAUDE.md.");
    }
}
