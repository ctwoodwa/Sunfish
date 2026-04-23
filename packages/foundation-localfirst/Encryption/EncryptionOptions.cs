namespace Sunfish.Foundation.LocalFirst.Encryption;

/// <summary>
/// Configuration for the Sunfish encrypted local store.
/// </summary>
public sealed class EncryptionOptions
{
    /// <summary>
    /// Absolute file path for the encrypted SQLite database. Defaults to
    /// <c>%LOCALAPPDATA%/Sunfish/data/sunfish.db</c> on Windows, or the
    /// equivalent XDG path elsewhere.
    /// </summary>
    public string DatabasePath { get; set; } = DefaultDatabasePath();

    /// <summary>
    /// Name of the key slot inside the OS keystore. Multiple Sunfish databases
    /// can coexist by using distinct slot names.
    /// </summary>
    public string KeystoreKeyName { get; set; } = "sunfish-primary";

    /// <summary>Argon2id parameters used when deriving the key from a user password.</summary>
    public Argon2idOptions Argon2Options { get; set; } = new();

    private static string DefaultDatabasePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(root))
        {
            root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        }

        return Path.Combine(root, "Sunfish", "data", "sunfish.db");
    }
}
