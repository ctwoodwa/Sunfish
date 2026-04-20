# Architecture Principles

**Status:** Accepted
**Last reviewed:** 2026-04-19
**Governs:** Every package, adapter, bundle, and accelerator in the Sunfish repo.
**Companion docs:** [roadmap-tracker.md](roadmap-tracker.md), [naming.md](naming.md), [compatibility-policy.md](compatibility-policy.md), [`docs/adrs/`](../../docs/adrs/).

This document is the canonical statement of Sunfish's architectural commitments. ADRs decide specific questions; this file summarizes the commitments those ADRs consolidate and adds structural rules that govern how the tiers relate. Where a principle is backed by an ADR, the ADR number is cited — read the ADR for the full rationale.

## The layered architecture

```
Accelerator (Bridge)                         ← hosts bundles, provides SaaS shell
       ↑ composes
Bundles (business-case manifests)            ← declarative JSON, not code
       ↑ declares activation of
Domain Modules (blocks-*)                    ← reusable bounded contexts
       ↑ depends on
UI Adapters (Blazor, React, …)               ← render UI-core contracts
       ↑ implements
UI Core                                      ← headless, render-agnostic contracts
       ↑ depends on
Foundation (Catalog, MultiTenancy,           ← contracts-first primitives
  FeatureManagement, LocalFirst,
  Integrations, …)
       ↑ depends on
Kernel (event bus, schema registry, …)       ← cross-cutting runtime substrate
```

Two orthogonal surfaces sit alongside:

- **Ingestion** — `ingestion-*` packages for data intake (forms, spreadsheets, voice, sensors, imagery, satellite). Feeds `blocks-dataexchange` and integration adapters.
- **Federation** — `federation-*` packages providing peer-to-peer sync with cryptographic capability delegation. One (advanced) implementation of local-first concerns; not required by lite or self-hosted modes.

## Dependency direction (strictly enforced)

Arrows point from consumer to provider. Violations should fail review.

| Allowed | Disallowed |
|---|---|
| Foundation ← UI Core | UI Core → Foundation.*adapter package* (e.g. Blazor-specific types in UI Core) |
| UI Core ← UI Adapters | UI Adapters → other UI Adapters (cross-adapter leakage) |
| Foundation ← Domain Modules | Foundation → Domain Modules |
| Domain Modules ← Bundles (via manifest keys only) | Bundles → code in other bundles |
| Bundles ← Accelerators | Domain Modules → Accelerators |
| Any tier ← Kernel | Kernel → any tier above foundation |

`compat-telerik` is an exception — a compatibility shim, not a first-party adapter. It mirrors a vendor surface and is policy-gated (ADR-driven when it changes).

## Core principles

### 1. Framework-agnostic core; adapter-specific rendering

UI Core owns headless contracts — state models, interaction semantics, accessibility contracts, slots and template abstractions. Adapters implement those contracts for a specific framework (Blazor today, React in P6). Blazor-specific, React-specific, or vendor-specific types do not appear in UI Core. Reviewers reject a UI-Core change that cannot be implemented in both adapters on principle. *(ADR 0014.)*

### 2. Bridge is a generic SaaS shell

Bridge hosts bundles. Bridge does not implement domain. If a change in Bridge names a business concept (Lease, Unit, Invoice, WorkOrder), it belongs in a `blocks-*` module, not in Bridge. Bridge's responsibilities are tenant lifecycle, subscription and edition enforcement, bundle activation, per-tenant feature management, admin backoffice, integration configuration, and observability. *(ADR 0006.)*

### 3. Bundles are data, not code

A business-case bundle is a `BusinessCaseBundleManifest` — JSON shipped as seed data. It names required and optional module keys, feature defaults, edition mappings, deployment modes, provider requirements, personas, and compliance notes. Tenants subscribe to bundles; at runtime Bridge's provisioning service reads the catalog and activates modules and features accordingly. No bundle owns its own code. *(ADR 0007.)*

### 4. Four-layer type-customization model

