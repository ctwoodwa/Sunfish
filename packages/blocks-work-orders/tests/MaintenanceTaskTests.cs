using Sunfish.Blocks.WorkOrders.Models;
using Xunit;

namespace Sunfish.Blocks.WorkOrders.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="MaintenanceTask"/>.
/// </summary>
public sealed class MaintenanceTaskTests
{
    private static readonly MaintenanceScheduleId ScheduleId = MaintenanceScheduleId.NewId();
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Create_MandatoryTask_PendingByDefault()
    {
        var task = MaintenanceTask.Create(
            ScheduleId, new DateOnly(2026, 4, 15), "Filter check", Actor);
        Assert.Equal(MaintenanceTaskStatus.Pending, task.Status);
        Assert.Null(task.WorkOrderId);
    }

    [Fact]
    public void DispatchTo_LinksToWorkOrder()
    {
        var task = MaintenanceTask.Create(
            ScheduleId, new DateOnly(2026, 4, 15), "Filter check", Actor);
        var woId = WorkOrderId.NewId();

        task.DispatchTo(woId, Actor);

        Assert.Equal(woId, task.WorkOrderId);
    }

    [Fact]
    public void DispatchTo_AlreadyDispatched_Throws()
    {
        var task = MaintenanceTask.Create(
            ScheduleId, new DateOnly(2026, 4, 15), "Filter check", Actor);
        task.DispatchTo(WorkOrderId.NewId(), Actor);

        Assert.Throws<InvalidOperationException>(
            () => task.DispatchTo(WorkOrderId.NewId(), Actor));
    }

    [Fact]
    public void Complete_StatusBecomesCompleted_WithTimestamp()
    {
        var task = MaintenanceTask.Create(
            ScheduleId, new DateOnly(2026, 4, 15), "Filter check", Actor);

        task.Complete(Actor, notes: "Filter replaced");

        Assert.Equal(MaintenanceTaskStatus.Completed, task.Status);
        Assert.NotNull(task.CompletedAt);
        Assert.Equal("Filter replaced", task.Notes);
    }

    [Fact]
    public void MarkNotApplicable_StatusBecomesNotApplicable()
    {
        var task = MaintenanceTask.Create(
            ScheduleId, new DateOnly(2026, 4, 15), "Filter check", Actor);
        task.MarkNotApplicable(Actor, notes: "Asset removed");
        Assert.Equal(MaintenanceTaskStatus.NotApplicable, task.Status);
    }

    [Fact]
    public void Create_EmptyTitle_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => MaintenanceTask.Create(ScheduleId, new DateOnly(2026, 4, 15), "  ", Actor));
    }
}
