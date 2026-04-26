---
uid: foundation-catalog-overview
title: Catalog — Overview
description: The bundle and module registry — what a tenant can activate, which editions unlock which modules, and which providers a bundle requires.
keywords:
  - catalog
  - bundle manifest
  - business case
  - extension fields
  - templates
  - ADR 0007
---

# Catalog — Overview

## What this package gives you

`Sunfish.Foundation.Catalog` is the declarative product catalog: the bundles a tenant can activate, the modules those bundles pull in, the editions that gate module availability, and the provider-category requirements that gate bundle activation. It is configuration, not code — a bundle is a manifest that names reusable modules, default feature values, supported deployment modes, and required integrations.

The package source lives at `packages/foundation-catalog/`. It has three areas:

- **`Bundles/`** — `BusinessCaseBundleManifest`, `ModuleManifest`, `IBundleCatalog`, `BundleManifestLoader`, and the supporting enums.
- **`ExtensionFields/`** — `IExtensionFieldCatalog` and spec types for per-tenant extension fields.
- **`Templates/`** — `TemplateDefinition`, `TenantTemplateOverlay`, and `TemplateMerger` for default-versus-tenant template resolution.

All three areas expose small, in-memory registries that hosts seed at startup. Durable back-ends (Postgres-backed catalogs, tenant-authored templates persisted per tenant) slot in behind the same contracts.

## Why bundles are declarative

Bundles describe capabilities a customer can turn on without rebuilding or redeploying the platform. Every production Sunfish host loads a fixed set of manifests at startup, then each tenant picks from those — so the catalog is the single source of truth for "what is installable". Keeping the shape declarative means:

- Bundle activation logic works for every host (Bridge, lite-mode, self-hosted, tests) without copy-pasted business rules.
- Bundle authoring happens in JSON manifests, reviewed like code — no runtime mutations.
- External tooling (documentation site, CLI, admin UIs) reads the catalog to generate accurate views.

See [ADR 0007](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0007-bundle-manifest-schema.md) for the historical rationale and schema versioning policy.

## Key bundle types

| Type | Purpose |
|---|---|
| [`BusinessCaseBundleManifest`](xref:Sunfish.Foundation.Catalog.Bundles.BusinessCaseBundleManifest) | The bundle record itself — key, version, required and optional modules, feature defaults, edition mappings, provider requirements, deployment-mode support. |
| [`BundleCategory`](xref:Sunfish.Foundation.Catalog.Bundles.BundleCategory) | Coarse classification — `Operations`, `Diligence`, `Finance`, `Platform`. |
| [`BundleStatus`](xref:Sunfish.Foundation.Catalog.Bundles.BundleStatus) | Lifecycle — `Draft`, `Preview`, `GA`, `Deprecated`. |
| [`DeploymentMode`](xref:Sunfish.Foundation.Catalog.Bundles.DeploymentMode) | `Lite`, `SelfHosted`, `HostedSaaS`. |
| [`ModuleManifest`](xref:Sunfish.Foundation.Catalog.Bundles.ModuleManifest) | Minimal module descriptor (key, name, version, capabilities). |
| [`ProviderCategory`](xref:Sunfish.Foundation.Catalog.Bundles.ProviderCategory) | Provider-category enum shared with `Sunfish.Foundation.Integrations`. |
| [`ProviderRequirement`](xref:Sunfish.Foundation.Catalog.Bundles.ProviderRequirement) | Declares a provider-category dependency a bundle needs. |
| [`IBundleCatalog`](xref:Sunfish.Foundation.Catalog.Bundles.IBundleCatalog) | Registry contract — `Register`, `GetBundles`, `TryGet`. |
| [`BundleCatalog`](xref:Sunfish.Foundation.Catalog.Bundles.BundleCatalog) | Default in-memory implementation. |
| [`BundleManifestLoader`](xref:Sunfish.Foundation.Catalog.Bundles.BundleManifestLoader) | JSON parser + embedded-resource loader for manifests. |

## Extension fields

`Sunfish.Foundation.Catalog.ExtensionFields` complements bundles with a per-entity registry of custom fields. An `ExtensionFieldSpec` names a key, a CLR `ValueType`, a scope (`Bundle` vs. `Tenant`), and a storage strategy (`JsonBag` vs. `PromotedColumn`), plus optional `IsRequired`, `IsSearchable`, `DisplayName`, and `Description`. Persistence adapters, UI renderers, and validators all read `IExtensionFieldCatalog` instead of discovering fields ad hoc:

```csharp
public interface IExtensionFieldCatalog
{
    void Register(Type entityType, ExtensionFieldSpec spec);
    IReadOnlyList<ExtensionFieldSpec> GetFields(Type entityType);
    bool TryGetField(Type entityType, ExtensionFieldKey key, out ExtensionFieldSpec? spec);
}
```

Bundles contribute fields at startup; tenant-scoped fields author themselves at admin time. The same catalog drives both.

## Templates

`Sunfish.Foundation.Catalog.Templates` carries the "designer-authored form / checklist / report" shape. A `TemplateDefinition` pairs a JSON Schema 2020-12 `DataSchema` with a renderer-facing `UiSchema` (e.g. JSONForms-style layout), identified by an id and version. `TenantTemplateOverlay` captures per-tenant customizations as RFC 7396 merge patches against data and UI schemas. `TemplateMerger.Resolve` applies the overlay and returns a fresh `TemplateDefinition`, validating that the overlay's `BaseRef` matches the base template's `id` or `id@version`.

## Registering the defaults

```csharp
using Sunfish.Foundation.Catalog.Bundles;

services.AddSunfishBundleCatalog();
```

`AddSunfishBundleCatalog` registers `BundleCatalog` as a singleton `IBundleCatalog`. The in-memory implementation is safe for concurrent reads after startup and throws on duplicate bundle keys. Hosts seed manifests from embedded JSON resources or `appsettings`-style configuration at startup, typically one call per shipped bundle:

```csharp
var catalog = sp.GetRequiredService<IBundleCatalog>();
foreach (var resource in BundleManifestLoader.ListEmbeddedBundleResourceNames())
{
    catalog.Register(BundleManifestLoader.LoadEmbedded(resource));
}
```

Sibling DI extensions (`ExtensionFieldCatalogExtensions.AddSunfishExtensionFieldCatalog`, template-registry helpers) register the extension-field and template catalogs the same way.

## Related

- [Bundle Manifests](bundle-manifests.md)
- [Feature Management — Entitlement Resolver](../feature-management/entitlement-resolver.md)
- [Integrations — Provider Registry](../integrations/registry.md)
- [ADR 0007 — Bundle Manifest Schema](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0007-bundle-manifest-schema.md)
</content>
</invoke>