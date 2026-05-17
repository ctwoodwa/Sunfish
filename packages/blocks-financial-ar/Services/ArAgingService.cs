using Sunfish.Blocks.FinancialAr.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.FinancialAr.Services;

/// <summary>
/// Default <see cref="IArAgingService"/>. Pure projection over the
/// <see cref="IInvoiceRepository"/> — no caching, no event
/// subscription. Each call materializes a fresh snapshot.
/// </summary>
public sealed class ArAgingService : IArAgingService
{
    private readonly IInvoiceRepository _invoices;

    public ArAgingService(IInvoiceRepository invoices)
    {
        _invoices = invoices ?? throw new ArgumentNullException(nameof(invoices));
    }

    /// <inheritdoc />
    public async Task<AgingSummary> GetAgingForChartAsync(
        ChartOfAccountsId chartId,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        var invoices = await _invoices.ListByChartAsync(chartId, cancellationToken).ConfigureAwait(false);
        return Summarize(invoices, asOf);
    }

    /// <inheritdoc />
    public async Task<AgingSummary> GetAgingForCustomerAsync(
        ChartOfAccountsId chartId,
        PartyId customerId,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        var invoices = await _invoices.ListByCustomerAsync(chartId, customerId, cancellationToken).ConfigureAwait(false);
        return Summarize(invoices, asOf);
    }

    /// <inheritdoc />
    public async Task<AgingSummary> GetAgingForPropertyAsync(
        ChartOfAccountsId chartId,
        string propertyId,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(propertyId)) return AgingSummary.Empty(asOf);
        var all = await _invoices.ListByChartAsync(chartId, cancellationToken).ConfigureAwait(false);
        var scoped = all.Where(i => string.Equals(i.PropertyId, propertyId, StringComparison.Ordinal)).ToList();
        return Summarize(scoped, asOf);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Internals
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pure aggregation over the input invoices. Exposed internally so
    /// tests can verify the algorithm without round-tripping the
    /// repository.
    /// </summary>
    internal static AgingSummary Summarize(IReadOnlyList<Invoice> invoices, DateOnly asOf)
    {
        decimal current = 0m, b0to30 = 0m, b31to60 = 0m, b61to90 = 0m, b90plus = 0m;
        var rows = new List<AgingRow>();

        foreach (var inv in invoices)
        {
            var bucket = AgingClassifier.TryClassify(inv, asOf);
            if (bucket is null) continue;

            var days = asOf.DayNumber - inv.DueDate.DayNumber;
            rows.Add(new AgingRow(
                InvoiceId: inv.Id,
                InvoiceNumber: inv.InvoiceNumber,
                CustomerId: inv.CustomerId,
                PropertyId: inv.PropertyId,
                IssueDate: inv.IssueDate,
                DueDate: inv.DueDate,
                DaysPastDue: days,
                Total: inv.Total,
                AmountPaid: inv.AmountPaid,
                Balance: inv.Balance,
                Bucket: bucket.Value));

            switch (bucket.Value)
            {
                case AgingBucket.Current:     current += inv.Balance; break;
                case AgingBucket.Days0To30:   b0to30  += inv.Balance; break;
                case AgingBucket.Days31To60:  b31to60 += inv.Balance; break;
                case AgingBucket.Days61To90:  b61to90 += inv.Balance; break;
                case AgingBucket.Days90Plus:  b90plus += inv.Balance; break;
            }
        }

        // Deterministic order: DueDate ascending, then InvoiceNumber ordinal as a tie-breaker.
        rows.Sort((a, b) =>
        {
            var d = a.DueDate.DayNumber.CompareTo(b.DueDate.DayNumber);
            return d != 0 ? d : string.CompareOrdinal(a.InvoiceNumber, b.InvoiceNumber);
        });

        var total = current + b0to30 + b31to60 + b61to90 + b90plus;
        return new AgingSummary(asOf, current, b0to30, b31to60, b61to90, b90plus, total, rows);
    }
}
