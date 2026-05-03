using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Kernel.Security.Tests;

/// <summary>
/// Exercises <see cref="KeystoreRootSeedProvider"/> against an
/// <see cref="InMemoryKeystore"/>. Covers first-call generation, subsequent
/// reads, in-process caching, concurrent first-call dedupe, and corruption
/// recovery when an existing slot has the wrong length.
/// </summary>
public sealed class KeystoreRootSeedProviderTests
{
    [Fact]
    public async Task First_resolve_generates_and_stores()
    {
        var keystore = new InMemoryKeystore();
        var sut = new KeystoreRootSeedProvider(keystore);

        var seed = await sut.GetRootSeedAsync(CancellationToken.None);

        Assert.Equal(KeystoreRootSeedProvider.SeedLength, seed.Length);

        var stored = await keystore.GetKeyAsync(KeystoreRootSeedProvider.SlotName, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal(seed.ToArray(), stored!.Value.ToArray());

        // Must not be all-zero — that was the failure mode of the old stub.
        Assert.Contains(seed.ToArray(), b => b != 0);
    }

    [Fact]
    public async Task Second_resolve_reads_from_store()
    {
        var keystore = new InMemoryKeystore();
        var known = new byte[KeystoreRootSeedProvider.SeedLength];
        for (var i = 0; i < known.Length; i++)
        {
            known[i] = (byte)(i + 1);
        }
        await keystore.SetKeyAsync(KeystoreRootSeedProvider.SlotName, known, CancellationToken.None);

        var sut = new KeystoreRootSeedProvider(keystore);
        var seed = await sut.GetRootSeedAsync(CancellationToken.None);

        Assert.Equal(known, seed.ToArray());
    }

    [Fact]
    public async Task Cached_between_calls()
    {
        var inner = new InMemoryKeystore();
        var counting = new CountingKeystore(inner);
        var sut = new KeystoreRootSeedProvider(counting);

        var first = await sut.GetRootSeedAsync(CancellationToken.None);
        for (var i = 0; i < 9; i++)
        {
            var again = await sut.GetRootSeedAsync(CancellationToken.None);
            Assert.Equal(first.ToArray(), again.ToArray());
        }

        Assert.Equal(1, counting.GetCalls);
        // Fresh slot on the first call → one SetKeyAsync; all subsequent calls
        // must be served from the in-process cache.
        Assert.Equal(1, counting.SetCalls);
    }

    [Fact]
    public async Task Concurrent_first_calls_dedupe()
    {
        var inner = new InMemoryKeystore();
        var counting = new CountingKeystore(inner);
        var sut = new KeystoreRootSeedProvider(counting);

        var tasks = new Task<ReadOnlyMemory<byte>>[10];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = sut.GetRootSeedAsync(CancellationToken.None).AsTask();
        }

        var results = await Task.WhenAll(tasks);

        // All concurrent callers must observe the same seed.
        var first = results[0].ToArray();
        foreach (var r in results)
        {
            Assert.Equal(first, r.ToArray());
        }

        // The Lazy<Task<T>> guard means exactly one Resolve runs, which means
        // exactly one SetKeyAsync (and exactly one GetKeyAsync) hit the store.
        Assert.Equal(1, counting.SetCalls);
        Assert.Equal(1, counting.GetCalls);
    }

    [Fact]
    public async Task Empty_or_wrong_length_existing_slot_regenerates()
    {
        var keystore = new InMemoryKeystore();
        var stub = new byte[16]; // wrong length — 16 bytes instead of 32
        stub[0] = 0xAB;
        await keystore.SetKeyAsync(KeystoreRootSeedProvider.SlotName, stub, CancellationToken.None);

        var sut = new KeystoreRootSeedProvider(keystore);
        var seed = await sut.GetRootSeedAsync(CancellationToken.None);

        Assert.Equal(KeystoreRootSeedProvider.SeedLength, seed.Length);
        Assert.NotEqual(stub, seed.ToArray());

        // The slot must now hold the regenerated 32-byte value.
        var restored = await keystore.GetKeyAsync(KeystoreRootSeedProvider.SlotName, CancellationToken.None);
        Assert.NotNull(restored);
        Assert.Equal(KeystoreRootSeedProvider.SeedLength, restored!.Value.Length);
        Assert.Equal(seed.ToArray(), restored.Value.ToArray());
    }

    /// <summary>
    /// Test double that wraps <see cref="InMemoryKeystore"/> and counts
    /// <see cref="IKeystore.GetKeyAsync"/> / <see cref="IKeystore.SetKeyAsync"/>
    /// invocations so tests can assert on the provider's caching and dedupe
    /// behaviour.
    /// </summary>
    private sealed class CountingKeystore : IKeystore
    {
        private readonly InMemoryKeystore _inner;
        private int _getCalls;
        private int _setCalls;

        public CountingKeystore(InMemoryKeystore inner) => _inner = inner;

        public int GetCalls => Volatile.Read(ref _getCalls);
        public int SetCalls => Volatile.Read(ref _setCalls);

        public Task<ReadOnlyMemory<byte>?> GetKeyAsync(string name, CancellationToken ct)
        {
            Interlocked.Increment(ref _getCalls);
            return _inner.GetKeyAsync(name, ct);
        }

        public Task SetKeyAsync(string name, ReadOnlyMemory<byte> key, CancellationToken ct)
        {
            Interlocked.Increment(ref _setCalls);
            return _inner.SetKeyAsync(name, key, ct);
        }

        public Task DeleteKeyAsync(string name, CancellationToken ct)
            => _inner.DeleteKeyAsync(name, ct);
    }
}
