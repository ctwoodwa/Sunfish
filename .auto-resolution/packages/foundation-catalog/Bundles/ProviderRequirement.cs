namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>
/// Declares a provider-category dependency a bundle needs (or optionally supports).
/// Bundles do not name specific vendors — resolution happens via provider adapters.
/// </summary>
public sealed record ProviderRequirement
{
    /// <summary>Provider category this requirement addresses.</summary>
    public required ProviderCategory Category { get; init; }

    /// <summary>If true, a provider must be configured before the bundle can provision.</summary>
    public bool Required { get; init; }

    /// <summary>Short human-readable purpose for admins configuring integrations.</summary>
    public string? Purpose { get; init; }
}
