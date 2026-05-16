using Sunfish.Blocks.WorkOrders.Services;
using Xunit;

namespace Sunfish.Blocks.WorkOrders.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="InMemoryRruleExpansionService"/>.
/// </summary>
public sealed class InMemoryRruleExpansionServiceTests
{
    private static readonly IRruleExpansionService Sut = new InMemoryRruleExpansionService();
    private static readonly DateOnly Today = new(2026, 1, 1);

    [Fact]
    public void Expand_DailyFreq_ReturnsCorrectCount()
    {
        var occurrences = Sut.ExpandOccurrences(
            rrule: "FREQ=DAILY",
            start: Today,
            end: null,
            lookaheadDays: 10,
            leadDays: 0,
            today: Today,
            timezone: "UTC");

        // start + lookahead 10 days, inclusive of both ends: 11 occurrences.
        Assert.Equal(11, occurrences.Count);
        Assert.Equal(Today, occurrences[0]);
        Assert.Equal(Today.AddDays(10), occurrences[^1]);
    }

    [Fact]
    public void Expand_MonthlyInterval3_ReturnsQuarterly()
    {
        var occurrences = Sut.ExpandOccurrences(
            rrule: "FREQ=MONTHLY;INTERVAL=3",
            start: Today,
            end: new DateOnly(2026, 12, 31),
            lookaheadDays: 365,
            leadDays: 0,
            today: Today,
            timezone: "UTC");

        // Jan 1, Apr 1, Jul 1, Oct 1 = 4 occurrences within 2026.
        Assert.Equal(4, occurrences.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), occurrences[0]);
        Assert.Equal(new DateOnly(2026, 10, 1), occurrences[^1]);
    }

    [Fact]
    public void Expand_WeeklyInterval2_ReturnsBiweekly()
    {
        var occurrences = Sut.ExpandOccurrences(
            rrule: "FREQ=WEEKLY;INTERVAL=2",
            start: Today,
            end: Today.AddDays(28),
            lookaheadDays: 28,
            leadDays: 0,
            today: Today,
            timezone: "UTC");

        // Jan 1, Jan 15, Jan 29 — 3 occurrences (Jan 29 = today+28).
        Assert.Equal(3, occurrences.Count);
    }

    [Fact]
    public void Expand_EndsOn_DoesNotExceedBound()
    {
        var hardEnd = new DateOnly(2026, 1, 5);
        var occurrences = Sut.ExpandOccurrences(
            rrule: "FREQ=DAILY",
            start: Today,
            end: hardEnd,
            lookaheadDays: 365,
            leadDays: 0,
            today: Today,
            timezone: "UTC");

        Assert.All(occurrences, d => Assert.True(d <= hardEnd));
        Assert.Equal(hardEnd, occurrences[^1]);
    }

    [Fact]
    public void Expand_LeadDaysHonored_SkipsEarlyOccurrences()
    {
        var occurrences = Sut.ExpandOccurrences(
            rrule: "FREQ=DAILY",
            start: Today,
            end: Today.AddDays(10),
            lookaheadDays: 10,
            leadDays: 5,
            today: Today,
            timezone: "UTC");

        // leadDays=5 + today=Jan 1 → earliest = Jan 6.
        Assert.All(occurrences, d => Assert.True(d >= Today.AddDays(5)));
    }

    [Fact]
    public void Expand_MissingFreq_Throws()
    {
        Assert.Throws<FormatException>(() => Sut.ExpandOccurrences(
            rrule: "INTERVAL=2",
            start: Today, end: null,
            lookaheadDays: 10, leadDays: 0,
            today: Today, timezone: "UTC"));
    }

    [Fact]
    public void Expand_UnsupportedFreq_Throws()
    {
        Assert.Throws<NotSupportedException>(() => Sut.ExpandOccurrences(
            rrule: "FREQ=YEARLY",
            start: Today, end: null,
            lookaheadDays: 365, leadDays: 0,
            today: Today, timezone: "UTC"));
    }

    [Fact]
    public void Expand_EmptyRrule_Throws()
    {
        Assert.Throws<ArgumentException>(() => Sut.ExpandOccurrences(
            rrule: "  ",
            start: Today, end: null,
            lookaheadDays: 10, leadDays: 0,
            today: Today, timezone: "UTC"));
    }
}
