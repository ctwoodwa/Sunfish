namespace Sunfish.Blocks.Subscriptions.Models;

/// <summary>
/// Canonical plan record. A plan is a catalog-level construct (not tenant-scoped)
/// that describes an available subscription tier and its base pricing.
/// </summary>
public sealed record Plan
{
    /// <summary>Unique identifier for this plan.</summary>
    public required PlanId Id { get; init; }

    /// <summary>Human-readable plan name.</summary>
    public required string Name { get; init; }

    /// <summary>The pricing/feature tier for this plan.</summary>
    public required Edition Edition { get; init; }

    /// <summary>Monthly base price in the catalog currency.</summary>
    public required decimal MonthlyPrice { get; init; }

    /// <summary>Short marketing description for this plan.</summary>
    public string Description { get; init; } = string.Empty;
}
