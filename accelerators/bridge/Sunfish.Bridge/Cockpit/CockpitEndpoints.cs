using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.Properties.Services;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Authorization;

namespace Sunfish.Bridge.Cockpit;

/// <summary>
/// Bridge route family for the W#29 Owner Web Cockpit.
/// All routes are guarded by <c>CockpitPolicy</c> (authenticated +
/// <see cref="CockpitPermissions.CanEnterCockpit"/>).
///
/// Phase 1 (this PR) ships only the property-selector endpoint backing the
/// cockpit landing page. PRs 2–5 attach detail / work-order / vendor /
/// dashboard endpoints under the same <c>/api/v1/cockpit</c> group.
/// </summary>
public static class CockpitEndpoints
{
    /// <summary>Authorization policy name used by all cockpit routes.</summary>
    public const string CockpitPolicyName = "CockpitPolicy";

    /// <summary>
    /// Registers <see cref="CockpitPolicyName"/>. Must be called from
    /// <c>AddAuthorization</c>'s configuration callback (or via a follow-up
    /// <c>AddPolicy</c>).
    /// </summary>
    public static AuthorizationOptions AddCockpitPolicy(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.AddPolicy(CockpitPolicyName, policy =>
        {
            policy.RequireAuthenticatedUser();
            policy.RequireAssertion(ctx =>
            {
                var role = ctx.User.FindFirst("role")?.Value
                           ?? ctx.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
                return CockpitPermissions.CanEnterCockpit(role);
            });
        });
        return options;
    }

    /// <summary>Maps the cockpit route family. Group is guarded by <see cref="CockpitPolicyName"/>.</summary>
    public static IEndpointRouteBuilder MapCockpitEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/v1/cockpit").RequireAuthorization(CockpitPolicyName);
        group.MapGet("/properties", HandleListPropertiesAsync).WithName("CockpitListProperties");
        group.MapPropertyDetail();
        group.MapWorkOrders();
        group.MapVendors();
        group.MapDashboard();
        return app;
    }

    /// <summary>
    /// PR 1 endpoint: returns the property summary list for the authenticated
    /// tenant. Backs the cockpit landing page property selector.
    /// </summary>
    internal static async Task<Ok<PropertySelectorListDto>> HandleListPropertiesAsync(
        ITenantContext tenantContext,
        IPropertyRepository properties,
        CancellationToken ct)
    {
        TenantId tenant = tenantContext.TenantId;
        var rows = await properties.ListByTenantAsync(tenant, includeDisposed: false, ct).ConfigureAwait(false);

        var items = rows
            .Select(p => new PropertySelectorItemDto(
                p.Id.Value,
                p.DisplayName,
                p.Kind.ToString(),
                p.Address.City,
                p.Address.Region))
            .ToArray();

        return TypedResults.Ok(new PropertySelectorListDto(items));
    }
}

/// <summary>Wire-format envelope for the property-selector endpoint.</summary>
public record PropertySelectorListDto(IReadOnlyList<PropertySelectorItemDto> Properties);

/// <summary>One row in the property-selector list. <c>Region</c> is the
/// state/province per <see cref="Sunfish.Blocks.Properties.Models.PostalAddress.Region"/>.</summary>
public record PropertySelectorItemDto(
    string PropertyId,
    string DisplayName,
    string Kind,
    string City,
    string Region);
