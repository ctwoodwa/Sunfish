using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Sunfish.Bridge.Proxy;
using Sunfish.Bridge.Reports;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Reports;

public sealed class ReportsEndpointsTests
{
    private const string DefaultCompany = "Royal Key Management LLC";

    // ── Rent Roll ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetRentRoll_ReturnsOk_WithRentRollRows()
    {
        var leaseData = JsonDocument.Parse("""
            {"data":[
              {"name":"LEASE-0001","tenant":"John Doe","property":"PROP-0001",
               "unit":"A","start_date":"2025-01-01","end_date":"2025-12-31",
               "monthly_rent":1500,"status":"Active"}
            ]}
            """).RootElement.Clone();

        var invoiceData = JsonDocument.Parse("""
            {"data":[
              {"customer":"John Doe","outstanding_amount":0,"posting_date":"2025-04-01","status":"Paid"}
            ]}
            """).RootElement.Clone();

        var client = new SequencedFakeERPNextClient([leaseData, invoiceData]);
        var options = Options.Create(new ERPNextOptions { DefaultCompany = DefaultCompany });

        var result = await ReportsEndpoints.HandleGetRentRollAsync(client, options, CancellationToken.None);

        // Returns Ok<anonymous> — verify it is not a problem/error result
        Assert.NotNull(result);
        Assert.False(result is Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult,
            "Expected Ok result, but got ProblemHttpResult");
    }

    [Fact]
    public async Task GetRentRoll_Returns503_WhenCompanyNotConfigured()
    {
        var client = new SequencedFakeERPNextClient([]);
        var options = Options.Create(new ERPNextOptions { DefaultCompany = "" });

        var result = await ReportsEndpoints.HandleGetRentRollAsync(client, options, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(result);
    }

    // ── Profit & Loss ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetProfitLoss_ReturnsOk_WithGlSummary()
    {
        var glData = JsonDocument.Parse("""
            {"data":[
              {"account_type":"Income","account":"Rent Income","credit":5000,"debit":0,"posting_date":"2026-01-15","cost_center":""},
              {"account_type":"Expense","account":"Repairs","credit":0,"debit":800,"posting_date":"2026-01-20","cost_center":""}
            ]}
            """).RootElement.Clone();

        var client = new SequencedFakeERPNextClient([glData]);
        var options = Options.Create(new ERPNextOptions { DefaultCompany = DefaultCompany });

        var result = await ReportsEndpoints.HandleGetProfitLossAsync(
            propertyId: null, period: "year", asOf: "2026-05-14",
            client, options, CancellationToken.None);

        var ok = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.Ok<ReportsEndpoints.ProfitLossData>>(result);
        Assert.NotNull(ok.Value);
        Assert.Equal(5000m, ok.Value!.Income);
        Assert.Equal(800m,  ok.Value!.Expenses);
        Assert.Equal(4200m, ok.Value!.Net);
    }

    [Fact]
    public async Task GetProfitLoss_Returns503_WhenCompanyNotConfigured()
    {
        var client = new SequencedFakeERPNextClient([]);
        var options = Options.Create(new ERPNextOptions { DefaultCompany = "" });

        var result = await ReportsEndpoints.HandleGetProfitLossAsync(
            propertyId: null, period: null, asOf: null,
            client, options, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.ProblemHttpResult>(result);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns successive JsonElement responses for each GetListWithFieldsAsync call.
    /// Supports handlers that call the client multiple times (e.g., rent roll = leases + invoices).
    /// </summary>
    private sealed class SequencedFakeERPNextClient(IReadOnlyList<JsonElement> responses) : IERPNextClient
    {
        private int _callIndex;

        private JsonElement NextResponse() =>
            _callIndex < responses.Count ? responses[_callIndex++] : default;

        public Task<JsonElement> GetResourceListAsync(
            string doctype, string company,
            IDictionary<string, object>? extraFilters = null,
            int limit = 20, CancellationToken ct = default) =>
            Task.FromResult(NextResponse());

        public Task<JsonElement> GetResourceAsync(
            string doctype, string name, string company, CancellationToken ct = default) =>
            Task.FromResult(NextResponse());

        public Task<JsonElement> PostAsync(
            string endpoint, object payload, string company, CancellationToken ct = default) =>
            Task.FromResult(NextResponse());

        public Task<JsonElement> GetListWithFieldsAsync(
            string doctype, string company,
            IReadOnlyList<string> fields,
            int limit = 100, CancellationToken ct = default) =>
            Task.FromResult(NextResponse());

        public Task<JsonElement> PutAsync(
            string doctype, string name, object payload, string company, CancellationToken ct = default) =>
            Task.FromResult(NextResponse());
    }
}
