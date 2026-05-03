using Sunfish.Blocks.Maintenance.Models;

namespace Sunfish.Blocks.Maintenance.Services;

/// <summary>Payload for sending a new <see cref="Rfq"/> to one or more vendors.</summary>
public sealed record SendRfqRequest
{
    /// <summary>The maintenance request this RFQ is associated with.</summary>
    public required MaintenanceRequestId RequestId { get; init; }

    /// <summary>Vendors invited to respond. Must contain at least one entry.</summary>
    public required IReadOnlyList<VendorId> InvitedVendors { get; init; }

    /// <summary>Date by which vendor responses are expected.</summary>
    public required DateOnly ResponseDueDate { get; init; }

    /// <summary>Description of the work scope vendors should quote against.</summary>
    public required string Scope { get; init; }
}
