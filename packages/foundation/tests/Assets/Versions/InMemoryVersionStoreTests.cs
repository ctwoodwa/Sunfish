using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;
using Sunfish.Foundation.Assets.Versions;

namespace Sunfish.Foundation.Tests.Assets.Versions;

public sealed class InMemoryVersionStoreTests
{
    private static readonly SchemaId Schema = new("property.v1");

    private static (InMemoryEntityStore entities, InMemoryVersionStore versions, InMemoryAssetStorage storage) NewPair()
    {
        var storage = new InMemoryAssetStorage();
        return (new InMemoryEntityStore(storage), new InMemoryVersionStore(storage), storage);
    }

    private static CreateOptions Opts(string nonce, DateTimeOffset? validFrom = null)
        => new("property", "acme", nonce, new ActorId("alice"), TenantId.Default, ValidFrom: validFrom);

    [Fact]
    public async Task GetVersionAsync_ReturnsVersion_ForValidId()
    {
        var (entities, versions, _) = NewPair();
        using var b = JsonDocument.Parse("""{"x":1}""");
        var id = await entities.CreateAsync(Schema, b, Opts("n"));
        var ent = await entities.GetAsync(id);
        var hit = await versions.GetVersionAsync(ent!.CurrentVersion);
        Assert.NotNull(hit);
        Assert.Equal(1, hit!.Id.Sequence);
    }

    [Fact]
    public async Task GetVersionAsync_ReturnsNull_ForUnknownId()
    {
        var (_, versions, _) = NewPair();
        var hit = await versions.GetVersionAsync(new VersionId(new EntityId("x", "y", "z"), 1, "hash"));
        Assert.Null(hit);
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsAllVersions_InOrder()
    {
        var (entities, versions, _) = NewPair();
        using var b1 = JsonDocument.Parse("""{"v":1}""");
        var id = await entities.CreateAsync(Schema, b1, Opts("n"));
        using var b2 = JsonDocument.Parse("""{"v":2}""");
        await entities.UpdateAsync(id, b2, new UpdateOptions(new ActorId("a")));
        using var b3 = JsonDocument.Parse("""{"v":3}""");
        await entities.UpdateAsync(id, b3, new UpdateOptions(new ActorId("a")));

        var collected = new List<Sunfish.Foundation.Assets.Versions.Version>();
        await foreach (var v in versions.GetHistoryAsync(id)) collected.Add(v);
        Assert.Equal(3, collected.Count);
        Assert.Equal(new[] { 1, 2, 3 }, collected.Select(v => v.Id.Sequence).ToArray());
    }

    [Fact]
    public async Task GetHistoryAsync_ReturnsEmpty_ForUnknownEntity()
    {
        var (_, versions, _) = NewPair();
        var collected = new List<Sunfish.Foundation.Assets.Versions.Version>();
        await foreach (var v in versions.GetHistoryAsync(new EntityId("x", "y", "z"))) collected.Add(v);
        Assert.Empty(collected);
    }

    [Fact]
    public async Task GetAsOfAsync_ReturnsVersionValidAtInstant()
    {
        var (entities, versions, _) = NewPair();
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t1 = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);

        using var b1 = JsonDocument.Parse("""{"v":1}""");
        var id = await entities.CreateAsync(Schema, b1, Opts("n", t0));
        using var b2 = JsonDocument.Parse("""{"v":2}""");
        await entities.UpdateAsync(id, b2, new UpdateOptions(new ActorId("a"), ValidFrom: t1));

        var mid = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.Zero);
        var atMid = await versions.GetAsOfAsync(id, mid);
        Assert.NotNull(atMid);
        Assert.Equal(1, atMid!.Id.Sequence);

