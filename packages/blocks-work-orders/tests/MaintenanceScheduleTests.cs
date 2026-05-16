using Sunfish.Blocks.WorkOrders.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkOrders.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="MaintenanceSchedule"/> per
/// <c>blocks-work-schema-design.md</c> §2.9.
/// </summary>
public sealed class MaintenanceScheduleTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Actor = Guid.NewGuid();

    private static MaintenanceTaskTemplate Template => new(
        Title: "Replace HVAC filter",
        Priority: Priority.Normal,
        Description: "Standard 16x25x1 filter replacement.");

    [Fact]
    public void Create_ValidSchedule_StatusIsActive()
    {
        var ms = MaintenanceSchedule.Create(
            Tenant, "Quarterly HVAC filter",
            recurrenceRule: "FREQ=MONTHLY;INTERVAL=3",
            startsOn: new DateOnly(2026, 1, 1),
            timezone: "America/New_York",
            taskTemplate: Template,
            createdBy: Actor);

        Assert.Equal(ScheduleStatus.Active, ms.Status);
        Assert.Equal("FREQ=MONTHLY;INTERVAL=3", ms.RecurrenceRule);
        Assert.Equal(7, ms.GenerateLeadDays);
        Assert.Equal(90, ms.LookaheadHorizonDays);
    }

    [Fact]
    public void Pause_ActiveSchedule_StatusIsPaused()
    {
        var ms = NewSchedule();
        ms.Pause(Actor);
        Assert.Equal(ScheduleStatus.Paused, ms.Status);
    }

    [Fact]
    public void Resume_FromPaused_StatusIsActive()
    {
        var ms = NewSchedule();
        ms.Pause(Actor);
        ms.Resume(Actor);
        Assert.Equal(ScheduleStatus.Active, ms.Status);
    }

    [Fact]
    public void Archive_FromActive_StatusIsArchived()
    {
        var ms = NewSchedule();
        ms.Archive(Actor);
        Assert.Equal(ScheduleStatus.Archived, ms.Status);
    }

    [Fact]
    public void Pause_ArchivedSchedule_Throws()
    {
        var ms = NewSchedule();
        ms.Archive(Actor);
        Assert.Throws<InvalidOperationException>(() => ms.Pause(Actor));
    }

    [Fact]
    public void Resume_FromActive_Throws()
    {
        var ms = NewSchedule();
        Assert.Throws<InvalidOperationException>(() => ms.Resume(Actor));
    }

    [Fact]
    public void Create_EmptyName_Throws()
    {
        Assert.Throws<ArgumentException>(() => MaintenanceSchedule.Create(
            Tenant, "  ",
            "FREQ=DAILY", new DateOnly(2026, 1, 1), "UTC", Template, Actor));
    }

    [Fact]
    public void Create_EmptyRrule_Throws()
    {
        Assert.Throws<ArgumentException>(() => MaintenanceSchedule.Create(
            Tenant, "Some name", "  ",
            new DateOnly(2026, 1, 1), "UTC", Template, Actor));
    }

    [Fact]
    public void SetEndsOn_BeforeStartsOn_Throws()
    {
        var ms = NewSchedule();
        Assert.Throws<ArgumentException>(
            () => ms.SetEndsOn(new DateOnly(2025, 12, 1), Actor));
    }

    private static MaintenanceSchedule NewSchedule()
        => MaintenanceSchedule.Create(
            Tenant, "Monthly inspection",
            recurrenceRule: "FREQ=MONTHLY",
            startsOn: new DateOnly(2026, 1, 1),
            timezone: "UTC",
            taskTemplate: Template,
            createdBy: Actor);
}
