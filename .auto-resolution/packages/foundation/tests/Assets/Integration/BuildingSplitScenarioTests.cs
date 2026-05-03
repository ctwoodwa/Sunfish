using System.Text.Json;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;
using Sunfish.Foundation.Assets.Hierarchy;

namespace Sunfish.Foundation.Tests.Assets.Integration;

/// <summary>
/// Spec §8.9 worked example: Building:42 evolves across 2020–2026 and eventually splits.
/// </summary>
public sealed class BuildingSplitScenarioTests
{
    private static readonly SchemaId Building = new("building.v1");
    private static readonly SchemaId Unit = new("unit.v1");
    private static readonly SchemaId Roof = new("roof.v1");
    private static readonly ActorId PM = new("pm");
    private static readonly TenantId Tenant = new("acme");

    private sealed record Ctx(
        InMemoryAssetStorage Storage,
        InMemoryEntityStore Entities,
        InMemoryHierarchyService Hierarchy,
        InMemoryAuditLog Audit,
        HierarchyOperations Ops,
        EntityId BuildingId,
        IReadOnlyList<EntityId> Units,
        EntityId NorthId,
        EntityId SouthId);

    private static async Task<Ctx> ArrangeAsync()
    {
        var storage = new InMemoryAssetStorage();
        var entities = new InMemoryEntityStore(storage);
        var hierarchy = new InMemoryHierarchyService();
        var audit = new InMemoryAuditLog(storage);
        var ops = new HierarchyOperations(entities, hierarchy, audit);

        var t2020 = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t2022 = new DateTimeOffset(2022, 6, 15, 0, 0, 0, TimeSpan.Zero);
        var t2024 = new DateTimeOffset(2024, 3, 10, 0, 0, 0, TimeSpan.Zero);
        var t2026 = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

        // 2020-01-01: Mint Building:42 with 10 floors and 120 units.
        var buildingId = await entities.CreateAsync(Building,
            JsonDocument.Parse("""{"floors":10,"address":"42 Main St"}"""),
            new CreateOptions("building", "acme", "42", PM, Tenant, ValidFrom: t2020, ExplicitLocalPart: "42"));
        await audit.AppendAsync(new AuditAppend(buildingId, null, Op.Mint, PM, Tenant, t2020,
            JsonDocument.Parse("""{"evt":"mint"}""")));

        var units = new List<EntityId>();
        for (int i = 0; i < 120; i++)
        {
            var u = await entities.CreateAsync(Unit,
                JsonDocument.Parse($$"""{"number":"U{{i}}"}"""),
                new CreateOptions("unit", "acme", $"U{i}", PM, Tenant, ValidFrom: t2020, ExplicitLocalPart: $"42-u{i}"));
            await hierarchy.AddEdgeAsync(u, buildingId, EdgeKind.ChildOf, t2020);
            units.Add(u);
        }

        // 2022-06-15: Replace the roof (SupersededBy edge, no building body change).
        var roofV1 = await entities.CreateAsync(Roof,
            JsonDocument.Parse("""{"material":"tar"}"""),
            new CreateOptions("roof", "acme", "42-v1", PM, Tenant, ValidFrom: t2020, ExplicitLocalPart: "42-roof-v1"));
        var roofV2 = await entities.CreateAsync(Roof,
            JsonDocument.Parse("""{"material":"membrane"}"""),
            new CreateOptions("roof", "acme", "42-v2", PM, Tenant, ValidFrom: t2022, ExplicitLocalPart: "42-roof-v2"));
        await hierarchy.AddEdgeAsync(roofV1, roofV2, EdgeKind.SupersededBy, t2022);

        // 2024-03-10: Correct floor count to 12 (an UpdateAsync with Op.Correct).
        await entities.UpdateAsync(buildingId,
            JsonDocument.Parse("""{"floors":12,"address":"42 Main St"}"""),
            new UpdateOptions(PM, ValidFrom: t2024, Justification: "original count was wrong"));
        await audit.AppendAsync(new AuditAppend(buildingId, null, Op.Correct, PM, Tenant, t2024,
            JsonDocument.Parse("""{"field":"floors","from":10,"to":12}"""),
            Justification: "original count was wrong"));

        // 2026-05-01: Split into north (60 units) + south (60 units).
        var northId = new EntityId("building", "acme", "42-north");
        var southId = new EntityId("building", "acme", "42-south");
        var targets = new[]
        {
            new SplitTarget(Building, JsonDocument.Parse("""{"floors":12,"side":"north"}"""),
                new CreateOptions("building", "acme", "42-north", PM, Tenant, ExplicitLocalPart: northId.LocalPart)),
            new SplitTarget(Building, JsonDocument.Parse("""{"floors":12,"side":"south"}"""),
                new CreateOptions("building", "acme", "42-south", PM, Tenant, ExplicitLocalPart: southId.LocalPart)),
        };
        var reassign = new Dictionary<EntityId, EntityId>();
        for (int i = 0; i < 60; i++) reassign[units[i]] = northId;
        for (int i = 60; i < 120; i++) reassign[units[i]] = southId;

        await ops.SplitAsync(buildingId, targets, reassign, "tenant restructure", PM, Tenant, t2026);

        return new Ctx(storage, entities, hierarchy, audit, ops, buildingId, units, northId, southId);
    }

