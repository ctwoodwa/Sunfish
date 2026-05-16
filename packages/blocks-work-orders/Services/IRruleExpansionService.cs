namespace Sunfish.Blocks.WorkOrders.Services;

/// <summary>
/// Expand an RFC 5545 RRULE string into a concrete sequence of due
/// dates. PR 2 ships an in-process stub that handles the
/// <c>FREQ=DAILY</c>, <c>FREQ=WEEKLY</c>, and <c>FREQ=MONTHLY</c>
/// cases (with optional <c>INTERVAL=N</c>); complex RRULE expansion
/// (BYDAY / BYMONTHDAY / EXDATE / COUNT) is deferred to a follow-on
/// hand-off that pulls in the <c>Ical.Net</c> NuGet dependency.
/// </summary>
public interface IRruleExpansionService
{
    /// <summary>
    /// Return all due dates that fall within
    /// [<paramref name="start"/> + <paramref name="leadDays"/>,
    /// <paramref name="end"/> or <c>start + lookaheadDays</c>],
    /// inclusive, per the parsed <paramref name="rrule"/>.
    /// </summary>
    /// <param name="rrule">RFC 5545 RRULE string (FREQ=… [;INTERVAL=N]).</param>
    /// <param name="start">Recurrence anchor.</param>
    /// <param name="end">Optional hard upper bound (e.g., <c>MaintenanceSchedule.EndsOn</c>).</param>
    /// <param name="lookaheadDays">Soft upper bound when <paramref name="end"/> is null.</param>
    /// <param name="leadDays">Skip occurrences that fall earlier than (today + leadDays).</param>
    /// <param name="today">Wall-clock today (callers can substitute for tests).</param>
    /// <param name="timezone">IANA timezone id (informational; v1 stub treats RRULE date-naive).</param>
    IReadOnlyList<DateOnly> ExpandOccurrences(
        string rrule,
        DateOnly start,
        DateOnly? end,
        int lookaheadDays,
        int leadDays,
        DateOnly today,
        string timezone);
}
