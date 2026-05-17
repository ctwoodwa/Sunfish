namespace Sunfish.Blocks.FinancialAp.Models;

/// <summary>
/// Bucket-classification rules for AP aging. Pure function — same
/// inputs always produce the same bucket. Single source of truth so
/// the service, reporting layer, and any future analytics agree on
/// what "61 days overdue" means.
/// </summary>
public static class AgingClassifier
{
    /// <summary>
    /// Classify a bill into an <see cref="AgingBucket"/>.
    ///
    /// <para>
    /// Only bills in an open status (<see cref="BillStatus.Received"/>,
    /// <see cref="BillStatus.Approved"/>, <see cref="BillStatus.PartiallyPaid"/>)
    /// with positive balance are classifiable. Drafts, terminals,
    /// <see cref="BillStatus.Disputed"/> (hold), and zero/negative balance
    /// rows return null — they don't belong on the AP aging report.
    /// </para>
    ///
    /// <para>
    /// "Days past due" = <paramref name="asOf"/> minus <c>Bill.DueDate</c>;
    /// negative or zero values (not yet past due) land in
    /// <see cref="AgingBucket.Current"/>.
    /// </para>
    /// </summary>
    public static AgingBucket? TryClassify(Bill bill, DateOnly asOf)
    {
        if (bill is null) throw new ArgumentNullException(nameof(bill));
        if (!bill.Status.IsOpen()) return null;
        if (bill.Balance <= 0m) return null;

        var daysPastDue = asOf.DayNumber - bill.DueDate.DayNumber;
        return ClassifyByDays(daysPastDue);
    }

    /// <summary>
    /// Internal helper for callers that have a precomputed
    /// "days past due" integer rather than a <see cref="Bill"/>.
    /// Negative inputs map to <see cref="AgingBucket.Current"/>.
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
