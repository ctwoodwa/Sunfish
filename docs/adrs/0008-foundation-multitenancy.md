---
id: 8
title: Foundation.MultiTenancy Contracts + Finbuckle Boundary
status: Accepted
date: 2026-04-19
tier: foundation
concern:
  - multi-tenancy
composes:
  - 5
  - 6
  - 7
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0008 — Foundation.MultiTenancy Contracts + Finbuckle Boundary

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** Consolidate scattered tenancy primitives into a dedicated Foundation package; establish the boundary between Sunfish tenancy abstractions and any third-party multi-tenant library used by the shell accelerator.

---

## Context

Tenancy concepts already exist in the Sunfish repo but are scattered and overloaded:

- `Sunfish.Foundation.Authorization.ITenantContext` bundles three distinct concerns into one interface: current tenant identifier (`TenantId`), current caller identity (`UserId` / `Roles`), and authorization (`HasPermission`). Consumers depending on any one of these concerns end up depending on all three.
- `Sunfish.Foundation.Assets.Common.TenantId` lives in an Assets-specific namespace even though it is a platform-level identifier used across every module.
- `accelerators/bridge/Sunfish.Bridge/Authorization/DemoTenantContext.cs` implements the overloaded interface and hardcodes a single tenant for local development.
- No third-party multi-tenant library is currently referenced. The ADR 0005 mandate — Finbuckle-compatible tenant resolution patterns inside the Bridge host, Sunfish-native abstractions everywhere else — has not been wired yet.

ADR 0006 established Bridge as a generic SaaS shell. That commitment requires a clean tenant model: a way to represent the current tenant, resolve it from request input, enumerate registered tenants, and mark persisted entities as tenant-scoped — independent of any specific hosting framework or vendor.

---

## Decision

Introduce a new package **`Sunfish.Foundation.MultiTenancy`** containing the minimum tenancy primitives Sunfish needs. The package is contracts-first; a small in-memory implementation ships alongside for tests, demos, and lite-mode deployments.

### Namespace & package layout

- New csproj: `packages/foundation-multitenancy/Sunfish.Foundation.MultiTenancy.csproj` (project-references `Sunfish.Foundation`).
- Root namespace: `Sunfish.Foundation.MultiTenancy`.
- Added to `Sunfish.slnx` under `/foundation/multitenancy/`.

### Public API

| Type | Purpose |
|---|---|
| `TenantStatus` | Enum: `Active`, `Suspended`, `Decommissioning`, `Archived`. |
| `TenantMetadata` (record) | Tenant identity + status + optional display name, locale, creation timestamp, free-form properties. Uses the existing `Sunfish.Foundation.Assets.Common.TenantId` as `Id`. |
| `ITenantContext` (interface) | Read-only surface exposing the resolved `TenantMetadata?` for the current scope, plus `IsResolved`. Consumed by anything that needs to know which tenant an operation is for. |
| `ITenantResolver` (interface) | `ValueTask<TenantMetadata?> ResolveAsync(string candidate, CancellationToken ct = default)`. Hosts decide where `candidate` comes from (host header, claim, route segment); the resolver only knows how to look it up. |
| `ITenantCatalog` (interface) | `GetAllAsync`, `TryGetAsync(TenantId)`. The authoritative list of tenants the host knows about. |
| `InMemoryTenantCatalog` (class) | Default impl of both `ITenantCatalog` and `ITenantResolver`; callers register `TenantMetadata` at startup. Thread-safe for reads after registration completes. |
| `ITenantScoped` (marker) | Entity marker: exposes `TenantId TenantId { get; }`. |
| `IMustHaveTenant` (marker) | Narrower marker: `ITenantScoped` where the tenant is required by persistence. |
| `IMayHaveTenant` (marker) | Opposite marker: entity may omit tenant (system-level / cross-tenant records). |
| `ServiceCollectionExtensions.AddSunfishTenantCatalog(…)` | DI sugar registering the in-memory catalog as a singleton for both `ITenantCatalog` and `ITenantResolver`. |

### Finbuckle boundary

