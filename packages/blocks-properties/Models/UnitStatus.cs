namespace Sunfish.Blocks.Properties.Models;

/// <summary>Operational status of a <see cref="PropertyUnit"/>.</summary>
public enum UnitStatus
{
    /// <summary>Ready for occupancy; no active lease.</summary>
    Available,

    /// <summary>Unit has an active lease.</summary>
    Occupied,

    /// <summary>Temporarily out of service for maintenance or renovation.</summary>
    MaintenanceHold,
}
