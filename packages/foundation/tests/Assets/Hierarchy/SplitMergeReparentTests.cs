using System.Text.Json;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;
using Sunfish.Foundation.Assets.Hierarchy;

namespace Sunfish.Foundation.Tests.Assets.Hierarchy;

public sealed class SplitMergeReparentTests
{
    private static readonly SchemaId BuildingSchema = new("building.v1");
    private static readonly SchemaId UnitSchema = new("unit.v1");
    private static readonly ActorId PM = new("pm");
    private static readonly TenantId Tenant = new("acme");

    private static (HierarchyOperations ops, InMemoryEntityStore entities, InMemoryHierarchyService hierarchy, InMemoryAuditLog audit) New()
    {
        var storage = new InMemoryAssetStorage();
        var entities = new InMemoryEntityStore(storage);
        var hierarchy = new InMemoryHierarchyService();
        var audit = new InMemoryAuditLog(storage);
        return (new HierarchyOperations(entities, hierarchy, audit), entities, hierarchy, audit);
    }

    private static CreateOptions Opts(string nonce, string authority = "acme")
        => new("building", authority, nonce, PM, Tenant);

    private static JsonDocument Body(string json) => JsonDocument.Parse(json);

    private async Task<(EntityId old, List<EntityId> children, DateTimeOffset t0)> SeedBuildingWithUnits(InMemoryEntityStore entities, InMemoryHierarchyService hierarchy, int unitCount = 4)
    {
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var building = await entities.CreateAsync(BuildingSchema, Body("""{"floors":10}"""),
            new CreateOptions("building", "acme", "B42", PM, Tenant, ValidFrom: t0));
        var units = new List<EntityId>();
        for (int i = 0; i < unitCount; i++)
        {
            var unit = await entities.CreateAsync(UnitSchema, Body($$"""{"n":{{i}}}"""),
                new CreateOptions("unit", "acme", $"U{i}", PM, Tenant, ValidFrom: t0));
            units.Add(unit);
            await hierarchy.AddEdgeAsync(unit, building, EdgeKind.ChildOf, t0);
        }
        return (building, units, t0);
    }

    [Fact]
    public async Task SplitAsync_MintsNewEntities()
    {
        var (ops, entities, hierarchy, _) = New();
        var (old, children, t0) = await SeedBuildingWithUnits(entities, hierarchy, unitCount: 4);
        var at = t0.AddDays(365);

        var targets = new[]
        {
            new SplitTarget(BuildingSchema, Body("""{"floors":5,"side":"north"}"""), new CreateOptions("building", "acme", "B42N", PM, Tenant)),
            new SplitTarget(BuildingSchema, Body("""{"floors":5,"side":"south"}"""), new CreateOptions("building", "acme", "B42S", PM, Tenant)),
        };
        var map = new Dictionary<EntityId, EntityId>
        {
            [children[0]] = EntityId.Parse("building:acme/will-be-replaced-0"),
        };
        // Note: we don't actually know the new ids upfront, so we'll patch the map after we run Split. Re-write test:
        // Instead, do this manually — call Split but with empty reassignment, then assert mint side only.

        var result = await ops.SplitAsync(old, targets, new Dictionary<EntityId, EntityId>(), "Phase rollout", PM, Tenant, at);
        Assert.Equal(2, result.NewEntities.Count);
    }

