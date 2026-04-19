using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// A price quotation submitted by a <see cref="Vendor"/> in response to an RFQ or direct solicitation.
/// </summary>
/// <param name="Id">Unique identifier for this quote.</param>
/// <param name="VendorId">The vendor that submitted this quote.</param>
/// <param name="RequestId">The maintenance request this quote addresses.</param>
/// <param name="Amount">Quoted cost in the property currency. Always <see cref="decimal"/>.</param>
/// <param name="ValidUntil">The last date on which this quote is valid for acceptance.</param>
/// <param name="Scope">Optional description of the work scope covered by this quote.</param>
/// <param name="Status">Current lifecycle status of this quote.</param>
/// <param name="SubmittedAtUtc">The instant this quote was first persisted.</param>
public sealed record Quote(
    QuoteId Id,
    VendorId VendorId,
    MaintenanceRequestId RequestId,
    decimal Amount,
    DateOnly ValidUntil,
    string? Scope,
    QuoteStatus Status,
    Instant SubmittedAtUtc);
