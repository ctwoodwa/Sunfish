using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Kernel.Security.Tests;

/// <summary>
/// W#67 / ADR 0046-A6 — <see cref="KeystoreRootSeedProvider"/>'s
/// <see cref="IRootSeedRestorer"/> path tests. Verifies that a restored
/// seed lands in the keystore and that the in-process cache is
/// invalidated so the next <see cref="IRootSeedProvider.GetRootSeedAsync"/>
/// observes the restored value.
/// </summary>
public sealed class KeystoreRootSeedRestorerTests
{
    [Fact]
    public async Task RestoreRootSeedAsync_writes_to_keystore_slot()
    {
        var keystore = new InMemoryKeystore();
        var sut = new KeystoreRootSeedProvider(keystore);
        var restored = new byte[KeystoreRootSeedProvider.SeedLength];
        for (var i = 0; i < restored.Length; i++) restored[i] = (byte)(i + 1);

        await sut.RestoreRootSeedAsync(restored, default);

        var stored = await keystore.GetKeyAsync(KeystoreRootSeedProvider.SlotName, default);
        Assert.NotNull(stored);
        Assert.Equal(restored, stored.Value.ToArray());
    }

    [Fact]
    public async Task GetRootSeedAsync_after_restore_returns_restored_seed()
    {
        // Even if the provider had previously materialized a seed (RNG draw
        // on the first GetRootSeedAsync call), the restore must invalidate
        // the cache so the next call returns the restored value rather
        // than the pre-recovery cached buffer.
        var keystore = new InMemoryKeystore();
        var sut = new KeystoreRootSeedProvider(keystore);

        var initial = await sut.GetRootSeedAsync(default);
        Assert.Equal(KeystoreRootSeedProvider.SeedLength, initial.Length);

        var restored = new byte[KeystoreRootSeedProvider.SeedLength];
        for (var i = 0; i < restored.Length; i++) restored[i] = 0xAB;
        await sut.RestoreRootSeedAsync(restored, default);

        var afterRestore = await sut.GetRootSeedAsync(default);
        Assert.Equal(restored, afterRestore.ToArray());
        Assert.NotEqual(initial.ToArray(), afterRestore.ToArray());
    }

    [Fact]
    public async Task RestoreRootSeedAsync_rejects_wrong_length_seed()
    {
        var sut = new KeystoreRootSeedProvider(new InMemoryKeystore());
        var tooShort = new byte[KeystoreRootSeedProvider.SeedLength - 1];

        await Assert.ThrowsAsync<ArgumentException>(
            () => sut.RestoreRootSeedAsync(tooShort, default));
    }

    [Fact]
    public async Task RestoreRootSeedAsync_is_idempotent_on_repeat_call()
    {
        var keystore = new InMemoryKeystore();
        var sut = new KeystoreRootSeedProvider(keystore);
        var restored = new byte[KeystoreRootSeedProvider.SeedLength];
        for (var i = 0; i < restored.Length; i++) restored[i] = 0x55;

        await sut.RestoreRootSeedAsync(restored, default);
        await sut.RestoreRootSeedAsync(restored, default);

        var stored = await keystore.GetKeyAsync(KeystoreRootSeedProvider.SlotName, default);
        Assert.NotNull(stored);
        Assert.Equal(restored, stored.Value.ToArray());
    }

    [Fact]
    public async Task RestoreRootSeedAsync_defensively_copies_input_buffer()
    {
        var keystore = new InMemoryKeystore();
        var sut = new KeystoreRootSeedProvider(keystore);
        var buffer = new byte[KeystoreRootSeedProvider.SeedLength];
        for (var i = 0; i < buffer.Length; i++) buffer[i] = 0x11;

        await sut.RestoreRootSeedAsync(buffer, default);
        // Caller mutates the buffer after the call — must not affect what
        // was persisted (defensive ToArray() copy inside RestoreRootSeedAsync).
        for (var i = 0; i < buffer.Length; i++) buffer[i] = 0x22;

        var stored = await keystore.GetKeyAsync(KeystoreRootSeedProvider.SlotName, default);
        Assert.NotNull(stored);
        Assert.All(stored.Value.ToArray(), b => Assert.Equal(0x11, b));
    }
}
