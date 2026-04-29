namespace Sunfish.Foundation.Taxonomy.Tests;

public sealed class SunfishSignatureScopesSeedTests
{
    [Fact]
    public void Seed_HasExpectedShape()
    {
        var pkg = TaxonomyCorePackages.SunfishSignatureScopes;

        Assert.Equal(new TaxonomyDefinitionId("Sunfish", "Signature", "Scopes"), pkg.Definition.Id);
        Assert.Equal(TaxonomyVersion.V1_0_0, pkg.Definition.Version);
        Assert.Equal(TaxonomyGovernanceRegime.Authoritative, pkg.Definition.Governance);
        Assert.Equal(ActorId.Sunfish, pkg.Definition.Owner);
        Assert.Equal(24, pkg.Nodes.Count);

        var roots = pkg.Nodes.Where(n => n.ParentCode is null).ToList();
        var children = pkg.Nodes.Where(n => n.ParentCode is not null).ToList();
        Assert.Equal(17, roots.Count);
        Assert.Equal(7, children.Count);
    }

    [Fact]
    public void Seed_AllChildrenHaveValidParents()
    {
        var pkg = TaxonomyCorePackages.SunfishSignatureScopes;
        var rootCodes = pkg.Nodes.Where(n => n.ParentCode is null).Select(n => n.Id.Code).ToHashSet();

        foreach (var child in pkg.Nodes.Where(n => n.ParentCode is not null))
        {
            Assert.Contains(child.ParentCode!, rootCodes);
        }
    }

    [Fact]
    public void Seed_AllNodesAreActive()
    {
        var pkg = TaxonomyCorePackages.SunfishSignatureScopes;
        Assert.All(pkg.Nodes, n => Assert.Equal(TaxonomyNodeStatus.Active, n.Status));
    }

    [Fact]
    public void Seed_HasUniqueCodes()
    {
        var pkg = TaxonomyCorePackages.SunfishSignatureScopes;
        var codes = pkg.Nodes.Select(n => n.Id.Code).ToList();
        Assert.Equal(codes.Distinct().Count(), codes.Count);
    }

    [Fact]
    public void Seed_LeaseExecution_HasRenewalAndOriginationChildren()
    {
        var pkg = TaxonomyCorePackages.SunfishSignatureScopes;
        var leaseExecChildren = pkg.Nodes.Where(n => n.ParentCode == "lease-execution").Select(n => n.Id.Code).ToHashSet();
        Assert.Contains("lease-origination", leaseExecChildren);
        Assert.Contains("lease-renewal", leaseExecChildren);
    }
}
