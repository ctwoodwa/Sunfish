using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Sunfish.Federation.PatternC.Tests.Endpoints;

/// <summary>
/// Minimal ASP.NET endpoint used by the Pattern C portal node. Returns the requested
/// inspection id plus an access timestamp when <see cref="MacaroonAuthMiddleware"/> has
/// flagged the request as macaroon-valid; otherwise the middleware will have already
/// short-circuited before the endpoint is hit.
/// </summary>
/// <remarks>
/// This is test-only scaffolding for the §10.4 worked example. A production portal would
/// wire a real inspection service, a capability check via <c>PolicyEvaluator</c>, and a
/// response contract from <c>Sunfish.Blocks.*</c>.
/// </remarks>
internal static class InspectionEndpoint
{
    /// <summary>
    /// Registers <c>GET /portal/inspections/{id}</c>. Returns <c>200 OK</c> with a small JSON
    /// payload when the request has been marked macaroon-valid upstream, or <c>401</c> if the
    /// middleware failed to mark it (defensive — should not be reachable in normal flow).
    /// </summary>
    public static void MapInspectionEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/portal/inspections/{id}", (HttpContext ctx, string id) =>
        {
            if (ctx.Items.TryGetValue("macaroon-valid", out var flag) && flag is true)
            {
                return Results.Ok(new
                {
                    id,
                    accessedAt = DateTimeOffset.UtcNow,
                    location = ctx.Items["macaroon-location"] as string,
                    identifier = ctx.Items["macaroon-identifier"] as string,
                });
            }
            return Results.Unauthorized();
        });
    }
}
