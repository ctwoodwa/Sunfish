using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace Sunfish.Foundation.LocalFirst.Encryption;

/// <summary>
/// Windows DPAPI-backed keystore. Each named key is persisted as a file under
/// the configured storage directory containing <see cref="ProtectedData"/>-encrypted
/// ciphertext scoped to <see cref="DataProtectionScope.CurrentUser"/>, meaning
/// only the logged-in Windows user can decrypt the key.
/// </summary>
/// <remarks>
/// This keystore is a good default for desktop Sunfish nodes. It does not
/// require the user to remember a secondary passphrase beyond their Windows
/// login, and the DPAPI master key is itself unlocked by the user's credentials.
/// </remarks>
public sealed class WindowsDpapiKeystore : IKeystore
{
    private static readonly byte[] s_entropy = "Sunfish.Foundation.LocalFirst.Encryption"u8.ToArray();

    private readonly string _storageDirectory;

    /// <summary>Initializes a new instance writing to <paramref name="storageDirectory"/>.</summary>
    /// <param name="storageDirectory">
    /// Directory for DPAPI-encrypted key files. When <c>null</c>, defaults to
    /// <c>%LOCALAPPDATA%/Sunfish/keys</c>.
    /// </param>
    public WindowsDpapiKeystore(string? storageDirectory = null)
    {
        _storageDirectory = storageDirectory
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Sunfish",
                "keys");
    }

    /// <summary>Directory where DPAPI-encrypted key files live.</summary>
    public string StorageDirectory => _storageDirectory;

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public Task<ReadOnlyMemory<byte>?> GetKeyAsync(string name, CancellationToken ct)
    {
        EnsureWindows();
        ArgumentException.ThrowIfNullOrEmpty(name);

        var path = GetPathForName(name);
        if (!File.Exists(path))
        {
            return Task.FromResult<ReadOnlyMemory<byte>?>(null);
        }

        var ciphertext = File.ReadAllBytes(path);
        var plaintext = ProtectedData.Unprotect(ciphertext, s_entropy, DataProtectionScope.CurrentUser);
        return Task.FromResult<ReadOnlyMemory<byte>?>(plaintext);
    }

    /// <inheritdoc />
    [SupportedOSPlatform("windows")]
    public Task SetKeyAsync(string name, ReadOnlyMemory<byte> key, CancellationToken ct)
    {
        EnsureWindows();
        ArgumentException.ThrowIfNullOrEmpty(name);

        Directory.CreateDirectory(_storageDirectory);
        var ciphertext = ProtectedData.Protect(key.ToArray(), s_entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(GetPathForName(name), ciphertext);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task DeleteKeyAsync(string name, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        var path = GetPathForName(name);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetPathForName(string name)
    {
        // Sanitize the filename — DPAPI names are free-form strings but we write them to disk.
        var safe = string.Join('_', name.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_storageDirectory, safe + ".dpapi");
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "WindowsDpapiKeystore is only available on Windows. "
                + "Use Keystore.CreateForCurrentPlatform() to select the right implementation.");
        }
    }
}
