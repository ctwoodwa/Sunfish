using System.Security.Cryptography;
using Sunfish.Foundation.LocalFirst.Encryption;

namespace Sunfish.Foundation.LocalFirst.Tests;

/// <summary>
/// Contract tests for the paper §11.2 Layer 1 encrypted store. The store is
/// backed by SQLCipher with an in-memory keystore; tests run against an
/// ephemeral database file in a per-test temp directory.
/// </summary>
public class EncryptedStoreContractTests : IAsyncLifetime
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sunfish-enc-" + Guid.NewGuid().ToString("N"));
    private string _dbPath = null!;
    private byte[] _key = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "test.db");
        _key = RandomNumberGenerator.GetBytes(32);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        try
        {
            if (Directory.Exists(_dir))
            {
                Directory.Delete(_dir, recursive: true);
            }
        }
        catch
        {
            // Best effort — Windows may still hold file locks.
        }

        return Task.CompletedTask;
    }

    private async Task<SqlCipherEncryptedStore> OpenNewAsync(byte[]? keyOverride = null)
    {
        var store = new SqlCipherEncryptedStore();
        await store.OpenAsync(_dbPath, keyOverride ?? _key, CancellationToken.None);
        return store;
    }

    [Fact]
    public async Task Open_then_Set_then_Get_roundtrips_the_value()
    {
        await using var store = await OpenNewAsync();

        var value = Encoding.UTF8.GetBytes("hello encrypted world");
        await store.SetAsync("greeting", value, CancellationToken.None);

        var actual = await store.GetAsync("greeting", CancellationToken.None);
        Assert.Equal(value, actual);
    }

    [Fact]
    public async Task Delete_removes_the_value()
    {
        await using var store = await OpenNewAsync();
        await store.SetAsync("k", new byte[] { 1, 2, 3 }, CancellationToken.None);

        await store.DeleteAsync("k", CancellationToken.None);

        Assert.Null(await store.GetAsync("k", CancellationToken.None));
    }

    [Fact]
    public async Task ListKeys_returns_prefix_matches_in_ordinal_order()
    {
        await using var store = await OpenNewAsync();
        await store.SetAsync("a/2", new byte[] { 1 }, CancellationToken.None);
        await store.SetAsync("a/1", new byte[] { 1 }, CancellationToken.None);
        await store.SetAsync("b/1", new byte[] { 1 }, CancellationToken.None);

        var keys = await store.ListKeysAsync("a/", CancellationToken.None);

        Assert.Equal(new[] { "a/1", "a/2" }, keys);
    }

    [Fact]
    public async Task Close_then_Open_with_correct_key_preserves_data()
    {
        var first = await OpenNewAsync();
        await first.SetAsync("persist", Encoding.UTF8.GetBytes("survives-restart"), CancellationToken.None);
        await first.CloseAsync();
        await first.DisposeAsync();

        await using var second = await OpenNewAsync();
        var actual = await second.GetAsync("persist", CancellationToken.None);
        Assert.Equal(Encoding.UTF8.GetBytes("survives-restart"), actual);
    }

    [Fact]
    public async Task Close_then_Open_with_wrong_key_throws_InvalidKeyException()
    {
        var first = await OpenNewAsync();
        await first.SetAsync("x", new byte[] { 9 }, CancellationToken.None);
        await first.CloseAsync();
        await first.DisposeAsync();

        var wrongKey = RandomNumberGenerator.GetBytes(32);
        var store = new SqlCipherEncryptedStore();

        await Assert.ThrowsAsync<InvalidKeyException>(() =>
            store.OpenAsync(_dbPath, wrongKey, CancellationToken.None));
    }

    [Fact]
    public async Task Set_on_same_key_is_last_write_wins()
    {
        await using var store = await OpenNewAsync();
        await store.SetAsync("k", Encoding.UTF8.GetBytes("first"), CancellationToken.None);
        await store.SetAsync("k", Encoding.UTF8.GetBytes("second"), CancellationToken.None);

        var actual = await store.GetAsync("k", CancellationToken.None);
        Assert.Equal(Encoding.UTF8.GetBytes("second"), actual);
    }

    [Fact]
    public async Task Get_missing_key_returns_null()
    {
        await using var store = await OpenNewAsync();
        Assert.Null(await store.GetAsync("never-set", CancellationToken.None));
    }

    [Fact]
    public async Task Keystore_roundtrip_feeds_the_store_with_a_cached_key()
    {
        // End-to-end: derive a key from a password, stash it in the keystore,
        // retrieve it, then use it to open the encrypted store.
        var kdf = new Argon2idKeyDerivation(new Argon2idOptions(MemoryKiB: 1024, Iterations: 1, Parallelism: 1));
        var salt = Encoding.UTF8.GetBytes("sunfish-salt-0123456789ab");
        var derived = kdf.DeriveKey(Encoding.UTF8.GetBytes("user-password"), salt);

        IKeystore keystore = new InMemoryKeystore();
        await keystore.SetKeyAsync("sunfish-primary", derived, CancellationToken.None);

        var cached = await keystore.GetKeyAsync("sunfish-primary", CancellationToken.None);
        Assert.NotNull(cached);

        await using var store = new SqlCipherEncryptedStore();
        await store.OpenAsync(_dbPath, cached!.Value, CancellationToken.None);
        await store.SetAsync("hello", Encoding.UTF8.GetBytes("from keystore"), CancellationToken.None);
        Assert.Equal(
            Encoding.UTF8.GetBytes("from keystore"),
            await store.GetAsync("hello", CancellationToken.None));
    }

    [Fact]
    public async Task Raw_file_inspection_shows_no_plaintext_values()
    {
        // Paper §11.2 Layer 1 correctness test: physical storage extraction
        // without credentials yields no plaintext.
        const string needleKey = "credit-card";
        var needleValue = Encoding.UTF8.GetBytes("VERY_SECRET_PAYLOAD_4242424242424242");

        var store = await OpenNewAsync();
        await store.SetAsync(needleKey, needleValue, CancellationToken.None);
        await store.CloseAsync();
        await store.DisposeAsync();

        var fileBytes = File.ReadAllBytes(_dbPath);

        // The raw ciphertext must not contain the plaintext payload.
        Assert.False(ContainsSubsequence(fileBytes, needleValue),
            "Encrypted DB file contains the plaintext value — SQLCipher is not actually encrypting.");

        // It also must not contain the plaintext value as UTF-8 inside a longer scan.
        var asText = Encoding.UTF8.GetString(fileBytes);
        Assert.DoesNotContain("VERY_SECRET_PAYLOAD", asText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_on_unopened_store_throws()
    {
        var store = new SqlCipherEncryptedStore();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.GetAsync("anything", CancellationToken.None));
    }

    [Fact]
    public async Task Open_twice_without_close_throws()
    {
        await using var store = await OpenNewAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.OpenAsync(_dbPath, _key, CancellationToken.None));
    }

    private static bool ContainsSubsequence(byte[] haystack, byte[] needle)
    {
        if (needle.Length == 0 || needle.Length > haystack.Length) return false;
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match) return true;
        }

        return false;
    }
}
