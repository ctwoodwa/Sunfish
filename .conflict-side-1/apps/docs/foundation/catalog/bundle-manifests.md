---
uid: foundation-catalog-bundle-manifests
title: Catalog — Bundle Manifests
description: The BusinessCaseBundleManifest shape, how it is loaded, and how it is queried.
keywords:
  - bundle manifest
  - BusinessCaseBundleManifest
  - BundleManifestLoader
  - IBundleCatalog
  - deployment mode
  - ADR 0007
---

# Catalog — Bundle Manifests

## Manifest shape

A `BusinessCaseBundleManifest` is a `sealed record` — immutable by construction, serializable via `System.Text.Json`. Every field has a required / optional story that the loader enforces at parse time.

```csharp
public sealed record BusinessCaseBundleManifest
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public string? Description { get; init; }
    public BundleCategory Category { get; init; } = BundleCategory.Operations;
    public BundleStatus Status { get; init; } = BundleStatus.Draft;
    public string Maturity { get; init; } = "Scaffold";

    public IReadOnlyList<string> RequiredModules { get; init; } = [];
    public IReadOnlyList<string> OptionalModules { get; init; } = [];
    public IReadOnlyDictionary<string, string> FeatureDefaults { get; init; } = ...;
    public IReadOnlyDictionary<string, IReadOnlyList<string>> EditionMappings { get; init; } = ...;

    public IReadOnlyList<DeploymentMode> DeploymentModesSupported { get; init; } = [];
    public IReadOnlyList<ProviderRequirement> ProviderRequirements { get; init; } = [];
    public IReadOnlyList<string> IntegrationProfiles { get; init; } = [];
    public IReadOnlyList<string> SeedWorkspaces { get; init; } = [];
    public IReadOnlyList<string> Personas { get; init; } = [];

    public string? DataOwnership { get; init; }
    public string? ComplianceNotes { get; init; }
}
```

### Required identity

- `Key` — stable reverse-DNS identifier (for example, `sunfish.bundles.property-management`). Never reused across bundles.
- `Name` — human-readable label shown in admin UIs.
- `Version` — semver. ADR 0007 governs which fields force a major-version bump.

### Module composition

- `RequiredModules` — module keys that must be installed for the bundle to activate. These match the `ModuleKey` values declared by blocks via [`ISunfishEntityModule`](xref:Sunfish.Foundation.Persistence.ISunfishEntityModule).
- `OptionalModules` — modules the bundle supports but does not require; pairs with `EditionMappings` to gate by edition.
- `EditionMappings` — edition key (`lite`, `standard`, `enterprise`) → module keys activated for that edition.

### Feature defaults

`FeatureDefaults` is a dictionary of `FeatureKey` (as string) → raw default value. At activation time, `IEntitlementResolver` consumers read this dictionary to contribute values to the evaluation chain.

### Deployment and providers

- `DeploymentModesSupported` — the modes the bundle is certified against (`Lite`, `SelfHosted`, `HostedSaaS`).
- `ProviderRequirements` — provider-category dependencies the bundle needs. A `ProviderRequirement` names a category (Billing, Payments, Messaging, …), marks it `Required` or optional, and documents a short purpose string. Bundles never name specific vendors — vendor resolution happens through `Sunfish.Foundation.Integrations`.
- `IntegrationProfiles` — free-form names for provider-configuration profiles a bundle expects to find at activation time.

### Seed data and persona hints

- `SeedWorkspaces` — named seed-data packages the provisioning service applies on tenant activation.
- `Personas` — role / persona hints that drive default permissions, navigation, and seed records.

### Policy framing

`DataOwnership` and `ComplianceNotes` are intentionally free-form strings. They are authored for operators, not machines, so hosts can display them verbatim on admin surfaces.

## Loading manifests

`BundleManifestLoader` is the static helper that reads manifests from JSON.

```csharp
using Sunfish.Foundation.Catalog.Bundles;

// Parse a known JSON string
var manifest = BundleManifestLoader.Parse(jsonText);

// Load from an embedded JSON resource shipped with Sunfish.Foundation.Catalog
var seed = BundleManifestLoader.LoadEmbedded("Bundles/property-management.bundle.json");

// Enumerate every embedded bundle resource
foreach (var name in BundleManifestLoader.ListEmbeddedBundleResourceNames())
{
    var m = BundleManifestLoader.LoadEmbedded(name);
    catalog.Register(m);
}
```

The loader uses `JsonSerializerOptions` with case-insensitive property matching, JSON comments, and trailing commas enabled — chosen so manifests authored by humans remain readable. Every bundle the package ships embedded is validated against the `Schemas/bundle-manifest.schema.json` JSON Schema.

## Using the catalog

Once seeded, modules read bundles through `IBundleCatalog`:

```csharp
public sealed class BundlePickerViewModel
{
    private readonly IBundleCatalog _bundles;

    public BundlePickerViewModel(IBundleCatalog bundles) => _bundles = bundles;

    public IEnumerable<BundleChoice> GetChoices(DeploymentMode mode)
        => _bundles.GetBundles()
            .Where(b => b.Status is BundleStatus.Preview or BundleStatus.GA)
            .Where(b => b.DeploymentModesSupported.Contains(mode))
            .Select(b => new BundleChoice(b.Key, b.Name, b.Description));
}
```

`IBundleCatalog` operations — `Register`, `GetBundles`, `TryGet` — are synchronous because the catalog is an in-memory registry seeded at startup. Database-backed catalogs remain a follow-up; the current default is safe for concurrent reads after registration completes.

## Related

- [Overview](overview.md)
- [Feature Management — Entitlement Resolver](../feature-management/entitlement-resolver.md)
- [Integrations — Provider Registry](../integrations/registry.md)