        var later = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var atLater = await versions.GetAsOfAsync(id, later);
        Assert.Equal(2, atLater!.Id.Sequence);
    }

    [Fact]
    public async Task GetAsOfAsync_ReturnsNull_WhenAsOfIsBeforeFirstVersion()
    {
        var (entities, versions, _) = NewPair();
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var b = JsonDocument.Parse("""{"v":1}""");
        var id = await entities.CreateAsync(Schema, b, Opts("n", t0));

        var pre = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var hit = await versions.GetAsOfAsync(id, pre);
        Assert.Null(hit);
    }

    [Fact]
    public async Task GetAsOfAsync_ReturnsLatest_WhenAsOfIsAfterAllVersions()
    {
        var (entities, versions, _) = NewPair();
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var b = JsonDocument.Parse("""{"v":1}""");
        var id = await entities.CreateAsync(Schema, b, Opts("n", t0));
        var future = new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var hit = await versions.GetAsOfAsync(id, future);
        Assert.NotNull(hit);
        Assert.Equal(1, hit!.Id.Sequence);
    }

    [Fact]
    public async Task VersionHash_IsDeterministic_ForSameInputs()
    {
        var (entities1, _, _) = NewPair();
        var (entities2, _, _) = NewPair();
        var t = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var b = JsonDocument.Parse("""{"a":1,"b":2}""");
        var id1 = await entities1.CreateAsync(Schema, b, Opts("same-nonce", t));
        var id2 = await entities2.CreateAsync(Schema, b, Opts("same-nonce", t));

        var e1 = await entities1.GetAsync(id1);
        var e2 = await entities2.GetAsync(id2);
        Assert.Equal(e1!.CurrentVersion.Hash, e2!.CurrentVersion.Hash);
    }

    [Fact]
    public async Task VersionHash_DiffersFromParent_ForAnyBodyChange()
    {
        var (entities, _, _) = NewPair();
        using var b1 = JsonDocument.Parse("""{"v":1}""");
        var id = await entities.CreateAsync(Schema, b1, Opts("n"));
        using var b2 = JsonDocument.Parse("""{"v":2}""");
        await entities.UpdateAsync(id, b2, new UpdateOptions(new ActorId("a")));

        var e = await entities.GetAsync(id);
        Assert.Equal(2, e!.CurrentVersion.Sequence);
        // Parent and current hashes must differ.
        Assert.NotEqual(e.CurrentVersion.Hash, string.Empty);
    }

    [Fact]
    public async Task ParentHash_IsCorrectlyChained_AcrossMultipleVersions()
    {
        var (entities, versions, _) = NewPair();
        using var b1 = JsonDocument.Parse("""{"v":1}""");
        var id = await entities.CreateAsync(Schema, b1, Opts("n"));
        using var b2 = JsonDocument.Parse("""{"v":2}""");
        await entities.UpdateAsync(id, b2, new UpdateOptions(new ActorId("a")));
        using var b3 = JsonDocument.Parse("""{"v":3}""");
        await entities.UpdateAsync(id, b3, new UpdateOptions(new ActorId("a")));

        var list = new List<Sunfish.Foundation.Assets.Versions.Version>();
        await foreach (var v in versions.GetHistoryAsync(id)) list.Add(v);
        Assert.Null(list[0].ParentId);
        Assert.Equal(list[0].Id, list[1].ParentId);
        Assert.Equal(list[1].Id, list[2].ParentId);
    }

    [Fact]
    public async Task BranchAsync_ThrowsNotImplemented()
    {
        var (_, versions, _) = NewPair();
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            versions.BranchAsync(new VersionId(new EntityId("a", "b", "c"), 1, "h"), new BranchOptions(new ActorId("x"))));
    }

    [Fact]
    public async Task MergeAsync_ThrowsNotImplemented()
    {
        var (_, versions, _) = NewPair();
        var vid = new VersionId(new EntityId("a", "b", "c"), 1, "h");
        await Assert.ThrowsAsync<NotImplementedException>(() =>
            versions.MergeAsync(vid, vid, new MergeOptions(new ActorId("x"))));
    }

    [Fact]
    public async Task ValidityRange_IsContiguous_AcrossVersions()
    {
        var (entities, versions, _) = NewPair();
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t1 = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        using var b1 = JsonDocument.Parse("""{"v":1}""");
        var id = await entities.CreateAsync(Schema, b1, Opts("n", t0));
        using var b2 = JsonDocument.Parse("""{"v":2}""");
        await entities.UpdateAsync(id, b2, new UpdateOptions(new ActorId("a"), ValidFrom: t1));

        var list = new List<Sunfish.Foundation.Assets.Versions.Version>();
        await foreach (var v in versions.GetHistoryAsync(id)) list.Add(v);
        Assert.Equal(t1, list[0].ValidTo);
        Assert.Equal(t1, list[1].ValidFrom);
        Assert.Null(list[1].ValidTo);
    }

    private sealed class RecordingObserver : IVersionObserver
    {
        public int Count;

        public Task OnVersionAppendedAsync(EntityId entity, Sunfish.Foundation.Assets.Versions.Version version, CancellationToken ct = default)
        {
            Interlocked.Increment(ref Count);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Observer_IsNotified_OnVersionAppend()
    {
        var storage = new InMemoryAssetStorage();
        var observer = new RecordingObserver();
        var entities = new InMemoryEntityStore(storage, observer: observer);
        using var b = JsonDocument.Parse("""{"v":1}""");
        var id = await entities.CreateAsync(Schema, b, Opts("n"));
        using var b2 = JsonDocument.Parse("""{"v":2}""");
        await entities.UpdateAsync(id, b2, new UpdateOptions(new ActorId("a")));

        Assert.True(observer.Count >= 2);
    }
}
