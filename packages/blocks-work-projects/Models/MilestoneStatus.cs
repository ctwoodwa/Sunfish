namespace Sunfish.Blocks.WorkProjects.Models;

/// <summary>Lifecycle state of a <see cref="ProjectMilestone"/>.</summary>
public enum MilestoneStatus
{
    /// <summary>Not yet achieved; planned date in the future or past.</summary>
    Pending,

    /// <summary>Flagged at-risk by the team (manual or computed).</summary>
    AtRisk,

    /// <summary>Achieved — ActualDate populated.</summary>
    Achieved,

    /// <summary>Missed — planned date passed without achievement; visible in reports.</summary>
    Missed,

    /// <summary>Cancelled before achievement.</summary>
    Cancelled,
}
