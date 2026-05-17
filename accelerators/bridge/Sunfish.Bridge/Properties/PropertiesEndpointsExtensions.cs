using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Sunfish.Bridge.Authorization;

namespace Sunfish.Bridge.Properties;

/// <summary>
/// DI / routing extension for the
/// <see cref="PropertiesEndpoints"/> top-level Bridge route family.
/// </summary>
public static class PropertiesEndpointsExtensions
{
    /// <summary>
    /// Register <c>GET /api/v1/properties</c> under
    /// <see cref="AuthenticatedTenantPolicy.PolicyName"/>. Per W#74
    /// PR 1 hand-off §3.1 step 6.
    /// </summary>
    public static IEndpointRouteBuilder MapPropertiesEndpoints(this IEndpointRouteBuilder app)
    {
        System.ArgumentNullException.ThrowIfNull(app);
        var group = app
            .MapGroup("/api/v1/properties")
            .RequireAuthorization(AuthenticatedTenantPolicy.PolicyName);
        group.MapGet("/", PropertiesEndpoints.HandleListPropertiesAsync).WithName("ListProperties");
        return app;
    }
}
