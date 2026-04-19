namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>
/// Declarative manifest for a business-case bundle. A bundle is configuration,
/// not code: it names the reusable modules to activate, the feature defaults
/// to apply, and the provider integrations it requires. See ADR 0007.
/// </summary>
public sealed record BusinessCaseBundleManifest
{
    /// <summary>Stable bundle identifier, reverse-DNS style (e.g. <c>sunfish.bundles.property-management</c>).</summary>
    public required string Key { get; init; }

    /// <summary>Human-readable bundle name.</summary>
    public required string Name { get; init; }

    /// <summary>Semver. See ADR 0007 for upgrade-safety semantics.</summary>
    public required string Version { get; init; }

    /// <summary>Optional longer description.</summary>
    public string? Description { get; init; }

    /// <summary>Bundle category.</summary>
    public BundleCategory Category { get; init; } = BundleCategory.Operations;

    /// <summary>Bundle lifecycle status.</summary>
    public BundleStatus Status { get; init; } = BundleStatus.Draft;

    /// <summary>Engineering readiness note; free-form by design.</summary>
    public string Maturity { get; init; } = "Scaffold";

    /// <summary>Module keys that must be installed for the bundle to activate.</summary>
    public IReadOnlyList<string> RequiredModules { get; init; } = Array.Empty<string>();

    /// <summary>Module keys that may be activated per edition.</summary>
    public IReadOnlyList<string> OptionalModules { get; init; } = Array.Empty<string>();

    /// <summary>Default feature values applied at tenant provisioning.</summary>
    public IReadOnlyDictionary<string, string> FeatureDefaults { get; init; }
        = new Dictionary<string, string>();

    /// <summary>Edition key → module keys activated for that edition.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> EditionMappings { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();

    /// <summary>Deployment modes this bundle supports.</summary>
    public IReadOnlyList<DeploymentMode> DeploymentModesSupported { get; init; }
        = Array.Empty<DeploymentMode>();

    /// <summary>Provider-category requirements.</summary>
    public IReadOnlyList<ProviderRequirement> ProviderRequirements { get; init; }
        = Array.Empty<ProviderRequirement>();

    /// <summary>Named provider-configuration profiles.</summary>
    public IReadOnlyList<string> IntegrationProfiles { get; init; } = Array.Empty<string>();

    /// <summary>Pre-built workspaces/dashboards seeded for new tenants.</summary>
    public IReadOnlyList<string> SeedWorkspaces { get; init; } = Array.Empty<string>();

    /// <summary>Personas (drives default roles, navigation, and seed data).</summary>
    public IReadOnlyList<string> Personas { get; init; } = Array.Empty<string>();

    /// <summary>Free-form data-ownership / export / residency policy.</summary>
    public string? DataOwnership { get; init; }

    /// <summary>Free-form compliance framing and notes.</summary>
    public string? ComplianceNotes { get; init; }
}
