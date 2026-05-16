using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Sunfish.Blocks.Inspections.Models;
using Sunfish.Blocks.Inspections.Services;
using Sunfish.Blocks.Leases.Models;
using Sunfish.Blocks.Leases.Services;
using Sunfish.Blocks.Maintenance.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.Properties.Services;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Blocks.PropertyEquipment.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization;

namespace Sunfish.Bridge.Cockpit;

/// <summary>
/// W#29 Phase 2 (initial stubbed) + W#29 Phase 1.5 / W#62 Phase 2 (this
/// upgrade) — owner cockpit property detail endpoint.
///
/// Aggregates a Property's card, equipment list, active lease, last
/// inspection, and open-work-order count. With W#62 PR 1
/// (PropertyUnit substrate) landed, the lease + inspection paths are
/// now live: walk Property → PropertyUnit → {Lease, Inspection} via
/// `IPropertyUnitRepository.ListByPropertyAsync` + in-memory filter on
/// the EntityId UnitId already carried on Lease / Inspection.
///
/// <c>OpenWorkOrderCount</c> remains stubbed at 0 until W#62 PR 3 adds
/// `WorkOrder.PropertyId` + `ListWorkOrdersQuery.PropertyId`. This
/// endpoint will then drop the stub for a real count.
/// </summary>
public static class PropertyDetailEndpoint
{
    /// <summary>Attaches GET /{propertyId}/detail under an existing cockpit route group.</summary>
    public static RouteGroupBuilder MapPropertyDetail(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);
        group.MapGet("/{propertyId}/detail", HandleGetPropertyDetailAsync).WithName("CockpitGetPropertyDetail");
        return group;
    }

    internal static async Task<Results<Ok<PropertyDetailDto>, NotFound>> HandleGetPropertyDetailAsync(
        string propertyId,
        ITenantContext tenantContext,
        IPropertyRepository properties,
        IPropertyUnitRepository units,
        IEquipmentRepository equipment,
        ILeaseService leases,
        IInspectionsService inspections,
        IMaintenanceService maintenance,
        CancellationToken ct)
    {
        TenantId tenant = tenantContext.TenantId;
        var typedId = new PropertyId(propertyId);

        var property = await properties.GetByIdAsync(tenant, typedId, ct).ConfigureAwait(false);
        if (property is null)
            return TypedResults.NotFound();

        var equipmentRows = await equipment.ListByPropertyAsync(tenant, typedId, includeDisposed: false, ct).ConfigureAwait(false);

        // W#62 Phase 2 aggregation. Walk Property → Units → {Lease, Inspection}.
        // Empty-units short-circuit keeps the query free of pointless full-tenant
        // scans on properties that have no units yet (e.g., land parcels).
        var propertyUnits = await units.ListByPropertyAsync(tenant, typedId, ct).ConfigureAwait(false);
        var unitIds = propertyUnits.Select(u => u.Id).ToHashSet();

        LeaseSummaryDto? activeLease = null;
        DateOnly? lastInspectionDate = null;
        string? lastInspectionResult = null;

        if (unitIds.Count > 0)
        {
            // Active lease — first match wins; multi-active per property is an
            // edge case (multi-unit property where each unit has its own active
            // lease) we surface as "any active lease" for the Phase 1.5 cut.
            await foreach (var lease in leases.ListAsync(new ListLeasesQuery { Phase = LeasePhase.Active }, ct).ConfigureAwait(false))
            {
                if (!unitIds.Contains(lease.UnitId)) continue;
                activeLease = new LeaseSummaryDto(
                    LeaseId:           lease.Id.Value,
                    TenantDisplayName: lease.Tenants.Count > 0 ? lease.Tenants[0].Value : "(no tenant)",
                    MonthlyRent:       lease.MonthlyRent,
                    EndDate:           lease.EndDate);
                break;
            }

            // Last inspection on any unit — sort by ScheduledDate descending.
            // We post-filter on UnitId because ListInspectionsQuery has a UnitId
            // filter but it only matches one unit at a time; cheaper to fetch
            // all-for-tenant and filter in memory than loop per-unit.
            Inspection? latest = null;
            await foreach (var inspection in inspections.ListInspectionsAsync(new ListInspectionsQuery(), ct).ConfigureAwait(false))
            {
                if (!unitIds.Contains(inspection.UnitId)) continue;
                if (latest is null || inspection.ScheduledDate > latest.ScheduledDate)
                    latest = inspection;
            }
            if (latest is not null)
            {
                lastInspectionDate   = latest.ScheduledDate;
                lastInspectionResult = latest.Phase.ToString();
            }
        }

        // W#62 PR 3 — open-work-order count via the new PropertyId filter on
        // ListWorkOrdersQuery. "Open" excludes terminal states (Closed,
        // Cancelled) so the cockpit count reflects live obligations.
        var openCount = 0;
        await foreach (var wo in maintenance.ListWorkOrdersAsync(
            new ListWorkOrdersQuery { PropertyId = typedId }, ct).ConfigureAwait(false))
        {
            if (wo.Status is WorkOrderStatus.Closed or WorkOrderStatus.Cancelled) continue;
            openCount++;
        }

        var dto = new PropertyDetailDto(
            PropertyId:           property.Id.Value,
            DisplayAddress:       FormatAddress(property),
            Kind:                 property.Kind.ToString(),
            Equipment:            equipmentRows.Select(MapEquipment).ToArray(),
            ActiveLease:          activeLease,
            OpenWorkOrderCount:   openCount,
            LastInspectionDate:   lastInspectionDate,
            LastInspectionResult: lastInspectionResult);

        return TypedResults.Ok(dto);
    }

    private static string FormatAddress(Property property)
    {
        var a = property.Address;
        return string.IsNullOrWhiteSpace(a.Line2)
            ? $"{a.Line1}, {a.City}, {a.Region} {a.PostalCode}"
            : $"{a.Line1} {a.Line2}, {a.City}, {a.Region} {a.PostalCode}";
    }

    private static EquipmentSummaryDto MapEquipment(Equipment e) => new(
        EquipmentId: e.Id.Value,
        DisplayName: e.DisplayName,
        Class:       e.Class.ToString(),
        Make:        e.Make,
        Model:       e.Model,
        InstalledAt: e.InstalledAt is null ? null : DateOnly.FromDateTime(e.InstalledAt.Value.UtcDateTime),
        LocationInProperty: e.LocationInProperty);
}

