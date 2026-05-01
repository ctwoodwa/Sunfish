# Sunfish.Blocks.BusinessCases

Business-cases resolver block — implements `IEntitlementResolver`, owns the bundle-provisioning service, and contributes EF Core entity configurations via `ISunfishEntityModule`.

## What this ships

### Models

- **`TenantEntitlementSnapshot`** — point-in-time snapshot of a tenant's resolved entitlements (active subscription + bundle features + add-ons).
- **`BundleActivationRecord`** + `BundleActivationRecordId` — append-only record of bundle activations per tenant.
- **`BusinessCaseId`** — Guid-wrapper record struct for the business-case identifier.

### Services

- **`IEntitlementResolver`** — single-source entitlement resolution from active subscription + plan + edition + add-ons. Consumers (feature flags, access checks) ask the resolver rather than hitting subscription state directly.
- **`IBundleProvisioner`** — orchestrates bundle activation against `IBundleCatalog` + `Sunfish.Blocks.Subscriptions`.
- **`InMemoryBusinessCasesService`** — reference impl.
- **`BusinessCasesEntityModule`** — `ISunfishEntityModule` contribution per ADR 0015.

### UI

- **`EntitlementSnapshotBlock.razor`** — read-display of the current tenant's entitlement snapshot.
- **Feature-management** integration — wires `IFeatureManager` against the resolver.

## DI

```csharp
services.AddInMemoryBusinessCases();
```

## Cluster role

The horizontal entitlement-resolution backbone for the platform. Consumed by:

- `Sunfish.Foundation.FeatureManagement` (feature flag evaluation)
- `blocks-tenant-admin` (admin UI shows resolved snapshot)
- Anywhere the Bridge needs "is this tenant licensed for X?"

## ADR map

- [ADR 0007](../../docs/adrs/0007-bundle-catalog.md) — bundle catalog
- [ADR 0009](../../docs/adrs/0009-feature-management.md) — feature-management chain
- [ADR 0015](../../docs/adrs/0015-module-entity-registration.md) — module-entity registration

## See also

- [apps/docs Overview](../../apps/docs/blocks/businesscases/overview.md)
- [Sunfish.Blocks.Subscriptions](../blocks-subscriptions/README.md) — subscription state input
- [Sunfish.Blocks.TenantAdmin](../blocks-tenant-admin/README.md) — admin UI consumer
