using System.Text.RegularExpressions;
using Sunfish.Blocks.WorkOrders.Models;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.WorkOrders.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="WorkOrder"/> per
/// <c>blocks-work-schema-design.md</c> §2.4.
/// </summary>
public sealed class WorkOrderTests
{
    private static readonly TenantId Tenant = new("test-tenant-1");
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Create_GeneratesNumberInFormat()
    {
        var now = new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero);
        var wo = WorkOrder.Create(
            Tenant, "Replace bathroom faucet", WorkOrderKind.Repair,
            Priority.Normal, Actor, createdAt: now);

        Assert.Matches(@"^WO-20260516-[0-9a-f]{7}$", wo.Number);
    }

    [Fact]
    public void Create_KindRepair_StatusIsNew()
    {
        var wo = WorkOrder.Create(
            Tenant, "Leaky faucet", WorkOrderKind.Repair, Priority.Normal, Actor);
        Assert.Equal(WorkOrderStatus.New, wo.Status);
    }

    [Fact]
    public void Transition_ValidTransition_UpdatesStatus()
    {
        var wo = WorkOrder.Create(
            Tenant, "Triage me", WorkOrderKind.Task, Priority.Normal, Actor);
        wo.Transition(WorkOrderStatus.Triaged, Actor);
        Assert.Equal(WorkOrderStatus.Triaged, wo.Status);
    }

    [Fact]
    public void Transition_InvalidTransition_Throws()
    {
        var wo = WorkOrder.Create(
            Tenant, "Direct-jump-to-completed", WorkOrderKind.Task, Priority.Normal, Actor);

        var ex = Assert.Throws<InvalidStatusTransitionException>(
            () => wo.Transition(WorkOrderStatus.Completed, Actor));
        Assert.Equal(WorkOrderStatus.New, ex.From);
        Assert.Equal(WorkOrderStatus.Completed, ex.To);
    }

    [Fact]
    public void Transition_ToTerminalState_CannotExit()
    {
        var wo = WorkOrder.Create(
            Tenant, "Drive to Closed", WorkOrderKind.Task, Priority.Normal, Actor);
        // Walk the legal path to Closed.
        wo.Transition(WorkOrderStatus.Triaged, Actor);
        wo.Transition(WorkOrderStatus.Scheduled, Actor);
        wo.Transition(WorkOrderStatus.InProgress, Actor);
        wo.Transition(WorkOrderStatus.Completed, Actor);
        wo.Transition(WorkOrderStatus.Verified, Actor);
        wo.Transition(WorkOrderStatus.Closed, Actor);

        Assert.Throws<InvalidStatusTransitionException>(
            () => wo.Transition(WorkOrderStatus.New, Actor));
        Assert.Throws<InvalidStatusTransitionException>(
            () => wo.Transition(WorkOrderStatus.InProgress, Actor));
    }

    [Fact]
    public void SeveritySafety_RequiresDueBy()
    {
        Assert.Throws<ArgumentException>(() => WorkOrder.Create(
            Tenant, "Leaking gas line",
            WorkOrderKind.Repair, Priority.Critical, Actor,
            severity: WorkOrderSeverity.Safety,
            dueBy: null));
    }

    [Fact]
    public void SeverityHabitability_RequiresDueBy()
    {
        Assert.Throws<ArgumentException>(() => WorkOrder.Create(
            Tenant, "Heating system out in winter",
            WorkOrderKind.Repair, Priority.High, Actor,
            severity: WorkOrderSeverity.Habitability,
            dueBy: null));
    }

    [Fact]
    public void SeveritySafety_WithDueBy_Succeeds()
    {
        var wo = WorkOrder.Create(
            Tenant, "Loose stair railing",
            WorkOrderKind.Repair, Priority.High, Actor,
            severity: WorkOrderSeverity.Safety,
            dueBy: DateTimeOffset.UtcNow.AddDays(1));
        Assert.Equal(WorkOrderSeverity.Safety, wo.Severity);
    }

    [Fact]
    public void Transition_InProgress_StampsStartedAt()
    {
        var wo = WorkOrder.Create(
            Tenant, "Stamp test", WorkOrderKind.Task, Priority.Normal, Actor);
        wo.Transition(WorkOrderStatus.Triaged, Actor);
        wo.Transition(WorkOrderStatus.Scheduled, Actor);
        Assert.Null(wo.StartedAt);

        wo.Transition(WorkOrderStatus.InProgress, Actor);

        Assert.NotNull(wo.StartedAt);
    }

    [Fact]
    public void Transition_BumpsVersion()
    {
        var wo = WorkOrder.Create(
            Tenant, "Version bump", WorkOrderKind.Task, Priority.Normal, Actor);
        var before = wo.Version;
        wo.Transition(WorkOrderStatus.Triaged, Actor);
        Assert.Equal(before + 1, wo.Version);
    }
}