    [Fact]
    public async Task QueryAsOf_2024_12_31_ReturnsCorrectedFloors_WithOriginalBuilding()
    {
        var ctx = await ArrangeAsync();
        var asOf = new DateTimeOffset(2024, 12, 31, 0, 0, 0, TimeSpan.Zero);

        var ent = await ctx.Entities.GetAsync(ctx.BuildingId, new VersionSelector(AsOf: asOf));
        Assert.NotNull(ent);
        using var body = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(ent!.Body.RootElement));
        Assert.Equal(12, body.RootElement.GetProperty("floors").GetInt32());

        var kids = new List<EntityEdge>();
        await foreach (var e in ctx.Hierarchy.GetChildrenAsync(ctx.BuildingId, asOf)) kids.Add(e);
        Assert.Equal(120, kids.Count);
    }

    [Fact]
    public async Task QueryAsOf_2022_01_01_ReturnsOriginalFloors_10()
    {
        var ctx = await ArrangeAsync();
        var asOf = new DateTimeOffset(2022, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var ent = await ctx.Entities.GetAsync(ctx.BuildingId, new VersionSelector(AsOf: asOf));
        Assert.NotNull(ent);
        using var body = JsonDocument.Parse(JsonSerializer.SerializeToUtf8Bytes(ent!.Body.RootElement));
        Assert.Equal(10, body.RootElement.GetProperty("floors").GetInt32());
    }

    [Fact]
    public async Task QueryAsOf_2026_09_01_ReturnsTwoPeerBuildings_AfterSplit()
    {
        var ctx = await ArrangeAsync();
        var asOf = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Null(await ctx.Entities.GetAsync(ctx.BuildingId, new VersionSelector(AsOf: asOf)));

        var north = await ctx.Entities.GetAsync(ctx.NorthId, new VersionSelector(AsOf: asOf));
        var south = await ctx.Entities.GetAsync(ctx.SouthId, new VersionSelector(AsOf: asOf));
        Assert.NotNull(north);
        Assert.NotNull(south);

        var northKids = new List<EntityEdge>();
        await foreach (var e in ctx.Hierarchy.GetChildrenAsync(ctx.NorthId, asOf)) northKids.Add(e);
        var southKids = new List<EntityEdge>();
        await foreach (var e in ctx.Hierarchy.GetChildrenAsync(ctx.SouthId, asOf)) southKids.Add(e);

        Assert.Equal(60, northKids.Count);
        Assert.Equal(60, southKids.Count);
    }

    [Fact]
    public async Task AuditTrail_ShowsFullEvolution_FromMintThroughSplit()
    {
        var ctx = await ArrangeAsync();
        var recs = new List<AuditRecord>();
        await foreach (var r in ctx.Audit.QueryAsync(new AuditQuery(Entity: ctx.BuildingId))) recs.Add(r);
        var ops = recs.Select(r => r.Op).ToArray();

        Assert.Contains(Op.Mint, ops);
        Assert.Contains(Op.Correct, ops);
        Assert.Contains(Op.Split, ops);
        // Ordering: Mint must come before Correct, which must come before Split.
        Assert.True(Array.IndexOf(ops, Op.Mint) < Array.IndexOf(ops, Op.Correct));
        Assert.True(Array.IndexOf(ops, Op.Correct) < Array.IndexOf(ops, Op.Split));
    }
}
