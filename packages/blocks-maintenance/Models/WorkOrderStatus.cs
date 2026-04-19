namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Lifecycle status of a <see cref="WorkOrder"/>.
/// </summary>
/// <remarks>
/// Allowed transitions:
/// <code>
/// Draft → Sent
/// Sent → Accepted | Cancelled
/// Accepted → Scheduled
/// Scheduled → InProgress
/// InProgress → Completed | OnHold
/// OnHold → InProgress
/// </code>
/// Terminal states: <see cref="Completed"/>, <see cref="Cancelled"/>.
/// </remarks>
public enum WorkOrderStatus
{
    /// <summary>Work order has been created but not yet sent to the vendor.</summary>
    Draft,

    /// <summary>Work order has been sent to the vendor awaiting acceptance.</summary>
    Sent,

    /// <summary>Vendor has accepted the work order.</summary>
    Accepted,

    /// <summary>Work has been scheduled for a specific date.</summary>
    Scheduled,

    /// <summary>Work is currently in progress.</summary>
    InProgress,

    /// <summary>Work has been temporarily put on hold.</summary>
    OnHold,

    /// <summary>Work has been completed.</summary>
    Completed,

    /// <summary>Work order has been cancelled.</summary>
    Cancelled,
}
