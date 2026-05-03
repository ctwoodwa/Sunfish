using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Hierarchy;
using Sunfish.Foundation.Assets.Postgres.Hierarchy;

namespace Sunfish.Foundation.Assets.Postgres.Tests;

public sealed class PostgresHierarchyServiceTests : IClassFixture<PostgresAssetStoreFixture>
{
    private readonly PostgresAssetStoreFixture _fixture;

    public PostgresHierarchyServiceTests(PostgresAssetStoreFixture fixture)
    {
        _fixture = fixture;
    }

    private PostgresHierarchyService NewService() => new(_fixture.CreateFactory());

    private static EntityId Id(string scope, string local) => new("hier-" + scope, "acme", local);

    [Fact]
    public async Task AddEdge_ChildOf_MaterializesClosureRows()
    {
        var svc = NewService();
        var parent = Id("basic", $"p-{Guid.NewGuid():N}");
        var child = Id("basic", $"c-{Guid.NewGuid():N}");
        var t = DateTimeOffset.UtcNow;

        await svc.AddEdgeAsync(child, parent, EdgeKind.ChildOf, t);

        var ancestors = new List<ClosureEntry>();
        await foreach (var c in svc.GetAncestorsAsync(child, t.AddSeconds(1)))
            ancestors.Add(c);
        Assert.Single(ancestors);
        Assert.Equal(parent, ancestors[0].Ancestor);
        Assert.Equal(1, ancestors[0].Depth);

        var descendants = new List<ClosureEntry>();
        await foreach (var c in svc.GetDescendantsAsync(parent, t.AddSeconds(1)))
            descendants.Add(c);
        Assert.Single(descendants);
        Assert.Equal(child, descendants[0].Descendant);
        Assert.Equal(1, descendants[0].Depth);
    }

    [Fact]
    public async Task Reparent_InvalidatesOldEdge_AndOpensNew()
    {
        var svc = NewService();
        var oldParent = Id("reparent", $"op-{Guid.NewGuid():N}");
        var newParent = Id("reparent", $"np-{Guid.NewGuid():N}");
        var child = Id("reparent", $"c-{Guid.NewGuid():N}");
        var t0 = DateTimeOffset.UtcNow;

        var oldEdge = await svc.AddEdgeAsync(child, oldParent, EdgeKind.ChildOf, t0);

        await Task.Delay(10);
        var t1 = DateTimeOffset.UtcNow;

        await svc.InvalidateEdgeAsync(oldEdge.Id, t1);
        await svc.AddEdgeAsync(child, newParent, EdgeKind.ChildOf, t1);

        // Historically (at t0 + small offset), old parent is ancestor.
        var atT0 = new List<ClosureEntry>();
        await foreach (var c in svc.GetAncestorsAsync(child, t0.AddMilliseconds(1)))
            atT0.Add(c);
        Assert.Contains(atT0, e => e.Ancestor == oldParent);

        // Now, new parent is ancestor, old parent is not.
        var atNow = new List<ClosureEntry>();
        await foreach (var c in svc.GetAncestorsAsync(child, DateTimeOffset.UtcNow.AddSeconds(1)))
            atNow.Add(c);
        Assert.Contains(atNow, e => e.Ancestor == newParent);
        Assert.DoesNotContain(atNow, e => e.Ancestor == oldParent);
    }

    [Fact]
    public async Task Split_AddsMultipleDescendantEdges_FromSameAncestor()
    {
        var svc = NewService();
        var original = Id("split", $"o-{Guid.NewGuid():N}");
        var north = Id("split", $"n-{Guid.NewGuid():N}");
        var south = Id("split", $"s-{Guid.NewGuid():N}");
        var child1 = Id("split", $"c1-{Guid.NewGuid():N}");
        var child2 = Id("split", $"c2-{Guid.NewGuid():N}");

        var t0 = DateTimeOffset.UtcNow;
        var e1 = await svc.AddEdgeAsync(child1, original, EdgeKind.ChildOf, t0);
        var e2 = await svc.AddEdgeAsync(child2, original, EdgeKind.ChildOf, t0);

        await Task.Delay(10);
        var t1 = DateTimeOffset.UtcNow;

        // Split: close old edges, re-parent children onto north/south, and mark supersession.
        await svc.InvalidateEdgeAsync(e1.Id, t1);
        await svc.InvalidateEdgeAsync(e2.Id, t1);
        await svc.AddEdgeAsync(child1, north, EdgeKind.ChildOf, t1);
        await svc.AddEdgeAsync(child2, south, EdgeKind.ChildOf, t1);
        await svc.AddEdgeAsync(original, north, EdgeKind.SupersededBy, t1);
        await svc.AddEdgeAsync(original, south, EdgeKind.SupersededBy, t1);

        // North has child1 as descendant, not child2.
        var northDesc = new List<ClosureEntry>();
        await foreach (var c in svc.GetDescendantsAsync(north, DateTimeOffset.UtcNow.AddSeconds(1)))
            northDesc.Add(c);
        Assert.Contains(northDesc, c => c.Descendant == child1);
        Assert.DoesNotContain(northDesc, c => c.Descendant == child2);

        // South has child2 but not child1.
        var southDesc = new List<ClosureEntry>();
        await foreach (var c in svc.GetDescendantsAsync(south, DateTimeOffset.UtcNow.AddSeconds(1)))
            southDesc.Add(c);
        Assert.Contains(southDesc, c => c.Descendant == child2);
        Assert.DoesNotContain(southDesc, c => c.Descendant == child1);
    }

