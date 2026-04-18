using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;

namespace Sunfish.Foundation.Tests.Assets.Entities;

public sealed class InMemoryEntityStoreTests
{
    private static readonly SchemaId Schema = new("property.v1");

    private static JsonDocument Body(string json) => JsonDocument.Parse(json);

    private static CreateOptions Opts(string nonce, string issuer = "alice", string authority = "acme")
        => new("property", authority, nonce, new ActorId(issuer), TenantId.Default);

    private static InMemoryEntityStore NewStore(out InMemoryAssetStorage storage)
    {
        storage = new InMemoryAssetStorage();
        return new InMemoryEntityStore(storage);
    }

    [Fact]
    public async Task CreateAsync_ReturnsIdempotentId_OnSameNonceAndSchemaAndBody()
    {
        var store = NewStore(out _);
        using var body = Body("""{"name":"Unit 3B"}""");
        var id1 = await store.CreateAsync(Schema, body, Opts("n-1"));
        var id2 = await store.CreateAsync(Schema, body, Opts("n-1"));
        Assert.Equal(id1, id2);
    }

    [Fact]
    public async Task CreateAsync_ThrowsIdempotencyConflict_OnDifferentBodyWithSameNonce()
    {
        var store = NewStore(out _);
        using var body1 = Body("""{"name":"Unit 3B"}""");
        using var body2 = Body("""{"name":"Unit 3C"}""");
        await store.CreateAsync(Schema, body1, Opts("n-1"));
        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            store.CreateAsync(Schema, body2, Opts("n-1")));
    }

    [Fact]
    public async Task CreateAsync_GeneratesDeterministicId_FromSchemaNonceIssuer()
    {
        var store1 = NewStore(out _);
        var store2 = NewStore(out _);
        using var b1 = Body("""{"x":1}""");
        using var b2 = Body("""{"y":2}""");
        var id1 = await store1.CreateAsync(Schema, b1, Opts("n-same"));
        var id2 = await store2.CreateAsync(Schema, b2, Opts("n-same"));
        // Same schema/authority/nonce/issuer → same entity id even across stores.
        Assert.Equal(id1, id2);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_OnUnknownId()
    {
        var store = NewStore(out _);
        var result = await store.GetAsync(new EntityId("property", "acme", "nope"));
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_ReturnsLatestByDefault()
    {
        var store = NewStore(out _);
        using var b1 = Body("""{"v":1}""");
        var id = await store.CreateAsync(Schema, b1, Opts("n"));
        using var b2 = Body("""{"v":2}""");
        await store.UpdateAsync(id, b2, new UpdateOptions(new ActorId("bob")));

        var ent = await store.GetAsync(id);
        Assert.NotNull(ent);
        Assert.Equal(2, ent!.CurrentVersion.Sequence);
        using var round = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(ent.Body.RootElement));
        Assert.Equal(2, round.RootElement.GetProperty("v").GetInt32());
    }

    [Fact]
    public async Task GetAsync_ReturnsExplicitVersion_WhenSpecified()
    {
        var store = NewStore(out _);
        using var b1 = Body("""{"v":1}""");
        var id = await store.CreateAsync(Schema, b1, Opts("n"));
        using var b2 = Body("""{"v":2}""");
        await store.UpdateAsync(id, b2, new UpdateOptions(new ActorId("bob")));

        var ent = await store.GetAsync(id, new VersionSelector(ExplicitSequence: 1));
        Assert.NotNull(ent);
        Assert.Equal(1, ent!.CurrentVersion.Sequence);
    }

    [Fact]
    public async Task GetAsync_ReturnsAsOfVersion_WhenAsOfSpecified()
    {
        var store = NewStore(out _);
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t1 = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

        using var b1 = Body("""{"v":1}""");
        var id = await store.CreateAsync(Schema, b1, new CreateOptions("property", "acme", "n", new ActorId("a"), TenantId.Default, ValidFrom: t0));
        using var b2 = Body("""{"v":2}""");
        await store.UpdateAsync(id, b2, new UpdateOptions(new ActorId("b"), ValidFrom: t1));

        var mid = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var atMid = await store.GetAsync(id, new VersionSelector(AsOf: mid));
        Assert.NotNull(atMid);
        Assert.Equal(1, atMid!.CurrentVersion.Sequence);
    }

    [Fact]
    public async Task UpdateAsync_AppendsNewVersion_AndIncrementsSequence()
    {
        var store = NewStore(out var storage);
        using var b1 = Body("""{"v":1}""");
        var id = await store.CreateAsync(Schema, b1, Opts("n"));
        using var b2 = Body("""{"v":2}""");
        var v2 = await store.UpdateAsync(id, b2, new UpdateOptions(new ActorId("b")));
        Assert.Equal(2, v2.Sequence);
        Assert.Equal(2, storage.Versions[id].Count);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesCurrentBody()
    {
        var store = NewStore(out var storage);
        using var b1 = Body("""{"floor":10}""");
        var id = await store.CreateAsync(Schema, b1, Opts("n"));
        using var b2 = Body("""{"floor":12}""");
        await store.UpdateAsync(id, b2, new UpdateOptions(new ActorId("corrector")));

        Assert.Contains("\"floor\":12", storage.Entities[id].BodyJson);
    }

    [Fact]
    public async Task UpdateAsync_ThrowsConcurrencyException_OnOptimisticLockFailure()
    {
        var store = NewStore(out _);
        using var b1 = Body("""{"v":1}""");
        var id = await store.CreateAsync(Schema, b1, Opts("n"));
        using var b2 = Body("""{"v":2}""");
        await store.UpdateAsync(id, b2, new UpdateOptions(new ActorId("bob")));

        // Try with stale expected version (the v1 VersionId we never captured, so construct an obviously wrong one).
        var stale = new VersionId(id, 1, "deadbeef");
        using var b3 = Body("""{"v":3}""");
        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            store.UpdateAsync(id, b3, new UpdateOptions(new ActorId("bob"), ExpectedVersion: stale)));
    }

    [Fact]
    public async Task DeleteAsync_InsertsTombstoneAndMakesGetReturnNullForLatest()
    {
        var store = NewStore(out var storage);
        using var b = Body("""{"v":1}""");
        var id = await store.CreateAsync(Schema, b, Opts("n"));
        await store.DeleteAsync(id, new DeleteOptions(new ActorId("a")));

        var latest = await store.GetAsync(id);
        Assert.Null(latest);
        Assert.Equal(2, storage.Versions[id].Count);
        Assert.NotNull(storage.Entities[id].DeletedAt);
    }

    [Fact]
    public async Task DeleteAsync_StillReturnsEntity_ForAsOfBeforeDelete()
    {
        var store = NewStore(out _);
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t1 = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

        using var b = Body("""{"v":1}""");
        var id = await store.CreateAsync(Schema, b, new CreateOptions("property", "acme", "n", new ActorId("a"), TenantId.Default, ValidFrom: t0));
        await store.DeleteAsync(id, new DeleteOptions(new ActorId("a"), ValidFrom: t1));

        var atMid = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var ent = await store.GetAsync(id, new VersionSelector(AsOf: atMid));
        Assert.NotNull(ent);
    }

    [Fact]
    public async Task QueryAsync_FiltersByTenant()
    {
        var store = NewStore(out _);
        using var b = Body("""{"x":1}""");
        await store.CreateAsync(Schema, b, new CreateOptions("property", "a", "n1", new ActorId("a"), new TenantId("t1")));
        using var b2 = Body("""{"x":2}""");
        await store.CreateAsync(Schema, b2, new CreateOptions("property", "a", "n2", new ActorId("a"), new TenantId("t2")));

        var results = new List<Entity>();
        await foreach (var e in store.QueryAsync(new EntityQuery(Tenant: new TenantId("t1"))))
            results.Add(e);
        Assert.Single(results);
        Assert.Equal(new TenantId("t1"), results[0].Tenant);
    }

    [Fact]
    public async Task QueryAsync_FiltersBySchema()
    {
        var store = NewStore(out _);
        using var b = Body("""{"x":1}""");
        await store.CreateAsync(new SchemaId("schema.A"), b, Opts("n1"));
        using var b2 = Body("""{"x":2}""");
        await store.CreateAsync(new SchemaId("schema.B"), b2, Opts("n2"));

        var results = new List<Entity>();
        await foreach (var e in store.QueryAsync(new EntityQuery(Schema: new SchemaId("schema.A"))))
            results.Add(e);
        Assert.Single(results);
    }

    [Fact]
    public async Task QueryAsync_ExcludesDeleted_ByDefault()
    {
        var store = NewStore(out _);
        using var b = Body("""{"v":1}""");
        var id = await store.CreateAsync(Schema, b, Opts("n"));
        await store.DeleteAsync(id, new DeleteOptions(new ActorId("a")));

        var results = new List<Entity>();
        await foreach (var e in store.QueryAsync(new EntityQuery())) results.Add(e);
        Assert.Empty(results);
    }

    [Fact]
    public async Task QueryAsync_IncludesDeleted_WhenRequested()
    {
        var store = NewStore(out _);
        using var b = Body("""{"v":1}""");
        var id = await store.CreateAsync(Schema, b, Opts("n"));
        await store.DeleteAsync(id, new DeleteOptions(new ActorId("a")));

        var results = new List<Entity>();
        await foreach (var e in store.QueryAsync(new EntityQuery(IncludeDeleted: true))) results.Add(e);
        Assert.Single(results);
    }

    [Fact]
    public async Task CreateAsync_SupportsExplicitLocalPartOverride()
    {
        var store = NewStore(out _);
        using var b = Body("""{"x":1}""");
        var id = await store.CreateAsync(Schema, b,
            new CreateOptions("property", "acme", "nonce", new ActorId("alice"), TenantId.Default, ExplicitLocalPart: "building-42"));
        Assert.Equal("building-42", id.LocalPart);
    }
}
