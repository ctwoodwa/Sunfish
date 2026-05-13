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

        // Phase 3 — leases + rent collection.
        group.MapGet("/leases",             HandleGetLeasesAsync).WithName("GetERPNextLeases");
        group.MapGet("/leases/{name}",      HandleGetLeaseAsync).WithName("GetERPNextLease");
        group.MapGet("/payments",           HandleGetPaymentsAsync).WithName("GetERPNextPayments");
        group.MapGet("/payments/{name}",    HandleGetPaymentAsync).WithName("GetERPNextPayment");
        group.MapPost("/payments",          HandlePostPaymentAsync).WithName("PostERPNextPayment");

        return app;
    }

    internal static async Task<IResult> HandleGetPropertiesAsync(
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        var result = await client.GetResourceListAsync("Property", company, ct: ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    internal static async Task<IResult> HandleGetLeasesAsync(
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        var result = await client.GetResourceListAsync("Lease", company, limit: 50, ct: ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    internal static async Task<IResult> HandleGetLeaseAsync(
        string name,
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        var result = await client.GetResourceAsync("Lease", name, company, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    internal static async Task<IResult> HandleGetPaymentsAsync(
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        var result = await client.GetResourceListAsync("Payment", company, limit: 100, ct: ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    internal static async Task<IResult> HandleGetPaymentAsync(
        string name,
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        var result = await client.GetResourceAsync("Payment", name, company, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    internal static async Task<IResult> HandlePostPaymentAsync(
        RecordPaymentRequest body,
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        var result = await client.PostAsync("Payment", body, company, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    private static IResult MissingCompanyResult() =>
        Results.Problem(
            detail: "ERPNext DefaultCompany is not configured. " +
                    "Set ERPNext:DefaultCompany in appsettings.Development.json.",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "ERPNext not configured");

    // DTO for POST /api/v1/erpnext/payments — matches ERPNext Payment doctype fields.
    internal sealed record RecordPaymentRequest(
        string Lease,
        decimal Amount,
        string Date,
        string PaymentMethod);
}
