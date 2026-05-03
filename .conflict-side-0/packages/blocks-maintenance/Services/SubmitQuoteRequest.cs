using Sunfish.Blocks.Maintenance.Models;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>Payload for a vendor submitting a <see cref="Quote"/>.</summary>
public sealed record SubmitQuoteRequest
{
    /// <summary>The vendor submitting this quote.</summary>
    public required VendorId VendorId { get; init; }

    /// <summary>The maintenance request being quoted.</summary>
    public required MaintenanceRequestId RequestId { get; init; }

    /// <summary>Total quoted cost. Must be non-negative. Always <see cref="decimal"/>.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Last date on which this quote is valid for acceptance.</summary>
    public required DateOnly ValidUntil { get; init; }

    /// <summary>Optional description of work scope covered by this quote.</summary>
    public string? Scope { get; init; }
}
