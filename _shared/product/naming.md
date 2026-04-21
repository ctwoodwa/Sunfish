# Naming Conventions

**Status:** Accepted
**Last reviewed:** 2026-04-19
**Governs:** Every new package, namespace, project, assembly, bundle key, module key, feature key, provider key, template id, JSON property, and fixture filename in the repo.
**Agent relevance:** Loaded by agents creating new packages, namespaces, projects, bundle keys, module keys, or feature keys. High-frequency for scaffolding tasks.

Naming is a load-bearing part of Sunfish's layered architecture. The conventions below are already in use across the repo; this document codifies them so new code lands consistently. When in doubt, look at a recent adjacent example (`packages/foundation-multitenancy/` for a Foundation-tier package, `packages/blocks-leases/` for a domain module) — then copy the shape.

## Repo paths

### Package folders

Kebab-case, prefixed by tier.

| Tier | Folder prefix | Example |
|---|---|---|
| Foundation | `foundation-*` (split packages) or `foundation` | `packages/foundation-multitenancy`, `packages/foundation-catalog` |
| Kernel | `kernel*` | `packages/kernel`, `packages/kernel-event-bus`, `packages/kernel-schema-registry` |
| UI Core | `ui-core` | `packages/ui-core` |
| UI Adapters | `ui-adapters-<framework>` | `packages/ui-adapters-blazor`, `packages/ui-adapters-react` |
| Compatibility shims | `compat-<vendor>` | `packages/compat-telerik` |
| Domain modules | `blocks-*` | `packages/blocks-leases`, `packages/blocks-rent-collection`, `packages/blocks-tax-reporting` |
| Ingestion | `ingestion-*` | `packages/ingestion-forms`, `packages/ingestion-spreadsheets` |
| Federation | `federation-*` | `packages/federation-common`, `packages/federation-entity-sync` |

Multi-word folder names use kebab (`blocks-rent-collection`, `blocks-tax-reporting`, `foundation-assets-postgres`). Never underscores. Never PascalCase.

### Accelerators

`accelerators/<name>/` — `name` is kebab-case if multi-word. Bridge is the only accelerator today: `accelerators/bridge/`.

### Scripts, docs, shared

- `scripts/` — shell scripts, kebab or snake, `.sh` extension.
- `docs/adrs/` — ADRs, filename `NNNN-short-slug.md` starting at 0001.
- `docs/specifications/` — long-form specs.
- `_shared/design/`, `_shared/engineering/`, `_shared/product/` — standards and policy docs.

## Namespaces, projects, assemblies

### Root namespace

`Sunfish.<Tier>.<Name>`:

| Folder | Root namespace |
|---|---|
| `packages/foundation` | `Sunfish.Foundation` |
| `packages/foundation-multitenancy` | `Sunfish.Foundation.MultiTenancy` |
| `packages/foundation-catalog` | `Sunfish.Foundation.Catalog` |
| `packages/kernel` | `Sunfish.Kernel` |
| `packages/kernel-event-bus` | `Sunfish.Kernel.EventBus` |
| `packages/ui-core` | `Sunfish.UICore` |
| `packages/ui-adapters-blazor` | `Sunfish.UIAdapters.Blazor` |
| `packages/ui-adapters-react` | **TBD — see React adapter ADR (not yet written).** Expected shape: `Sunfish.UIAdapters.React`. |
| `packages/blocks-leases` | `Sunfish.Blocks.Leases` |
| `packages/blocks-rent-collection` | `Sunfish.Blocks.RentCollection` |
| `packages/compat-telerik` | `Sunfish.Compat.Telerik` |
| `packages/ingestion-core` | `Sunfish.Ingestion.Core` |
| `packages/federation-common` | `Sunfish.Federation.Common` |

Kebab folder segments become PascalCase namespace segments (`rent-collection` → `RentCollection`).

### Apps and accelerators

Apps (under `apps/`) and accelerators (under `accelerators/`) use a **flat `Sunfish.<Name>` pattern** — no `Apps.` or `Accelerators.` tier prefix — because they are top-level deployables, not library tiers. The kebab folder collapses to a single PascalCase segment; hyphens are removed, not preserved as namespace separators. Sub-projects extend the root. *See [ADR 0016](../../docs/adrs/0016-app-and-accelerator-naming.md) for the full rationale.*

| Folder | Root namespace |
|---|---|
| `apps/kitchen-sink` | `Sunfish.KitchenSink` |
| `accelerators/bridge` | `Sunfish.Bridge` (multi-project: `Sunfish.Bridge.Data`, `Sunfish.Bridge.Client`, `Sunfish.Bridge.ServiceDefaults`, `Sunfish.Bridge.MigrationService`, `Sunfish.Bridge.AppHost`) |
| `apps/admin-console` (hypothetical) | `Sunfish.AdminConsole` |