Customization is bounded by layer. Platform invariants are hard-typed and changed only by ADR. Canonical business entities are typed with a registered extension bag (`IHasExtensionData` + `IExtensionFieldCatalog`). User-authored artifacts (forms, diligence checklists, reports, notifications, document templates) are metadata — JSON Schema 2020-12 for data plus a separate UI schema — with tenant overlays applied via RFC 7396 merge patch. Ad-hoc custom objects exist as a gated escape hatch. *(ADR 0005.)*

### 5. Catalog-required for customization

Extension fields, feature specs, and template definitions must be registered in a catalog before persistence or rendering. Silent schema drift is prevented because no module can bypass the catalog to write or render novel shapes. The same rule scales across domains: `IExtensionFieldCatalog` (ADR 0005), `IFeatureCatalog` (ADR 0009), `IBundleCatalog` (ADR 0007). *(ADR 0005, ADR 0009.)*

### 6. Provider neutrality by contract

Every external vendor sits behind a Sunfish contract. Domain modules never reference vendor SDKs directly — `blocks-billing` does not `using Stripe`. Provider adapter packages (`Sunfish.Providers.*`) wrap vendor SDKs and implement `Foundation.Integrations` contracts. Domain entities are Sunfish-modeled, not vendor-mirrored — a `Payment` in `blocks-billing` is not a Stripe `Charge`. *(ADR 0013.)*

### 7. Multi-tenancy is a dedicated concern

Tenant identity lives in `Foundation.MultiTenancy`. User identity and authorization are separate abstractions. Interfaces that conflate them (the legacy `Foundation.Authorization.ITenantContext`) are tolerated for compatibility but not extended. Finbuckle.MultiTenant is a Bridge-host implementation detail; Sunfish module packages never reference Finbuckle. *(ADR 0008.)*

### 8. Feature decomposition (flags vs. entitlements vs. editions)

Four distinct concepts:

- **Technical flags** — runtime on/off or variant, often for rollouts.
- **Product features** — named capabilities (`blocks.leases.renewals.autoReminders`).
- **Entitlements** — what a tenant may use based on bundle + edition + add-ons.
- **Editions** — named tiers bundling a set of capabilities.

One evaluator composes them in a strict resolution order: provider override → entitlement resolver → catalog default → error. The provider seam is OpenFeature-style; any OpenFeature-compatible backend plugs in through `IFeatureProvider`. *(ADR 0009.)*

### 9. Local-first is a product strategy

Three deployment modes ship from one codebase: lite (local-first), self-hosted, and hosted SaaS. `Foundation.LocalFirst` contracts — offline store, outbound queue, sync engine, conflict resolver — are contracts-first; modules choose implementations appropriate to their mode. Federation is one possible sync implementation, not a prerequisite. Tenant-owned data export and import are first-class platform primitives, not bolt-ons. *(ADR 0012.)*

### 10. Bundle versioning is semver with tenant-safe upgrades

Manifest authors bump patch/minor/major against a normative table (ADR 0011). Patch and minor auto-apply; major requires explicit tenant opt-in. Template overlays survive base upgrades via three-way merge. Deprecated bundles sit in Deprecated status for at least 180 days before archival. Rollback is forward-only for majors. *(ADR 0011.)*

### 11. Adapter parity is default; exceptions are registered

Every new component or UI contract lands in every first-party adapter in the same pull request, or registers an explicit, time-boxed exception in [`_shared/engineering/adapter-parity.md`](../engineering/adapter-parity.md). Single-adapter components require their own ADR. *(ADR 0014.)*

### 12. Templates stay in Foundation.Catalog until they grow behavior

Form, diligence-checklist, report, notification, and document templates are pure data today — they live in `Foundation.Catalog.Templates`. Extraction to `blocks-templating` is triggered by specific criteria (three or more kinds with materially different runtime behavior; authoring UIs; persistence beyond embedded seed). *(ADR 0010.)*

## Structural rules beyond the ADRs

### ICM — Interface → Contract → Module

Every new capability flows in this order: interface defined at the appropriate layer, contracts (DTOs, manifests, events, options) defined next, implementation in a concrete module last. Starting with a framework-specific implementation is rejected. Contract-first also means: no code depends on an implementation class when an interface exists.

