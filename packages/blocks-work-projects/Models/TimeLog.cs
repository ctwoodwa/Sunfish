namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Read-side projection over <see cref="TimeEntry"/> rows for a
/// <c>(workerPartyId, dateRange)</c> tuple per Stage 02 §2.20. Used
/// by the time-log UI; not the canonical entity.
/// </summary>
/// <param name="WorkerPartyId">FK to the worker party.</param>
/// <param name="From">Inclusive range start.</param>
/// <param name="Until">Inclusive range end.</param>
/// <param name="Entries">Entries in chronological order (StartedAt ascending).</param>
/// <param name="TotalMinutes">Sum of <see cref="TimeEntry.DurationMinutes"/> across <see cref="Entries"/>.</param>
/// <param name="BillableMinutes">Sum of <see cref="TimeEntry.DurationMinutes"/> for billable entries only.</param>
/// <param name="TotalAmount">Sum of <see cref="TimeEntry.Amount"/> across billable+stopped entries; null when no entries have amounts.</param>
public sealed record TimeLog(
    Guid WorkerPartyId,
    DateOnly From,
    DateOnly Until,
    IReadOnlyList<TimeEntry> Entries,
    int TotalMinutes,
    int BillableMinutes,
    decimal? TotalAmount)
{
    /// <summary>
    /// Build a <see cref="TimeLog"/> from a flat list of entries
    /// (filtering + ordering happens here). The list is filtered to
    /// entries whose <see cref="TimeEntry.WorkerPartyId"/> matches
    /// + whose <see cref="TimeEntry.StartedAt"/> falls within
    /// [<paramref name="from"/> 00:00 UTC, <paramref name="until"/>
    /// 23:59:59.9999999 UTC] inclusive.
    /// </summary>
    /// <remarks>
    /// WINDOWING IS UTC-BASED. An entry started 2026-05-16T23:30:00-08:00
    /// (Pacific) — i.e., 2026-05-17T07:30:00Z — lands on UTC-day
    /// 2026-05-17, not the worker's local 2026-05-16. Callers that need
    /// local-day semantics must pre-shift <paramref name="from"/> +
    /// <paramref name="until"/> to the worker's timezone before calling.
    /// A TZ-aware overload is tracked for PR 6 service-layer compose.
    /// </remarks>
    public static TimeLog Build(
        Guid workerPartyId,
        DateOnly from,
        DateOnly until,
        IEnumerable<TimeEntry> entries)
    {
        if (until < from)
            throw new ArgumentException(
                "Until must be >= From.", nameof(until));

        var fromUtc = from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var untilUtc = until.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var filtered = entries
            .Where(e => e.WorkerPartyId == workerPartyId
                        && e.DeletedAt is null
                        && e.StartedAt.Value.UtcDateTime >= fromUtc
                        && e.StartedAt.Value.UtcDateTime <= untilUtc)
            .OrderBy(e => e.StartedAt.Value)
            .ToList();

        var totalMin    = filtered.Sum(e => e.DurationMinutes);
        var billableMin = filtered.Where(e => e.Billable).Sum(e => e.DurationMinutes);
        decimal? total  = filtered.Where(e => e.Amount is not null).Sum(e => e.Amount!.Value);
        if (total == 0m && !filtered.Any(e => e.Amount is not null)) total = null;

        return new TimeLog(workerPartyId, from, until, filtered, totalMin, billableMin, total);
    }
}
