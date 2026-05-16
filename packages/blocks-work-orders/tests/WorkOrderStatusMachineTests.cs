using Sunfish.Blocks.WorkOrders.Models;
using Xunit;

namespace Sunfish.Blocks.WorkOrders.Tests;

/// <summary>
/// W#60 P4 — coverage for <see cref="WorkOrderStatusMachine"/> per
/// <c>blocks-work-schema-design.md</c> §2.6.
/// </summary>
public sealed class WorkOrderStatusMachineTests
{
    [Fact]
    public void AllTerminalStates_ReturnEmptyTransitions()
    {
        Assert.Empty(WorkOrderStatusMachine.AllowedTransitionsFrom(WorkOrderStatus.Closed));
        Assert.Empty(WorkOrderStatusMachine.AllowedTransitionsFrom(WorkOrderStatus.Cancelled));
    }

    [Fact]
    public void StateMachine_AllowsTriagedSkippingEstimate_DirectToScheduled()
    {
        Assert.True(WorkOrderStatusMachine.CanTransition(
            WorkOrderStatus.Triaged, WorkOrderStatus.Scheduled));
    }

    [Fact]
    public void StateMachine_RejectsBackwardsFromCompletedToTriaged()
    {
        Assert.False(WorkOrderStatusMachine.CanTransition(
            WorkOrderStatus.Completed, WorkOrderStatus.Triaged));
    }

    [Fact]
    public void StateMachine_AllowsCompletedReopenToInProgress()
    {
        Assert.True(WorkOrderStatusMachine.CanTransition(
            WorkOrderStatus.Completed, WorkOrderStatus.InProgress));
    }

    [Fact]
    public void StateMachine_RejectsSelfTransitions()
    {
        // Self-transitions are NOT in the allowed map — callers should
        // no-op instead of round-tripping the state machine.
        Assert.False(WorkOrderStatusMachine.CanTransition(
            WorkOrderStatus.InProgress, WorkOrderStatus.InProgress));
    }

    [Fact]
    public void StateMachine_NewToCancelled_Allowed()
    {
        Assert.True(WorkOrderStatusMachine.CanTransition(
            WorkOrderStatus.New, WorkOrderStatus.Cancelled));
    }
}