### Early-stage discipline (pre-1.0)

Sunfish is pre-1.0. Prefer stable contracts, minimal but extensible primitives, ADR-backed decisions, package scaffolds with representative implementations. Reject "final framework" rewrites that speculate about hypothetical future needs. Three similar lines is better than a premature abstraction.

### Generic domain before provider

Native Sunfish domain models are the default. Provider adapters translate to and from them; they do not dictate their shape. When a generic seam exists (generic reservations, shipments, payments, communications), providers plug in through adapter packages.

### Tests ship with each package

Every `packages/*` code package has a sibling `tests/` folder with a `tests.csproj`. Test projects use xUnit with NSubstitute for mocks, `bunit` for Blazor component tests, and `Testcontainers.PostgreSql` for integration tests needing a real database. `GenerateDocumentationFile` is off for test projects.

### XML documentation is required on public API

`Directory.Build.props` sets `GenerateDocumentationFile=true` and `TreatWarningsAsErrors=true`. Every public type and member gets at least a one-line XML summary. CS1591 is suppressed only at the project level for legacy packages undergoing migration — new packages do not suppress it.

### Contracts over implementations in registration

DI registers interfaces, not concrete classes, wherever a seam exists. `AddSunfishTenantCatalog` registers `ITenantCatalog` via an in-memory default; callers replace the implementation without changing their consuming code.

## Anti-patterns (reject in review)

- **Vertical-specific code in Bridge.** If a new entity belongs to Property Management, it belongs in a `blocks-*` module.
- **Blazor- or React-specific types in UI Core.** UI Core is headless. Adapters render.
- **Vendor SDK imports in domain modules.** Every vendor goes through a `Sunfish.Providers.*` adapter.
- **Collapsing distinct finance concerns.** Invoicing, billing, accounting, and reconciliation are separate modules.
- **Merging scheduling and reservations.** Scheduling is calendars and capacity; reservations are bookable resources with policies. Different modules, different domains.
- **EAV or pure JSON-bag-without-catalog.** Extension fields must be registered (ADR 0005). A tenant-specific JSON blob on an entity without a catalog entry is rejected.
- **Non-JSON-Schema template dialects** (Form.io, …). JSON Schema 2020-12 + separate UI schema is the contract.
- **Feature flags used as entitlements.** Flags and entitlements are different (ADR 0009). Mixing them masks revenue state.
- **Giant "shared" or "core" dumping-ground modules.** If a concept is cross-cutting, it gets its own Foundation package (MultiTenancy, FeatureManagement, LocalFirst, Integrations).
- **Changes to ui-core without adapter updates.** Either update every adapter or register a parity exception (ADR 0014).
- **Bypassing the catalog.** Any mechanism that writes schema-shaped data around Foundation.Catalog or Foundation.FeatureManagement violates principle #5 and is rejected.

## Principles → ADRs map

| # | Principle | ADR |
|---|---|---|
| 1 | Framework-agnostic core | 0014 |
| 2 | Bridge is a SaaS shell | 0006 |
| 3 | Bundles are data | 0007 |
| 4 | Four-layer type-customization | 0005 |
| 5 | Catalog-required | 0005, 0009 |
| 6 | Provider neutrality | 0013 |
| 7 | Multi-tenancy dedicated | 0008 |
| 8 | Feature decomposition | 0009 |
| 9 | Local-first product strategy | 0012 |
| 10 | Bundle versioning semver | 0011 |
| 11 | Adapter parity | 0014 |
| 12 | Templates stay in Catalog | 0010 |

## When to revise this document

- A new ADR that establishes a cross-cutting principle: add a principle entry and cross-reference.
- A structural rule that repeatedly surfaces in review (e.g. a new anti-pattern): add to the anti-patterns list.
- A principle that conflicts with a newer ADR: open a superseding ADR; don't silently edit the principle.

This document is descriptive (synthesizes decisions) and prescriptive (reviewers cite it). Keep it in sync with ADRs — a drift between this file and the ADRs is a bug.
