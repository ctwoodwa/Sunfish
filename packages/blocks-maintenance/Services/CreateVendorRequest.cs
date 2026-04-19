using Sunfish.Blocks.Maintenance.Models;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>Payload for creating a new <see cref="Vendor"/>.</summary>
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

    /// <summary>The trade or specialization this vendor provides.</summary>
    public required VendorSpecialty Specialty { get; init; }

    /// <summary>Initial lifecycle status. Defaults to <see cref="VendorStatus.Active"/>.</summary>
    public VendorStatus Status { get; init; } = VendorStatus.Active;
}
