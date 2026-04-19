using Sunfish.Foundation.Blobs;
using Sunfish.Kernel.Schema;

namespace Sunfish.Kernel.SchemaRegistry.Tests;

/// <summary>
/// Tests for G20 — per-schema blob-boundary override (spec §3.4,
/// <c>x-sunfish.blobThreshold</c>). Covers:
/// <list type="bullet">
///   <item>Default value is null when no override is supplied.</item>
///   <item>Override round-trips through RegisterAsync → GetAsync.</item>
///   <item>Zero / negative overrides are rejected with ArgumentException at the
///       registry layer (not deep in ingestion).</item>
///   <item>Idempotent re-registration returns the original schema (first-write-wins).</item>
/// </list>
/// </summary>
public class BlobThresholdTests
{
    private static InMemorySchemaRegistry NewRegistry()
        => new(new NoopBlobStore());

    private const string SimpleSchemaText = """{ "type": "object" }""";

    // ------------------------------------------------------------------ //
    //  Descriptor field — Schema record                                   //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task RegisterAsync_WithoutBlobThreshold_SchemaHasNullThreshold()
    {
        var registry = NewRegistry();

        var schema = await registry.RegisterAsync(SimpleSchemaText);

        Assert.Null(schema.BlobThreshold);
    }

    [Fact]
    public async Task RegisterAsync_WithBlobThreshold_SchemaRoundTripsValue()
    {
        var registry = NewRegistry();
        const int threshold = 128 * 1024; // 128 KiB

        var schema = await registry.RegisterAsync(SimpleSchemaText, blobThreshold: threshold);

        Assert.Equal(threshold, schema.BlobThreshold);
    }

    [Fact]
    public async Task GetAsync_AfterRegisterWithThreshold_ReturnsThreshold()
    {
        var registry = NewRegistry();
        const int threshold = 32 * 1024; // 32 KiB

        var registered = await registry.RegisterAsync(SimpleSchemaText, blobThreshold: threshold);
        var fetched = await registry.GetAsync(registered.Id);

        Assert.NotNull(fetched);
        Assert.Equal(threshold, fetched!.BlobThreshold);
    }

    // ------------------------------------------------------------------ //
    //  Validation — reject invalid threshold values at registry layer     //
    // ------------------------------------------------------------------ //

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-1024)]
    public async Task RegisterAsync_WithNonPositiveThreshold_ThrowsArgumentException(int badValue)
    {
        var registry = NewRegistry();

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            async () => await registry.RegisterAsync(SimpleSchemaText, blobThreshold: badValue));

        Assert.Contains("blobThreshold", ex.Message);
        Assert.Contains(badValue.ToString(), ex.Message);
    }

    [Fact]
    public async Task RegisterAsync_WithPositiveThreshold_DoesNotThrow()
    {
        var registry = NewRegistry();

        // Should not throw
        var schema = await registry.RegisterAsync(SimpleSchemaText, blobThreshold: 1);

        Assert.Equal(1, schema.BlobThreshold);
    }

    // ------------------------------------------------------------------ //
    //  Idempotence — first registration wins                              //
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task RegisterAsync_SameSchemaWithDifferentThreshold_FirstWins()
    {
        // The content-address is determined by the schema text, not the metadata.
        // Two registrations of the same schema text with different thresholds must
        // be idempotent (GetOrAdd semantics); the first threshold wins.
        var registry = NewRegistry();

        var first = await registry.RegisterAsync(SimpleSchemaText, blobThreshold: 100_000);
        var second = await registry.RegisterAsync(SimpleSchemaText, blobThreshold: 200_000);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(100_000, second.BlobThreshold); // first registration's value
    }

    // ------------------------------------------------------------------ //
    //  Minimal no-op blob store                                           //
    // ------------------------------------------------------------------ //

    private sealed class NoopBlobStore : IBlobStore
    {
        public ValueTask<Cid> PutAsync(ReadOnlyMemory<byte> content, CancellationToken ct = default)
            => new(Cid.FromBytes(content.Span));
        public ValueTask<ReadOnlyMemory<byte>?> GetAsync(Cid cid, CancellationToken ct = default)
            => new((ReadOnlyMemory<byte>?)null);
        public ValueTask<bool> ExistsLocallyAsync(Cid cid, CancellationToken ct = default) => new(false);
        public ValueTask PinAsync(Cid cid, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask UnpinAsync(Cid cid, CancellationToken ct = default) => ValueTask.CompletedTask;
    }
}
