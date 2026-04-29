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

    /// <summary>
    /// Atomically re-encrypt the underlying store with <paramref name="newKey"/>.
    /// On success the connection remains open under the new key; subsequent
    /// <see cref="OpenAsync"/> calls (or new processes) must use the new key
    /// or fail with <see cref="InvalidKeyException"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Phase 1 G6 task 5 (per ADR 0046) wires this against the
    /// <c>RecoveryCompleted</c> event from
    /// <c>Sunfish.Foundation.Recovery.IRecoveryCoordinator</c>. After a
    /// successful trustee-attested recovery the host derives a new SQLCipher
    /// key from the recovering device's identity and calls this method to
    /// re-key the per-team database in place — this is the "key reissue"
    /// the plan §G6 step 5 calls out.
    /// </para>
    /// <para>
    /// Default implementation throws <see cref="NotSupportedException"/>;
    /// stores that cannot rotate keys (e.g., a future read-only or
    /// CDN-backed store) inherit that behavior. The SQLCipher-backed
    /// implementation overrides via <c>PRAGMA rekey</c>.
    /// </para>
    /// <para>
    /// The store must be open before this is called. Concurrent reads /
    /// writes against the rotating connection are serialized by the
    /// implementation's internal lock; callers do not need to drain
    /// pending operations first.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">If <paramref name="newKey"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">If the store is not open.</exception>
    /// <exception cref="NotSupportedException">If the implementation does not support rotation.</exception>
    Task RotateKeyAsync(ReadOnlyMemory<byte> newKey, CancellationToken ct)
        => throw new NotSupportedException(
            "This encrypted store implementation does not support key rotation.");

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
