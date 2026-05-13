using System.Text.Json;
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

        // Phase 4 — accounting summary + outstanding balances.
        // Note (HALT condition): ERPNext P&L Statement report requires admin privileges;
        // we use GL Entry queries instead (narrower, accessible to API key users).
        group.MapGet("/accounting/summary",     HandleGetAccountingSummaryAsync).WithName("GetERPNextAccountingSummary");
        group.MapGet("/accounting/outstanding", HandleGetAccountingOutstandingAsync).WithName("GetERPNextAccountingOutstanding");

        // Phase 5 — maintenance queue.
        group.MapGet("/maintenance",         HandleGetMaintenanceAsync).WithName("GetERPNextMaintenance");
        group.MapPost("/maintenance",        HandlePostMaintenanceAsync).WithName("PostERPNextMaintenance");
        group.MapMethods("/maintenance/{name}", ["PATCH"], HandlePatchMaintenanceAsync).WithName("PatchERPNextMaintenance");

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

    internal static async Task<IResult> HandleGetAccountingSummaryAsync(
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        // Query GL Entry for income (credits) and expense (debits) by account_type.
        // Limit 500 — adequate for CO's portfolio size in Phase 2 demo.
        var glResult = await client.GetListWithFieldsAsync(
            "GL Entry", company,
            ["account_type", "debit", "credit", "posting_date", "account"],
            limit: 500, ct: ct).ConfigureAwait(false);

        decimal totalIncome = 0m, totalExpenses = 0m;
        if (glResult.TryGetProperty("data", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in rows.EnumerateArray())
            {
                var accountType = row.TryGetProperty("account_type", out var at) ? at.GetString() : null;
                var credit = row.TryGetProperty("credit", out var cr) ? cr.GetDecimal() : 0m;
                var debit  = row.TryGetProperty("debit",  out var dr) ? dr.GetDecimal() : 0m;
                if (accountType is "Income" or "Other Income")
                    totalIncome += credit - debit;
                else if (accountType is "Expense" or "Cost of Goods Sold")
                    totalExpenses += debit - credit;
            }
        }

        var period = $"{DateTime.UtcNow.Year}";
        return Results.Ok(new
        {
            period,
            income   = Math.Round(totalIncome, 2),
            expenses = Math.Round(totalExpenses, 2),
            net      = Math.Round(totalIncome - totalExpenses, 2),
        });
    }

    internal static async Task<IResult> HandleGetAccountingOutstandingAsync(
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        // Outstanding Sales Invoices (tenant receivables).
        var result = await client.GetListWithFieldsAsync(
            "Sales Invoice", company,
            ["customer", "outstanding_amount", "due_date", "name", "status"],
            limit: 100, ct: ct).ConfigureAwait(false);

        return Results.Ok(result);
    }

    internal static async Task<IResult> HandleGetMaintenanceAsync(
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        var result = await client.GetListWithFieldsAsync(
            "Maintenance Ticket", company,
            ["name", "subject", "property", "status", "priority", "assigned_to", "cost"],
            limit: 50, ct: ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    internal static async Task<IResult> HandlePostMaintenanceAsync(
        CreateMaintenanceRequest body,
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        var result = await client.PostAsync("Maintenance Ticket", body, company, ct).ConfigureAwait(false);
        return Results.Ok(result);
    }

    internal static async Task<IResult> HandlePatchMaintenanceAsync(
        string name,
        UpdateMaintenanceRequest body,
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        var result = await client.PutAsync("Maintenance Ticket", name, body, company, ct).ConfigureAwait(false);
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

    internal sealed record CreateMaintenanceRequest(
        string Subject,
        string Property,
        string Priority,
        string? AssignedTo,
        string? Description);

    internal sealed record UpdateMaintenanceRequest(
        string? Status,
        string? AssignedTo,
        decimal? Cost,
        string? Resolution);
}
