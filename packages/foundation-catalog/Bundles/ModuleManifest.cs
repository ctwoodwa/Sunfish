namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>
/// Minimal module manifest shape. Authored alongside each <c>blocks-*</c>
/// module once P2 module work begins; consumed by bundle-reference
/// validation.
/// </summary>
public sealed record ModuleManifest
{
    /// <summary>Stable reverse-DNS-style module key (e.g. <c>sunfish.blocks.leases</c>).</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable module name.</summary>
    public required string Name { get; init; }

    /// <summary>Semver for the module contract.</summary>
    public required string Version { get; init; }

    /// <summary>Optional longer description.</summary>
    public string? Description { get; init; }

    /// <summary>Named capabilities this module exposes (used by bundles and CLI tooling).</summary>
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();
}