    [Fact]
    public async Task SplitAsync_InvalidatesOldChildEdges_AtEffectiveAt()
    {
        var (ops, entities, hierarchy, _) = New();
        var (old, children, t0) = await SeedBuildingWithUnits(entities, hierarchy, 2);
        var at = t0.AddDays(10);

        // Mint the new target first so we can plumb reassignments. This is easier using an explicit local-part.
        var targets = new[]
        {
            new SplitTarget(BuildingSchema, Body("""{"side":"north"}"""),
                new CreateOptions("building", "acme", "N", PM, Tenant, ExplicitLocalPart: "42-north")),
        };
        var newNorth = new EntityId("building", "acme", "42-north");
        var reassignments = new Dictionary<EntityId, EntityId> { [children[0]] = newNorth, [children[1]] = newNorth };

        var result = await ops.SplitAsync(old, targets, reassignments, "Phase rollout", PM, Tenant, at);
        Assert.Equal(newNorth, result.NewEntities[0]);

        // Children should be under the new parent, not the old one, post split.
        var kidsOld = new List<EntityEdge>();
        await foreach (var e in hierarchy.GetChildrenAsync(old, at.AddSeconds(1))) kidsOld.Add(e);
        Assert.Empty(kidsOld);

        var kidsNew = new List<EntityEdge>();
        await foreach (var e in hierarchy.GetChildrenAsync(newNorth, at.AddSeconds(1))) kidsNew.Add(e);
        Assert.Equal(2, kidsNew.Count);
    }

    [Fact]
    public async Task SplitAsync_MarksOldEntityAsSupersededAndTombstonesIt()
    {
        var (ops, entities, hierarchy, _) = New();
        var (old, _, t0) = await SeedBuildingWithUnits(entities, hierarchy, 0);
        var at = t0.AddDays(1);
        var targets = new[]
        {
            new SplitTarget(BuildingSchema, Body("""{"side":"only"}"""),
                new CreateOptions("building", "acme", "solo", PM, Tenant, ExplicitLocalPart: "42-solo")),
        };
        await ops.SplitAsync(old, targets, new Dictionary<EntityId, EntityId>(), "one-way", PM, Tenant, at);

        var tomb = await entities.GetAsync(old);
        Assert.Null(tomb);

        // Historical read before the split still returns the entity.
        var pre = await entities.GetAsync(old, new VersionSelector(AsOf: t0.AddHours(1)));
        Assert.NotNull(pre);
    }

    [Fact]
    public async Task SplitAsync_EmitsSplitAuditRecord_WithJustification()
    {
        var (ops, entities, hierarchy, audit) = New();
        var (old, _, t0) = await SeedBuildingWithUnits(entities, hierarchy, 0);
        var at = t0.AddDays(2);
        var targets = new[]
        {
            new SplitTarget(BuildingSchema, Body("""{"s":1}"""),
                new CreateOptions("building", "acme", "x", PM, Tenant, ExplicitLocalPart: "42-a")),
        };
        await ops.SplitAsync(old, targets, new Dictionary<EntityId, EntityId>(), "justified reason", PM, Tenant, at);

        var recs = new List<AuditRecord>();
        await foreach (var r in audit.QueryAsync(new AuditQuery(Entity: old, Op: Op.Split))) recs.Add(r);
        Assert.Single(recs);
        Assert.Equal("justified reason", recs[0].Justification);
    }

    [Fact]
    public async Task MergeAsync_MintsMergedEntity()
    {
        var (ops, entities, hierarchy, _) = New();
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = await entities.CreateAsync(BuildingSchema, Body("""{"a":1}"""),
            new CreateOptions("building", "acme", "A", PM, Tenant, ValidFrom: t0));
        var b = await entities.CreateAsync(BuildingSchema, Body("""{"b":1}"""),
            new CreateOptions("building", "acme", "B", PM, Tenant, ValidFrom: t0));

        var result = await ops.MergeAsync(
            new[] { a, b },
            BuildingSchema,
            Body("""{"merged":true}"""),
            new CreateOptions("building", "acme", "AB", PM, Tenant, ExplicitLocalPart: "42-ab"),
            "consolidation",
            PM, Tenant, t0.AddDays(10));

        var ent = await entities.GetAsync(result.NewEntity);
        Assert.NotNull(ent);
        Assert.Null(await entities.GetAsync(a));
        Assert.Null(await entities.GetAsync(b));
    }

