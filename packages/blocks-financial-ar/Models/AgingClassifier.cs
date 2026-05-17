namespace Sunfish.Blocks.FinancialAr.Models;

/// <summary>
/// Bucket-classification rules for AR aging. Pure function — same
/// inputs always produce the same bucket, no dependency on
/// repository state. The single source of truth so the service,
/// reporting layer, and any future analytics agree on what
/// "61 days overdue" means.
/// </summary>
public static class AgingClassifier
{
    /// <summary>
    /// Classify an invoice into an <see cref="AgingBucket"/>.
    ///
    /// <para>
    /// Only invoices in an open status (<see cref="InvoiceStatus.Issued"/> /
    /// <see cref="InvoiceStatus.PartiallyPaid"/>) with positive balance
    /// are classifiable. Drafts, terminals, and zero-balance rows return
    /// null — they don't belong on the aging report.
    /// </para>
    ///
    /// <para>
    /// "Days past due" = <paramref name="asOf"/> minus <c>Invoice.DueDate</c>;
    /// negative or zero values (not yet past due) land in
    /// <see cref="AgingBucket.Current"/>.
    /// </para>
    /// </summary>
    public static AgingBucket? TryClassify(Invoice invoice, DateOnly asOf)
    {
        if (invoice is null) throw new ArgumentNullException(nameof(invoice));
        if (!invoice.Status.IsOpen()) return null;
        if (invoice.Balance <= 0m) return null;

        var daysPastDue = asOf.DayNumber - invoice.DueDate.DayNumber;
        return ClassifyByDays(daysPastDue);
    }

    /// <summary>
    /// Internal helper exposed for callers that have a precomputed
    /// "days past due" integer rather than an <see cref="Invoice"/>
    /// instance. Negative inputs map to <see cref="AgingBucket.Current"/>.
    /// </summary>
    public static AgingBucket ClassifyByDays(int daysPastDue) =>
        daysPastDue switch
        {
            <= 0 => AgingBucket.Current,
            <= 30 => AgingBucket.Days0To30,
            <= 60 => AgingBucket.Days31To60,
            <= 90 => AgingBucket.Days61To90,
            _     => AgingBucket.Days90Plus,
        };
}
