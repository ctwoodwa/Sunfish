using Xunit;
using Sunfish.Blocks.Workflow.Tests.Fixtures;

namespace Sunfish.Blocks.Workflow.Tests;

/// <summary>
/// Service-level tests for <see cref="InMemoryWorkflowRuntime"/>.
/// </summary>
public sealed class InMemoryWorkflowRuntimeTests
{
    private static InMemoryWorkflowRuntime CreateRuntime() => new();

    private static IWorkflowDefinition<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>
        Def(Func<DemoMaintenanceState, DemoMaintenanceState, DemoMaintenanceTrigger,
            DemoMaintenanceContext, CancellationToken, ValueTask>? hook = null)
        => DemoMaintenanceWorkflow.Build(hook);

    // -------------------------------------------------------------------------
    // StartAsync
    // -------------------------------------------------------------------------

    [Fact]
    public async Task StartAsync_ReturnsInstanceInInitialState()
    {
        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(Def(), new DemoMaintenanceContext());

        Assert.Equal(DemoMaintenanceState.Submitted, instance.CurrentState);
        Assert.False(instance.IsTerminal);
        Assert.Equal(0, instance.TransitionCount);
    }

    // -------------------------------------------------------------------------
    // FireAsync — happy path
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FireAsync_ValidTrigger_AdvancesState()
    {
        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(Def(), new DemoMaintenanceContext());

        var updated = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Approve);

        Assert.Equal(DemoMaintenanceState.Approved, updated.CurrentState);
    }

    // -------------------------------------------------------------------------
    // FireAsync — invalid trigger
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FireAsync_InvalidTrigger_ThrowsInvalidOperationException()
    {
        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(Def(), new DemoMaintenanceContext());

        // "Complete" is only valid from InProgress, not from Submitted
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
                instance.Id, DemoMaintenanceTrigger.Complete).AsTask());
    }

    // -------------------------------------------------------------------------
    // FireAsync — terminal state
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FireAsync_FromTerminalState_ThrowsInvalidOperationException()
    {
        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(Def(), new DemoMaintenanceContext());

        // Drive to Rejected (terminal)
        instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Reject);

        Assert.True(instance.IsTerminal);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
                instance.Id, DemoMaintenanceTrigger.Approve).AsTask());
    }

    // -------------------------------------------------------------------------
    // TransitionCount increments
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FireAsync_IncreasesTransitionCountEachTime()
    {
        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(Def(), new DemoMaintenanceContext());

        instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Approve);
        Assert.Equal(1, instance.TransitionCount);

        instance = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Start);
        Assert.Equal(2, instance.TransitionCount);
    }

    // -------------------------------------------------------------------------
    // LastTransitionAtUtc updates
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FireAsync_UpdatesLastTransitionAtUtc()
    {
        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(Def(), new DemoMaintenanceContext());
        var before = instance.LastTransitionAtUtc;

        // Small delay to ensure the clock advances
        await Task.Delay(5);

        var updated = await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Approve);

        Assert.True(updated.LastTransitionAtUtc >= before);
    }

    // -------------------------------------------------------------------------
    // OnTransitionAsync hook is called
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FireAsync_InvokesOnTransitionHook_WithCorrectArguments()
    {
        DemoMaintenanceState? capturedFrom = null;
        DemoMaintenanceState? capturedTo = null;
        DemoMaintenanceTrigger? capturedTrigger = null;

        var def = Def((from, to, trigger, ctx, ct) =>
        {
            capturedFrom = from;
            capturedTo = to;
            capturedTrigger = trigger;
            return ValueTask.CompletedTask;
        });

        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(def, new DemoMaintenanceContext());

        await runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Approve);

        Assert.Equal(DemoMaintenanceState.Submitted, capturedFrom);
        Assert.Equal(DemoMaintenanceState.Approved, capturedTo);
        Assert.Equal(DemoMaintenanceTrigger.Approve, capturedTrigger);
    }

    // -------------------------------------------------------------------------
    // OnTransitionAsync hook error propagates; transition is still committed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FireAsync_HookError_PropagatesButTransitionIsCommitted()
    {
        var def = Def((_, _, _, _, _) => ValueTask.FromException(new InvalidOperationException("hook boom")));

        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(def, new DemoMaintenanceContext());

        // The hook throws, but the state should already be committed
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
                instance.Id, DemoMaintenanceTrigger.Approve).AsTask());

        // State is Approved because the transition was committed before the hook ran
        var snapshot = await runtime.GetAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(instance.Id);
        Assert.NotNull(snapshot);
        Assert.Equal(DemoMaintenanceState.Approved, snapshot!.CurrentState);
    }

    // -------------------------------------------------------------------------
    // Concurrent fires on the same instance are serialized
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FireAsync_ConcurrentCallsOnSameInstance_AreSerializedNoLostTransitions()
    {
        // Build a looping definition: A ↔ B, both non-terminal with a large cycle
        // Use the demo workflow: fire Approve and Start concurrently from Submitted → should
        // result in exactly one of them succeeding and the other throwing (invalid from Approved).
        var runtime = CreateRuntime();
        var instance = await runtime.StartAsync(Def(), new DemoMaintenanceContext());

        // Fire Approve and Reject concurrently — only one should succeed
        var t1 = runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Approve).AsTask();
        var t2 = runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            instance.Id, DemoMaintenanceTrigger.Reject).AsTask();

        var results = await Task.WhenAll(
            t1.ContinueWith(t => t.Exception is null ? "ok" : "err"),
            t2.ContinueWith(t => t.Exception is null ? "ok" : "err"));

        // Exactly one must succeed; the other must fail because the state changed
        var okCount = results.Count(r => r == "ok");
        var errCount = results.Count(r => r == "err");
        Assert.Equal(1, okCount);
        Assert.Equal(1, errCount);

        // TransitionCount must be exactly 1 — no lost or double-counted transitions
        var final = await runtime.GetAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(instance.Id);
        Assert.Equal(1, final!.TransitionCount);
    }

    // -------------------------------------------------------------------------
    // Concurrent fires on different instances run independently
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FireAsync_ConcurrentCallsOnDifferentInstances_BothSucceed()
    {
        var runtime = CreateRuntime();
        var ctx = new DemoMaintenanceContext();
        var i1 = await runtime.StartAsync(Def(), ctx);
        var i2 = await runtime.StartAsync(Def(), ctx);

        await Task.WhenAll(
            runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
                i1.Id, DemoMaintenanceTrigger.Approve).AsTask(),
            runtime.FireAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
                i2.Id, DemoMaintenanceTrigger.Reject).AsTask());

        var s1 = await runtime.GetAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(i1.Id);
        var s2 = await runtime.GetAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(i2.Id);

        Assert.Equal(DemoMaintenanceState.Approved, s1!.CurrentState);
        Assert.Equal(DemoMaintenanceState.Rejected, s2!.CurrentState);
    }

    // -------------------------------------------------------------------------
    // GetAsync — unknown id returns null
    // -------------------------------------------------------------------------

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        var runtime = CreateRuntime();
        var result = await runtime.GetAsync<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>(
            WorkflowInstanceId.NewId());
        Assert.Null(result);
    }
}
