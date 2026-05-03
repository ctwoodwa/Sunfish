using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.PropertyEquipment.Models;

/// <summary>
/// A physical piece of equipment attached to a <see cref="Property"/> — water heater,
/// HVAC unit, appliance, roof, vehicle, etc. Provides the inventory
/// backbone for inspections, work orders, receipts (acquisition cost
/// basis), and depreciation reporting.
/// </summary>
/// <remarks>
/// Implements <see cref="IMustHaveTenant"/>; persistence adapters reject
/// records with the default <see cref="TenantId"/>. Every equipment record has a
/// required <see cref="Property"/> FK — there are no orphan equipment.
/// PropertyUnit-level scoping (multi-unit modelling) is deferred until
/// the PropertyUnit child entity ships in a follow-up hand-off; until
/// then, multi-unit properties carry equipment records at the property level
/// only.
/// </remarks>
public sealed record Equipment : IMustHaveTenant
{
    /// <summary>Stable identifier for this equipment.</summary>
    public required EquipmentId Id { get; init; }

    /// <summary>Owning tenant. Required (default-rejected by persistence adapters).</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>FK to the parent <see cref="Property"/>. Required (no orphan equipment).</summary>
    public required PropertyId Property { get; init; }

    /// <summary>Coarse classification driving downstream behaviour.</summary>
    public required EquipmentClass Class { get; init; }

    /// <summary>Human-friendly name (e.g. <c>"Master bath water heater"</c>).</summary>
    public required string DisplayName { get; init; }

    /// <summary>Manufacturer (e.g. <c>"Rheem"</c>).</summary>
    public string? Make { get; init; }

    /// <summary>Model number (e.g. <c>"XR50T06EC36U1"</c>).</summary>
    public string? Model { get; init; }

    /// <summary>Serial number captured from the nameplate.</summary>
    public string? SerialNumber { get; init; }

    /// <summary>Free-text location within the property (e.g. <c>"Garage west wall"</c>).</summary>
    public string? LocationInProperty { get; init; }

    /// <summary>Installation timestamp.</summary>
    public DateTimeOffset? InstalledAt { get; init; }

    /// <summary>
    /// Acquisition cost basis for tax / depreciation purposes.
    /// </summary>
    /// <remarks>
    /// Stored as <see cref="decimal"/> until ADR 0051 (Foundation.Integrations.Payments)
    /// is Accepted; will be migrated to a typed <c>Money</c> value object in a
    /// one-line follow-up commit. Currency assumed USD until ADR 0051 introduces
    /// a typed currency code.
    /// TODO: Money — gated on ADR 0051 acceptance.
    /// </remarks>
    public decimal? AcquisitionCost { get; init; }

    /// <summary>
    /// Opaque reference to the receipt that documents the acquisition cost.
    /// First-slice ships this as a <see cref="string"/>; will be migrated to a
    /// typed <c>ReceiptId?</c> when the Receipts module ships (workstream #26).
    /// TODO: ReceiptId — gated on Receipts first-slice merge.
    /// </summary>
    public string? AcquisitionReceiptRef { get; init; }

    /// <summary>Expected useful life in years (e.g. <c>12</c> for a residential water heater).</summary>
    public int? ExpectedUsefulLifeYears { get; init; }

    /// <summary>Optional warranty metadata.</summary>
    public WarrantyMetadata? Warranty { get; init; }

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
    /// Disposition timestamp (replacement, sale, demolition). Soft-delete marker:
    /// records remain queryable via <c>includeDisposed: true</c> but are
    /// excluded from default listings.
    /// </summary>
    public DateTimeOffset? DisposedAt { get; init; }

    /// <summary>Free-text reason captured on disposition (replaced, sold, scrapped, etc.).</summary>
    public string? DisposalReason { get; init; }
}
