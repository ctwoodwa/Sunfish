using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;
using Sunfish.Foundation.Assets.Postgres.Entities;
using Sunfish.Foundation.Assets.Postgres.Versions;
using Sunfish.Foundation.Assets.Versions;
using Version = Sunfish.Foundation.Assets.Versions.Version;

namespace Sunfish.Foundation.Assets.Postgres.Tests;

public sealed class PostgresVersionStoreTests : IClassFixture<PostgresAssetStoreFixture>
{
    private readonly PostgresAssetStoreFixture _fixture;

    public PostgresVersionStoreTests(PostgresAssetStoreFixture fixture)
    {
        _fixture = fixture;
    }

    private PostgresEntityStore NewEntityStore() => new(_fixture.CreateFactory());
    private PostgresVersionStore NewVersionStore() => new(_fixture.CreateFactory());

    private static CreateOptions Opts(string scheme, string nonce, string issuer = "alice")
        => new(scheme, "acme", nonce, new ActorId(issuer), TenantId.Default);

    [Fact]
    public async Task GetVersionAsync_ReturnsVersion_ByVersionId()
    {
        var entities = NewEntityStore();
        var versions = NewVersionStore();

        using var b1 = JsonDocument.Parse("""{"v":1}""");
        var id = await entities.CreateAsync(new SchemaId("p.v1"), b1, Opts("ver-get", "g-1"));
        using var b2 = JsonDocument.Parse("""{"v":2}""");
        var v2 = await entities.UpdateAsync(id, b2, new UpdateOptions(new ActorId("bob")));

        var fetched = await versions.GetVersionAsync(v2);
        Assert.NotNull(fetched);
        Assert.Equal(2, fetched!.Id.Sequence);
        Assert.Equal(v2.Hash, fetched.Id.Hash);
        Assert.NotNull(fetched.ParentId);
        Assert.Equal(1, fetched.ParentId!.Value.Sequence);
    }

    [Fact]
    public async Task GetHistoryAsync_StreamsAllVersionsInSequenceOrder()
    {
        var entities = NewEntityStore();
        var versions = NewVersionStore();

        using var b1 = JsonDocument.Parse("""{"v":1}""");
        var id = await entities.CreateAsync(new SchemaId("p.v1"), b1, Opts("ver-hist", "h-1"));
        using var b2 = JsonDocument.Parse("""{"v":2}""");
        await entities.UpdateAsync(id, b2, new UpdateOptions(new ActorId("bob")));
        using var b3 = JsonDocument.Parse("""{"v":3}""");
        await entities.UpdateAsync(id, b3, new UpdateOptions(new ActorId("carol")));

        var history = new List<Version>();
        await foreach (var v in versions.GetHistoryAsync(id))
            history.Add(v);

        Assert.Equal(3, history.Count);
        Assert.Equal(new[] { 1, 2, 3 }, history.Select(h => h.Id.Sequence));
        // Chain: each parent id matches the prior sequence / hash.
        Assert.Null(history[0].ParentId);
        Assert.Equal(history[0].Id, history[1].ParentId);
        Assert.Equal(history[1].Id, history[2].ParentId);
    }

    [Fact]
    public async Task GetAsOfAsync_ReturnsCorrectVersionForInstant()
    {
        var entities = NewEntityStore();
        var versions = NewVersionStore();

        using var b1 = JsonDocument.Parse("""{"v":1}""");
        var id = await entities.CreateAsync(new SchemaId("p.v1"), b1, Opts("ver-asof", "a-1"));
        var afterV1 = DateTimeOffset.UtcNow;
        await Task.Delay(10);

        using var b2 = JsonDocument.Parse("""{"v":2}""");
        var v2Id = await entities.UpdateAsync(id, b2, new UpdateOptions(new ActorId("bob")));

        var atV1 = await versions.GetAsOfAsync(id, afterV1);
        Assert.NotNull(atV1);
        Assert.Equal(1, atV1!.Id.Sequence);

        var latestInstant = DateTimeOffset.UtcNow.AddSeconds(1);
        var atLatest = await versions.GetAsOfAsync(id, latestInstant);
        Assert.NotNull(atLatest);
        Assert.Equal(2, atLatest!.Id.Sequence);
        Assert.Equal(v2Id.Hash, atLatest.Id.Hash);
    }

    [Fact]
    public async Task GetVersionAsync_ReturnsNull_WhenHashDoesNotMatch()
    {
        var entities = NewEntityStore();
        var versions = NewVersionStore();

        using var b = JsonDocument.Parse("""{"v":1}""");
        var id = await entities.CreateAsync(new SchemaId("p.v1"), b, Opts("ver-miss", "m-1"));

        var bogus = new VersionId(id, Sequence: 1, Hash: "not-the-real-hash");
        var fetched = await versions.GetVersionAsync(bogus);
        Assert.Null(fetched);
    }
}
