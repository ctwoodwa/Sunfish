namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Classification of a <see cref="WorkOrder"/>. Drives reporting + the
/// state-machine entry point.
/// </summary>
public enum WorkOrderKind
{
    /// <summary>Generic task (admin, paperwork, follow-up).</summary>
    Task,

    /// <summary>Repair triggered by a deficiency, inspection finding, or tenant request.</summary>
    Repair,

    /// <summary>Recurring maintenance generated from a <c>MaintenanceSchedule</c>.</summary>
    PreventiveMaintenance,

    /// <summary>Unit-turnover work between tenancies.</summary>
    Turnover,

    /// <summary>Follow-up work generated from an inspection finding.</summary>
    InspectionFollowup,
}
