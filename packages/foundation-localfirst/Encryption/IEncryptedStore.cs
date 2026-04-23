namespace Sunfish.Foundation.LocalFirst.Encryption;

/// <summary>
/// Encrypted-at-rest local store. Paper §11.2 Layer 1: SQLCipher-style
/// full-database encryption. Keys derived from user credentials via Argon2id,
/// cached in OS-native keystore.
/// </summary>
/// <remarks>
/// The store is expected to be opened exactly once for the lifetime of the
/// application and closed on shutdown. Concurrent <c>Get</c> / <c>Set</c> /
/// <c>Delete</c> / <c>ListKeys</c> calls against a single open instance are
/// safe as long as the underlying implementation serializes physical I/O;
/// the default SQLCipher implementation uses a single connection guarded by
/// an async lock.
/// </remarks>
public interface IEncryptedStore
{
    /// <summary>
    /// Initializes the store using the derived key from <see cref="IKeyDerivation"/>.
    /// First call for a given path creates the database; subsequent calls must
    /// provide the same key or <see cref="InvalidKeyException"/> is thrown.
    /// </summary>
    /// <param name="databasePath">Absolute file path for the encrypted database.</param>
    /// <param name="key">32-byte derived key. Must match the key previously used for this file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidKeyException">Thrown when the key does not match an existing database.</exception>
    Task OpenAsync(string databasePath, ReadOnlyMemory<byte> key, CancellationToken ct);

    /// <summary>Reads the value for a key. Returns <c>null</c> if absent.</summary>
    Task<byte[]?> GetAsync(string key, CancellationToken ct);

    /// <summary>Writes (or replaces) the value for a key. Last write wins.</summary>
    Task SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken ct);

    /// <summary>Deletes the value for a key. No-op if the key does not exist.</summary>
    Task DeleteAsync(string key, CancellationToken ct);

    /// <summary>Returns a snapshot of keys starting with the given prefix, in ordinal order.</summary>
    Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct);

    /// <summary>Closes the underlying connection and flushes any pending writes.</summary>
    Task CloseAsync();
}

/// <summary>
/// Thrown when an encrypted store is opened with a key that does not match the
/// existing database file. Callers should treat this as a recoverable user-facing
/// error (e.g. prompt for credentials again) rather than a programming fault.
/// </summary>
public sealed class InvalidKeyException : Exception
{
    /// <summary>Initializes a new instance with a default message.</summary>
    public InvalidKeyException()
        : base("The provided key does not match the encrypted database.") { }

    /// <summary>Initializes a new instance with a custom message.</summary>
    public InvalidKeyException(string message) : base(message) { }

    /// <summary>Initializes a new instance with a custom message and inner exception.</summary>
    public InvalidKeyException(string message, Exception innerException)
        : base(message, innerException) { }
}
