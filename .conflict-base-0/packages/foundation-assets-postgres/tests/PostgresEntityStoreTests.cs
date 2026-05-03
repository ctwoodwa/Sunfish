using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;
using Sunfish.Foundation.Assets.Postgres.Entities;

namespace Sunfish.Foundation.Assets.Postgres.Tests;

public sealed class PostgresEntityStoreTests : IClassFixture<PostgresAssetStoreFixture>
{
    private readonly PostgresAssetStoreFixture _fixture;

    public PostgresEntityStoreTests(PostgresAssetStoreFixture fixture)
    {
        _fixture = fixture;
    }

    private PostgresEntityStore NewStore() => new(_fixture.CreateFactory());

    private static JsonDocument Body(string json) => JsonDocument.Parse(json);

    private static CreateOptions Opts(string scheme, string authority, string nonce, string issuer = "alice")
        => new(scheme, authority, nonce, new ActorId(issuer), TenantId.Default);

    [Fact]
    public async Task CreateAsync_RoundTrips_BodyAndMetadata()
    {
        var store = NewStore();
        using var body = Body("""{"name":"Unit 3B","floors":2}""");
        var id = await store.CreateAsync(new SchemaId("prop.v1"), body, Opts("entity-rt", "acme", "n-1"));

        var entity = await store.GetAsync(id);
        Assert.NotNull(entity);
        Assert.Equal(1, entity!.CurrentVersion.Sequence);
        Assert.Equal("prop.v1", entity.Schema.Value);
        Assert.Equal("entity-rt", id.Scheme);
        Assert.Equal("acme", id.Authority);
        Assert.Null(entity.DeletedAt);

        using var doc = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(entity.Body.RootElement));
        Assert.Equal("Unit 3B", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal(2, doc.RootElement.GetProperty("floors").GetInt32());
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_OnUnknownId()
    {
        var store = NewStore();
        var id = new EntityId("entity-missing", "acme", "does-not-exist");
        var entity = await store.GetAsync(id);
        Assert.Null(entity);
    }

    [Fact]
    public async Task UpdateAsync_AppendsVersion_AndAdvancesTip()
    {
        var store = NewStore();
        using var b1 = Body("""{"v":1}""");
        var id = await store.CreateAsync(new SchemaId("p.v1"), b1, Opts("entity-upd", "acme", "u-1"));

        using var b2 = Body("""{"v":2}""");
        var v2 = await store.UpdateAsync(id, b2, new UpdateOptions(new ActorId("bob")));

        Assert.Equal(2, v2.Sequence);

        var entity = await store.GetAsync(id);
        Assert.NotNull(entity);
        Assert.Equal(2, entity!.CurrentVersion.Sequence);
        using var roundTrip = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(entity.Body.RootElement));
        Assert.Equal(2, roundTrip.RootElement.GetProperty("v").GetInt32());
    }

    [Fact]
    public async Task UpdateAsync_WithWrongExpectedVersion_RaisesConcurrencyException()
    {
        var store = NewStore();
        using var b1 = Body("""{"v":1}""");
        var id = await store.CreateAsync(new SchemaId("p.v1"), b1, Opts("entity-conc", "acme", "c-1"));

        // Advance the tip with a successful update.
        using var b2 = Body("""{"v":2}""");
        await store.UpdateAsync(id, b2, new UpdateOptions(new ActorId("bob")));

        // Stale expected-version should fail.
        using var b3 = Body("""{"v":3}""");
        var stale = new VersionId(id, Sequence: 1, Hash: "deadbeef");
        await Assert.ThrowsAsync<ConcurrencyException>(() =>
            store.UpdateAsync(id, b3, new UpdateOptions(new ActorId("carol"), ExpectedVersion: stale)));
    }

    [Fact]
    public async Task DeleteAsync_Tombstones_DefaultGetReturnsNull_ButHistoryRemains()
    {
        var store = NewStore();
        using var b1 = Body("""{"v":1}""");
        var id = await store.CreateAsync(new SchemaId("p.v1"), b1, Opts("entity-del", "acme", "d-1"));
        var mintedAt = DateTimeOffset.UtcNow;

        await Task.Delay(10);
        await store.DeleteAsync(id, new DeleteOptions(new ActorId("bob")));

        // Default read sees the tombstone → null.
        var defaultRead = await store.GetAsync(id);
        Assert.Null(defaultRead);

        // As-of read before the delete still returns the live body.
        var asOfRead = await store.GetAsync(id, VersionSelector.AtInstant(mintedAt));
        Assert.NotNull(asOfRead);
        Assert.Equal(1, asOfRead!.CurrentVersion.Sequence);
    }
}
