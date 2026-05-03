using Sunfish.Foundation.Blobs;
using Sunfish.Kernel.Schema;

namespace Sunfish.Kernel.SchemaRegistry.Tests;

/// <summary>
/// Proves the content-addressing invariant that makes schema federation work:
/// structurally-equal JSON Schemas must produce the same CID regardless of
/// whitespace or key order. If this test regresses, federation peers will
/// diverge on schema ids when they ingest the "same" schema through
/// different text pipelines.
/// </summary>
public class ContentAddressingTests
{
    private static InMemorySchemaRegistry NewRegistry()
        => new(new NoopBlobStore());

    [Fact]
    public async Task RegisterAsync_SameSchemaText_ProducesSameCid()
    {
        var registry = NewRegistry();
        const string text = """{ "type": "string", "maxLength": 100 }""";

        var first = await registry.RegisterAsync(text);
        var second = await registry.RegisterAsync(text);

        Assert.Equal(first.ContentAddress, second.ContentAddress);
        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task RegisterAsync_DifferentWhitespaceSameStructure_ProducesSameCid()
    {
        var registry = NewRegistry();

        // Same structural schema, three surface forms:
        //  - minified, keys in order
        //  - pretty-printed, keys in order
        //  - pretty-printed, keys reversed
        const string minified = """{"type":"string","maxLength":100}""";
        const string pretty = """
            {
              "type": "string",
              "maxLength": 100
            }
            """;
        const string reorderedPretty = """
            {
              "maxLength": 100,
              "type": "string"
            }
            """;

        var a = await registry.RegisterAsync(minified);
        var b = await registry.RegisterAsync(pretty);
        var c = await registry.RegisterAsync(reorderedPretty);

        // Canonicalization collapses whitespace AND sorts keys, so all three
        // resolve to a single content address / schema id.
        Assert.Equal(a.ContentAddress, b.ContentAddress);
        Assert.Equal(a.ContentAddress, c.ContentAddress);
        Assert.Equal(a.Id, c.Id);
    }

    /// <summary>Minimal blob store that satisfies the interface without recording anything.</summary>
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
