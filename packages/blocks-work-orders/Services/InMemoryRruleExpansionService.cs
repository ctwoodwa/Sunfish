using System.Globalization;

namespace Sunfish.Blocks.WorkOrders.Services;

/// <summary>
/// In-process <see cref="IRruleExpansionService"/> handling the
/// FREQ=DAILY / WEEKLY / MONTHLY [;INTERVAL=N] subset of RFC 5545.
/// Complex RRULE features (BYDAY, BYMONTHDAY, EXDATE, COUNT, UNTIL)
/// are deferred to a follow-on hand-off that introduces a dependency
/// on <c>Ical.Net</c>.
/// </summary>
public sealed class InMemoryRruleExpansionService : IRruleExpansionService
{
    /// <inheritdoc />
    public IReadOnlyList<DateOnly> ExpandOccurrences(
        string rrule,
        DateOnly start,
        DateOnly? end,
        int lookaheadDays,
        int leadDays,
        DateOnly today,
        string timezone)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rrule);
        var (freq, interval) = ParseSimpleRrule(rrule);
        var horizon = end ?? today.AddDays(lookaheadDays);
        var earliest = today.AddDays(leadDays);

        var result = new List<DateOnly>();
        var cursor = start;
        // Cap the loop at a defensive bound so a malformed
        // interval (e.g., 0) cannot infinite-loop.
        const int maxIterations = 10_000;
        var i = 0;
        while (cursor <= horizon && i++ < maxIterations)
        {
            if (cursor >= earliest)
                result.Add(cursor);

            cursor = freq switch
            {
                "DAILY"   => cursor.AddDays(interval),
                "WEEKLY"  => cursor.AddDays(7 * interval),
                "MONTHLY" => cursor.AddMonths(interval),
                _ => throw new NotSupportedException(
                    $"RRULE FREQ '{freq}' is not supported by the InMemory stub. "
                    + "Supported: DAILY / WEEKLY / MONTHLY. Complex RRULE expansion "
                    + "(BYDAY / BYMONTHDAY / EXDATE / COUNT / UNTIL) lands in the "
                    + "Ical.Net-backed follow-on hand-off."),
            };
        }
        return result;
    }

    private static (string Freq, int Interval) ParseSimpleRrule(string rrule)
    {
        string? freq = null;
        int interval = 1;
        foreach (var token in rrule.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = token.IndexOf('=');
            if (eq < 0) continue;
            var key = token[..eq].Trim().ToUpperInvariant();
            var value = token[(eq + 1)..].Trim();
            switch (key)
            {
                case "FREQ":
                    freq = value.ToUpperInvariant();
                    break;
                case "INTERVAL":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n > 0)
                        interval = n;
                    break;
                // Silently ignore BYDAY / BYMONTHDAY / COUNT / UNTIL /
                // etc. in v1; complex parsing lands in the follow-on.
            }
        }
        if (freq is null)
            throw new FormatException(
                $"RRULE missing FREQ= component: '{rrule}'.");
        return (freq, interval);
    }
}
