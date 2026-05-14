using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Sunfish.Bridge.Proxy;

namespace Sunfish.Bridge.Reports;

/// <summary>
/// Bridge route family for W#60 Phase 5 reporting surface.
/// GET /api/v1/reports/rent-roll      — all properties × units × payment status.
/// GET /api/v1/reports/profit-loss    — P&amp;L summary, optionally filtered by property.
/// GET /api/v1/reports/profit-loss/export — P&amp;L as CSV or JSON download.
/// </summary>
public static class ReportsEndpoints
{
    public static IEndpointRouteBuilder MapReportsEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        var group = app.MapGroup("/api/v1/reports");
        group.MapGet("/rent-roll",           HandleGetRentRollAsync).WithName("GetReportsRentRoll");
        group.MapGet("/profit-loss",         HandleGetProfitLossAsync).WithName("GetReportsProfitLoss");
        group.MapGet("/profit-loss/export",  HandleExportProfitLossAsync).WithName("ExportReportsProfitLoss");
        return app;
    }

    internal static async Task<IResult> HandleGetRentRollAsync(
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        var leaseResult = await client.GetListWithFieldsAsync(
            "Lease", company,
            ["name", "tenant", "property", "unit", "start_date", "end_date", "monthly_rent", "status"],
            limit: 100, ct: ct).ConfigureAwait(false);

        var invoiceResult = await client.GetListWithFieldsAsync(
            "Sales Invoice", company,
            ["customer", "outstanding_amount", "due_date", "posting_date", "status"],
            limit: 200, ct: ct).ConfigureAwait(false);

        // Build per-tenant balance + last-payment-date from invoices
        var tenantBalance  = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var tenantLastPaid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (invoiceResult.TryGetProperty("data", out var invoices) && invoices.ValueKind == JsonValueKind.Array)
        {
            foreach (var inv in invoices.EnumerateArray())
            {
                var customer    = inv.TryGetProperty("customer",           out var c)  ? c.GetString()   ?? "" : "";
                var outstanding = inv.TryGetProperty("outstanding_amount", out var oa) ? oa.GetDecimal()      : 0m;
                var postDate    = inv.TryGetProperty("posting_date",       out var pd) ? pd.GetString()  ?? "" : "";

                if (string.IsNullOrEmpty(customer)) continue;

                tenantBalance[customer] = tenantBalance.TryGetValue(customer, out var prev)
                    ? prev + outstanding
                    : outstanding;

                if (!string.IsNullOrEmpty(postDate) &&
                    (!tenantLastPaid.TryGetValue(customer, out var last) || string.Compare(postDate, last, StringComparison.Ordinal) > 0))
                    tenantLastPaid[customer] = postDate;
            }
        }

        // Compose rent roll rows from leases
        var rows = new List<RentRollRow>();

        if (leaseResult.TryGetProperty("data", out var leases) && leases.ValueKind == JsonValueKind.Array)
        {
            foreach (var lease in leases.EnumerateArray())
            {
                var tenant      = lease.TryGetProperty("tenant",       out var t)  ? t.GetString()  ?? "" : "";
                var property    = lease.TryGetProperty("property",     out var p)  ? p.GetString()  ?? "" : "";
                var unit        = lease.TryGetProperty("unit",         out var u)  ? u.GetString()        : null;
                var startDate   = lease.TryGetProperty("start_date",   out var sd) ? sd.GetString()       : null;
                var endDate     = lease.TryGetProperty("end_date",     out var ed) ? ed.GetString()       : null;
                var monthlyRent = lease.TryGetProperty("monthly_rent", out var mr) ? mr.GetDecimal()      : 0m;
                var leaseStatus = lease.TryGetProperty("status",       out var ls) ? ls.GetString() ?? "" : "";

                var balanceDue      = tenantBalance.TryGetValue(tenant,  out var b)   ? b   : 0m;
                var lastPaymentDate = tenantLastPaid.TryGetValue(tenant, out var lpd) ? lpd : null;

                var rentStatus = leaseStatus == "Active"
                    ? (balanceDue > 0 ? "Overdue" : "Current")
                    : "Vacant";

                rows.Add(new RentRollRow(
                    PropertyId:      property,
                    PropertyName:    property,
                    Unit:            unit,
                    TenantName:      tenant,
                    LeaseStart:      startDate,
                    LeaseEnd:        endDate,
                    MonthlyRent:     Math.Round(monthlyRent, 2),
                    LastPaymentDate: lastPaymentDate,
                    BalanceDue:      Math.Round(balanceDue, 2),
                    Status:          rentStatus));
            }
        }

        return Results.Ok(new { data = rows });
    }

    internal static async Task<IResult> HandleGetProfitLossAsync(
        string? propertyId,
        string? period,
        string? asOf,
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        var data = await ComputeProfitLossAsync(propertyId, period, asOf, client, company, ct).ConfigureAwait(false);
        return Results.Ok(data);
    }

    internal static async Task<IResult> HandleExportProfitLossAsync(
        string? propertyId,
        string? period,
        string? asOf,
        string? format,
        IERPNextClient client,
        IOptions<ERPNextOptions> options,
        CancellationToken ct)
    {
        var company = options.Value.DefaultCompany;
        if (string.IsNullOrWhiteSpace(company))
            return MissingCompanyResult();

        var data = await ComputeProfitLossAsync(propertyId, period, asOf, client, company, ct).ConfigureAwait(false);

        if (format?.Equals("json", StringComparison.OrdinalIgnoreCase) == true)
            return Results.Ok(data);

        // Build CSV
        var csv = new StringBuilder();
        csv.AppendLine("Section,Account,Amount");

        foreach (var line in data.IncomeLines)
            csv.AppendLine($"Income,\"{EscapeCsv(line.Account)}\",{line.Amount:F2}");

        foreach (var line in data.ExpenseLines)
            csv.AppendLine($"Expense,\"{EscapeCsv(line.Account)}\",{line.Amount:F2}");

        csv.AppendLine($"Net,,{data.Net:F2}");

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        var filename = $"profit-loss-{data.Period}.csv";
        return Results.File(bytes, "text/csv", filename);
    }

    private static async Task<ProfitLossData> ComputeProfitLossAsync(
        string? propertyId,
        string? period,
        string? asOf,
        IERPNextClient client,
        string company,
        CancellationToken ct)
    {
        var (fromDate, toDate) = ComputeDateRange(period, asOf);

        var glResult = await client.GetListWithFieldsAsync(
            "GL Entry", company,
            ["account_type", "debit", "credit", "posting_date", "account", "cost_center"],
            limit: 500, ct: ct).ConfigureAwait(false);

        decimal totalIncome = 0m, totalExpenses = 0m;
        var incomeByAccount  = new Dictionary<string, decimal>();
        var expenseByAccount = new Dictionary<string, decimal>();

        if (glResult.TryGetProperty("data", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in rows.EnumerateArray())
            {
                var postDate = row.TryGetProperty("posting_date", out var pd) ? pd.GetString() ?? "" : "";

                // Date-range filter
                if (!string.IsNullOrEmpty(fromDate) && string.Compare(postDate, fromDate, StringComparison.Ordinal) < 0) continue;
                if (!string.IsNullOrEmpty(toDate)   && string.Compare(postDate, toDate,   StringComparison.Ordinal) > 0) continue;

                // Property filter via cost_center (best-effort — cost_center may include property name)
                if (!string.IsNullOrEmpty(propertyId))
                {
                    var costCenter = row.TryGetProperty("cost_center", out var cc) ? cc.GetString() ?? "" : "";
                    if (!costCenter.Contains(propertyId, StringComparison.OrdinalIgnoreCase)) continue;
                }

                var accountType = row.TryGetProperty("account_type", out var at)  ? at.GetString()  : null;
                var account     = row.TryGetProperty("account",      out var acc) ? acc.GetString() ?? "Unknown" : "Unknown";
                var credit      = row.TryGetProperty("credit",       out var cr)  ? cr.GetDecimal()  : 0m;
                var debit       = row.TryGetProperty("debit",        out var dr)  ? dr.GetDecimal()  : 0m;

                if (accountType is "Income" or "Other Income")
                {
                    var net = credit - debit;
                    totalIncome += net;
                    incomeByAccount[account] = incomeByAccount.TryGetValue(account, out var ex) ? ex + net : net;
                }
                else if (accountType is "Expense" or "Cost of Goods Sold")
                {
                    var net = debit - credit;
                    totalExpenses += net;
                    expenseByAccount[account] = expenseByAccount.TryGetValue(account, out var ex) ? ex + net : net;
                }
            }
        }

        var periodLabel = string.IsNullOrEmpty(asOf)
            ? $"{DateTime.UtcNow.Year}"
            : asOf[..Math.Min(7, asOf.Length)];

        return new ProfitLossData(
            Period:       periodLabel,
            PropertyId:   propertyId,
            Income:       Math.Round(totalIncome,    2),
            Expenses:     Math.Round(totalExpenses,  2),
            Net:          Math.Round(totalIncome - totalExpenses, 2),
            IncomeLines:  incomeByAccount.Select(kvp  => new AccountLine(kvp.Key,  Math.Round(kvp.Value, 2))).ToList(),
            ExpenseLines: expenseByAccount.Select(kvp => new AccountLine(kvp.Key,  Math.Round(kvp.Value, 2))).ToList());
    }

    private static (string From, string To) ComputeDateRange(string? period, string? asOf)
    {
        var refDate = string.IsNullOrEmpty(asOf)
            ? DateTime.UtcNow
            : DateTime.TryParse(asOf, out var d) ? d : DateTime.UtcNow;

        return period?.ToLowerInvariant() switch
        {
            "month" => (
                new DateTime(refDate.Year, refDate.Month, 1).ToString("yyyy-MM-dd"),
                new DateTime(refDate.Year, refDate.Month, DateTime.DaysInMonth(refDate.Year, refDate.Month)).ToString("yyyy-MM-dd")),
            "quarter" => (
                new DateTime(refDate.Year, ((refDate.Month - 1) / 3) * 3 + 1, 1).ToString("yyyy-MM-dd"),
                refDate.ToString("yyyy-MM-dd")),
            _ => (
                new DateTime(refDate.Year, 1, 1).ToString("yyyy-MM-dd"),
                refDate.ToString("yyyy-MM-dd")),
        };
    }

    private static string EscapeCsv(string value) =>
        value.Replace("\"", "\"\"");

    private static IResult MissingCompanyResult() =>
        Results.Problem(
            detail: "ERPNext DefaultCompany is not configured. " +
                    "Set ERPNext:DefaultCompany in appsettings.Development.json.",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "ERPNext not configured");

    internal sealed record RentRollRow(
        string PropertyId,
        string PropertyName,
        string? Unit,
        string TenantName,
        string? LeaseStart,
        string? LeaseEnd,
        decimal MonthlyRent,
        string? LastPaymentDate,
        decimal BalanceDue,
        string Status);

    internal sealed record ProfitLossData(
        string Period,
        string? PropertyId,
        decimal Income,
        decimal Expenses,
        decimal Net,
        IReadOnlyList<AccountLine> IncomeLines,
        IReadOnlyList<AccountLine> ExpenseLines);

    internal sealed record AccountLine(string Account, decimal Amount);
}
