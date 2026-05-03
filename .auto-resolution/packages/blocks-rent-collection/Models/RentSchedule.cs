namespace Sunfish.Blocks.RentCollection.Models;

/// <summary>
/// Defines the recurring billing parameters for a tenancy.
/// One schedule per lease period is the expected cardinality, though the model allows multiple.
/// </summary>
/// <param name="Id">Unique schedule identifier.</param>
/// <param name="LeaseId">
/// Opaque reference to the associated lease. Uses a plain <see langword="string"/> in this pass
/// so that <c>blocks-rent-collection</c> and <c>blocks-leases</c> (G14) can ship independently.
/// TODO: migrate to <c>Sunfish.Blocks.Leases.Models.LeaseId</c> once G14 is on main.
/// </param>
/// <param name="StartDate">The date on which rent obligations begin.</param>
/// <param name="EndDate">Optional end date; <see langword="null"/> means open-ended.</param>
/// <param name="MonthlyAmount">
/// The base monthly rent amount in the application's default currency.
/// <b>Precision note:</b> stored as <see cref="decimal"/> with a two-decimal-place
/// assumption (e.g. 1500.00). Rounding enforcement is deferred to a follow-up — do not
/// assume the service will truncate or round values supplied by callers.
/// </param>
/// <param name="DueDayOfMonth">
/// Calendar day (1–28 inclusive) on which rent is due each billing period.
/// Capped at 28 to avoid month-end ambiguity across February and 30-day months.
/// </param>
/// <param name="Frequency">How often invoices are generated from this schedule.</param>
public sealed record RentSchedule(
    RentScheduleId Id,
    string LeaseId,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal MonthlyAmount,
    int DueDayOfMonth,
    BillingFrequency Frequency);