The `Sunfish.Foundation.MultiTenancy` package does **not** reference `Finbuckle.MultiTenant` or any third-party tenancy library. Bridge is free to adopt Finbuckle inside the ASP.NET Core host for request-pipeline tenant resolution (host / subdomain / path / header strategies, per-tenant options, `app.UseMultiTenant()` middleware ordering). When it does, Bridge provides its own `ITenantContext` adapter that reads from Finbuckle's `IMultiTenantContext` and exposes a Sunfish `TenantMetadata`. No other Sunfish code sees Finbuckle.

This makes Finbuckle a **hosting implementation detail of the Bridge accelerator**, not a Sunfish dependency. Other hosts (lite-mode apps, self-hosted deployments, non-ASP.NET hosts, test harnesses) plug in their own `ITenantResolver` / `ITenantContext` implementations without pulling Finbuckle in.

### Relationship to existing types

- `Sunfish.Foundation.Authorization.ITenantContext` (the overloaded current interface) **remains in place and unchanged** under this ADR. It continues to serve Bridge's existing code. A follow-up ADR will decompose it into tenant / caller / authorization concerns once a concrete migration is ready.
- `Sunfish.Foundation.Assets.Common.TenantId` **stays where it is** for now. Moving it to `Sunfish.Foundation.MultiTenancy` is a breaking change tracked as a follow-up; the MultiTenancy package imports it via the existing namespace.
- New code in Sunfish modules, accelerators, and blocks should prefer the new `Sunfish.Foundation.MultiTenancy.ITenantContext` for tenant-only dependencies. Code that genuinely needs tenant + user + authorization together should hold three injected dependencies (`ITenantContext` + an identity contract + an authorization contract) rather than one overloaded interface.

### Async shape

`ITenantResolver` and `ITenantCatalog` return `ValueTask<T>` to stay hot-path-friendly for in-memory stores while still supporting I/O-backed implementations (database-backed catalog, external tenant API). The in-memory impl returns completed `ValueTask`s via `ValueTask.FromResult`.

---

## Consequences

### Positive

- Tenancy has a dedicated home, separate from authorization and assets.
- Bridge can adopt Finbuckle without Sunfish packages gaining a Finbuckle reference.
- New modules depend only on the tenancy concept they need (current tenant, resolution, catalog, or entity marker), not on a three-in-one interface.
- Lite-mode and self-hosted deployments can use `InMemoryTenantCatalog` without additional packages.
- Entity-level tenant scoping via `IMustHaveTenant` / `IMayHaveTenant` becomes a declarative contract the persistence layer can enforce.

### Negative

- Two `ITenantContext` interfaces now exist (`Foundation.Authorization` and `Foundation.MultiTenancy`). Callers must disambiguate with `using` directives; new code should prefer the MultiTenancy one.
- `TenantId` remains in `Foundation.Assets.Common` — namespace ugliness until a later ADR moves it.
- The namespace-level separation is policy-enforced, not compile-enforced. Reviewers must watch for code that tries to wire Finbuckle types into Sunfish module packages.

### Follow-ups

1. **Decompose `Foundation.Authorization.ITenantContext`** into tenant / caller / authorization contracts. Migrate Bridge to the new interfaces. Deprecate the old one. Separate ADR.
2. **Move `TenantId` to `Sunfish.Foundation.MultiTenancy`**. Breaking change; pair with the follow-up above.
3. **Bridge Finbuckle integration**. Wire Finbuckle in `Sunfish.Bridge` host startup behind a `FinbuckleTenantResolver : ITenantResolver` adapter. No Sunfish module package gains a Finbuckle reference.
4. **Database-backed `ITenantCatalog`** — a separate adapter package when Bridge's tenant registry moves from in-memory seed to Postgres.
5. **Per-tenant options / features** — wiring to `Sunfish.Foundation.FeatureManagement` (ADR 0009) so feature evaluation can receive an `ITenantContext` consistently.

---

## References

- ADR 0005 — Type-Customization Model.
- ADR 0006 — Bridge Is a Generic SaaS Shell.
- ADR 0007 — Bundle Manifest Schema.
- Finbuckle.MultiTenant — tenant resolution strategies, per-tenant options, ASP.NET middleware ordering. Referenced as the Bridge host's intended implementation, not a Sunfish-package dependency.
- `Sunfish.Foundation.Authorization.ITenantContext` — overloaded existing interface; untouched under this ADR.
- `Sunfish.Foundation.Assets.Common.TenantId` — existing value type imported by the new package.
