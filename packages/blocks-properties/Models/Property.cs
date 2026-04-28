using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.Properties.Models;

/// <summary>
/// Root entity of the property-operations vertical. Every downstream domain
/// (Assets, Inspections, Leases, Work Orders, Receipts, Public Listings,
/// Owner Cockpit) FKs to <see cref="Property"/>. First-slice scope: the
/// root entity itself; <c>PropertyUnit</c> child + <c>PropertyOwnershipRecord</c>
/// event log are deferred to follow-up hand-offs.
/// </summary>
/// <remarks>
/// Implements <see cref="IMustHaveTenant"/>: every property is owned by a
/// concrete tenant (LLC). Cross-tenant ownership questions
/// (e.g. holding-co reading into child LLCs) are tracked in workstream #1
/// and intentionally not resolved here.
/// </remarks>
public sealed record Property : IMustHaveTenant
{
    /// <summary>Stable identifier for this property.</summary>
    public required PropertyId Id { get; init; }

    /// <summary>Owning tenant. Required (default-rejected by persistence adapters).</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>Human-friendly name (often the street address, e.g. <c>"123 Main St"</c>).</summary>
    public required string DisplayName { get; init; }

    /// <summary>Postal address. Used for showings, mapping, and tax-jurisdiction inference.</summary>
    public required PostalAddress Address { get; init; }

    /// <summary>
    /// Assessor parcel number / parcel ID. Nullable for jurisdictions that do
    /// not issue one or for properties whose APN has not yet been recorded.
    /// </summary>
    public string? ParcelNumber { get; init; }

    /// <summary>Coarse classification driving downstream behaviour.</summary>
    public required PropertyKind Kind { get; init; }

    /// <summary>
    /// Acquisition cost basis for tax/depreciation purposes.
    /// </summary>
    /// <remarks>
    /// Stored as <see cref="decimal"/> until ADR 0051 (Foundation.Integrations.Payments)
    /// is Accepted; will be migrated to a typed <c>Money</c> value object in a
    /// one-line follow-up commit. Currency assumed USD until ADR 0051 introduces
    /// a typed currency code.
    /// TODO: Money — gated on ADR 0051 acceptance.
    /// </remarks>
    public decimal? AcquisitionCost { get; init; }

    /// <summary>Acquisition timestamp; pairs with <see cref="AcquisitionCost"/>.</summary>
    public DateTimeOffset? AcquiredAt { get; init; }

    /// <summary>Year the structure was built. Optional; null for land or unknown.</summary>
    public int? YearBuilt { get; init; }

    /// <summary>Total interior square footage across all units. Optional.</summary>
    public decimal? TotalSquareFeet { get; init; }

    /// <summary>Total bedroom count across all units (sum for multi-unit). Optional.</summary>
    public int? TotalBedrooms { get; init; }

    /// <summary>Total bathroom count across all units. Allows half-baths (e.g. <c>1.5</c>).</summary>
    public decimal? TotalBathrooms { get; init; }

    /// <summary>Free-text operator notes.</summary>
    public string? Notes { get; init; }

    /// <summary>
    /// Reference to a primary photo in the existing blob storage substrate.
    /// First-slice carries the FK as an opaque string; the Bridge blob-ingest
    /// API integration is gated on cluster cross-cutting OQ3.
    /// </summary>
    public string? PrimaryPhotoBlobRef { get; init; }

    /// <summary>Record-creation timestamp; immutable after first persist.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Disposition timestamp (sale, transfer, demolition). Soft-delete marker:
    /// records remain queryable via <c>includeDisposed: true</c> but are
    /// excluded from default listings.
    /// </summary>
    public DateTimeOffset? DisposedAt { get; init; }

    /// <summary>Free-text reason captured on disposition (sold, transferred, etc.).</summary>
    public string? DisposalReason { get; init; }
}
