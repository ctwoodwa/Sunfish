using System.Security.Cryptography;
using Sunfish.Foundation.LocalFirst.Encryption;

namespace Sunfish.Foundation.LocalFirst.Tests;

/// <summary>
/// Coverage for Phase 1 G6 task 5 — the SQLCipher key-rotation primitive
/// the recovery coordinator's <c>RecoveryCompleted</c> event wires against
/// per ADR 0046. Verifies that <see cref="IEncryptedStore.RotateKeyAsync"/>
/// re-encrypts the database under a new key, the new key opens the store,
/// the old key fails, and persisted data survives the rotation.
/// </summary>
public sealed class SqlCipherKeyRotationTests : IAsyncLifetime
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "sunfish-rekey-" + Guid.NewGuid().ToString("N"));
    private string _dbPath = null!;
    private byte[] _originalKey = null!;
    private byte[] _rotatedKey = null!;

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_dir);
        _dbPath = Path.Combine(_dir, "test.db");
        _originalKey = RandomNumberGenerator.GetBytes(32);
        _rotatedKey = RandomNumberGenerator.GetBytes(32);
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

    [Fact]
    public async Task RotateKey_reencrypts_database_new_key_opens_old_key_fails()
    {
        // Open under the original key, write a value.
        await using (var original = new SqlCipherEncryptedStore())
        {
            await original.OpenAsync(_dbPath, _originalKey, CancellationToken.None);
            await original.SetAsync("survives.rotation", new byte[] { 1, 2, 3 }, CancellationToken.None);

            // Rotate to the new key in-place.
            await original.RotateKeyAsync(_rotatedKey, CancellationToken.None);
        }

        // Re-open under the rotated key — must succeed and see the value.
        await using (var afterRotation = new SqlCipherEncryptedStore())
        {
            await afterRotation.OpenAsync(_dbPath, _rotatedKey, CancellationToken.None);
            var value = await afterRotation.GetAsync("survives.rotation", CancellationToken.None);
            Assert.Equal(new byte[] { 1, 2, 3 }, value);
        }

        // Re-open under the OLD key — must fail with InvalidKeyException
        // (proves the rotation actually re-encrypted the file, not just
        // updated a side-channel reference).
        await using var stale = new SqlCipherEncryptedStore();
        await Assert.ThrowsAsync<InvalidKeyException>(
            () => stale.OpenAsync(_dbPath, _originalKey, CancellationToken.None));
    }

    [Fact]
    public async Task RotateKey_preserves_all_data_across_rotation()
    {
        await using var store = new SqlCipherEncryptedStore();
        await store.OpenAsync(_dbPath, _originalKey, CancellationToken.None);

        // Write a representative mix of small + larger values across multiple keys.
        var payload1 = new byte[] { 0xAA, 0xBB, 0xCC };
        var payload2 = RandomNumberGenerator.GetBytes(2048);
        var payload3 = "string-roundtrip"u8.ToArray();
        await store.SetAsync("k.short", payload1, CancellationToken.None);
        await store.SetAsync("k.long", payload2, CancellationToken.None);
        await store.SetAsync("k.utf8", payload3, CancellationToken.None);

        await store.RotateKeyAsync(_rotatedKey, CancellationToken.None);

        // Same connection, post-rotate — values still readable.
        Assert.Equal(payload1, await store.GetAsync("k.short", CancellationToken.None));
        Assert.Equal(payload2, await store.GetAsync("k.long", CancellationToken.None));
        Assert.Equal(payload3, await store.GetAsync("k.utf8", CancellationToken.None));

        // List still returns prefix matches in order.
        var keys = await store.ListKeysAsync("k.", CancellationToken.None);
        Assert.Equal(new[] { "k.long", "k.short", "k.utf8" }, keys.ToArray());
    }

    [Fact]
    public async Task RotateKey_post_rotation_writes_persist_under_new_key()
    {
        await using (var store = new SqlCipherEncryptedStore())
        {
            await store.OpenAsync(_dbPath, _originalKey, CancellationToken.None);
            await store.RotateKeyAsync(_rotatedKey, CancellationToken.None);
            await store.SetAsync("written.after.rotate", new byte[] { 9 }, CancellationToken.None);
        }

        await using var reopen = new SqlCipherEncryptedStore();
        await reopen.OpenAsync(_dbPath, _rotatedKey, CancellationToken.None);
        var value = await reopen.GetAsync("written.after.rotate", CancellationToken.None);
        Assert.Equal(new byte[] { 9 }, value);
    }

    [Fact]
    public async Task RotateKey_rejects_empty_key()
    {
        await using var store = new SqlCipherEncryptedStore();
        await store.OpenAsync(_dbPath, _originalKey, CancellationToken.None);

        await Assert.ThrowsAsync<ArgumentException>(
            () => store.RotateKeyAsync(ReadOnlyMemory<byte>.Empty, CancellationToken.None));
    }

    [Fact]
    public async Task RotateKey_throws_when_store_not_open()
    {
        var store = new SqlCipherEncryptedStore();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => store.RotateKeyAsync(_rotatedKey, CancellationToken.None));
    }
}
