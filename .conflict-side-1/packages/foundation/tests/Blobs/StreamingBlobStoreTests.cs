using System.Security.Cryptography;
using Sunfish.Foundation.Blobs;

namespace Sunfish.Foundation.Tests.Blobs;

/// <summary>
/// Tests for <see cref="IBlobStore.PutStreamingAsync"/> and <see cref="FileSystemBlobStore"/>'s
/// streaming override. Includes a memory-profile guard to verify that large payloads are not
/// buffered in managed memory (G22 first pass).
/// </summary>
public class StreamingBlobStoreTests : IDisposable
{
    private readonly string _root;
    private readonly FileSystemBlobStore _store;

    public StreamingBlobStoreTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "sunfish-streaming-tests-" + Path.GetRandomFileName());
        _store = new FileSystemBlobStore(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public async Task PutStreamingAsync_CidMatchesPutAsync_ForSameBytes()
    {
        // Generate a deterministic 256 KiB payload.
        var payload = new byte[256 * 1024];
        new Random(42).NextBytes(payload);

        var cidFromBytes = await _store.PutAsync(payload);

        // Use a fresh store to avoid the idempotency short-circuit on the streaming path.
        var root2 = Path.Combine(Path.GetTempPath(), "sunfish-streaming-tests2-" + Path.GetRandomFileName());
        try
        {
            var store2 = new FileSystemBlobStore(root2);
            using var stream = new MemoryStream(payload, writable: false);
            var cidFromStream = await store2.PutStreamingAsync(stream);

            Assert.Equal(cidFromBytes, cidFromStream);
        }
        finally
        {
            if (Directory.Exists(root2))
            {
                Directory.Delete(root2, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PutStreamingAsync_IsIdempotent()
    {
        var payload = new byte[4096];
        new Random(7).NextBytes(payload);

        using var s1 = new MemoryStream(payload, writable: false);
        var cid1 = await _store.PutStreamingAsync(s1);

        using var s2 = new MemoryStream(payload, writable: false);
        var cid2 = await _store.PutStreamingAsync(s2);

        Assert.Equal(cid1, cid2);
        // Blob should still be retrievable.
        var retrieved = await _store.GetAsync(cid1);
        Assert.NotNull(retrieved);
        Assert.Equal(payload, retrieved!.Value.ToArray());
    }

    [Fact]
    public async Task PutStreamingAsync_RoundTrips_ViaGetAsync()
    {
        var payload = new byte[1024];
        RandomNumberGenerator.Fill(payload);

        using var stream = new MemoryStream(payload, writable: false);
        var cid = await _store.PutStreamingAsync(stream);

        var retrieved = await _store.GetAsync(cid);
        Assert.NotNull(retrieved);
        Assert.Equal(payload, retrieved!.Value.ToArray());
    }

    [Fact]
    public async Task PutStreamingAsync_ExistsLocally_AfterWrite()
    {
        var payload = new byte[512];
        new Random(99).NextBytes(payload);

        using var stream = new MemoryStream(payload, writable: false);
        var cid = await _store.PutStreamingAsync(stream);

        Assert.True(await _store.ExistsLocallyAsync(cid));
    }

    /// <summary>
    /// Writes 100 MiB of pseudo-random data via <see cref="IBlobStore.PutStreamingAsync"/> and
    /// asserts that the managed-memory delta for the current thread is well below the payload size.
    /// This is the key evidence that <see cref="FileSystemBlobStore"/> streams without buffering.
    /// </summary>
    [Fact]
    public async Task PutStreamingAsync_100MiB_DoesNotBufferPayloadInManagedMemory()
    {
        const int payloadBytes = 100 * 1024 * 1024; // 100 MiB
        const long memoryBudgetBytes = 4 * 1024 * 1024; // 4 MiB delta allowed

        // Allocate the payload up-front so the allocation itself is not attributed to the PUT.
        var payload = new byte[payloadBytes];
        new Random(1234).NextBytes(payload);

        GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true);

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();

        using var stream = new MemoryStream(payload, writable: false);
        var cid = await _store.PutStreamingAsync(stream);

        long allocAfter = GC.GetAllocatedBytesForCurrentThread();
        long delta = allocAfter - allocBefore;

        Assert.NotEqual(default, cid);
        Assert.True(
            delta < memoryBudgetBytes,
            $"Managed-memory delta during PutStreamingAsync was {delta:N0} bytes " +
            $"(budget: {memoryBudgetBytes:N0} bytes, payload: {payloadBytes:N0} bytes). " +
            "The implementation is likely buffering the full payload.");
    }

    [Fact]
    public async Task DimFallback_CidMatchesPutAsync()
    {
        // Test the DIM (default interface method) on a minimal IBlobStore stub to confirm
        // it delegates correctly and produces the same CID.
        var payload = new byte[8192];
        new Random(55).NextBytes(payload);

        IBlobStore stub = new DimOnlyBlobStore();
        using var stream = new MemoryStream(payload, writable: false);
        var cidFromStream = await stub.PutStreamingAsync(stream);

        var cidFromBytes = await stub.PutAsync(payload);
        Assert.Equal(cidFromBytes, cidFromStream);
    }

    /// <summary>
    /// A minimal <see cref="IBlobStore"/> that does NOT override <see cref="IBlobStore.PutStreamingAsync"/>,
    /// so the DIM path is exercised.
    /// </summary>
    private sealed class DimOnlyBlobStore : IBlobStore
    {
        private readonly Dictionary<Cid, byte[]> _store = new();

        public ValueTask<Cid> PutAsync(ReadOnlyMemory<byte> content, CancellationToken ct = default)
        {
            var cid = Cid.FromBytes(content.Span);
            _store[cid] = content.ToArray();
            return ValueTask.FromResult(cid);
        }

        public ValueTask<ReadOnlyMemory<byte>?> GetAsync(Cid cid, CancellationToken ct = default)
            => _store.TryGetValue(cid, out var bytes)
                ? ValueTask.FromResult<ReadOnlyMemory<byte>?>(bytes)
                : ValueTask.FromResult<ReadOnlyMemory<byte>?>(null);

        public ValueTask<bool> ExistsLocallyAsync(Cid cid, CancellationToken ct = default)
            => ValueTask.FromResult(_store.ContainsKey(cid));

        public ValueTask PinAsync(Cid cid, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask UnpinAsync(Cid cid, CancellationToken ct = default) => ValueTask.CompletedTask;
    }
}
