namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Priority level assigned to a <see cref="MaintenanceRequest"/>.
/// </summary>
public enum MaintenancePriority
{
    /// <summary>Non-urgent, schedule when convenient.</summary>
    Low,

    /// <summary>Standard priority; schedule within normal timeframes.</summary>
    Normal,

    /// <summary>High urgency; expedite scheduling.</summary>
    High,

    /// <summary>Immediate response required (e.g., safety hazard, flooding).</summary>
    Emergency,
}
