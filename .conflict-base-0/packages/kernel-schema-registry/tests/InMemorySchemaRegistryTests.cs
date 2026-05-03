using System.Text;

using Sunfish.Foundation.Blobs;
using Sunfish.Kernel.Schema;

// Alias the Schema record to disambiguate from the Sunfish.Kernel.Schema namespace
// when referenced as a bare identifier (e.g. List<Schema>). Without this alias, the
// compiler prefers the namespace name over the type in that position.
using SchemaRecord = Sunfish.Kernel.Schema.Schema;

namespace Sunfish.Kernel.SchemaRegistry.Tests;

/// <summary>
/// Core register / get / list / validate coverage for
/// <see cref="InMemorySchemaRegistry"/>. Each test spins up a fresh
/// registry backed by an in-memory blob store so register side-effects
/// are observable without filesystem state.
/// </summary>
public class InMemorySchemaRegistryTests
{
    private const string UserSchemaText = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "type": "object",
          "properties": {
            "name": { "type": "string" },
            "age":  { "type": "integer", "minimum": 0 }
          },
          "required": ["name"]
        }
        """;

    private static InMemorySchemaRegistry NewRegistry(out InMemoryBlobStore blobs)
    {
        blobs = new InMemoryBlobStore();
        return new InMemorySchemaRegistry(blobs);
    }

    [Fact]
    public async Task RegisterAsync_ValidJsonSchema_ReturnsSchemaWithCidContentAddress()
    {
        var registry = NewRegistry(out var blobs);

        var schema = await registry.RegisterAsync(UserSchemaText);

        // Id embeds the CID — self-identifying per spec §3.4.
        Assert.StartsWith("schema:", schema.Id.Value);
        Assert.Equal($"schema:{schema.ContentAddress.Value}", schema.Id.Value);

        // Blob store got the canonical bytes as a side-effect.
        Assert.True(await blobs.ExistsLocallyAsync(schema.ContentAddress));
    }

    [Fact]
    public async Task RegisterAsync_MalformedJsonSchema_ThrowsInvalidSchema()
    {
        var registry = NewRegistry(out _);

        // Not valid JSON at all — parser rejects it at canonicalization.
        await Assert.ThrowsAsync<InvalidSchemaException>(
            async () => await registry.RegisterAsync("{ this is not json"));
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        var registry = NewRegistry(out _);

        var result = await registry.GetAsync(new("schema:bogus"));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_RegisteredSchema_RoundTrips()
    {
        var registry = NewRegistry(out _);
        var registered = await registry.RegisterAsync(UserSchemaText, tags: new[] { "demo" });

        var fetched = await registry.GetAsync(registered.Id);

        Assert.NotNull(fetched);
        Assert.Equal(registered.Id, fetched!.Id);
        Assert.Equal(registered.ContentAddress, fetched.ContentAddress);
        Assert.Equal(registered.JsonSchemaText, fetched.JsonSchemaText);
        Assert.Contains("demo", fetched.Tags);
    }

    [Fact]
    public async Task ValidateAsync_DocumentMatchingSchema_ReturnsValid()
    {
        var registry = NewRegistry(out _);
        var schema = await registry.RegisterAsync(UserSchemaText);

        var doc = Encoding.UTF8.GetBytes("""{ "name": "Ada", "age": 36 }""");
        var result = await registry.ValidateAsync(schema.Id, doc);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_DocumentViolatingSchema_ReturnsErrors_WithJsonPointer()
    {
        var registry = NewRegistry(out _);
        var schema = await registry.RegisterAsync(UserSchemaText);

        // "name" is required and must be a string; age below minimum.
        var doc = Encoding.UTF8.GetBytes("""{ "age": -5 }""");
        var result = await registry.ValidateAsync(schema.Id, doc);

        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);

        // Every error carries a JSON-Pointer instance location. Pointers are
        // either "" (root) or start with "/" per RFC 6901 — verify shape
        // rather than the exact validator-specific message text.
        Assert.All(result.Errors, e =>
        {
            Assert.NotNull(e.JsonPointer);
            Assert.True(e.JsonPointer.Length == 0 || e.JsonPointer.StartsWith('/'),
                $"Unexpected pointer shape: '{e.JsonPointer}'");
            Assert.False(string.IsNullOrWhiteSpace(e.Message));
        });
    }

    [Fact]
    public async Task ValidateAsync_UnknownSchemaId_ThrowsSchemaNotFound()
    {
        // Choice documented in interface xmldoc: unknown id => SchemaNotFoundException
        // (rather than "valid with no errors"). The caller should never be asking
        // us to validate against a schema the registry doesn't know — that's a
        // programming error worth surfacing loudly.
        var registry = NewRegistry(out _);
        var doc = Encoding.UTF8.GetBytes("""{}""");

        await Assert.ThrowsAsync<SchemaNotFoundException>(
            async () => await registry.ValidateAsync(new("schema:nope"), doc));
    }

    [Fact]
    public async Task ListAsync_ReturnsRegisteredSchemas_OptionallyFilteredByTag()
    {
        var registry = NewRegistry(out _);

        var a = await registry.RegisterAsync(
            """{ "type": "string" }""",
            tags: new[] { "contact", "public" });
        var b = await registry.RegisterAsync(
            """{ "type": "integer" }""",
            tags: new[] { "metric" });
        var c = await registry.RegisterAsync(
            """{ "type": "boolean" }""",
            tags: new[] { "contact", "internal" });

        var all = await ToListAsync(registry.ListAsync());
        Assert.Equal(3, all.Count);
        Assert.Contains(all, s => s.Id == a.Id);
        Assert.Contains(all, s => s.Id == b.Id);
        Assert.Contains(all, s => s.Id == c.Id);

        var contacts = await ToListAsync(registry.ListAsync("contact"));
        Assert.Equal(2, contacts.Count);
        Assert.Contains(contacts, s => s.Id == a.Id);
        Assert.Contains(contacts, s => s.Id == c.Id);
        Assert.DoesNotContain(contacts, s => s.Id == b.Id);
    }

    private static async Task<List<SchemaRecord>> ToListAsync(IAsyncEnumerable<SchemaRecord> source)
    {
        var list = new List<SchemaRecord>();
        await foreach (var item in source)
        {
            list.Add(item);
        }
        return list;
    }

    /// <summary>Tiny in-memory <see cref="IBlobStore"/> for side-effect assertions.</summary>
    private sealed class InMemoryBlobStore : IBlobStore
    {
        private readonly Dictionary<Cid, byte[]> _blobs = new();

        public ValueTask<Cid> PutAsync(ReadOnlyMemory<byte> content, CancellationToken ct = default)
        {
            var cid = Cid.FromBytes(content.Span);
            _blobs[cid] = content.ToArray();
            return new ValueTask<Cid>(cid);
        }

        public ValueTask<ReadOnlyMemory<byte>?> GetAsync(Cid cid, CancellationToken ct = default)
        {
            return new ValueTask<ReadOnlyMemory<byte>?>(
                _blobs.TryGetValue(cid, out var bytes) ? bytes : (ReadOnlyMemory<byte>?)null);
        }

        public ValueTask<bool> ExistsLocallyAsync(Cid cid, CancellationToken ct = default)
            => new(_blobs.ContainsKey(cid));

        public ValueTask PinAsync(Cid cid, CancellationToken ct = default) => ValueTask.CompletedTask;
        public ValueTask UnpinAsync(Cid cid, CancellationToken ct = default) => ValueTask.CompletedTask;
    }
}
