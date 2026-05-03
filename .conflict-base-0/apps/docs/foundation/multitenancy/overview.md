---
uid: foundation-multitenancy-overview
title: Multitenancy — Overview
description: Framework-agnostic tenancy primitives that keep tenant identity separate from caller identity and authorization.
keywords:
  - multitenancy
  - tenant context
  - tenant catalog
  - tenant resolver
  - Finbuckle boundary
  - ADR 0008
---

# Multitenancy — Overview

## What this package gives you

`Sunfish.Foundation.MultiTenancy` is the minimum tenancy surface a Sunfish host needs: a way to represent the current tenant, resolve one from a candidate string, enumerate registered tenants, and mark persisted entities as tenant-scoped. Everything else — user identity, authorization, per-request pipeline wiring — lives in other packages.

The package source lives at `packages/foundation-multitenancy/`.

## Why it is a separate package

[ADR 0008](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0008-foundation-multitenancy.md) introduced this package to fix a concrete problem: the older `Sunfish.Foundation.Authorization.ITenantContext` bundled three concerns (current tenant, caller identity, permission checks) into one interface. Any module that wanted "which tenant am I operating on?" ended up importing caller identity and authorization as well. Splitting tenancy out gives modules a focused dependency that reflects the single concept they care about.

A second goal was to establish a **Finbuckle boundary**. Bridge, the SaaS-shell accelerator, is free to adopt `Finbuckle.MultiTenant` inside its ASP.NET Core host for request-pipeline tenant resolution. It does that by exposing a Sunfish `ITenantResolver` adapter on top of Finbuckle's `IMultiTenantContext`. No Sunfish module package ever references Finbuckle directly. Self-hosted deployments, lite-mode apps, and test harnesses plug in their own resolver implementations without pulling Finbuckle in.

## Key types

| Type | Purpose |
|---|---|
| [`TenantMetadata`](xref:Sunfish.Foundation.MultiTenancy.TenantMetadata) | Descriptive record for one tenant: identity, status, optional display name, locale, creation timestamp, free-form properties. |
| [`TenantStatus`](xref:Sunfish.Foundation.MultiTenancy.TenantStatus) | Lifecycle enum — `Active`, `Suspended`, `Decommissioning`, `Archived`. |
| [`ITenantContext`](xref:Sunfish.Foundation.MultiTenancy.ITenantContext) | Read-only accessor exposing the resolved tenant for the current scope. |
| [`ITenantResolver`](xref:Sunfish.Foundation.MultiTenancy.ITenantResolver) | Turns a host-supplied candidate string into `TenantMetadata`. |
| [`ITenantCatalog`](xref:Sunfish.Foundation.MultiTenancy.ITenantCatalog) | Authoritative list of tenants the host knows about. |
| [`ITenantScoped`](xref:Sunfish.Foundation.MultiTenancy.ITenantScoped) / `IMustHaveTenant` / `IMayHaveTenant` | Entity markers used by persistence adapters to apply tenant filters. |
| `InMemoryTenantCatalog` | Reference implementation of both the catalog and the resolver; suitable for tests, demos, and lite-mode. |

## `TenantMetadata` shape

Every piece of tenant state the platform needs lives on one immutable record:

```csharp
public sealed record TenantMetadata
{
    public required TenantId Id { get; init; }
    public required string Name { get; init; }
    public TenantStatus Status { get; init; } = TenantStatus.Active;
    public string? DisplayName { get; init; }
    public string? Locale { get; init; }      // BCP-47 locale tag
    public DateTimeOffset? CreatedAt { get; init; }
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();
}
```

`Id` is the stable machine identity; `Name` is a short routable slug (suitable for subdomains or path segments). `DisplayName`, `Locale`, and `CreatedAt` are optional metadata for UI and audit. The free-form `Properties` bag carries host-specific metadata without forcing every deployment onto the same structural schema.

## Lifecycle

`TenantStatus` has four stages:

- `Active` — serving traffic.
- `Suspended` — registered but temporarily not servicing requests (billing hold, manual freeze).
- `Decommissioning` — deactivation in progress; data retained.
- `Archived` — fully deactivated; only read-only historical access allowed.

Hosts choose how each status maps to request-level behaviour — rejecting suspended tenants at the edge, surfacing banner warnings in the UI, or routing decommissioning tenants to a read-only view.

## Registering the defaults

```csharp
using Sunfish.Foundation.MultiTenancy;

services.AddSunfishTenantCatalog();
```

`AddSunfishTenantCatalog` registers `InMemoryTenantCatalog` as a singleton and exposes it as both `ITenantCatalog` and `ITenantResolver`. The in-memory catalog supports concurrent reads after registration and throws on duplicate tenant ids, which makes it a safe default for tests, demos, and lite-mode. Seed tenants from configuration or test fixtures.

For hosts that need durable storage, register a custom `ITenantCatalog` (and typically a separate `ITenantResolver`) that reads from the persistent store of record.

## Typical composition

A production Bridge deployment composes three tenancy adapters:

- `ITenantCatalog` backed by Postgres (the authoritative tenant list).
- `ITenantResolver` from a Finbuckle `IMultiTenantContext` adapter, resolving via host header / subdomain / claim.
- `ITenantContext` scoped per request, populated by the resolver middleware.

A lite-mode or single-tenant desktop app collapses the three into one: a `FixedTenantContext` returning a single configured tenant, with `ITenantResolver` unused and `InMemoryTenantCatalog` seeded with the single row.

## Related

- [Tenant Context](tenant-context.md)
- [Tenant-Scoped Markers](tenant-scoped-markers.md)
- [ADR 0008 — Foundation.MultiTenancy Contracts + Finbuckle Boundary](https://github.com/ctwoodwa/Sunfish/blob/main/docs/adrs/0008-foundation-multitenancy.md)
</content>
</invoke>