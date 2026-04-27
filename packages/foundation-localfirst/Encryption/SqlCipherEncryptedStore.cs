using Microsoft.Data.Sqlite;

namespace Sunfish.Foundation.LocalFirst.Encryption;

/// <summary>
/// SQLCipher-backed <see cref="IEncryptedStore"/> using Microsoft.Data.Sqlite
/// with the <c>SQLitePCLRaw.bundle_e_sqlcipher</c> provider. Paper §11.2
/// Layer 1 — encryption at rest with a key derived from user credentials and
/// cached in an OS-native keystore.
/// </summary>
/// <remarks>
/// <para>
/// Schema: a single table <c>kv_store(key TEXT PRIMARY KEY, value BLOB NOT NULL)</c>.
/// More structured tables can layer on top in later waves — paper §2.4 tier 1
/// only requires a "primary operational store".
/// </para>
/// <para>
/// Concurrency: one long-lived connection guarded by an async lock. SQLCipher
/// mutates the database file in place using standard SQLite pager semantics,
/// so a single writer is sufficient for an offline local-first node.
/// </para>
/// </remarks>
public sealed class SqlCipherEncryptedStore : IEncryptedStore, IAsyncDisposable
{
    private static int s_providerInitialized;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private SqliteConnection? _connection;

    /// <summary>Initializes a new, unopened instance.</summary>
    public SqlCipherEncryptedStore()
    {
        EnsureProviderInitialized();
    }

    /// <inheritdoc />
    public async Task OpenAsync(string databasePath, ReadOnlyMemory<byte> key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(databasePath);
        if (key.Length == 0)
        {
            throw new ArgumentException("Derived key must not be empty.", nameof(key));
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_connection is not null)
            {
                throw new InvalidOperationException("Store is already open; call CloseAsync first.");
            }

            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = databasePath,
                Mode = SqliteOpenMode.ReadWriteCreate,
            }.ToString();

            var connection = new SqliteConnection(connectionString);
            try
            {
                await connection.OpenAsync(ct).ConfigureAwait(false);

                // SQLCipher key via PRAGMA key. Use the hex form so a raw 32-byte
                // derived key is not re-hashed by SQLCipher. See SQLCipher docs:
                // https://www.zetetic.net/sqlcipher/sqlcipher-api/#key.
                var hex = Convert.ToHexString(key.Span);
                using (var keyCmd = connection.CreateCommand())
                {
                    keyCmd.CommandText = $"PRAGMA key = \"x'{hex}'\";";
                    await keyCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                // Force SQLCipher to verify the key by reading from sqlite_schema.
                // A wrong key surfaces as a SqliteException (SQLITE_NOTADB / file is not a database).
                using (var probeCmd = connection.CreateCommand())
                {
                    probeCmd.CommandText = "SELECT count(*) FROM sqlite_schema;";
                    try
                    {
                        await probeCmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
                    }
                    catch (SqliteException ex) when (IsInvalidKeyError(ex))
                    {
                        await connection.DisposeAsync().ConfigureAwait(false);
                        throw new InvalidKeyException(
                            "The encrypted database could not be opened with the provided key.", ex);
                    }
                }

                // Ensure schema.
                using (var schemaCmd = connection.CreateCommand())
                {
                    schemaCmd.CommandText =
                        "CREATE TABLE IF NOT EXISTS kv_store (key TEXT PRIMARY KEY, value BLOB NOT NULL);";
                    await schemaCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                }

                _connection = connection;
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetAsync(string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var connection = RequireOpen();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT value FROM kv_store WHERE key = $k;";
            cmd.Parameters.AddWithValue("$k", key);
            var result = await cmd.ExecuteScalarAsync(ct).ConfigureAwait(false);
            return result is byte[] bytes ? bytes : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task SetAsync(string key, ReadOnlyMemory<byte> value, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var connection = RequireOpen();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "INSERT INTO kv_store(key, value) VALUES($k, $v) " +
                "ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
            cmd.Parameters.AddWithValue("$k", key);
            cmd.Parameters.AddWithValue("$v", value.ToArray());
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var connection = RequireOpen();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM kv_store WHERE key = $k;";
            cmd.Parameters.AddWithValue("$k", key);
            await cmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ListKeysAsync(string prefix, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var connection = RequireOpen();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "SELECT key FROM kv_store WHERE key LIKE $p ORDER BY key ASC;";
            cmd.Parameters.AddWithValue("$p", EscapeLike(prefix) + "%");

            var results = new List<string>();
            using var reader = await cmd.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                results.Add(reader.GetString(0));
            }

            return results;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task RotateKeyAsync(ReadOnlyMemory<byte> newKey, CancellationToken ct)
    {
        if (newKey.Length == 0)
        {
            throw new ArgumentException("New key must not be empty.", nameof(newKey));
        }

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var connection = RequireOpen();

            // SQLCipher rekey via PRAGMA rekey. Hex form ensures the raw key
            // bytes are not re-hashed by SQLCipher — same convention as
            // OpenAsync's PRAGMA key. The pragma re-encrypts the whole database
            // file in place under the new key in a single atomic operation
            // (https://www.zetetic.net/sqlcipher/sqlcipher-api/#rekey).
            var hex = Convert.ToHexString(newKey.Span);
            using var rekeyCmd = connection.CreateCommand();
            rekeyCmd.CommandText = $"PRAGMA rekey = \"x'{hex}'\";";
            await rekeyCmd.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_connection is null)
            {
                return;
            }

            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;

            // Clear the connection pool so the file handle is fully released.
            // Needed for tests that inspect the raw file bytes after close.
            SqliteConnection.ClearAllPools();
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await CloseAsync().ConfigureAwait(false);
        _gate.Dispose();
    }

    private SqliteConnection RequireOpen()
    {
        if (_connection is null)
        {
            throw new InvalidOperationException(
                "Encrypted store is not open. Call OpenAsync before reading or writing.");
        }

        return _connection;
    }

    private static bool IsInvalidKeyError(SqliteException ex)
    {
        // SQLCipher surfaces a wrong key as SQLITE_NOTADB (26) or "file is not a database".
        // SQLite extended result codes can also appear (e.g. SQLITE_NOTADB).
        const int SQLITE_NOTADB = 26;
        return ex.SqliteErrorCode == SQLITE_NOTADB
            || ex.SqliteExtendedErrorCode == SQLITE_NOTADB
            || ex.Message.Contains("not a database", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("file is encrypted", StringComparison.OrdinalIgnoreCase);
    }

    private static string EscapeLike(string prefix)
    {
        // Escape the two LIKE wildcards so a prefix containing them is matched literally.
        return prefix
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    private static void EnsureProviderInitialized()
    {
        if (Interlocked.Exchange(ref s_providerInitialized, 1) == 0)
        {
            // bundle_e_sqlcipher self-registers when referenced; this call is
            // idempotent and defends against test harnesses that load multiple
            // SQLitePCL providers into the same AppDomain.
            SQLitePCL.Batteries_V2.Init();
        }
    }
}
