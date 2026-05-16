namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Lifecycle state of a <see cref="MaintenanceSchedule"/>.
/// </summary>
public enum ScheduleStatus
{
    /// <summary>Generating tasks on the configured cadence.</summary>
    Active,

    /// <summary>Temporarily not generating new tasks; resume returns to Active.</summary>
    Paused,

    /// <summary>Terminal — historical record; never resumed.</summary>
    Archived,
}