### Project / assembly names

Project file (`.csproj`) name typically matches the namespace:

- `packages/foundation-multitenancy/Sunfish.Foundation.MultiTenancy.csproj`
- `packages/blocks-leases/Sunfish.Blocks.Leases.csproj`

No legacy naming exceptions currently live in the repo.

- `packages/ui-adapters-blazor/` was previously a legacy exception (csproj `Sunfish.Components.Blazor.csproj`, namespace `Sunfish.Components.Blazor`). Renamed to `Sunfish.UIAdapters.Blazor` on 2026-04-19 — folder, csproj, `PackageId`, `AssemblyName`, `RootNamespace`, every namespace declaration in `.cs`/`.razor` files, every consumer's `using`/`@using`/`ProjectReference`, and `Sunfish.slnx` all match.
- `apps/kitchen-sink/Sunfish.KitchenSink.csproj` was previously listed here as an exception. It is on-pattern under [ADR 0016](../../docs/adrs/0016-app-and-accelerator-naming.md) (apps use the flat `Sunfish.<Name>` pattern) and needs no rename.

### Test projects

Test projects are named `tests.csproj` inside a `tests/` subfolder. Their assembly name is the full `<Project>.Tests`:

```xml
<PropertyGroup>
  <AssemblyName>Sunfish.Foundation.MultiTenancy.Tests</AssemblyName>
  <RootNamespace>Sunfish.Foundation.MultiTenancy.Tests</RootNamespace>
</PropertyGroup>
```

This allows many test projects named `tests.csproj` to coexist in one solution without assembly-name collisions.

## slnx folder names

`Sunfish.slnx` organizes projects by tier with lowercase, forward-slash paths:

```xml
<Folder Name="/foundation/">
<Folder Name="/foundation/catalog/">
<Folder Name="/foundation/multitenancy/">
<Folder Name="/foundation/feature-management/">
<Folder Name="/foundation/local-first/">
<Folder Name="/foundation/integrations/">
<Folder Name="/kernel/">
<Folder Name="/kernel/event-bus/">
<Folder Name="/blocks/leases/">
<Folder Name="/blocks/rent-collection/">
<Folder Name="/ingestion/core/">
<Folder Name="/federation/common/">
<Folder Name="/accelerators/bridge/">
<Folder Name="/apps/">
```

Multi-word segments become kebab in slnx folder paths (`feature-management`, `local-first`, `rent-collection`) to match the package folder.

## Reverse-DNS keys

Runtime identifiers follow reverse-DNS style with lowercase segments separated by dots.

### Bundle keys

`sunfish.bundles.<slug>` — kebab-case slug for multi-word names.

- `sunfish.bundles.property-management`
- `sunfish.bundles.asset-management`
- `sunfish.bundles.project-management`
- `sunfish.bundles.facility-operations`
- `sunfish.bundles.acquisition-underwriting`

### Module keys

`sunfish.blocks.<slug>` — matches the kebab package folder slug.

- `sunfish.blocks.leases`
- `sunfish.blocks.rent-collection`
- `sunfish.blocks.tax-reporting`
- `sunfish.blocks.maintenance`

### Feature keys

`sunfish.blocks.<module>.<dotted-scope>` — dotted scope using lowercase words or camelCase for multi-word leaves.

- `sunfish.blocks.leases.renewals.autoReminders`
- `sunfish.blocks.rent-collection.lateFees.enabled`
- `sunfish.blocks.maintenance.vendorQuotes.enabled`

Boolean flags tend to end in `.enabled` or an adjective (`.required`). Value features use a short noun leaf.

### Provider keys

`sunfish.providers.<vendor>` — lowercase vendor slug.

- `sunfish.providers.stripe`
- `sunfish.providers.plaid`
- `sunfish.providers.twilio`

Third-party publishers use their own reverse-DNS (`com.example.providers.custom-billing`).

### Extension field keys

Case-sensitive camelCase within an entity scope. The catalog registers them per-entity; the key does not need a reverse-DNS prefix because the entity type disambiguates.

- `moveInChecklist`
- `emergencyContact`
- `petPolicy`

### Template ids

URL-style with a version suffix:

```
https://sunfish.io/schemas/<module-or-bundle>/<template-slug>/<version>
```

- `https://sunfish.io/schemas/property-management/lease-renewal/1.0.0`
- `https://sunfish.io/schemas/facility-operations/work-order-intake/1.0.0`

Tenant overlays use a tenant-scoped URL:

```
tenant://<tenant-slug>/<template-slug>
```

- `tenant://acme/lease-renewal`

## File names

