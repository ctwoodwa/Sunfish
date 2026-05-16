namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Per-instance task lifecycle on a generated <see cref="MaintenanceTask"/>.
/// Distinct from <see cref="WorkOrderStatus"/> — a task can be marked
/// <see cref="NotApplicable"/> without becoming a full work order.
/// </summary>
public enum MaintenanceTaskStatus
{
    /// <summary>Awaiting execution.</summary>
    Pending,

    /// <summary>Executed and signed off.</summary>
    Completed,

    /// <summary>Inspected and determined not applicable for this occurrence.</summary>
    NotApplicable,

    /// <summary>Execution failed; follow-up needed.</summary>
    Failed,
}
