using System.Threading;

namespace Sunfish.Foundation.Taxonomy.Tests;

public sealed class InMemoryTaxonomyRegistryTests
{
    private static readonly TenantId Tenant = new("tenant-a");
    private static readonly TaxonomyDefinitionId DefId = new("Acme", "Demo", "Things");
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static InMemoryTaxonomyRegistry NewRegistry() => new();

    // ─────────── Definition lifecycle ───────────

    [Fact]
    public async Task Create_PersistsDefinition()
    {
        var r = NewRegistry();
        var def = await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);

        Assert.Equal(DefId, def.Id);
        Assert.Equal(TaxonomyGovernanceRegime.Civilian, def.Governance);

        var fetched = await r.GetDefinitionAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, Ct);
        Assert.Equal(def, fetched);
    }

    [Fact]
    public async Task Create_RejectsDuplicate()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);
        await Assert.ThrowsAsync<TaxonomyConflictException>(() =>
            r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
                "demo", new ActorId("u1"), null, Ct));
    }

    [Fact]
    public async Task PublishVersion_AddsNewVersion()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);

        var v2 = await r.PublishVersionAsync(Tenant, DefId, new TaxonomyVersion(1, 1, 0), new ActorId("u1"), Ct);
        Assert.Equal(new TaxonomyVersion(1, 1, 0), v2.Version);
    }

    [Fact]
    public async Task Retire_SetsRetiredAt()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);
        await r.RetireDefinitionVersionAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, new ActorId("u1"), Ct);

        var fetched = await r.GetDefinitionAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, Ct);
        Assert.NotNull(fetched);
        Assert.NotNull(fetched!.RetiredAt);
    }

    // ─────────── Node lifecycle ───────────

    [Fact]
    public async Task AddNode_PersistsNode()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);

        var node = await r.AddNodeAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
            "thing-a", "Thing A", "first thing", parentCode: null, new ActorId("u1"), Ct);

        Assert.Equal("thing-a", node.Id.Code);
        Assert.Equal(TaxonomyNodeStatus.Active, node.Status);

        var nodes = await r.GetNodesAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, Ct);
        Assert.Single(nodes);
    }

    [Fact]
    public async Task AddNode_RejectsDuplicateCode()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);
        await r.AddNodeAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
            "thing-a", "Thing A", "first thing", null, new ActorId("u1"), Ct);

        await Assert.ThrowsAsync<TaxonomyGovernanceException>(() =>
            r.AddNodeAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
                "thing-a", "Thing A2", "dup", null, new ActorId("u1"), Ct));
    }

    [Fact]
    public async Task ReviseDisplay_AppendsHistory()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);
        await r.AddNodeAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
            "thing-a", "Thing A", "first thing", null, new ActorId("u1"), Ct);

        var nodeId = new TaxonomyNodeId(DefId, "thing-a");
        var revised = await r.ReviseDisplayAsync(Tenant, nodeId, TaxonomyVersion.V1_0_0,
            "Thing A v2", "first thing rev2", "typo", new ActorId("u1"), Ct);

        Assert.Equal("Thing A v2", revised.Display);
        Assert.Single(revised.DisplayHistoryEntries);
        Assert.Equal("Thing A", revised.DisplayHistoryEntries[0].Display);
        Assert.Equal("typo", revised.DisplayHistoryEntries[0].RevisionReason);
    }

    [Fact]
    public async Task Tombstone_FlipsStatus()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);
        await r.AddNodeAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
            "thing-a", "Thing A", "first thing", null, new ActorId("u1"), Ct);

        var nodeId = new TaxonomyNodeId(DefId, "thing-a");
        await r.TombstoneNodeAsync(Tenant, nodeId, TaxonomyVersion.V1_0_0,
            "deprecated", successorCode: null, new ActorId("u1"), Ct);

        var fetched = await r.GetNodeAsync(Tenant, nodeId, TaxonomyVersion.V1_0_0, Ct);
        Assert.NotNull(fetched);
        Assert.Equal(TaxonomyNodeStatus.Tombstoned, fetched!.Status);
        Assert.Equal("deprecated", fetched.DeprecationReason);
    }

    [Fact]
    public async Task Tombstone_IsMonotonic()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);
        await r.AddNodeAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
            "thing-a", "Thing A", "first thing", null, new ActorId("u1"), Ct);

        var nodeId = new TaxonomyNodeId(DefId, "thing-a");
        await r.TombstoneNodeAsync(Tenant, nodeId, TaxonomyVersion.V1_0_0,
            "first deprecation", null, new ActorId("u1"), Ct);
        var firstReason = (await r.GetNodeAsync(Tenant, nodeId, TaxonomyVersion.V1_0_0, Ct))!.DeprecationReason;

        // Re-tombstoning is a no-op (does not change reason).
        await r.TombstoneNodeAsync(Tenant, nodeId, TaxonomyVersion.V1_0_0,
            "second deprecation", null, new ActorId("u1"), Ct);
        var secondReason = (await r.GetNodeAsync(Tenant, nodeId, TaxonomyVersion.V1_0_0, Ct))!.DeprecationReason;

        Assert.Equal(firstReason, secondReason);
    }

    // ─────────── Governance rules ───────────

    [Fact]
    public async Task Authoritative_RejectsNonSunfishOwner()
    {
        var r = NewRegistry();
        await Assert.ThrowsAsync<TaxonomyGovernanceException>(() =>
            r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Authoritative,
                "demo", new ActorId("not-sunfish"), null, Ct));
    }

    [Fact]
    public async Task Authoritative_AllowsSunfishOwner()
    {
        var r = NewRegistry();
        var def = await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Authoritative,
            "demo", ActorId.Sunfish, null, Ct);
        Assert.Equal(ActorId.Sunfish, def.Owner);
    }

    [Fact]
    public async Task Authoritative_RejectsNonSunfishAddNode()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Authoritative,
            "demo", ActorId.Sunfish, null, Ct);
        await Assert.ThrowsAsync<TaxonomyGovernanceException>(() =>
            r.AddNodeAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
                "thing-a", "Thing A", "first thing", null, new ActorId("not-sunfish"), Ct));
    }

    [Fact]
    public async Task Alter_RejectsEmptyReason()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);
        await Assert.ThrowsAsync<TaxonomyGovernanceException>(() =>
            r.AlterAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
                new TaxonomyDefinitionId("Acme", "Demo", "ThingsAlt"),
                new ActorId("u1"), reason: "  ", Ct));
    }

    // ─────────── Lineage ───────────

    [Fact]
    public async Task Clone_CreatesCivilianDerivative()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Authoritative,
            "demo", ActorId.Sunfish, null, Ct);
        await r.AddNodeAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
            "x", "X", "x", null, ActorId.Sunfish, Ct);

        var newId = new TaxonomyDefinitionId("Tenant", "Demo", "Things");
        var derived = await r.CloneAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
            newId, new ActorId("u1"), "tenant fork", Ct);

        Assert.Equal(TaxonomyGovernanceRegime.Civilian, derived.Governance);
        Assert.Equal(TaxonomyLineageOp.Clone, derived.DerivedFrom!.Operation);

        // Source nodes copied over (per OQ-7).
        var nodes = await r.GetNodesAsync(Tenant, newId, TaxonomyVersion.V1_0_0, Ct);
        Assert.Single(nodes);
        Assert.Equal("x", nodes[0].Id.Code);
    }

    [Fact]
    public async Task Lineage_IsImmutable()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Authoritative,
            "demo", ActorId.Sunfish, null, Ct);

        var newId = new TaxonomyDefinitionId("Tenant", "Demo", "Things");
        var derived = await r.CloneAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, newId, new ActorId("u1"), "fork", Ct);

        // Cannot re-create the same derived id.
        await Assert.ThrowsAsync<TaxonomyConflictException>(() =>
            r.CloneAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, newId, new ActorId("u1"), "fork-2", Ct));
        Assert.NotNull(derived.DerivedFrom);
    }

    // ─────────── Reads + filters ───────────

    [Fact]
    public async Task ListDefinitions_FiltersByVendor()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, new TaxonomyDefinitionId("V1", "D", "T"), TaxonomyVersion.V1_0_0,
            TaxonomyGovernanceRegime.Civilian, "demo", new ActorId("u1"), null, Ct);
        await r.CreateAsync(Tenant, new TaxonomyDefinitionId("V2", "D", "T"), TaxonomyVersion.V1_0_0,
            TaxonomyGovernanceRegime.Civilian, "demo", new ActorId("u1"), null, Ct);

        var v1List = await r.ListDefinitionsAsync(Tenant, "V1", Ct);
        Assert.Single(v1List);
        Assert.Equal("V1", v1List[0].Id.Vendor);
    }

    [Fact]
    public async Task GetDefinition_ReturnsNullWhenAbsent()
    {
        var r = NewRegistry();
        var def = await r.GetDefinitionAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, Ct);
        Assert.Null(def);
    }

    // ─────────── Bootstrap ───────────

    [Fact]
    public async Task RegisterCorePackage_LoadsSeed()
    {
        var r = NewRegistry();
        await r.RegisterCorePackageAsync(Tenant, TaxonomyCorePackages.SunfishSignatureScopes, Ct);

        var nodes = await r.GetNodesAsync(Tenant, new TaxonomyDefinitionId("Sunfish", "Signature", "Scopes"),
            TaxonomyVersion.V1_0_0, Ct);
        Assert.Equal(24, nodes.Count); // 17 root + 7 children
    }

    [Fact]
    public async Task RegisterCorePackage_IsIdempotent()
    {
        var r = NewRegistry();
        await r.RegisterCorePackageAsync(Tenant, TaxonomyCorePackages.SunfishSignatureScopes, Ct);
        await r.RegisterCorePackageAsync(Tenant, TaxonomyCorePackages.SunfishSignatureScopes, Ct);

        var nodes = await r.GetNodesAsync(Tenant, new TaxonomyDefinitionId("Sunfish", "Signature", "Scopes"),
            TaxonomyVersion.V1_0_0, Ct);
        Assert.Equal(24, nodes.Count);
    }

    [Fact]
    public async Task RegisterCorePackage_RejectsConflictingData()
    {
        var r = NewRegistry();
        var pkg = TaxonomyCorePackages.SunfishSignatureScopes;
        await r.RegisterCorePackageAsync(Tenant, pkg, Ct);

        var conflict = pkg with
        {
            Definition = pkg.Definition with { Description = "different description" },
        };
        await Assert.ThrowsAsync<TaxonomyConflictException>(() =>
            r.RegisterCorePackageAsync(Tenant, conflict, Ct));
    }

    // ─────────── Concurrency ───────────

    [Fact]
    public async Task ParallelAddNode_NoLostWrites()
    {
        var r = NewRegistry();
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);

        var tasks = Enumerable.Range(0, 50)
            .Select(i => r.AddNodeAsync(Tenant, DefId, TaxonomyVersion.V1_0_0,
                $"thing-{i}", $"Thing {i}", $"#{i}", null, new ActorId("u1"), Ct))
            .ToArray();
        await Task.WhenAll(tasks);

        var nodes = await r.GetNodesAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, Ct);
        Assert.Equal(50, nodes.Count);
    }

    // ─────────── Tenant-scoping ───────────

    [Fact]
    public async Task CrossTenant_IsolationHolds()
    {
        var r = NewRegistry();
        var tenant2 = new TenantId("tenant-b");
        await r.CreateAsync(Tenant, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
            "demo", new ActorId("u1"), null, Ct);

        var t2View = await r.GetDefinitionAsync(tenant2, DefId, TaxonomyVersion.V1_0_0, Ct);
        Assert.Null(t2View);
    }

    [Fact]
    public async Task DefaultTenant_IsRejected()
    {
        var r = NewRegistry();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            r.CreateAsync(default, DefId, TaxonomyVersion.V1_0_0, TaxonomyGovernanceRegime.Civilian,
                "demo", new ActorId("u1"), null, Ct));
    }
}