    [Fact]
    public async Task MergeAsync_MovesChildrenToMergedEntity()
    {
        var (ops, entities, hierarchy, _) = New();
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var bA = await entities.CreateAsync(BuildingSchema, Body("""{"a":1}"""),
            new CreateOptions("building", "acme", "A", PM, Tenant, ValidFrom: t0));
        var bB = await entities.CreateAsync(BuildingSchema, Body("""{"b":1}"""),
            new CreateOptions("building", "acme", "B", PM, Tenant, ValidFrom: t0));
        var u1 = await entities.CreateAsync(UnitSchema, Body("""{"u":1}"""),
            new CreateOptions("unit", "acme", "U1", PM, Tenant, ValidFrom: t0));
        var u2 = await entities.CreateAsync(UnitSchema, Body("""{"u":2}"""),
            new CreateOptions("unit", "acme", "U2", PM, Tenant, ValidFrom: t0));
        await hierarchy.AddEdgeAsync(u1, bA, EdgeKind.ChildOf, t0);
        await hierarchy.AddEdgeAsync(u2, bB, EdgeKind.ChildOf, t0);

        var at = t0.AddDays(1);
        var result = await ops.MergeAsync(
            new[] { bA, bB }, BuildingSchema, Body("""{"merged":true}"""),
            new CreateOptions("building", "acme", "AB", PM, Tenant, ExplicitLocalPart: "42-ab"),
            "consolidation", PM, Tenant, at);

        var kids = new List<EntityEdge>();
        await foreach (var e in hierarchy.GetChildrenAsync(result.NewEntity, at.AddSeconds(1))) kids.Add(e);
        Assert.Equal(2, kids.Count);
    }

    [Fact]
    public async Task MergeAsync_EmitsMergeAuditRecord()
    {
        var (ops, entities, hierarchy, audit) = New();
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = await entities.CreateAsync(BuildingSchema, Body("""{"a":1}"""),
            new CreateOptions("building", "acme", "A", PM, Tenant, ValidFrom: t0));
        var b = await entities.CreateAsync(BuildingSchema, Body("""{"b":1}"""),
            new CreateOptions("building", "acme", "B", PM, Tenant, ValidFrom: t0));

        var result = await ops.MergeAsync(new[] { a, b }, BuildingSchema, Body("""{"ok":1}"""),
            new CreateOptions("building", "acme", "AB", PM, Tenant, ExplicitLocalPart: "42-ab"),
            "cons", PM, Tenant, t0.AddDays(1));

        var recs = new List<AuditRecord>();
        await foreach (var r in audit.QueryAsync(new AuditQuery(Entity: result.NewEntity, Op: Op.Merge))) recs.Add(r);
        Assert.Single(recs);
    }

    [Fact]
    public async Task ReparentAsync_InvalidatesOldEdge_CreatesNew()
    {
        var (ops, entities, hierarchy, _) = New();
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var buildingA = await entities.CreateAsync(BuildingSchema, Body("""{"a":1}"""),
            new CreateOptions("building", "acme", "A", PM, Tenant, ValidFrom: t0));
        var buildingB = await entities.CreateAsync(BuildingSchema, Body("""{"b":1}"""),
            new CreateOptions("building", "acme", "B", PM, Tenant, ValidFrom: t0));
        var unit = await entities.CreateAsync(UnitSchema, Body("""{"u":1}"""),
            new CreateOptions("unit", "acme", "U", PM, Tenant, ValidFrom: t0));
        await hierarchy.AddEdgeAsync(unit, buildingA, EdgeKind.ChildOf, t0);

        var at = t0.AddDays(30);
        await ops.ReparentAsync(unit, buildingA, buildingB, "move", PM, Tenant, at);

        var aKids = new List<EntityEdge>();
        await foreach (var e in hierarchy.GetChildrenAsync(buildingA, at.AddSeconds(1))) aKids.Add(e);
        Assert.Empty(aKids);

        var bKids = new List<EntityEdge>();
        await foreach (var e in hierarchy.GetChildrenAsync(buildingB, at.AddSeconds(1))) bKids.Add(e);
        Assert.Single(bKids);
    }

