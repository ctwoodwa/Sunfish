namespace Sunfish.Foundation.Taxonomy.Tests;

public sealed class SunfishLeasingJurisdictionRulesSeedTests
{
    [Fact]
    public void Seed_HasExpectedShape()
    {
        var pkg = TaxonomyCorePackages.SunfishLeasingJurisdictionRules;

        Assert.Equal(new TaxonomyDefinitionId("Sunfish", "Leasing", "JurisdictionRules"), pkg.Definition.Id);
        Assert.Equal(TaxonomyVersion.V1_0_0, pkg.Definition.Version);
        Assert.Equal(TaxonomyGovernanceRegime.Authoritative, pkg.Definition.Governance);
        Assert.Equal(ActorId.Sunfish, pkg.Definition.Owner);

        var roots = pkg.Nodes.Where(n => n.ParentCode is null).ToList();
        var children = pkg.Nodes.Where(n => n.ParentCode is not null).ToList();
        Assert.Equal(7, roots.Count);
        Assert.Equal(23, children.Count);
        Assert.Equal(30, pkg.Nodes.Count);
    }

    [Fact]
    public void Seed_AllChildrenHaveValidParents()
    {
        var pkg = TaxonomyCorePackages.SunfishLeasingJurisdictionRules;
        var rootCodes = pkg.Nodes.Where(n => n.ParentCode is null).Select(n => n.Id.Code).ToHashSet();

        foreach (var child in pkg.Nodes.Where(n => n.ParentCode is not null))
        {
            Assert.Contains(child.ParentCode!, rootCodes);
        }
    }

    [Fact]
    public void Seed_AllNodesAreActive()
    {
        var pkg = TaxonomyCorePackages.SunfishLeasingJurisdictionRules;
        Assert.All(pkg.Nodes, n => Assert.Equal(TaxonomyNodeStatus.Active, n.Status));
    }

    [Fact]
    public void Seed_NodeCodesAreUnique()
    {
        var pkg = TaxonomyCorePackages.SunfishLeasingJurisdictionRules;
        var codes = pkg.Nodes.Select(n => n.Id.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void Seed_AllRootsArePresent()
    {
        var pkg = TaxonomyCorePackages.SunfishLeasingJurisdictionRules;
        var rootCodes = pkg.Nodes.Where(n => n.ParentCode is null).Select(n => n.Id.Code).ToHashSet();

        Assert.Contains("us-fed.fha", rootCodes);
        Assert.Contains("us-fed.fcra", rootCodes);
        Assert.Contains("us-fed.fha-source-of-income", rootCodes);
        Assert.Contains("us-state.ca.unruh", rootCodes);
        Assert.Contains("us-state.ca.fehc", rootCodes);
        Assert.Contains("us-state.ny.tpa", rootCodes);
        Assert.Contains("us-state.ny.adverse-action-extended-window", rootCodes);
    }

    [Fact]
    public void Seed_FhaProtectedClasses_AreSeven()
    {
        var pkg = TaxonomyCorePackages.SunfishLeasingJurisdictionRules;
        var fhaChildren = pkg.Nodes.Where(n => n.ParentCode == "us-fed.fha").ToList();
        Assert.Equal(7, fhaChildren.Count);

        var fhaCodes = fhaChildren.Select(n => n.Id.Code).ToHashSet();
        Assert.Contains("us-fed.fha.race", fhaCodes);
        Assert.Contains("us-fed.fha.color", fhaCodes);
        Assert.Contains("us-fed.fha.religion", fhaCodes);
        Assert.Contains("us-fed.fha.sex", fhaCodes);
        Assert.Contains("us-fed.fha.familial-status", fhaCodes);
        Assert.Contains("us-fed.fha.national-origin", fhaCodes);
        Assert.Contains("us-fed.fha.disability", fhaCodes);
    }

    [Fact]
    public void Seed_FcraWorkflowRules_FourChildren()
    {
        var pkg = TaxonomyCorePackages.SunfishLeasingJurisdictionRules;
        var fcraChildren = pkg.Nodes.Where(n => n.ParentCode == "us-fed.fcra").ToList();
        Assert.Equal(4, fcraChildren.Count);

        var fcraCodes = fcraChildren.Select(n => n.Id.Code).ToHashSet();
        Assert.Contains("us-fed.fcra.adverse-action-notice", fcraCodes);
        Assert.Contains("us-fed.fcra.dispute-window-60d", fcraCodes);
        Assert.Contains("us-fed.fcra.consent-required", fcraCodes);
        Assert.Contains("us-fed.fcra.permissible-purpose", fcraCodes);
    }

    [Fact]
    public void Seed_DefinitionVersionConsistent()
    {
        var pkg = TaxonomyCorePackages.SunfishLeasingJurisdictionRules;
        Assert.All(pkg.Nodes, n => Assert.Equal(pkg.Definition.Version, n.DefinitionVersion));
    }
}
