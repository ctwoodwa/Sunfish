using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Hierarchy;

namespace Sunfish.Foundation.Tests.Assets.Hierarchy;

public sealed class InMemoryHierarchyServiceTests
{
    private static readonly EntityId Site = new("site", "acme", "1");
    private static readonly EntityId Building = new("building", "acme", "42");
    private static readonly EntityId Floor3 = new("floor", "acme", "42-3");
    private static readonly EntityId Unit = new("unit", "acme", "42-3b");

    [Fact]
    public async Task AddEdgeAsync_CreatesDirectEdge()
    {
        var svc = new InMemoryHierarchyService();
        var edge = await svc.AddEdgeAsync(Building, Site, EdgeKind.ChildOf, DateTimeOffset.UtcNow);
        Assert.Equal(Building, edge.From);
        Assert.Equal(Site, edge.To);
        Assert.Equal(EdgeKind.ChildOf, edge.Kind);
    }

    [Fact]
    public async Task AddEdgeAsync_CreatesClosureRow_ForDirectParent()
    {
        var svc = new InMemoryHierarchyService();
        var t = DateTimeOffset.UtcNow;
        await svc.AddEdgeAsync(Building, Site, EdgeKind.ChildOf, t);

        var ancestors = new List<ClosureEntry>();
        await foreach (var c in svc.GetAncestorsAsync(Building)) ancestors.Add(c);
        Assert.Contains(ancestors, c => c.Ancestor == Site && c.Depth == 1);
    }

    [Fact]
    public async Task AddEdgeAsync_CreatesClosureRows_ForGrandparents()
    {
        var svc = new InMemoryHierarchyService();
        var t = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await svc.AddEdgeAsync(Building, Site, EdgeKind.ChildOf, t);
        await svc.AddEdgeAsync(Floor3, Building, EdgeKind.ChildOf, t);

        var ancestors = new List<ClosureEntry>();
        await foreach (var c in svc.GetAncestorsAsync(Floor3, t.AddSeconds(1))) ancestors.Add(c);
        Assert.Contains(ancestors, c => c.Ancestor == Building && c.Depth == 1);
        Assert.Contains(ancestors, c => c.Ancestor == Site && c.Depth == 2);
    }

    [Fact]
    public async Task AddEdgeAsync_CreatesClosureRows_ForExistingDescendantsOfChild()
    {
        var svc = new InMemoryHierarchyService();
        var t = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        // Build floor→unit first, then attach floor to building.
        await svc.AddEdgeAsync(Unit, Floor3, EdgeKind.ChildOf, t);
        await svc.AddEdgeAsync(Floor3, Building, EdgeKind.ChildOf, t.AddDays(1));

        var ancestors = new List<ClosureEntry>();
        await foreach (var c in svc.GetAncestorsAsync(Unit, t.AddDays(2))) ancestors.Add(c);
        Assert.Contains(ancestors, c => c.Ancestor == Floor3 && c.Depth == 1);
        Assert.Contains(ancestors, c => c.Ancestor == Building && c.Depth == 2);
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsOnlyActiveEdges_ByDefault()
    {
        var svc = new InMemoryHierarchyService();
        var t = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var e = await svc.AddEdgeAsync(Building, Site, EdgeKind.ChildOf, t);
        await svc.InvalidateEdgeAsync(e.Id, t.AddDays(1));

        var children = new List<EntityEdge>();
        await foreach (var c in svc.GetChildrenAsync(Site, t.AddDays(2))) children.Add(c);
        Assert.Empty(children);
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsEdgesValidAtAsOfInstant()
    {
        var svc = new InMemoryHierarchyService();
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var e = await svc.AddEdgeAsync(Building, Site, EdgeKind.ChildOf, t0);
        await svc.InvalidateEdgeAsync(e.Id, t0.AddDays(10));

        var children = new List<EntityEdge>();
        await foreach (var c in svc.GetChildrenAsync(Site, t0.AddDays(5))) children.Add(c);
        Assert.Single(children);
    }

    [Fact]
    public async Task GetAncestorsAsync_ReturnsClosureRowsOrderedByDepth()
    {
        var svc = new InMemoryHierarchyService();
        var t = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await svc.AddEdgeAsync(Building, Site, EdgeKind.ChildOf, t);
        await svc.AddEdgeAsync(Floor3, Building, EdgeKind.ChildOf, t);
        await svc.AddEdgeAsync(Unit, Floor3, EdgeKind.ChildOf, t);

        var ancestors = new List<ClosureEntry>();
        await foreach (var c in svc.GetAncestorsAsync(Unit, t.AddSeconds(1))) ancestors.Add(c);
        Assert.Equal(new[] { 1, 2, 3 }, ancestors.Select(a => a.Depth).ToArray());
    }

    [Fact]
    public async Task InvalidateEdgeAsync_SetsValidToOnDirectEdge()
    {
        var svc = new InMemoryHierarchyService();
        var t = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var e = await svc.AddEdgeAsync(Building, Site, EdgeKind.ChildOf, t);
        await svc.InvalidateEdgeAsync(e.Id, t.AddDays(1));

        var children = new List<EntityEdge>();
        await foreach (var c in svc.GetChildrenAsync(Site, t.AddDays(0.5))) children.Add(c);
        Assert.Single(children);
        await foreach (var c in svc.GetChildrenAsync(Site, t.AddDays(2))) children.Add(c);
        Assert.Single(children); // count unchanged — the post-invalidation query returned zero
    }

    [Fact]
    public async Task InvalidateEdgeAsync_SetsValidToOnClosureRows()
    {
        var svc = new InMemoryHierarchyService();
        var t = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await svc.AddEdgeAsync(Building, Site, EdgeKind.ChildOf, t);
        var floorEdge = await svc.AddEdgeAsync(Floor3, Building, EdgeKind.ChildOf, t);

        // Invalidate the floor→building edge: closure row (Site → Floor3) must close.
        await svc.InvalidateEdgeAsync(floorEdge.Id, t.AddDays(1));

        var ancestorsAfter = new List<ClosureEntry>();
        await foreach (var c in svc.GetAncestorsAsync(Floor3, t.AddDays(2))) ancestorsAfter.Add(c);
        Assert.DoesNotContain(ancestorsAfter, c => c.Ancestor == Site);
    }

    [Fact]
    public async Task GetSubtreeAsync_ReturnsCorrectTree_AsOfPastInstant()
    {
        var svc = new InMemoryHierarchyService();
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t1 = new DateTimeOffset(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
        await svc.AddEdgeAsync(Building, Site, EdgeKind.ChildOf, t0);
        var floorEdge = await svc.AddEdgeAsync(Floor3, Building, EdgeKind.ChildOf, t0);
        await svc.InvalidateEdgeAsync(floorEdge.Id, t1);

        var before = await svc.GetSubtreeAsync(Site, t0.AddMonths(1));
        Assert.Contains(before.Closure, c => c.Descendant == Floor3);

        var after = await svc.GetSubtreeAsync(Site, t1.AddDays(1));
        Assert.DoesNotContain(after.Closure, c => c.Descendant == Floor3);
    }
}
