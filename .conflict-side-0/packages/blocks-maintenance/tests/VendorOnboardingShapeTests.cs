using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Taxonomy.Models;
using Xunit;

namespace Sunfish.Blocks.Maintenance.Tests;

/// <summary>
/// W#18 Phase 1: verifies the Vendor record's new init-only shape +
/// default-value invariants per ADR 0058.
/// </summary>
public sealed class VendorOnboardingShapeTests
{
    [Fact]
    public async Task NewVendor_Defaults_SpecialtiesEmpty_ContactsEmpty_OnboardingPending()
    {
        var svc = new InMemoryMaintenanceService();
        var v = await svc.CreateVendorAsync(new CreateVendorRequest
        {
            DisplayName = "Defaults Test",
        });

        Assert.Empty(v.Specialties);
        Assert.Empty(v.Contacts);
        Assert.Equal(VendorOnboardingState.Pending, v.OnboardingState);
        Assert.Null(v.W9);
        Assert.Null(v.PaymentPreference);
        Assert.Equal(VendorStatus.Active, v.Status); // CreateVendorRequest default
    }

    [Fact]
    public async Task NewVendor_AcceptsExplicitOnboardingState()
    {
        var svc = new InMemoryMaintenanceService();
        var v = await svc.CreateVendorAsync(new CreateVendorRequest
        {
            DisplayName = "Onboarding Test",
            OnboardingState = VendorOnboardingState.W9Requested,
        });

        Assert.Equal(VendorOnboardingState.W9Requested, v.OnboardingState);
    }

    [Fact]
    public async Task NewVendor_AcceptsMultipleSpecialties()
    {
        var svc = new InMemoryMaintenanceService();
        var v = await svc.CreateVendorAsync(new CreateVendorRequest
        {
            DisplayName = "Multi-Trade",
            Specialties = new[]
            {
                VendorSpecialtyClassifications.FromLegacyEnum(VendorSpecialty.Plumbing),
                VendorSpecialtyClassifications.FromLegacyEnum(VendorSpecialty.HVAC),
            },
        });

        Assert.Equal(2, v.Specialties.Count);
        Assert.Contains(v.Specialties, c => c.Code == "plumbing");
        Assert.Contains(v.Specialties, c => c.Code == "hvac");
    }

    [Fact]
    public void VendorSpecialtyClassifications_FromLegacyEnum_BindsToTaxonomy()
    {
        var c = VendorSpecialtyClassifications.FromLegacyEnum(VendorSpecialty.Roofing);
        Assert.Equal(new TaxonomyDefinitionId("Sunfish", "Vendor", "Specialties"), c.Definition);
        Assert.Equal("roofing", c.Code);
        Assert.Equal(TaxonomyVersion.V1_0_0, c.Version);
    }

    [Fact]
    public void VendorSpecialtyClassifications_FromLegacyEnum_CoversEveryEnumValue()
    {
        // Migration invariant: every existing enum value MUST map to a
        // taxonomy classification (so callers using the legacy enum
        // can mechanically migrate without losing semantic meaning).
        foreach (VendorSpecialty value in Enum.GetValues<VendorSpecialty>())
        {
            var c = VendorSpecialtyClassifications.FromLegacyEnum(value);
            Assert.False(string.IsNullOrEmpty(c.Code));
        }
    }

    [Fact]
    public async Task ListVendorsQuery_OnboardingState_FiltersCorrectly()
    {
        var svc = new InMemoryMaintenanceService();
        await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "Pending", OnboardingState = VendorOnboardingState.Pending });
        await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "Active", OnboardingState = VendorOnboardingState.Active });
        await svc.CreateVendorAsync(new CreateVendorRequest { DisplayName = "Suspended", OnboardingState = VendorOnboardingState.Suspended });

        var actives = new List<Vendor>();
        await foreach (var v in svc.ListVendorsAsync(new ListVendorsQuery { OnboardingState = VendorOnboardingState.Active }))
        {
            actives.Add(v);
        }

        Assert.Single(actives);
        Assert.Equal("Active", actives[0].DisplayName);
    }

    [Fact]
    public async Task ListVendorsQuery_SpecialtyCodeFilter_MatchesByCode()
    {
        var svc = new InMemoryMaintenanceService();
        await svc.CreateVendorAsync(new CreateVendorRequest
        {
            DisplayName = "MultiTrade",
            Specialties = new[]
            {
                VendorSpecialtyClassifications.FromLegacyEnum(VendorSpecialty.Plumbing),
                VendorSpecialtyClassifications.FromLegacyEnum(VendorSpecialty.HVAC),
            },
        });
        await svc.CreateVendorAsync(new CreateVendorRequest
        {
            DisplayName = "RoofingOnly",
            Specialties = VendorSpecialtyClassifications.ToList(VendorSpecialty.Roofing),
        });

        var hvacVendors = new List<Vendor>();
        await foreach (var v in svc.ListVendorsAsync(new ListVendorsQuery { SpecialtyCode = "hvac" }))
        {
            hvacVendors.Add(v);
        }

        Assert.Single(hvacVendors);
        Assert.Equal("MultiTrade", hvacVendors[0].DisplayName);
    }

    [Fact]
    public void Vendor_RequiresInitOnlyConstruction()
    {
        // Compile-time + reflection-level check: the Vendor record uses
        // init-only fields (no positional ctor). This test fails if the
        // record reverts to positional, which would re-introduce the v0.x
        // breaking change.
        var ctors = typeof(Vendor).GetConstructors();
        // The compiler-generated parameterless ctor for an init-only
        // record is the canonical shape; positional records expose a
        // single ctor with all parameters.
        Assert.Contains(ctors, c => c.GetParameters().Length == 0);
    }
}
