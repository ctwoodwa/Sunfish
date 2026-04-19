# ADR 0007 — Bundle Manifest Schema

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** Missing formal shape for business-case bundle manifests; required before Bridge can provision tenants or before any second bundle is composable.

---

## Context

ADR 0005 established that bundles are *configuration*, not code — a bundle is a named entitlement composition that tells the shell which reusable modules to activate, which feature defaults to apply, and which provider integrations are required for a given business case. ADR 0006 established that Bridge is a generic SaaS shell that hosts bundles rather than being a bundle itself.

To make bundles operational, Sunfish needs a stable, versioned shape for bundle manifests that:

1. Can be authored as JSON (shipped as seed data), as a C# record (loaded into services), and as CLI template output (P6).
2. Composes reusable modules by string key, without hardcoding module types or references.
3. Declares feature defaults and edition → module mappings so tenants can subscribe to a tier and inherit a consistent module set.
4. Declares which deployment modes (lite, self-hosted, hosted SaaS) the bundle supports.
5. Declares provider requirements (billing, payments, feature flags, channel managers, …) without locking to any vendor.
6. Stays backwards-compatible across bundle version bumps — consumers must be able to upgrade a tenant from v0.1 to v0.2 of a bundle.

The bundle manifest is the control-plane contract for every downstream feature: tenant provisioning, edition enforcement, feature evaluation, template activation, and integration setup.

---

## Decision

Adopt the following record shapes in `Sunfish.Foundation.Catalog.Bundles`, deserializable from JSON via `System.Text.Json` with default camelCase property names. All collections default to empty so manifests can grow additively.

### `BusinessCaseBundleManifest`

| Field | Type | Purpose |
|---|---|---|
| `key` | `string` (required) | Stable bundle identifier, reverse-DNS style (e.g. `sunfish.bundles.property-management`). |
| `name` | `string` (required) | Human-readable name. |
| `version` | `string` (required) | Semver. Bundle upgrades move tenants forward safely when minor/patch; major requires migration. |
| `description` | `string?` | Longer prose. |
| `category` | `BundleCategory` | Coarse classification (Operations, Diligence, Finance, Platform). |
| `status` | `BundleStatus` | Lifecycle (Draft, Preview, GA, Deprecated). |
| `maturity` | `string` | Engineering readiness note ("Scaffold", "Beta", etc.). Free-form by design. |
| `requiredModules` | `string[]` | Module keys that must be installed. |
| `optionalModules` | `string[]` | Module keys that may be installed per edition. |
| `featureDefaults` | `map<string,string>` | Default feature values applied at tenant provisioning. |
| `editionMappings` | `map<string, string[]>` | Edition key → module keys activated (e.g. "lite" → `[blocks.leases, blocks.maintenance]`). |
| `deploymentModesSupported` | `DeploymentMode[]` | Which of Lite / SelfHosted / HostedSaaS this bundle supports. |
| `providerRequirements` | `ProviderRequirement[]` | Provider categories this bundle needs (optional or required). |
| `integrationProfiles` | `string[]` | Named provider-configuration profiles (e.g. `payment-gateway-default`). |
| `seedWorkspaces` | `string[]` | Pre-built workspaces/dashboards to seed for new tenants. |
| `personas` | `string[]` | Named personas the bundle ships roles and navigation for. |
| `dataOwnership` | `string?` | Free-form policy statement (ownership, export, residency). |
| `complianceNotes` | `string?` | Free-form compliance framing. |

### `ProviderRequirement`

| Field | Type | Purpose |
|---|---|---|
| `category` | `ProviderCategory` | Payments, BankingFeed, FeatureFlags, ChannelManager, etc. |
| `required` | `bool` | If false, feature degrades gracefully when absent. |
| `purpose` | `string?` | One-line explanation. |

### `ModuleManifest` (minimal, authored alongside each `blocks-*` module)

| Field | Type |
|---|---|
| `key` | `string` (required) |
| `name` | `string` (required) |
| `version` | `string` (required) |
| `description` | `string?` |
| `capabilities` | `string[]` |

