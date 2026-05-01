# Sunfish.Foundation.Catalog

Customization catalog — extension-field registrations and template definitions with tenant overlays. Control plane for [ADR 0005](../../docs/adrs/0005-customization-architecture.md).

## What this ships

### Extension fields

- **`ExtensionFieldRegistration`** — per-tenant override that adds a custom field to a base entity (e.g., add `LeaseRenewalProbability: decimal` to `Lease`).
- **`IExtensionFieldRegistry`** — read/write registry for extension-field definitions.

### Templates

- **`TemplateDefinition`** — declarative template (form / report / email-message) overridable per tenant.
- **`ITemplateCatalog`** — read API for resolving the active template for `(tenant, template-key)` with fallback to the platform default.

### Bundles (consumed by `Sunfish.Blocks.Subscriptions` + `Sunfish.Blocks.BusinessCases`)

- **`BundleManifest`** — declarative bundle package (a set of features + entitlements + included templates).
- **`IBundleCatalog`** — read API for resolving "which bundle is this tenant on" + "what's in that bundle".

## DI

```csharp
services.AddSunfishBundleCatalog();
```

## Cluster role

The control plane for tenant-customizable behavior. Reads from this catalog inform:

- `Sunfish.Foundation.FeatureManagement` (feature-flag evaluation)
- `Sunfish.Blocks.BusinessCases` (entitlement resolution)
- `Sunfish.Blocks.TenantAdmin` (admin UI surfaces bundle activation)
- Any block that needs per-tenant customization (extension fields, template overrides)

## ADR map

- [ADR 0005](../../docs/adrs/0005-customization-architecture.md) — customization architecture
- [ADR 0007](../../docs/adrs/0007-bundle-catalog.md) — bundle catalog

## See also

- [apps/docs Overview](../../apps/docs/foundation/catalog/overview.md)
- [Sunfish.Blocks.BusinessCases](../blocks-businesscases/README.md) — entitlement resolver consumer
- [Sunfish.Blocks.TenantAdmin](../blocks-tenant-admin/README.md) — admin UI consumer
