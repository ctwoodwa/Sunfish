namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Lifecycle status of a <see cref="Vendor"/> in the system.
/// </summary>
public enum VendorStatus
{
    /// <summary>Vendor is available to receive work orders and RFQs.</summary>
    Active,

    /// <summary>Vendor is preferred and receives priority routing.</summary>
    Preferred,

    /// <summary>Vendor is temporarily suspended (e.g., compliance hold).</summary>
    Suspended,

    /// <summary>Vendor is no longer used.</summary>
    Inactive,
}