Module manifests are a forward primitive: the first consumer is bundle-reference validation (does the bundle reference any module key that isn't installed?), which can be implemented once real modules start shipping their own manifests in P2.

### Enums

- `BundleCategory`: `Operations`, `Diligence`, `Finance`, `Platform`.
- `BundleStatus`: `Draft`, `Preview`, `GA`, `Deprecated`.
- `DeploymentMode`: `Lite`, `SelfHosted`, `HostedSaaS`.
- `ProviderCategory`: `Billing`, `Payments`, `BankingFeed`, `FeatureFlags`, `ChannelManager`, `Messaging`, `Storage`, `IdentityProvider`, `Other`.

All enums serialize as strings in JSON (via `JsonStringEnumConverter`).

### Runtime: `IBundleCatalog`

```csharp
public interface IBundleCatalog
{
    void Register(BusinessCaseBundleManifest manifest);
    IReadOnlyList<BusinessCaseBundleManifest> GetBundles();
    bool TryGet(string key, out BusinessCaseBundleManifest? manifest);
}
```

Bridge's provisioning service consumes `IBundleCatalog` to: look up a selected bundle by key, verify required modules are installed, evaluate edition mappings, apply feature defaults, and activate provider integrations.

### Shipping manifests

Reference bundle manifests ship as **embedded JSON resources inside `Sunfish.Foundation.Catalog`**, under `Manifests/Bundles/*.bundle.json` with clean `LogicalName`s. A small `BundleManifestLoader` reads embedded or arbitrary JSON into `BusinessCaseBundleManifest`. This was left as an open decision in ADR 0005; this ADR resolves it to Option (1) — embedded — for the first several manifests. If the catalog grows or external teams ship their own bundles, a sibling `catalog-seed` package becomes the follow-up split (no code migration required).

### Versioning policy

- Manifest `version` is semver.
- **Patch** bumps may change prose, `featureDefaults`, `personas`, `seedWorkspaces`, `complianceNotes`, `maturity`. No tenant action required.
- **Minor** bumps may add `optionalModules`, add entries to `editionMappings`, add `providerRequirements` where `required: false`. Tenants auto-upgrade; newly-optional modules are inactive until the tenant opts in.
- **Major** bumps may alter `requiredModules`, remove edition mappings, flip `providerRequirements.required` to true, or change `deploymentModesSupported`. Tenants require an explicit migration, surfaced in Bridge admin.

This is the same contract that bundle-store UX (P3) will communicate to tenant admins.

---

## Consequences

### Positive

- Bundle composition is declarative and diffable across versions.
- Property Management (and soon Asset, Project, Facility, Acquisition) become coherent, activatable artifacts.
- External bundles become feasible — the manifest is portable JSON; nothing forces a bundle to live in the Sunfish repo.
- Bridge's provisioning logic shrinks to "read catalog, apply manifest."
- Feature management (ADR 0009, P1) consumes bundle manifests directly — no ad-hoc feature lists.

### Negative

- Manifest evolution requires versioning discipline. Without it, tenant upgrades break.
- `editionMappings` duplicates some information from `optionalModules` intentionally for clarity at the expense of terseness.
- Embedding manifests in the library grows the assembly. First five bundles are a few kilobytes total; not material for a decade.

### Follow-ups

1. **Bundle manifest JSON Schema** — a meta-schema for authoring editor validation, to ship once three or more bundles exist.
2. **Module manifest runtime** — `IModuleCatalog` + registration from module assemblies, triggered when P2 modules start carrying their own manifests.
3. **Bridge `BundleProvisioningService`** — consumes `IBundleCatalog` + `IFeatureEvaluator` to realize a tenant's bundle selection.
4. **Catalog-seed split** — only if/when manifests grow or external publishers appear.

---

## References

- ADR 0005 — Type-Customization Model (establishes bundles as configuration, manifests as data).
- ADR 0006 — Bridge Is a Generic SaaS Shell (establishes the shell that consumes the catalog).
- ABP Framework module/bundle patterns — inspiration for module manifest granularity.
- Salesforce managed packages, Microsoft Dataverse Solutions — prior-art for versioned bundle artifacts with edition-like enforcement.
- `Sunfish.Foundation.Catalog` package — home of the new types and embedded seed.
