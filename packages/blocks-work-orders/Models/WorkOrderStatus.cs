namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Lifecycle state of a <see cref="WorkOrder"/> per
/// <c>blocks-work-schema-design.md</c> §2.6. Transitions are gated by
/// <see cref="WorkOrderStatusMachine.CanTransition"/>.
/// </summary>
public enum WorkOrderStatus
{
    /// <summary>Freshly created; not yet triaged.</summary>
    New,

    /// <summary>Triaged: priority + severity + assignment evaluated.</summary>
    Triaged,

    /// <summary>Estimate prepared awaiting approval.</summary>
    Estimated,

    /// <summary>Estimate approved; ready to schedule.</summary>
    Approved,

    /// <summary>Scheduled with a start window.</summary>
    Scheduled,

    /// <summary>Work in progress.</summary>
    InProgress,

    /// <summary>Paused by the assignee (parts ordered, waiting on access, etc.).</summary>
    OnHold,

    /// <summary>Blocked by an external dependency (permits, third-party access).</summary>
    Blocked,

    /// <summary>Work physically complete; awaiting verification.</summary>
    Completed,

    /// <summary>Verified by the requester; ready to invoice (if billable).</summary>
    Verified,

    /// <summary>Invoiced (if tenant-billable or contractor-rebillable).</summary>
    Invoiced,

    /// <summary>Terminal — work order is closed.</summary>
    Closed,

    /// <summary>Terminal — work order was cancelled before completion.</summary>
    Cancelled,
}
