using Sunfish.Blocks.People.Foundation.Models;

namespace Sunfish.Blocks.FinancialAr.Models;

/// <summary>
/// One row of an aging-snapshot. Carries the smallest set of fields
/// the rent-roll / collections UIs need to render a usable line:
/// invoice identity + customer + property + balance + bucket +
/// days-past-due.
/// </summary>
/// <param name="InvoiceId">Stable id for the source invoice.</param>
/// <param name="InvoiceNumber">Customer-facing number for display.</param>
/// <param name="CustomerId">The party holding the customer role.</param>
/// <param name="PropertyId">Optional cost-center / property handle.</param>
/// <param name="IssueDate">Issue date for tie-breaking sort in some reports.</param>
/// <param name="DueDate">Due date — the bucket anchor.</param>
/// <param name="DaysPastDue">Negative when not yet due; floor-zero when bucketed as <see cref="AgingBucket.Current"/>.</param>
/// <param name="Total">Original total (Subtotal + Tax).</param>
/// <param name="AmountPaid">Cumulative payment applied.</param>
/// <param name="Balance">Open balance — what actually rolls up into the bucket totals.</param>
/// <param name="Bucket">Which bucket this row belongs to.</param>
public sealed record AgingRow(
    InvoiceId InvoiceId,
    string InvoiceNumber,
    PartyId CustomerId,
    string? PropertyId,
    DateOnly IssueDate,
    DateOnly DueDate,
    int DaysPastDue,
    decimal Total,
    decimal AmountPaid,
    decimal Balance,
    AgingBucket Bucket);

/// <summary>
/// Aggregate aging totals across a population of <see cref="AgingRow"/>.
/// All-zero defaults when no rows match a bucket.
/// </summary>
/// <param name="AsOf">Snapshot date used to compute days-past-due.</param>
/// <param name="Current">Sum of <see cref="AgingRow.Balance"/> in <see cref="AgingBucket.Current"/>.</param>
/// <param name="Days0To30">Sum in 1–30 days past due.</param>
/// <param name="Days31To60">Sum in 31–60 days past due.</param>
/// <param name="Days61To90">Sum in 61–90 days past due.</param>
/// <param name="Days90Plus">Sum in 91+ days past due.</param>
/// <param name="Total">Convenience: total of the five buckets.</param>
/// <param name="Rows">The contributing rows in deterministic order (DueDate ascending, then InvoiceNumber ordinal).</param>
public sealed record AgingSummary(
    DateOnly AsOf,
    decimal Current,
    decimal Days0To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Days90Plus,
    decimal Total,
    IReadOnlyList<AgingRow> Rows)
{
    /// <summary>Empty snapshot — useful as a default when no invoices match.</summary>
    public static AgingSummary Empty(DateOnly asOf) =>
        new(asOf, 0m, 0m, 0m, 0m, 0m, 0m, Array.Empty<AgingRow>());
}
