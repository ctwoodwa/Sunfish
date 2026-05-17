using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.HttpResults;
using Sunfish.Blocks.Properties.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization;

namespace Sunfish.Bridge.Properties;

/// <summary>
/// Top-level Bridge route family for the Anchor React Cohort 1
/// PropertiesPage rebind (W#74 PR 1). Mirrors the cockpit
/// PropertySelector endpoint shape but lives outside the
/// <c>/api/v1/cockpit</c> group + uses
/// <see cref="Sunfish.Bridge.Authorization.AuthenticatedTenantPolicy"/>
/// instead of CockpitPolicy so any authenticated tenant user can
/// load the page.
/// </summary>
/// <remarks>
/// EntityTag-derived server-side filtering is a NO-OP in Phase 1 —
/// <c>Property.EntityTag</c> + <c>IEntityTagResolver</c> ship with
/// W#64 (not yet on main as of this PR's authoring). When W#64 lands,
/// a follow-on touch-up wires the filter via
/// <c>IPropertyRepository.ListByEntityTagAsync</c>. The DTO carries
/// an <c>entityTag</c> read-only echo field today so the frontend
/// surface is forward-compatible.
/// </remarks>
public static class PropertiesEndpoints
{
    /// <summary>PR 1 endpoint: list properties for the authenticated tenant.</summary>
    internal static async Task<Ok<PropertyListDto>> HandleListPropertiesAsync(
        ITenantContext tenantContext,
        IPropertyRepository properties,
        CancellationToken ct)
    {
        var tenant = new TenantId(tenantContext.TenantId);

        // TODO(W#64): once IEntityTagResolver lands on main + IPropertyRepository
        // gains ListByEntityTagAsync, branch on the resolver's GetCurrentEntityTag()
        // and call ListByEntityTagAsync when non-null. Until then, fall through
        // to the unfiltered tenant-scoped list per hand-off §3.1 step 5.
        var rows = await properties
            .ListByTenantAsync(tenant, includeDisposed: false, ct)
            .ConfigureAwait(false);

        var items = rows
            .Select(ToSummaryDto)
            .ToArray();

        return TypedResults.Ok(new PropertyListDto(items));
    }

    private static PropertySummaryDto ToSummaryDto(Sunfish.Blocks.Properties.Models.Property p)
        => new PropertySummaryDto(
            PropertyId: p.Id.Value,
            DisplayName: p.DisplayName,
            Kind: p.Kind.ToString(),
            AddressLine1: p.Address.Line1,
            City: p.Address.City,
            Region: p.Address.Region,
            // PropertyUnit child entity ships in a follow-on hand-off; default 0 per hand-off §3.1 step 4.
            UnitCount: 0,
            // Property.Status is not yet a field on the model; default "Active" per hand-off §3.1 step 4.
            // TODO: when Property.Status lands, map: { Disposed → "Sold"; underMaint → "Maintenance"; ... }
            Status: "Active",
            // EntityTag is W#64's contribution; null until then per hand-off step 5.
            EntityTag: null);
}

/// <summary>Wire-format envelope for <c>GET /api/v1/properties</c>.</summary>
public record PropertyListDto(IReadOnlyList<PropertySummaryDto> Properties);

/// <summary>
/// One row in the property-list response. <c>Region</c> is the
/// state/province per
/// <see cref="Sunfish.Blocks.Properties.Models.PostalAddress.Region"/>.
/// <c>EntityTag</c> is a server-side echo for read-only display;
/// the frontend MUST NOT pass an <c>?entityTag=</c> query parameter —
/// server is the source of truth.
/// </summary>
public record PropertySummaryDto(
    string PropertyId,
    string DisplayName,
    string Kind,
    string? AddressLine1,
    string City,
    string Region,
    int UnitCount,
    string Status,
    string? EntityTag);
