---
uid: foundation-multitenancy-overview
title: Multitenancy — Overview
description: Framework-agnostic tenancy primitives that keep tenant identity separate from caller identity and authorization.
---

# Multitenancy — Overview

## What this package gives you

`Sunfish.Foundation.MultiTenancy` is the minimum tenancy surface a Sunfish host needs: a way to represent the current tenant, resolve one from a candidate string, enumerate registered tenants, and mark persisted entities as tenant-scoped. Everything else — user identity, authorization, per-request pipeline wiring — lives in other packages.

The package source lives at `packages/foundation-multitenancy/`.

## Why it is a separate package

[ADR 0008](xref:adr-0008-foundation-multitenancy) introduced this package to fix a concrete problem: the older `Sunfish.Foundation.Authorization.ITenantContext` bundled three concerns (current tenant, caller identity, permission checks) into one interface. Any module that wanted "which tenant am I operating on?" ended up importing caller identity and authorization as well. Splitting tenancy out gives modules a focused dependency that reflects the single concept they care about.

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

## Registering the defaults

```csharp
using Sunfish.Foundation.MultiTenancy;

services.AddSunfishTenantCatalog();
```

`AddSunfishTenantCatalog` registers `InMemoryTenantCatalog` as a singleton and exposes it as both `ITenantCatalog` and `ITenantResolver`. Seed tenants from configuration or test fixtures.

## Related

- [Tenant Context](tenant-context.md)
- [Tenant-Scoped Markers](tenant-scoped-markers.md)
- [ADR 0008 — Foundation.MultiTenancy Contracts + Finbuckle Boundary](xref:adr-0008-foundation-multitenancy)
