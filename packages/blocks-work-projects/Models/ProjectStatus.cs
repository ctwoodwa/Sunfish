namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Lifecycle state of a <see cref="Project"/> per Stage 02 §2.2
/// 8-state machine. Transitions gated by
/// <see cref="ProjectStatusMachine.CanTransition"/>.
/// </summary>
public enum ProjectStatus
{
    /// <summary>Freshly created; not yet committed to execution.</summary>
    Draft,

    /// <summary>Scope locked; awaiting kickoff.</summary>
    Planned,

    /// <summary>Active execution.</summary>
    InProgress,

    /// <summary>Paused by the team (parts ordered, awaiting stakeholder input).</summary>
    OnHold,

    /// <summary>Blocked by an external dependency (permits, third-party).</summary>
    Blocked,

    /// <summary>Work complete; awaiting close-out reconciliation.</summary>
    Completed,

    /// <summary>Terminal — close-out reconciliation done; immutable except via audit-correction (H6).</summary>
    Closed,

    /// <summary>Terminal — project cancelled before completion.</summary>
    Cancelled,
}
