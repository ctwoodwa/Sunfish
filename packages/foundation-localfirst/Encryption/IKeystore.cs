namespace Sunfish.Foundation.LocalFirst.Encryption;

/// <summary>
/// OS-native secret storage (Windows DPAPI, macOS Keychain, Linux libsecret).
/// Used by paper §11.2 Layer 1 to cache Argon2id-derived keys so the user does
/// not have to re-enter credentials on every app launch.
/// </summary>
public interface IKeystore
{
    /// <summary>Retrieves a named key, or <c>null</c> if none is stored.</summary>
    Task<ReadOnlyMemory<byte>?> GetKeyAsync(string name, CancellationToken ct);

    /// <summary>Stores (or replaces) a named key.</summary>
    Task SetKeyAsync(string name, ReadOnlyMemory<byte> key, CancellationToken ct);

    /// <summary>Deletes a named key. No-op if it does not exist.</summary>
    Task DeleteKeyAsync(string name, CancellationToken ct);
}

/// <summary>
/// Factory for the platform-appropriate <see cref="IKeystore"/>.
/// </summary>
public static class Keystore
{
    /// <summary>
    /// Creates the default keystore for the current OS: DPAPI on Windows,
    /// Keychain on macOS, libsecret on Linux. The macOS and Linux integrations
    /// are Wave 2 stubs that throw <see cref="PlatformNotSupportedException"/>
    /// when first used.
    /// </summary>
    /// <param name="storageDirectory">
    /// Directory for platform-specific storage. On Windows this backs the DPAPI
    /// ciphertext file. Defaults to <c>%LOCALAPPDATA%/Sunfish/keys</c>.
    /// </param>
    public static IKeystore CreateForCurrentPlatform(string? storageDirectory = null)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsDpapiKeystore(storageDirectory);
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacOsKeychainKeystore();
        }

        if (OperatingSystem.IsLinux())
        {
            return new LinuxLibsecretKeystore();
        }

        throw new PlatformNotSupportedException(
            $"No keystore implementation is available for the current OS.");
    }
}