| Artifact | Pattern | Example |
|---|---|---|
| ADR | `NNNN-short-slug.md` | `0007-bundle-manifest-schema.md` |
| Bundle seed JSON | `<key-slug>.bundle.json` | `property-management.bundle.json` |
| Meta-schema | `<kind>.schema.json` | `bundle-manifest.schema.json` |
| Test fixture JSON | `<template-slug>.{schema,uischema,overlay}.json` | `lease-renewal.schema.json` |
| C# source | `<TypeName>.cs` (one public type per file when practical) | `BundleCatalog.cs` |
| Interface file | `I<Name>.cs` | `IBundleCatalog.cs` |
| Enum file | `<EnumName>.cs` (often colocated with consumer when tiny) | `BundleCategory.cs` |
| Record file | `<RecordName>.cs` | `ProviderRequirement.cs` |

## JSON conventions

- Property names are **camelCase**: `requiredModules`, `deploymentModesSupported`, `editionMappings`, `providerRequirements`.
- Enum values are **PascalCase** (serialized via `JsonStringEnumConverter`): `"category": "Operations"`, `"status": "Draft"`, `"deploymentModesSupported": ["Lite", "SelfHosted", "HostedSaaS"]`.
- Keys in dictionaries (edition names, feature defaults) follow the conventions of what they identify: edition names are lowercase (`lite`, `standard`, `enterprise`); feature keys match the reverse-DNS feature-key convention.

## C# style

- File-scoped namespaces (`namespace Sunfish.Foundation.Catalog;` on one line, no braces).
- PascalCase types, methods, properties.
- `camelCase` parameters, locals, private fields (prefix `_` optional for instance fields — existing code mixes; prefer no prefix in new code).
- Interfaces prefixed `I`.
- Enums as single-concept nouns (`BundleCategory`, `TenantStatus`, `DeploymentMode`).
- Records preferred for DTOs, manifests, value objects.
- `required` keyword on init-only properties that must be supplied.
- Extension-method static classes named `<ConsumerType>Extensions` (e.g. `BundleCatalogExtensions`).
- DI sugar named `AddSunfish<Concept>` (e.g. `AddSunfishFeatureManagement`, `AddSunfishBundleCatalog`).

## Commit message subjects

The repo uses short, specific subjects:

- Feature-gate commits (from PRs): `G37 C3: SunfishDataGrid column menu (Sort / Filter / Lock dropdown) (#55)` — `G<number> <letter>: <subject> (#PR)` style.
- Planning / infrastructure commits: descriptive prefix, colon, summary — `Planning phase: ADRs 0005-0014 + 4 Foundation packages + 5 bundle manifests`.
- Chore/housekeeping: `Housekeeping: add Bridge PWA assets + repo logo; ignore Claude local state; untrack .wolf transient files`.

Keep subjects under ~70 characters where practical. PRs carry detail in body; direct-to-main commits still follow the subject/body pattern.

## Pitfalls observed in the repo

- **React adapter namespace is unresolved.** `packages/ui-adapters-react` does not yet exist; the expected namespace is `Sunfish.UIAdapters.React` but the final shape is pending a React adapter ADR. Do not introduce React-adapter code that assumes a namespace until the ADR lands.
- **Embedded-resource `LogicalName`** needs forward slashes: `LogicalName>Bundles/property-management.bundle.json</LogicalName>`. MSBuild's default `%(RecursiveDir)` yields backslashes on Windows, which produces unreadable resource names. Always set `LogicalName` explicitly with forward slashes.
- **Tests assembly name defaults.** Without an explicit `<AssemblyName>`, multiple test csprojs named `tests.csproj` all build to `tests.dll` — fine per-build, but confusing in tooling and `InternalsVisibleTo`. New test projects set `<AssemblyName>Sunfish.<Package>.Tests</AssemblyName>`.

## What to never do

- Never rename a tier prefix casually (`blocks-*` → `domain-*` is a breaking change and needs an ADR).
- Never introduce a singular `block-leases` — the plural `blocks-*` is established.
- Never use generic "Helper" / "Utility" / "Common" suffixes on new packages. If a concept is cross-cutting, it gets its own Foundation package (see MultiTenancy, FeatureManagement, LocalFirst, Integrations).
- Never use underscores in package-folder or slnx segment names.
- Never ship a public type without a one-line XML doc comment (CS1591 enforcement).

## Cross-references

- [architecture-principles.md](architecture-principles.md) — the layering these names reflect.
- [package-conventions.md](../engineering/package-conventions.md) — csproj patterns that use these names.
- [`docs/adrs/0007-bundle-manifest-schema.md`](../../docs/adrs/0007-bundle-manifest-schema.md) — bundle-key conventions.
- [`docs/adrs/0013-foundation-integrations.md`](../../docs/adrs/0013-foundation-integrations.md) — provider-key conventions.
