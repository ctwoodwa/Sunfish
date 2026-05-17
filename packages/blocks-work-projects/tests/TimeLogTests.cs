using Sunfish.Blocks.WorkProjects.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkProjects.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="TimeLog"/> read-side view.
/// </summary>
public sealed class TimeLogTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Worker = Guid.NewGuid();
    private static readonly Guid OtherWorker = Guid.NewGuid();

    private static TimeEntry MakeStopped(Guid worker, DateTimeOffset started, int minutes, bool billable = true, decimal? rate = null)
    {
        var entry = TimeEntry.Open(
            Tenant, TimeEntryId.NewId(), worker, ActivityKind.Labor,
            new Instant(started), worker, new Instant(started),
            projectId: ProjectId.NewId(), billable: billable);
        entry.Stop(new Instant(started.AddMinutes(minutes)), rate, rate is null ? null : "USD", worker);
        return entry;
    }

    [Fact]
    public void Build_FiltersByWorker()
    {
        var d1 = new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero);
        var entries = new[]
        {
            MakeStopped(Worker, d1, 60),
            MakeStopped(OtherWorker, d1.AddHours(2), 30),
        };
        var log = TimeLog.Build(Worker, new DateOnly(2026, 5, 16), new DateOnly(2026, 5, 16), entries);
        Assert.Single(log.Entries);
        Assert.Equal(60, log.TotalMinutes);
    }

    [Fact]
    public void Build_FiltersByDateRange()
    {
        var entries = new[]
        {
            MakeStopped(Worker, new DateTimeOffset(2026, 5, 15, 23, 0, 0, TimeSpan.Zero), 30),
            MakeStopped(Worker, new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero), 60),
            MakeStopped(Worker, new DateTimeOffset(2026, 5, 17, 0, 30, 0, TimeSpan.Zero), 45),
        };
        var log = TimeLog.Build(Worker, new DateOnly(2026, 5, 16), new DateOnly(2026, 5, 16), entries);
        Assert.Single(log.Entries);
    }

    [Fact]
    public void Build_BillableMinutes_OnlyCountsBillable()
    {
        var d1 = new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero);
        var entries = new[]
        {
            MakeStopped(Worker, d1, 60, billable: true),
            MakeStopped(Worker, d1.AddHours(2), 30, billable: false),
        };
        var log = TimeLog.Build(Worker, new DateOnly(2026, 5, 16), new DateOnly(2026, 5, 16), entries);
        Assert.Equal(90, log.TotalMinutes);
        Assert.Equal(60, log.BillableMinutes);
    }

    [Fact]
    public void Build_TotalAmount_SumsAmountsWhenPresent()
    {
        var d1 = new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero);
        var entries = new[]
        {
            MakeStopped(Worker, d1, 60, rate: 100m),                  // $100
            MakeStopped(Worker, d1.AddHours(2), 30, rate: 80m),       // $40
        };
        var log = TimeLog.Build(Worker, new DateOnly(2026, 5, 16), new DateOnly(2026, 5, 16), entries);
        Assert.Equal(140m, log.TotalAmount);
    }

    [Fact]
    public void Build_TotalAmount_NullWhenNoEntriesHaveAmount()
    {
        var d1 = new DateTimeOffset(2026, 5, 16, 9, 0, 0, TimeSpan.Zero);
        var entries = new[] { MakeStopped(Worker, d1, 60) };  // no rate
        var log = TimeLog.Build(Worker, new DateOnly(2026, 5, 16), new DateOnly(2026, 5, 16), entries);
        Assert.Null(log.TotalAmount);
    }

    [Fact]
    public void Build_UntilBeforeFrom_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            TimeLog.Build(Worker, new DateOnly(2026, 5, 17), new DateOnly(2026, 5, 16), Array.Empty<TimeEntry>()));
    }
}
