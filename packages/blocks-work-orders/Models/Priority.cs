namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Generic priority classifier used by <see cref="WorkOrder"/> + future
/// maintenance schedules + repair tickets. Local definition until the
/// canonical foundation-priorities home (if any) is ratified; same
/// pattern as the per-cluster <c>IDomainEventPublisher</c> seam.
/// </summary>
public enum Priority
{
    /// <summary>Lowest urgency.</summary>
    Low,

    /// <summary>Default day-to-day urgency.</summary>
    Normal,

    /// <summary>Elevated — schedule ahead of Normal-priority items.</summary>
    High,

    /// <summary>Critical — drop other work; address immediately.</summary>
    Critical,
}
