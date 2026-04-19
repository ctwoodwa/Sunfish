using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// A Request for Quote sent to one or more vendors for a specific <see cref="MaintenanceRequest"/>.
/// </summary>
/// <param name="Id">Unique identifier for this RFQ.</param>
/// <param name="RequestId">The maintenance request this RFQ is associated with.</param>
/// <param name="InvitedVendors">Vendors invited to respond with a quote.</param>
/// <param name="ResponseDueDate">The date by which vendor responses are expected.</param>
/// <param name="Scope">Description of the work scope vendors should quote against.</param>
/// <param name="Status">Current lifecycle status of this RFQ.</param>
/// <param name="SentAtUtc">The instant this RFQ was first persisted.</param>
public sealed record Rfq(
    RfqId Id,
    MaintenanceRequestId RequestId,
    IReadOnlyList<VendorId> InvitedVendors,
    DateOnly ResponseDueDate,
    string Scope,
    RfqStatus Status,
    Instant SentAtUtc);
