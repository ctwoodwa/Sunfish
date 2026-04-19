namespace Sunfish.Blocks.Workflow.Tests.Fixtures;

/// <summary>
/// Factory for the §6.4 reference maintenance workflow definition.
/// </summary>
public static class DemoMaintenanceWorkflow
{
    public static IWorkflowDefinition<DemoMaintenanceState, DemoMaintenanceTrigger, DemoMaintenanceContext>
        Build(
            Func<DemoMaintenanceState, DemoMaintenanceState, DemoMaintenanceTrigger,
                DemoMaintenanceContext, CancellationToken, ValueTask>? onTransition = null)
    {
        var builder = new WorkflowDefinitionBuilder<
            DemoMaintenanceState,
            DemoMaintenanceTrigger,
            DemoMaintenanceContext>()
            .StartAt(DemoMaintenanceState.Submitted)
            // Submitted → Approved / Rejected / Cancelled
            .Transition(DemoMaintenanceState.Submitted,   DemoMaintenanceTrigger.Approve,   DemoMaintenanceState.Approved)
            .Transition(DemoMaintenanceState.Submitted,   DemoMaintenanceTrigger.Reject,    DemoMaintenanceState.Rejected)
            .Transition(DemoMaintenanceState.Submitted,   DemoMaintenanceTrigger.Cancel,    DemoMaintenanceState.Cancelled)
            // Approved → InProgress / Cancelled
            .Transition(DemoMaintenanceState.Approved,    DemoMaintenanceTrigger.Start,     DemoMaintenanceState.InProgress)
            .Transition(DemoMaintenanceState.Approved,    DemoMaintenanceTrigger.Cancel,    DemoMaintenanceState.Cancelled)
            // InProgress → Completed / Cancelled
            .Transition(DemoMaintenanceState.InProgress,  DemoMaintenanceTrigger.Complete,  DemoMaintenanceState.Completed)
            .Transition(DemoMaintenanceState.InProgress,  DemoMaintenanceTrigger.Cancel,    DemoMaintenanceState.Cancelled)
            // Terminal states
            .Terminal(
                DemoMaintenanceState.Rejected,
                DemoMaintenanceState.Cancelled,
                DemoMaintenanceState.Completed);

        if (onTransition is not null)
            builder.OnTransition(onTransition);

        return builder.Build();
    }
}

/// <summary>Minimal context object for the demo maintenance workflow.</summary>
public sealed class DemoMaintenanceContext
{
    public string? Notes { get; set; }
}
