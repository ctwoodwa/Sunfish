namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Lifecycle status of a <see cref="WorkOrder"/>.
/// </summary>
/// <remarks>
/// Allowed transitions (post-completion segment per ADR 0053 amendment A4):
/// <code>
/// Draft → Sent
/// Sent → Accepted | Cancelled
/// Accepted → Scheduled
/// Scheduled → InProgress
/// InProgress → Completed | OnHold
/// OnHold → InProgress
/// Completed → AwaitingSignOff | Invoiced
/// AwaitingSignOff → Invoiced | OnHold
/// Invoiced → Paid | Disputed | OnHold
/// Paid → Closed | Disputed
/// Disputed → Invoiced | Paid | Closed
/// </code>
/// <see cref="Cancelled"/> remains terminal-from-anywhere-pre-<see cref="Closed"/>.
/// Final terminal: <see cref="Closed"/>, <see cref="Cancelled"/>.
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

    /// <summary>Work has been completed by the vendor.</summary>
    Completed,

    /// <summary>Vendor-completed; awaiting BDFL/operator signature attestation (ADR 0053 A4).</summary>
    AwaitingSignOff,

    /// <summary>Receipt arrived; payment not yet authorized (ADR 0053 A4).</summary>
    Invoiced,

    /// <summary>Payment authorized + captured per ADR 0051 (ADR 0053 A4).</summary>
    Paid,

    /// <summary>Side-branch from <see cref="Invoiced"/> or <see cref="Paid"/>; awaiting resolution (ADR 0053 A4).</summary>
    Disputed,

    /// <summary>Final terminal; all parties settled (ADR 0053 A4).</summary>
    Closed,

    /// <summary>Work order has been cancelled.</summary>
    Cancelled,
}
