namespace Sunfish.Blocks.Workflow.Tests.Fixtures;

/// <summary>
/// Fixture enum mirroring the §6.4 maintenance workflow states.
/// Uses no blocks-* dependencies — purely for exercising the workflow primitive.
/// </summary>
public enum DemoMaintenanceState
{
    Submitted,
    Approved,
    Rejected,
    InProgress,
    Cancelled,
    Completed
}
