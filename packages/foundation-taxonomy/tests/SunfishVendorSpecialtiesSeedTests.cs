namespace Sunfish.Foundation.Taxonomy.Tests;

public sealed class SunfishVendorSpecialtiesSeedTests
{
    [Fact]
    public void Seed_HasExpectedShape()
    {
        var pkg = TaxonomyCorePackages.SunfishVendorSpecialties;

        Assert.Equal(new TaxonomyDefinitionId("Sunfish", "Vendor", "Specialties"), pkg.Definition.Id);
        Assert.Equal(TaxonomyVersion.V1_0_0, pkg.Definition.Version);
        Assert.Equal(TaxonomyGovernanceRegime.Authoritative, pkg.Definition.Governance);
        Assert.Equal(ActorId.Sunfish, pkg.Definition.Owner);

        var roots = pkg.Nodes.Where(n => n.ParentCode is null).ToList();
        var children = pkg.Nodes.Where(n => n.ParentCode is not null).ToList();
        Assert.Equal(11, roots.Count);
        Assert.Equal(19, children.Count);
        Assert.Equal(30, pkg.Nodes.Count);
    }

    [Fact]
    public void Seed_AllExistingEnumValues_HaveRootNode()
    {
        // Migration invariant: every VendorSpecialty enum value MUST exist
        // as a root node so callers can mechanically map enum → taxonomy.
        var pkg = TaxonomyCorePackages.SunfishVendorSpecialties;
        var rootCodes = pkg.Nodes.Where(n => n.ParentCode is null).Select(n => n.Id.Code).ToHashSet();

        Assert.Contains("general-contractor", rootCodes);
        Assert.Contains("plumbing", rootCodes);
        Assert.Contains("electrical", rootCodes);
        Assert.Contains("hvac", rootCodes);
        Assert.Contains("landscaping", rootCodes);
        Assert.Contains("painting", rootCodes);
        Assert.Contains("roofing", rootCodes);
        Assert.Contains("pest-control", rootCodes);
        Assert.Contains("appliances", rootCodes);
        Assert.Contains("cleaning", rootCodes);
        Assert.Contains("other", rootCodes);
    }

    [Fact]
    public void Seed_AllChildrenHaveValidParents()
    {
        var pkg = TaxonomyCorePackages.SunfishVendorSpecialties;
        var rootCodes = pkg.Nodes.Where(n => n.ParentCode is null).Select(n => n.Id.Code).ToHashSet();

        foreach (var child in pkg.Nodes.Where(n => n.ParentCode is not null))
        {
            Assert.Contains(child.ParentCode!, rootCodes);
        }
    }

    [Fact]
    public void Seed_AllNodesAreActive()
    {
        var pkg = TaxonomyCorePackages.SunfishVendorSpecialties;
        Assert.All(pkg.Nodes, n => Assert.Equal(TaxonomyNodeStatus.Active, n.Status));
    }

    [Fact]
    public void Seed_NodeCodesAreUnique()
    {
        var pkg = TaxonomyCorePackages.SunfishVendorSpecialties;
        var codes = pkg.Nodes.Select(n => n.Id.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct().Count());
    }

    [Fact]
    public void Seed_ChildCodes_FollowParentDottedPrefix()
    {
        // Convention: every child's code starts with "<parent-code>." so
        // the hierarchy is reflected in the code itself + tooling can
        // navigate without consulting the parent pointer.
        var pkg = TaxonomyCorePackages.SunfishVendorSpecialties;
        foreach (var child in pkg.Nodes.Where(n => n.ParentCode is not null))
        {
            Assert.StartsWith($"{child.ParentCode}.", child.Id.Code);
        }
    }

    [Fact]
    public void Seed_DefinitionVersionConsistent()
    {
        var pkg = TaxonomyCorePackages.SunfishVendorSpecialties;
        Assert.All(pkg.Nodes, n => Assert.Equal(pkg.Definition.Version, n.DefinitionVersion));
    }

    [Fact]
    public void Seed_PlumbingChildren_AreThree()
    {
        var pkg = TaxonomyCorePackages.SunfishVendorSpecialties;
        var plumbingChildren = pkg.Nodes.Where(n => n.ParentCode == "plumbing").ToList();
        Assert.Equal(3, plumbingChildren.Count);
    }

    [Fact]
    public void Seed_CleaningChildren_AreFour()
    {
        var pkg = TaxonomyCorePackages.SunfishVendorSpecialties;
        var cleaningChildren = pkg.Nodes.Where(n => n.ParentCode == "cleaning").ToList();
        Assert.Equal(4, cleaningChildren.Count);
    }
}
