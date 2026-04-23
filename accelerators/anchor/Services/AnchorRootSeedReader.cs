using Microsoft.Maui.Storage;

namespace Sunfish.Anchor.Services;

/// <summary>
/// Wave 6.3.F carve-out: the real Anchor root-seed loading path goes through
/// the platform keystore (DPAPI / Keychain / libsecret) via
/// <c>IKeystore.CreateForCurrentPlatform()</c>, which Wave 6.7 will fully
/// deliver for the multi-team migration. For now this reader yields a
/// deterministic zero-filled seed so the dev loop has a stable SQLCipher key
/// across builds — matching <c>LocalNodeRootSeedReader</c> in
/// <c>apps/local-node-host</c>.
/// </summary>
/// <remarks>
/// <para>
/// The Ed25519 + SQLCipher key derivations downstream of this seed are pure
/// HKDF-based functions of <c>(seed, team_id)</c>. A deterministic zero seed
/// across dev devices yields identical derived keys, which is fine for the
/// test/dev loop but MUST NOT be used against real SQLCipher databases in a
/// shipped Anchor build.
/// </para>
/// <para>
/// TODO (Wave 6.7): replace with a proper <c>IKeystore</c>-backed loader that
/// reads the install's root seed from the MAUI platform keystore (DPAPI on
/// Windows, Keychain on mac/iOS, KeyStore on Android) and falls back to
/// <c>FileSystem.AppDataDirectory/root.seed</c> only when keystore APIs are
/// unavailable.
/// </para>
/// <para>
/// Duplicated from <c>apps/local-node-host/LocalNodeRootSeedReader.cs</c>
/// rather than co-located because that reader is <c>internal</c> and Anchor
/// does not take a ProjectReference on <c>Sunfish.LocalNodeHost</c>. Keeping
/// both stubs in sync is Wave 6.7's job — both are scheduled for replacement
/// by the real keystore-backed loader at that time.
/// </para>
/// </remarks>
internal static class AnchorRootSeedReader
{
    /// <summary>Length of an Ed25519 root seed (bytes).</summary>
    public const int SeedLength = 32;

    /// <summary>
    /// Read Anchor's 32-byte root seed. Currently returns a deterministic
    /// zero-filled buffer; Wave 6.7 replaces this with a keystore-backed
    /// loader.
    /// </summary>
    /// <returns>The 32-byte root seed.</returns>
    public static byte[] Read()
    {
        // Deterministic zero seed for the dev/test loop. Wave 6.7 replaces
        // this with a real keystore-backed loader that reads from DPAPI /
        // Keychain / Android KeyStore via IKeystore.CreateForCurrentPlatform().
        return new byte[SeedLength];
    }

    /// <summary>
    /// Platform-conventional default for Anchor's on-disk data directory. MAUI
    /// resolves this to the per-user app-data location
    /// (<c>%LOCALAPPDATA%\Packages\...\LocalState</c> on Windows,
    /// <c>~/Library/Containers/.../Data/Library/Application Support</c> on
    /// macOS/iOS, the app-private files directory on Android).
    /// </summary>
    public static string GetDefaultDataDirectory()
        => FileSystem.AppDataDirectory;
}
