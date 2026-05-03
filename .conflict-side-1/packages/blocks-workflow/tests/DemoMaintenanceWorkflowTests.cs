using Xunit;
using Sunfish.Blocks.Workflow.Tests.Fixtures;

namespace Sunfish.Blocks.Workflow.Tests;

/// <summary>
/// Reference-workflow tests using the §6.4 demo maintenance fixture.
/// These tests validate the workflow semantics end-to-end without depending on
/// any real <c>blocks-*</c> package.
/// </summary>
public sealed class DemoMaintenanceWorkflowTests
{
    private static InMemoryWorkflowRuntime CreateRuntime() => new();
    private static readonly IWorkflowDefinition<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>
        _def = DemoMaintenanceWorkflow.Build();

    // -------------------------------------------------------------------------
    // Happy path: Submit → Approve → InProgress → Complete
    // -------------------------------------------------------------------------

    [Fact]
    public async Task HappyPath_SubmitApproveStartComplete_ReachesCompleted()
    {
        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(_def, new DemoMaintenanceContext { Notes = "boiler" });

        Assert.Equal(DemoMaintenanceState.Submitted, instance.CurrentState);

        instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Approve);
        Assert.Equal(DemoMaintenanceState.Approved, instance.CurrentState);

        instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Start);
        Assert.Equal(DemoMaintenanceState.InProgress, instance.CurrentState);

        instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Complete);
        Assert.Equal(DemoMaintenanceState.Completed, instance.CurrentState);

        Assert.True(instance.IsTerminal);
        Assert.Equal(3, instance.TransitionCount);
    }

    // -------------------------------------------------------------------------
    // Rejected from Submitted is terminal
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Reject_FromSubmitted_IsTerminal()
    {
        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(_def, new DemoMaintenanceContext());

        instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Reject);

        Assert.Equal(DemoMaintenanceState.Rejected, instance.CurrentState);
        Assert.True(instance.IsTerminal);
    }

    // -------------------------------------------------------------------------
    // Cancellation from Submitted
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Cancel_FromSubmitted_IsTerminal()
    {
        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(_def, new DemoMaintenanceContext());

        instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Cancel);

        Assert.Equal(DemoMaintenanceState.Cancelled, instance.CurrentState);
        Assert.True(instance.IsTerminal);
    }

    // -------------------------------------------------------------------------
    // Cancellation from Approved
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Cancel_FromApproved_IsTerminal()
    {
        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(_def, new DemoMaintenanceContext());

        instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Approve);
        instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Cancel);

        Assert.Equal(DemoMaintenanceState.Cancelled, instance.CurrentState);
        Assert.True(instance.IsTerminal);
    }

    // -------------------------------------------------------------------------
    // Cancellation from InProgress
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Cancel_FromInProgress_IsTerminal()
    {
        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(_def, new DemoMaintenanceContext());

        instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Approve);
        instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Start);
        instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Cancel);

        Assert.Equal(DemoMaintenanceState.Cancelled, instance.CurrentState);
        Assert.True(instance.IsTerminal);
    }
}
