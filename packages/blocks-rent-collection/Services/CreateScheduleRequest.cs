using Sunfish.Blocks.RentCollection.Models;

namespace Sunfish.Blocks.RentCollection.Services;

/// <summary>Input model for creating a new <see cref="RentSchedule"/>.</summary>
/// <param name="LeaseId">
/// Opaque lease reference. See <see cref="RentSchedule.LeaseId"/> for the migration TODO.
/// </param>
/// <param name="StartDate">The date on which rent obligations begin.</param>
/// <param name="EndDate">Optional end date; <see langword="null"/> means open-ended.</param>
/// <param name="MonthlyAmount">
/// Base monthly rent amount.
/// <para><b>Precision note:</b> two-decimal-place assumption; rounding enforcement deferred.</para>
/// </param>
/// <param name="DueDayOfMonth">Calendar day (1–28) on which rent is due each period.</param>
/// <param name="Frequency">How often invoices are generated.</param>
public sealed record CreateScheduleRequest(
    string LeaseId,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal MonthlyAmount,
    int DueDayOfMonth,
    BillingFrequency Frequency = BillingFrequency.Monthly);
