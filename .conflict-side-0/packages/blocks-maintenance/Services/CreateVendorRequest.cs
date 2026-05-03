using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Foundation.Taxonomy.Models;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Payload for creating a new <see cref="Vendor"/>. Migrated to init-only
/// W#18 Phase 1 per ADR 0058 amendment A2.
/// </summary>
public sealed record CreateVendorRequest
{
    /// <summary>Human-readable display name for the vendor.</summary>
    public required string DisplayName { get; init; }

    /// <summary>Optional name of the primary contact person.</summary>
    public string? ContactName { get; init; }

    /// <summary>Optional email address for the primary contact.</summary>
    public string? ContactEmail { get; init; }

    /// <summary>Optional phone number for the primary contact.</summary>
    public string? ContactPhone { get; init; }

    /// <summary>
    /// Trades + service categories this vendor offers. Empty list when no
    /// specialty has been recorded yet. References nodes in
    /// <c>Sunfish.Vendor.Specialties@1.0.0</c> (seed shipped W#18 Phase 6 / PR #346).
    /// Use <see cref="VendorSpecialtyClassifications.ToList"/> for mechanical
    /// migration from the legacy <see cref="VendorSpecialty"/> enum.
    /// </summary>
    public IReadOnlyList<TaxonomyClassification> Specialties { get; init; } = Array.Empty<TaxonomyClassification>();

    /// <summary>Initial lifecycle status. Defaults to <see cref="VendorStatus.Active"/>.</summary>
    public VendorStatus Status { get; init; } = VendorStatus.Active;

    /// <summary>Initial onboarding-flow state. Defaults to <see cref="VendorOnboardingState.Pending"/> per ADR 0058.</summary>
    public VendorOnboardingState OnboardingState { get; init; } = VendorOnboardingState.Pending;
}
