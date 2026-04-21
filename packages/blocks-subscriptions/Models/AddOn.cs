namespace Sunfish.Blocks.Subscriptions.Models;

/// <summary>
/// A catalog-level add-on product that can be attached to a <see cref="Subscription"/>.
/// Add-ons are not tenant-scoped themselves; the association between a tenant's
/// subscription and an add-on lives on <see cref="Subscription.AddOns"/>.
/// </summary>
public sealed record AddOn
{
    /// <summary>Unique identifier for this add-on.</summary>
    public required AddOnId Id { get; init; }

    /// <summary>Human-readable add-on name.</summary>
    public required string Name { get; init; }

    /// <summary>Monthly price for this add-on in the catalog currency.</summary>
    public required decimal MonthlyPrice { get; init; }

    /// <summary>Short marketing description for this add-on.</summary>
    public string Description { get; init; } = string.Empty;
}
