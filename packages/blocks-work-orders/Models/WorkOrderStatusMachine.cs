namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Allowed-transitions guard for <see cref="WorkOrderStatus"/> per
/// <c>blocks-work-schema-design.md</c> §2.6. The app-layer enforces
/// this on every <see cref="WorkOrder.Transition"/> call; CRDT merges
/// fall back to last-write-wins and a future reconciler catches
/// illegal merged states (Stage 02 Q7).
/// </summary>
public static class WorkOrderStatusMachine
{
    private static readonly Dictionary<WorkOrderStatus, HashSet<WorkOrderStatus>> Allowed = new()
    {
        [WorkOrderStatus.New]        = new() { WorkOrderStatus.Triaged, WorkOrderStatus.Cancelled },
        [WorkOrderStatus.Triaged]    = new() { WorkOrderStatus.Estimated, WorkOrderStatus.Scheduled, WorkOrderStatus.Cancelled },
        [WorkOrderStatus.Estimated]  = new() { WorkOrderStatus.Approved, WorkOrderStatus.Cancelled },
        [WorkOrderStatus.Approved]   = new() { WorkOrderStatus.Scheduled },
        [WorkOrderStatus.Scheduled]  = new() { WorkOrderStatus.InProgress, WorkOrderStatus.OnHold, WorkOrderStatus.Cancelled },
        [WorkOrderStatus.InProgress] = new() { WorkOrderStatus.OnHold, WorkOrderStatus.Blocked, WorkOrderStatus.Completed },
        [WorkOrderStatus.OnHold]     = new() { WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled },
        [WorkOrderStatus.Blocked]    = new() { WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled },
        [WorkOrderStatus.Completed]  = new() { WorkOrderStatus.Verified, WorkOrderStatus.InProgress },
        [WorkOrderStatus.Verified]   = new() { WorkOrderStatus.Invoiced, WorkOrderStatus.Closed },
        [WorkOrderStatus.Invoiced]   = new() { WorkOrderStatus.Closed },
        [WorkOrderStatus.Closed]     = new(),   // terminal
        [WorkOrderStatus.Cancelled]  = new(),   // terminal
    };

    /// <summary>
    /// True when transitioning <paramref name="from"/> → <paramref name="to"/>
    /// is allowed by the state diagram. Self-transitions
    /// (<paramref name="from"/> == <paramref name="to"/>) are always
    /// disallowed — callers should no-op instead.
    /// </summary>
    public static bool CanTransition(WorkOrderStatus from, WorkOrderStatus to)
        => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>
    /// Snapshot of the allowed-targets set from a given status —
    /// useful for UI hint rendering and tests.
    /// </summary>
    public static IReadOnlySet<WorkOrderStatus> AllowedTransitionsFrom(WorkOrderStatus from)
        => Allowed.TryGetValue(from, out var targets) ? targets : new HashSet<WorkOrderStatus>();
}

/// <summary>
/// Thrown by <see cref="WorkOrder.Transition"/> when the requested
/// status change violates the <see cref="WorkOrderStatusMachine"/>.
/// </summary>
public sealed class InvalidStatusTransitionException : InvalidOperationException
{
    public WorkOrderStatus From { get; }
    public WorkOrderStatus To { get; }

    public InvalidStatusTransitionException(WorkOrderStatus from, WorkOrderStatus to)
        : base($"Invalid WorkOrder status transition: {from} → {to}.")
    {
        From = from;
        To   = to;
    }
}
