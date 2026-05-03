using Sunfish.Blocks.Maintenance.Models;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Optional filter parameters for <see cref="IMaintenanceService.ListVendorsAsync"/>.
/// All filters are additive (AND). A <see langword="null"/> value means
/// "no filter on that field".
/// </summary>
/// <remarks>
/// W#18 Phase 1 migration: the pre-W#18 <c>Specialty</c> enum filter is
/// replaced by <see cref="SpecialtyCode"/> (a taxonomy code string from
/// <c>Sunfish.Vendor.Specialties@1.0.0</c>). Use
/// <see cref="VendorSpecialtyClassifications.FromLegacyEnum"/> +
/// <c>.Code</c> to obtain the migration-equivalent code from a legacy
/// enum value.
/// </remarks>
public sealed record ListVendorsQuery
{
    /// <summary>When set, only vendors whose <c>Specialties</c> list contains a classification with this <c>Code</c> are returned.</summary>
    public string? SpecialtyCode { get; init; }

    /// <summary>When set, only vendors with this status are returned.</summary>
    public VendorStatus? Status { get; init; }

    /// <summary>When set, only vendors in this onboarding state are returned (W#18 Phase 1).</summary>
    public VendorOnboardingState? OnboardingState { get; init; }

    /// <summary>Shared empty query that applies no filters.</summary>
    public static ListVendorsQuery Empty { get; } = new();
}
