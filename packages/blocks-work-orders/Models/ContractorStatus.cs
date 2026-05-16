namespace Sunfish.Blocks.WorkOrders.Models;

/// <summary>
/// Lifecycle state of a <see cref="Contractor"/>.
/// </summary>
public enum ContractorStatus
{
    /// <summary>Available for new dispatch.</summary>
    Active,

    /// <summary>Temporarily not dispatched (compliance lapse, capacity).</summary>
    Paused,

    /// <summary>Never dispatched — track for vendor-history audit; do not surface in pickers.</summary>
    Blacklisted,

    /// <summary>Historical record; archived (e.g., vendor closed).</summary>
    Archived,
}
