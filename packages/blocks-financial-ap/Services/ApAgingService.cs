using Sunfish.Blocks.FinancialAp.Models;
using Sunfish.Blocks.FinancialLedger.Models;
using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.FinancialAp.Services;

/// <summary>
/// Default <see cref="IApAgingService"/>. Pure projection over
/// <see cref="IBillRepository"/> — no caching, no event subscription.
/// Each call materializes a fresh snapshot.
/// </summary>
public sealed class ApAgingService : IApAgingService
{
    private readonly IBillRepository _bills;

    public ApAgingService(IBillRepository bills)
    {
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
    }

    /// <inheritdoc />
    public async Task<AgingSummary> GetAgingForChartAsync(
        ChartOfAccountsId chartId,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        var bills = await _bills.ListByChartAsync(chartId, cancellationToken).ConfigureAwait(false);
        return Summarize(bills, asOf);
    }

    /// <inheritdoc />
    public async Task<AgingSummary> GetAgingForVendorAsync(
        ChartOfAccountsId chartId,
        PartyId vendorId,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        var bills = await _bills.ListByVendorAsync(chartId, vendorId, cancellationToken).ConfigureAwait(false);
        return Summarize(bills, asOf);
    }

    /// <inheritdoc />
    public async Task<AgingSummary> GetAgingForPropertyAsync(
        ChartOfAccountsId chartId,
        string propertyId,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(propertyId)) return AgingSummary.Empty(asOf);
        var all = await _bills.ListByChartAsync(chartId, cancellationToken).ConfigureAwait(false);
        var scoped = all.Where(b => string.Equals(b.PropertyId, propertyId, StringComparison.Ordinal)).ToList();
        return Summarize(scoped, asOf);
    }

    // ──────────────────────────────────────────────────────────────────
    //  Internals
    // ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Pure aggregation over the input bills. Exposed internally so tests
    /// can verify the algorithm without round-tripping the repository.
    /// </summary>
    internal static AgingSummary Summarize(IReadOnlyList<Bill> bills, DateOnly asOf)
    {
        decimal current = 0m, b0to30 = 0m, b31to60 = 0m, b61to90 = 0m, b90plus = 0m;
        var rows = new List<AgingRow>();

        foreach (var bill in bills)
        {
            var bucket = AgingClassifier.TryClassify(bill, asOf);
            if (bucket is null) continue;

            var days = asOf.DayNumber - bill.DueDate.DayNumber;
            rows.Add(new AgingRow(
                BillId: bill.Id,
                BillNumber: bill.BillNumber,
                VendorId: bill.VendorId,
                PropertyId: bill.PropertyId,
                BillDate: bill.BillDate,
                DueDate: bill.DueDate,
                DaysPastDue: days,
                Total: bill.Total,
                AmountPaid: bill.AmountPaid,
                Balance: bill.Balance,
                Bucket: bucket.Value));

            switch (bucket.Value)
            {
                case AgingBucket.Current:     current += bill.Balance; break;
                case AgingBucket.Days0To30:   b0to30  += bill.Balance; break;
                case AgingBucket.Days31To60:  b31to60 += bill.Balance; break;
                case AgingBucket.Days61To90:  b61to90 += bill.Balance; break;
                case AgingBucket.Days90Plus:  b90plus += bill.Balance; break;
            }
        }

        rows.Sort((a, b) =>
        {
            var d = a.DueDate.DayNumber.CompareTo(b.DueDate.DayNumber);
            return d != 0 ? d : string.CompareOrdinal(a.BillNumber, b.BillNumber);
        });

        var total = current + b0to30 + b31to60 + b61to90 + b90plus;
        return new AgingSummary(asOf, current, b0to30, b31to60, b61to90, b90plus, total, rows);
    }
}
