using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.Properties.Services;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Blocks.PropertyEquipment.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization;

namespace Sunfish.Bridge.Cockpit;

/// <summary>
/// W#29 Phase 2 — owner cockpit property detail endpoint.
///
/// Aggregates a Property's card, equipment list, and (stubbed for now)
/// lease / work-order / inspection summaries. Per XO ruling on
/// 2026-05-16: lease + WO + inspection aggregation is stubbed because
/// the source services lack PropertyId / unit-join surface — the
/// authoritative fix is workstream W#62 (PropertyUnit substrate).
/// `PropertyDetailDto` shape stays exactly as the hand-off specifies;
/// the stub fields return null/0 with comments naming the upgrade path.
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
        IEquipmentRepository equipment,
        CancellationToken ct)
    {
        TenantId tenant = tenantContext.TenantId;
        var typedId = new PropertyId(propertyId);

        var property = await properties.GetByIdAsync(tenant, typedId, ct).ConfigureAwait(false);
        if (property is null)
            return TypedResults.NotFound();

        var equipmentRows = await equipment.ListByPropertyAsync(tenant, typedId, includeDisposed: false, ct).ConfigureAwait(false);

        var dto = new PropertyDetailDto(
            PropertyId:           property.Id.Value,
            DisplayAddress:       FormatAddress(property),
            Kind:                 property.Kind.ToString(),
            Equipment:            equipmentRows.Select(MapEquipment).ToArray(),
            // Stubbed — W#62 PropertyUnit substrate required before real aggregation.
            ActiveLease:          null,
            OpenWorkOrderCount:   0,
            LastInspectionDate:   null,
            LastInspectionResult: null);

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
/// Wire format for the property-detail endpoint. Shape per W#29 hand-off
/// PR 2; lease/WO/inspection fields are stubbed until W#62 (PropertyUnit
/// substrate) lands the real aggregation surface.
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
/// Lease summary embedded in the property detail. W#62 will populate this
/// from the active lease for the property; until then the endpoint returns
/// <c>null</c> for <c>ActiveLease</c>.
/// </summary>
public record LeaseSummaryDto(
    string LeaseId,
    string TenantDisplayName,
    decimal MonthlyRent,
    DateOnly EndDate);