    [Fact]
    public async Task ReparentAsync_EmitsReparentAuditRecord()
    {
        var (ops, entities, hierarchy, audit) = New();
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = await entities.CreateAsync(BuildingSchema, Body("""{"a":1}"""),
            new CreateOptions("building", "acme", "A", PM, Tenant, ValidFrom: t0));
        var b = await entities.CreateAsync(BuildingSchema, Body("""{"b":1}"""),
            new CreateOptions("building", "acme", "B", PM, Tenant, ValidFrom: t0));
        var u = await entities.CreateAsync(UnitSchema, Body("""{"u":1}"""),
            new CreateOptions("unit", "acme", "U", PM, Tenant, ValidFrom: t0));
        await hierarchy.AddEdgeAsync(u, a, EdgeKind.ChildOf, t0);
        await ops.ReparentAsync(u, a, b, "move", PM, Tenant, t0.AddDays(1));

        var recs = new List<AuditRecord>();
        await foreach (var r in audit.QueryAsync(new AuditQuery(Entity: u, Op: Op.Reparent))) recs.Add(r);
        Assert.Single(recs);
    }

    [Fact]
    public async Task ReparentAsync_PreservesHistoricalQueries_BeforeEffectiveAt()
    {
        var (ops, entities, hierarchy, _) = New();
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var a = await entities.CreateAsync(BuildingSchema, Body("""{"a":1}"""),
            new CreateOptions("building", "acme", "A", PM, Tenant, ValidFrom: t0));
        var b = await entities.CreateAsync(BuildingSchema, Body("""{"b":1}"""),
            new CreateOptions("building", "acme", "B", PM, Tenant, ValidFrom: t0));
        var u = await entities.CreateAsync(UnitSchema, Body("""{"u":1}"""),
            new CreateOptions("unit", "acme", "U", PM, Tenant, ValidFrom: t0));
        await hierarchy.AddEdgeAsync(u, a, EdgeKind.ChildOf, t0);
        await ops.ReparentAsync(u, a, b, "move", PM, Tenant, t0.AddDays(30));

        var aKidsHistoric = new List<EntityEdge>();
        await foreach (var e in hierarchy.GetChildrenAsync(a, t0.AddDays(10))) aKidsHistoric.Add(e);
        Assert.Single(aKidsHistoric);
    }

    [Fact]
    public async Task SplitAsync_CreatesNewChildEdges_WithCorrectParent()
    {
        var (ops, entities, hierarchy, _) = New();
        var (old, children, t0) = await SeedBuildingWithUnits(entities, hierarchy, 3);
        var at = t0.AddDays(2);

        var northId = new EntityId("building", "acme", "42-north");
        var southId = new EntityId("building", "acme", "42-south");
        var targets = new[]
        {
            new SplitTarget(BuildingSchema, Body("""{"s":"n"}"""),
                new CreateOptions("building", "acme", "n", PM, Tenant, ExplicitLocalPart: northId.LocalPart)),
            new SplitTarget(BuildingSchema, Body("""{"s":"s"}"""),
                new CreateOptions("building", "acme", "s", PM, Tenant, ExplicitLocalPart: southId.LocalPart)),
        };
        var map = new Dictionary<EntityId, EntityId>
        {
            [children[0]] = northId,
            [children[1]] = southId,
            [children[2]] = northId,
        };
        await ops.SplitAsync(old, targets, map, "split", PM, Tenant, at);

        var north = new List<EntityEdge>();
        await foreach (var e in hierarchy.GetChildrenAsync(northId, at.AddSeconds(1))) north.Add(e);
        Assert.Equal(2, north.Count);

        var south = new List<EntityEdge>();
        await foreach (var e in hierarchy.GetChildrenAsync(southId, at.AddSeconds(1))) south.Add(e);
        Assert.Single(south);
    }
}