    [Fact]
    public async Task Merge_CombinesDescendantsUnderSingleNewAncestor()
    {
        var svc = NewService();
        var oldA = Id("merge", $"oa-{Guid.NewGuid():N}");
        var oldB = Id("merge", $"ob-{Guid.NewGuid():N}");
        var merged = Id("merge", $"m-{Guid.NewGuid():N}");
        var childA = Id("merge", $"ca-{Guid.NewGuid():N}");
        var childB = Id("merge", $"cb-{Guid.NewGuid():N}");

        var t0 = DateTimeOffset.UtcNow;
        var eA = await svc.AddEdgeAsync(childA, oldA, EdgeKind.ChildOf, t0);
        var eB = await svc.AddEdgeAsync(childB, oldB, EdgeKind.ChildOf, t0);

        await Task.Delay(10);
        var t1 = DateTimeOffset.UtcNow;

        // Merge: invalidate old ChildOf edges, move both children onto merged, mark supersession.
        await svc.InvalidateEdgeAsync(eA.Id, t1);
        await svc.InvalidateEdgeAsync(eB.Id, t1);
        await svc.AddEdgeAsync(childA, merged, EdgeKind.ChildOf, t1);
        await svc.AddEdgeAsync(childB, merged, EdgeKind.ChildOf, t1);
        await svc.AddEdgeAsync(oldA, merged, EdgeKind.SupersededBy, t1);
        await svc.AddEdgeAsync(oldB, merged, EdgeKind.SupersededBy, t1);

        var descendants = new List<ClosureEntry>();
        await foreach (var c in svc.GetDescendantsAsync(merged, DateTimeOffset.UtcNow.AddSeconds(1)))
            descendants.Add(c);
        Assert.Contains(descendants, c => c.Descendant == childA);
        Assert.Contains(descendants, c => c.Descendant == childB);
    }

    [Fact]
    public async Task AsOf_HistoricalQueries_ReturnSnapshotAtInstant()
    {
        var svc = NewService();
        var parent = Id("asof", $"p-{Guid.NewGuid():N}");
        var child = Id("asof", $"c-{Guid.NewGuid():N}");

        // Record a "before" instant (parent exists, no edge yet).
        var before = DateTimeOffset.UtcNow;
        await Task.Delay(10);

        var t0 = DateTimeOffset.UtcNow;
        await svc.AddEdgeAsync(child, parent, EdgeKind.ChildOf, t0);

        // Before the edge existed, no ancestors.
        var atBefore = new List<ClosureEntry>();
        await foreach (var c in svc.GetAncestorsAsync(child, before))
            atBefore.Add(c);
        Assert.Empty(atBefore);

        // After the edge, parent is an ancestor.
        var atAfter = new List<ClosureEntry>();
        await foreach (var c in svc.GetAncestorsAsync(child, t0.AddSeconds(1)))
            atAfter.Add(c);
        Assert.Contains(atAfter, e => e.Ancestor == parent);
    }

    [Fact]
    public async Task AddEdge_CycleDetection_PreventsDirectBackEdge()
    {
        var svc = NewService();
        var a = Id("cycle", $"a-{Guid.NewGuid():N}");
        var b = Id("cycle", $"b-{Guid.NewGuid():N}");
        var t = DateTimeOffset.UtcNow;

        // b child-of a.
        await svc.AddEdgeAsync(b, a, EdgeKind.ChildOf, t);
        // Attempting a child-of b would form a cycle (a → b → a).
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddEdgeAsync(a, b, EdgeKind.ChildOf, t.AddMilliseconds(1)));
    }
}
