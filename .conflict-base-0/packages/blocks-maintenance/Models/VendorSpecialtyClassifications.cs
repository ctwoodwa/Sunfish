using Sunfish.Foundation.Taxonomy.Models;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Mechanical migration helpers between the pre-W#18 <see cref="VendorSpecialty"/>
/// enum and the W#18 Phase 6 <c>Sunfish.Vendor.Specialties@1.0.0</c>
/// taxonomy (seed shipped in PR #346). Existing callers using the enum
/// should call <see cref="FromLegacyEnum"/> to obtain the equivalent
/// <see cref="TaxonomyClassification"/> reference.
/// </summary>
public static class VendorSpecialtyClassifications
{
    /// <summary>The taxonomy this helper binds to.</summary>
    public static readonly TaxonomyDefinitionId Taxonomy = new("Sunfish", "Vendor", "Specialties");

    /// <summary>The version pinned at migration time. Update when the taxonomy ships a new major version.</summary>
    public static readonly TaxonomyVersion Version = TaxonomyVersion.V1_0_0;

    /// <summary>Maps a legacy <see cref="VendorSpecialty"/> enum value to its W#18 Phase 6 taxonomy classification.</summary>
    public static TaxonomyClassification FromLegacyEnum(VendorSpecialty specialty)
    {
        var code = specialty switch
        {
            VendorSpecialty.GeneralContractor => "general-contractor",
            VendorSpecialty.Plumbing => "plumbing",
            VendorSpecialty.Electrical => "electrical",
            VendorSpecialty.HVAC => "hvac",
            VendorSpecialty.Landscaping => "landscaping",
            VendorSpecialty.Painting => "painting",
            VendorSpecialty.Roofing => "roofing",
            VendorSpecialty.PestControl => "pest-control",
            VendorSpecialty.Appliances => "appliances",
            VendorSpecialty.Cleaning => "cleaning",
            VendorSpecialty.Other => "other",
            _ => throw new ArgumentOutOfRangeException(nameof(specialty), specialty, "Unknown VendorSpecialty enum value."),
        };
        return new TaxonomyClassification
        {
            Definition = Taxonomy,
            Code = code,
            Version = Version,
        };
    }

    /// <summary>Builds a singleton classification list from a legacy enum value (convenience for <see cref="Vendor.Specialties"/>).</summary>
    public static IReadOnlyList<TaxonomyClassification> ToList(VendorSpecialty specialty) =>
        new[] { FromLegacyEnum(specialty) };
}
