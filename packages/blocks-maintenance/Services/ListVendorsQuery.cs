using Sunfish.Blocks.Maintenance.Models;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>
/// Optional filter parameters for <see cref="IMaintenanceService.ListVendorsAsync"/>.
/// All filters are additive (AND). A <see langword="null"/> value means "no filter on that field".
/// </summary>
public sealed record ListVendorsQuery
{
    /// <summary>When set, only vendors with this specialty are returned.</summary>
    public VendorSpecialty? Specialty { get; init; }

    /// <summary>When set, only vendors with this status are returned.</summary>
    public VendorStatus? Status { get; init; }

    /// <summary>Shared empty query that applies no filters.</summary>
    public static ListVendorsQuery Empty { get; } = new();
}
