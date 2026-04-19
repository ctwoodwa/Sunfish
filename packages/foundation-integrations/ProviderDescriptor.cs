using Sunfish.Foundation.Catalog.Bundles;

namespace Sunfish.Foundation.Integrations;

/// <summary>
/// Metadata for a registered external provider. Adapters register a
/// descriptor at startup so Bridge admin surfaces can enumerate what is wired
/// and modules can look up providers by key or category.
/// </summary>
public sealed record ProviderDescriptor
{
    /// <summary>Stable reverse-DNS-style provider key (e.g. <c>sunfish.providers.stripe</c>).</summary>
    public required string Key { get; init; }

    /// <summary>Category the provider serves.</summary>
    public required ProviderCategory Category { get; init; }

    /// <summary>Human-readable provider name.</summary>
    public required string Name { get; init; }

    /// <summary>Semver for the adapter package.</summary>
    public required string Version { get; init; }

    /// <summary>Optional longer description.</summary>
    public string? Description { get; init; }

    /// <summary>Named capabilities this provider exposes (free-form until a taxonomy ADR ships).</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();

    /// <summary>Supported regions (BCP-47 region tags or vendor-specific region codes).</summary>
    public IReadOnlyList<string> SupportedRegions { get; init; } = Array.Empty<string>();
}
