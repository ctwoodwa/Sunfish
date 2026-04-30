namespace Sunfish.Blocks.Maintenance.Models;

/// <summary>
/// Categorical event recorded against a <see cref="VendorPerformanceRecord"/>
/// per ADR 0058. Sourced from work-order completion events
/// (<c>blocks-maintenance</c> WorkOrder lifecycle emits; the performance log
/// projects).
/// </summary>
public enum VendorPerformanceEvent
{
    /// <summary>The vendor was first hired (initial onboarding marker).</summary>
    Hired,

    /// <summary>A work order was completed by the vendor on time + within scope.</summary>
    JobCompleted,

    /// <summary>The vendor failed to show up for a scheduled work order.</summary>
    JobNoShow,

    /// <summary>The vendor arrived but completed late.</summary>
    JobLate,

    /// <summary>The vendor cancelled before starting (or scope-incompatible).</summary>
    JobCancelled,

    /// <summary>An operator manually adjusted the vendor's rating (free-form score change).</summary>
    RatingAdjusted,

    /// <summary>The vendor's insurance policy lapsed (operationally significant).</summary>
    InsuranceLapse,

    /// <summary>The vendor was suspended (temporary hold).</summary>
    Suspended,

    /// <summary>The vendor was retired (terminal).</summary>
    Retired,
}
