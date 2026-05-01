# Sunfish.Foundation.MultiTenancy

Tenancy primitives — `TenantMetadata`, `ITenantContext`, `ITenantResolver`, `ITenantCatalog`, scoped markers, and an in-memory default.

Does NOT reference Finbuckle — Bridge handles that adaptation in its host. Implements [ADR 0008](../../docs/adrs/0008-multi-tenancy.md).

## What this ships

### Contracts

- **`ITenantContext`** — per-request tenant identity (TenantId, UserId, Roles, HasPermission). Resolved by middleware in the host (Bridge, Anchor, etc.).
- **`ITenantResolver`** — strategy for resolving the active tenant from a request (subdomain, header, claim, etc.).
- **`ITenantCatalog`** — registry of known tenants (slug → TenantMetadata).
- **`TenantMetadata`** — per-tenant configuration record (name, slug, status, contact, branding hooks).

### Markers

- **`IMustHaveTenant`** — interface marker on entities that must carry a `TenantId`. Persistence enforces the global query filter (ADR 0008) on any type implementing this marker.
- **`TenantId`** — string-wrapper record struct from `Sunfish.Foundation.Assets.Common` (sentinel: `TenantId.Default`).

### Default impls

- **In-memory `ITenantCatalog`** — for tests + non-production hosts.
- Helper extensions for tenant-scoped query filters in `Sunfish.Foundation.Persistence`.

## Boundary

Bridge / Anchor hosts provide their own `ITenantContext` implementation. Bridge wires Finbuckle (subdomain resolution); Anchor wires a workspace-switching context (per ADR 0032). This package stays Finbuckle-agnostic so the contracts are reusable across hosts.

## ADR map

- [ADR 0008](../../docs/adrs/0008-multi-tenancy.md) — multi-tenancy + global query filter pattern

## See also

- [apps/docs Overview](../../apps/docs/foundation/multitenancy/overview.md)
- [Sunfish.Foundation.Persistence](../foundation-persistence/README.md) — consumer (`IMustHaveTenant` query filter)
