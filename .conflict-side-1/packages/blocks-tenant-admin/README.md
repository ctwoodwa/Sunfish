# Sunfish.Blocks.TenantAdmin

Tenant-admin block — tenant profile, users, roles, and the bundle-activation surface over `IBundleCatalog`.

## What this ships

### Models

- **`TenantProfile`** — per-tenant configuration record (name, slug, contact, branding hooks).
- **`TenantUser`** + `TenantUserId` — user-account entity scoped to a tenant; references the platform identity.
- **`TenantRole`** — role assignment (Owner / Admin / Manager / Operator / ReadOnly etc.).
- **`BundleActivation`** + `BundleActivationId` — activation record for a `Sunfish.Foundation.Catalog` bundle (links to subscription + plan + edition).

### Services

- **`ITenantAdminService`** + `InMemoryTenantAdminService` — CRUD + role management + bundle activation flow.
- **`TenantAdminEntityModule`** — `ISunfishEntityModule` contribution per ADR 0015.

### UI

- **`BundleActivationPanel.razor`** — drives bundle activation against `IBundleCatalog` from `Sunfish.Foundation.Catalog`.

## DI

```csharp
services.AddInMemoryTenantAdmin();
```

## Cluster role

Horizontal block consumed by the Bridge tenant-management cockpit. Pairs with:

- `blocks-subscriptions` (subscription state)
- `blocks-businesscases` (entitlement resolution)
- `Sunfish.Foundation.Catalog` (bundle catalog)

## ADR map

- [ADR 0007](../../docs/adrs/0007-bundle-catalog.md) — bundle catalog
- [ADR 0015](../../docs/adrs/0015-module-entity-registration.md) — module-entity registration

## See also

- [apps/docs Overview](../../apps/docs/blocks/tenant-admin/overview.md)
- [Sunfish.Blocks.Subscriptions](../blocks-subscriptions/README.md)
- [Sunfish.Blocks.BusinessCases](../blocks-businesscases/README.md)
