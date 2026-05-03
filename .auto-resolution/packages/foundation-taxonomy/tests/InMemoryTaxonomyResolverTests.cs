using System.Threading;

namespace Sunfish.Foundation.Taxonomy.Tests;

public sealed class InMemoryTaxonomyResolverTests
{
    private static readonly TenantId Tenant = new("tenant-a");
    private static readonly TaxonomyDefinitionId DefId = new("Sunfish", "Signature", "Scopes");
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<(InMemoryTaxonomyRegistry, InMemoryTaxonomyResolver)> NewSeededAsync()
    {
        var registry = new InMemoryTaxonomyRegistry();
        await registry.RegisterCorePackageAsync(Tenant, TaxonomyCorePackages.SunfishSignatureScopes, Ct);
        var resolver = new InMemoryTaxonomyResolver(registry);
        return (registry, resolver);
    }

    [Fact]
    public async Task Resolve_FindsKnownNode()
    {
        var (_, resolver) = await NewSeededAsync();
        var node = await resolver.ResolveAsync(Tenant, new TaxonomyClassification
        {
            Definition = DefId,
            Code = "lease-execution",
            Version = TaxonomyVersion.V1_0_0,
        }, Ct);

        Assert.NotNull(node);
        Assert.Equal("Lease Execution", node!.Display);
    }

    [Fact]
    public async Task Resolve_ReturnsNullForUnknownCode()
    {
        var (_, resolver) = await NewSeededAsync();
        var node = await resolver.ResolveAsync(Tenant, new TaxonomyClassification
        {
            Definition = DefId,
            Code = "no-such-code",
            Version = TaxonomyVersion.V1_0_0,
        }, Ct);
        Assert.Null(node);
    }

    [Fact]
    public async Task Resolve_FindsTombstonedNode()
    {
        var (registry, resolver) = await NewSeededAsync();
        var nodeId = new TaxonomyNodeId(DefId, "lease-execution");
        await registry.TombstoneNodeAsync(Tenant, nodeId, TaxonomyVersion.V1_0_0,
            "test deprecation", null, ActorId.Sunfish, Ct);

        var node = await resolver.ResolveAsync(Tenant, new TaxonomyClassification
        {
            Definition = DefId,
            Code = "lease-execution",
            Version = TaxonomyVersion.V1_0_0,
        }, Ct);

        // Per OQ-2: tombstoned nodes still resolve.
        Assert.NotNull(node);
        Assert.Equal(TaxonomyNodeStatus.Tombstoned, node!.Status);
    }

    [Fact]
    public async Task IsActive_ReturnsFalseForTombstoned()
    {
        var (registry, resolver) = await NewSeededAsync();
        var nodeId = new TaxonomyNodeId(DefId, "lease-execution");
        await registry.TombstoneNodeAsync(Tenant, nodeId, TaxonomyVersion.V1_0_0,
            "test deprecation", null, ActorId.Sunfish, Ct);

        var active = await resolver.IsActiveAsync(Tenant, new TaxonomyClassification
        {
            Definition = DefId,
            Code = "lease-execution",
            Version = TaxonomyVersion.V1_0_0,
        }, Ct);
        Assert.False(active);
    }

    [Fact]
    public async Task IsActive_ReturnsFalseForUnknown()
    {
        var (_, resolver) = await NewSeededAsync();
        var active = await resolver.IsActiveAsync(Tenant, new TaxonomyClassification
        {
            Definition = DefId,
            Code = "no-such-code",
            Version = TaxonomyVersion.V1_0_0,
        }, Ct);
        Assert.False(active);
    }

    [Fact]
    public async Task ResolveAll_PreservesOrder()
    {
        var (_, resolver) = await NewSeededAsync();
        var inputs = new[]
        {
            new TaxonomyClassification { Definition = DefId, Code = "lease-amendment", Version = TaxonomyVersion.V1_0_0 },
            new TaxonomyClassification { Definition = DefId, Code = "no-such-code", Version = TaxonomyVersion.V1_0_0 },
            new TaxonomyClassification { Definition = DefId, Code = "lease-execution", Version = TaxonomyVersion.V1_0_0 },
        };

        var results = await resolver.ResolveAllAsync(Tenant, inputs, Ct);
        Assert.Equal(3, results.Count);
        Assert.Equal("Lease Amendment", results[0]!.Display);
        Assert.Null(results[1]);
        Assert.Equal("Lease Execution", results[2]!.Display);
    }
}
