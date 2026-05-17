namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>
/// Allowed-transitions guard for <see cref="ProjectStatus"/> per
/// Stage 02 §2.2. App-layer enforces on every
/// <see cref="Project.TransitionStatus"/> call; CRDT merges fall
/// back to last-write-wins per CRDT Pattern A.
/// </summary>
public static class ProjectStatusMachine
{
    private static readonly Dictionary<ProjectStatus, HashSet<ProjectStatus>> Allowed = new()
    {
        [ProjectStatus.Draft]      = new() { ProjectStatus.Planned, ProjectStatus.Cancelled },
        [ProjectStatus.Planned]    = new() { ProjectStatus.InProgress, ProjectStatus.Cancelled },
        [ProjectStatus.InProgress] = new() { ProjectStatus.OnHold, ProjectStatus.Blocked, ProjectStatus.Completed, ProjectStatus.Cancelled },
        [ProjectStatus.OnHold]     = new() { ProjectStatus.InProgress, ProjectStatus.Cancelled },
        [ProjectStatus.Blocked]    = new() { ProjectStatus.InProgress, ProjectStatus.Cancelled },
        [ProjectStatus.Completed]  = new() { ProjectStatus.Closed, ProjectStatus.InProgress },
        [ProjectStatus.Closed]     = new(),  // terminal (except via audit-correction; H6)
        [ProjectStatus.Cancelled]  = new(),  // terminal
    };

    /// <summary>
    /// True when transitioning <paramref name="from"/> →
    /// <paramref name="to"/> is allowed by the 8-state diagram.
    /// Self-transitions are not in the allowed map (callers no-op).
    /// </summary>
    public static bool CanTransition(ProjectStatus from, ProjectStatus to)
        => Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>Snapshot of allowed targets from a given status.</summary>
    public static IReadOnlySet<ProjectStatus> AllowedTransitionsFrom(ProjectStatus from)
        => Allowed.TryGetValue(from, out var targets) ? targets : new HashSet<ProjectStatus>();
}

/// <summary>
/// Thrown by <see cref="Project.TransitionStatus"/> when the
/// requested transition violates <see cref="ProjectStatusMachine"/>.
/// </summary>
public sealed class InvalidProjectStatusTransitionException : InvalidOperationException
{
    public ProjectStatus From { get; }
    public ProjectStatus To { get; }
    public InvalidProjectStatusTransitionException(ProjectStatus from, ProjectStatus to)
        : base($"Invalid Project status transition: {from} → {to}.")
    {
        From = from;
        To   = to;
    }
}