/// <summary>
/// Wire format for the property-detail endpoint. Shape per W#29 hand-off PR 2.
/// W#62 Phase 2 populated <see cref="ActiveLease"/>,
/// <see cref="LastInspectionDate"/>, and <see cref="LastInspectionResult"/>.
/// W#62 Phase 3 (this PR) populates <see cref="OpenWorkOrderCount"/> from the
/// new <c>WorkOrder.PropertyId</c> FK + <c>ListWorkOrdersQuery.PropertyId</c>
/// filter; all four hand-off DTO fields are now live.
/// </summary>
public record PropertyDetailDto(
    string PropertyId,
    string DisplayAddress,
    string Kind,
    IReadOnlyList<EquipmentSummaryDto> Equipment,
    LeaseSummaryDto? ActiveLease,
    int OpenWorkOrderCount,
    DateOnly? LastInspectionDate,
    string? LastInspectionResult);

/// <summary>One row in the equipment list of a property-detail response.</summary>
public record EquipmentSummaryDto(
    string EquipmentId,
    string DisplayName,
    string Class,
    string? Make,
    string? Model,
    DateOnly? InstalledAt,
    string? LocationInProperty);

/// <summary>
/// Lease summary embedded in the property detail. Populated from the
/// first <see cref="LeasePhase.Active"/> lease whose <c>UnitId</c> belongs
/// to the property.
/// </summary>
public record LeaseSummaryDto(
    string LeaseId,
    string TenantDisplayName,
    decimal MonthlyRent,
    DateOnly EndDate);
