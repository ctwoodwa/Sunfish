using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Sunfish.Bridge.Proxy;

/// <summary>
/// Bridge route family for the W#60 ERPNext proxy surface.
/// Phase 1 ships only GET /api/v1/erpnext/properties.
/// Subsequent phases add leases, payments, accounting, and maintenance.
/// </summary>
public static class ERPNextProxy
{
    public static IEndpointRouteBuilder MapERPNextProxy(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/v1/erpnext");
        group.MapGet("/properties", HandleGetPropertiesAsync).WithName("GetERPNextProperties");
        return app;
    }

    internal static async Task<IResult> HandleGetPropertiesAsync(
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        // Phase 1: company sourced from config DefaultCompany.
        // TODO(W#60 UserService): replace with user.FindFirstValue("company") once
        // OIDC + UserService claim wiring lands.
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
        {
            return Results.Problem(
                detail: "ERPNext DefaultCompany is not configured. " +
                        "Set ERPNext:DefaultCompany in appsettings.Development.json.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "ERPNext not configured");
        }

        var result = await client.GetResourceListAsync("Property", company, ct: ct).ConfigureAwait(false);
        return Results.Ok(result);
    }
}
