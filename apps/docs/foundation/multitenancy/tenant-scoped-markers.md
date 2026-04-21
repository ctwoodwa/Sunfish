---
uid: foundation-multitenancy-tenant-scoped-markers
title: Multitenancy — Tenant-Scoped Markers
description: Declarative markers that let persistence adapters apply tenant filters, enforce isolation, and surface per-tenant indexes.
---

# Multitenancy — Tenant-Scoped Markers

## The three markers

Three interfaces declare how an entity relates to a tenant. They live in `Sunfish.Foundation.MultiTenancy` and carry no behaviour of their own — the persistence layer reads them and applies the right rules.

```csharp
public interface ITenantScoped
{
    TenantId TenantId { get; }
}

public interface IMustHaveTenant : ITenantScoped { }

public interface IMayHaveTenant
{
    TenantId? TenantId { get; }
}
```

| Marker | Meaning |
|---|---|
| `ITenantScoped` | Entity belongs to a specific tenant; exposes a non-null `TenantId`. |
| `IMustHaveTenant` | Narrower guarantee: the entity cannot be persisted without a populated tenant. Persistence adapters reject writes where `TenantId` is the default value. |
| `IMayHaveTenant` | The entity **may** be tenant-scoped but sometimes represents a system-level or cross-tenant record. Persistence adapters apply tenant filters only when `TenantId` is non-null. |

Pick the narrowest marker the entity deserves. `IMustHaveTenant` is the right default for tenant-owned domain data; `IMayHaveTenant` is the right choice for lookup tables, shared catalogs, or records that the platform owns on behalf of every tenant.

## Example — an `IMustHaveTenant` entity

```csharp
public sealed class Subscription : IMustHaveTenant
{
    public Guid Id { get; init; }
    public TenantId TenantId { get; init; }
    public string Plan { get; init; } = "standard";
    public DateTimeOffset ActivatedAt { get; init; }
}
```

## How persistence enforces the contract

Bridge's shared `SunfishBridgeDbContext` (see [ADR 0015](xref:adr-0015-module-entity-registration)) combines the markers with EF Core query filters.

At model-build time, Bridge's `OnModelCreating` walks every registered entity type. For any type that implements `IMustHaveTenant` it registers a global query filter of the form `e => e.TenantId == _currentTenantId`, where `_currentTenantId` is injected per-`DbContext` from `ITenantContext`. Each scoped DbContext sees only its own tenant's rows — no hand-rolled `.Where()` calls, no leakage risk from a module that forgets to filter.

The same pass can attach `IMayHaveTenant` filters that additionally admit system-level rows (`TenantId == null`).

Because enforcement happens centrally, blocks that implement [`ISunfishEntityModule`](xref:Sunfish.Foundation.Persistence.ISunfishEntityModule) and register their entity configurations via standard `IEntityTypeConfiguration<T>` classes pick up tenant isolation for free — they just mark the entity and the shell does the rest.

## Non-EF adapters

Nothing in the markers is EF-specific. Non-EF storage adapters (blob-backed stores, event stores, local-first backends) can inspect the same markers at runtime. The rule each adapter owns is: **if the marker demands a tenant, the adapter must scope reads and writes to that tenant**. The markers make the intent declarative; adapters choose the mechanism.

## Related

- [Overview](overview.md)
- [Tenant Context](tenant-context.md)
- [Persistence — Entity-Module Pattern](../persistence/entity-module-pattern.md)
- [ADR 0008 — Foundation.MultiTenancy Contracts + Finbuckle Boundary](xref:adr-0008-foundation-multitenancy)
- [ADR 0015 — Module-Entity Registration Pattern](xref:adr-0015-module-entity-registration)
