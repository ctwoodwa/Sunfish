# Intake Note — P1 Domain Blocks (Subscriptions, Tenant-Admin, Business-Cases)

**Date:** 2026-04-20
**Requestor:** Christopher Wood (BDFL)
**Request:** Scaffold the three P1 domain blocks that close the "tenant → bundle(s) → modules + features" resolution pipeline, then wire them into Bridge's shell. This is the work that turns Foundation.Catalog + Foundation.FeatureManagement from contracts into a running bundle-provisioning flow.

## Problem Statement

Sunfish has the bundle/feature **contracts** ([ADR 0007](../../../docs/adrs/0007-bundle-manifest-schema.md), [ADR 0009](../../../docs/adrs/0009-foundation-featuremanagement.md)) and the **manifests** (5 bundle JSONs seeded in `packages/foundation-catalog/Manifests/Bundles/`) but no runtime that **resolves a tenant's selected bundle into active modules and feature values**. Bridge today wires `.AddFeatureManagement()` but has no `IBundleCatalog` registration, no `IEntitlementResolver` implementation, and no tenant-admin UI for bundle activation. The result: Bundle manifests are dead JSON; the feature-evaluation chain has no entitlement leg; and Bridge's multi-tenant shell is stuck on a single hardcoded demo tenant.

Three blocks close the gap:

1. **`blocks-subscriptions`** — owns plan/edition/subscription/usage-meter data. Declares which editions a tenant has purchased (lite/standard/enterprise). Feeds edition data into the entitlement resolver.
2. **`blocks-tenant-admin`** — owns tenant-profile, user/role basics, and the **Bundle Activation UI** that lets an operator select which bundles a tenant runs. Consumes `IBundleCatalog`.
3. **`blocks-businesscases`** — the **resolver block**. Implements `IEntitlementResolver` by mapping `(tenant) → (bundle selections + edition) → (active modules + feature values)` using the manifest's `EditionMappings` and `FeatureDefaults`. This block is the glue — it's what makes the bundle manifests executable rather than declarative.

Three signals make now the right moment:

1. **ADR 0015 just landed** ([ADR 0015](../../../docs/adrs/0015-module-entity-registration.md)) — the module-entity registration pattern (shared `SunfishBridgeDbContext` with `ISunfishEntityModule` seam) is the prerequisite for any block to own persisted entities. P1 blocks are the first to use it.
2. **Bridge.Data is still PM-leaky.** The audit ([bridge-data-audit.md](../../../_shared/engineering/bridge-data-audit.md)) needs P1 blocks to ship their own entities before it can credibly move the 8 vertical PM entities out of Bridge.Data.
3. **The Web Components migration (ADR 0017) cannot realistically start** until we know the shell actually runs a multi-tenant bundle-aware flow. P1 blocks make that flow real.

## Affected Sunfish Areas

Impact markers are approximate — Stage 01 Discovery will refine.

| Area | Impact | Note |
|---|---|---|
| `packages/blocks-subscriptions` | **new** | Brand new block. Plans, editions, subscriptions, add-ons, usage meters. Ships its `ISunfishEntityModule` per ADR 0015. |
| `packages/blocks-tenant-admin` | **new** | Brand new block. Tenant profile, users/roles basics, bundle-activation UI. Consumes `IBundleCatalog`. |
| `packages/blocks-businesscases` | **new** | Brand new block. Implements `IEntitlementResolver`. The bundle-provisioning service lives here too. |
| `packages/foundation` | **affected** | Adds `ISunfishEntityModule` contract per ADR 0015. Small, ~30 LOC. |
| `packages/foundation-multitenancy` | **affected** | Adds `ApplyTenantQueryFilters(ITenantContext)` extension per ADR 0015. |
| `accelerators/bridge/Sunfish.Bridge.Data` | **affected** | `SunfishBridgeDbContext` consumes `IEnumerable<ISunfishEntityModule>` and composes module configurations. |
| `accelerators/bridge/Sunfish.Bridge` | **affected** | Program.cs wires `.AddSubscriptions()`, `.AddTenantAdmin()`, `.AddBusinessCases()`, registers `IBundleCatalog`, wires the bundle-provisioning service, and exposes the tenant-shell routes. |
| `accelerators/bridge/Sunfish.Bridge.Client` | **possible** | Tenant-admin UI lands here (Blazor host); tenant-shell Blazor layouts update. |
| `packages/foundation-catalog` | **possible** | If manifests need new fields the P1 blocks require (usage-meter definitions, addon shapes), the manifest contract grows. Stage 01 Discovery validates. |
| `apps/kitchen-sink` | **affected** | Needs a demo page showing the bundle-activation flow end-to-end. |
| `tooling/scaffolding-cli` | **possible** | `dotnet sunfish bundle uninstall <key>` subcommand per ADR 0015 Consequences. Can land with P1 or in a follow-up. |
| `Sunfish.slnx` | **affected** | Three new project entries + three test project entries under `/blocks/` folder. |
| `Directory.Packages.props` | **possible** | No new packages anticipated — all dependencies already centralized (Microsoft.EntityFrameworkCore, xunit, bunit are in place). |

## Selected Pipeline Variant

- [x] **`sunfish-feature-change`** — three new features (blocks) + Bridge-shell integration. Default variant.

Not a `sunfish-api-change` — no existing public contracts break. `IBundleCatalog`, `IFeatureEvaluator`, `IEntitlementResolver` gain implementations; no existing signatures change.

Not a `sunfish-scaffolding` — the `dotnet sunfish bundle uninstall` subcommand is a follow-up, not a prerequisite.

## Dependencies and Constraints

### Dependencies (inbound)

- [ADR 0015 — Module-Entity Registration](../../../docs/adrs/0015-module-entity-registration.md) — **Accepted today (2026-04-20)**. Defines `ISunfishEntityModule` and the composition seam. Must ship before blocks-subscriptions/tenant-admin/businesscases can carry persisted entities.
- [ADR 0007 — Bundle Manifest Schema](../../../docs/adrs/0007-bundle-manifest-schema.md) — `BusinessCaseBundleManifest` record (`packages/foundation-catalog/Bundles/BusinessCaseBundleManifest.cs`). `blocks-businesscases` consumes `EditionMappings`, `FeatureDefaults`, `RequiredModules`, `OptionalModules`.
- [ADR 0008 — Foundation.MultiTenancy](../../../docs/adrs/0008-foundation-multitenancy.md) — `ITenantContext`, `TenantMetadata`. All three blocks consume.
- [ADR 0009 — Foundation.FeatureManagement](../../../docs/adrs/0009-foundation-featuremanagement.md) — `IFeatureEvaluator`, `IEntitlementResolver`, `FeatureKey`, `FeatureValue`, `FeatureEvaluationContext`. `blocks-businesscases` implements `IEntitlementResolver`.
- [bridge-data-audit.md](../../../_shared/engineering/bridge-data-audit.md) — Recommendation 1 (land ADR 0015) is now resolved; recommendation 3 (freeze new Bridge.Data.Entities additions) is enforced for this work.

### Dependencies (outbound — this work authors)

- **Scaffolding CLI subcommand** (follow-up) — `dotnet sunfish bundle uninstall <key>` emits cleanup SQL from a block's last-known entity configuration (per ADR 0015 Consequences). Ships with P1 if there's bandwidth; otherwise a follow-up task file in `docs/superpowers/plans/`.
- **Parity tests (future)** — bundle-provisioning correctness is a shell-level integration test, not an adapter-parity test. Adapter parity applies when `blocks-tenant-admin` ships the React adapter alongside Blazor (tracked under ADR 0014, not this intake).

### Constraints

- **No breaking changes to `IBundleCatalog`, `IFeatureEvaluator`, `IEntitlementResolver`.** These are Foundation contracts. `blocks-businesscases` implements the resolver; the contract stays as-is.
- **`SunfishBridgeDbContext` schema changes require migrations.** Each new block that registers entities must ship an EF Core migration. This is the first wave — expect 3 migrations (one per block).
- **Tenant isolation is non-negotiable.** Every persisted entity must be `IMustHaveTenant` or explicitly documented as cross-tenant (the bundle catalog itself is cross-tenant; everything else is tenant-scoped).
- **Bridge.Data.Entities freeze.** Per bridge-data-audit recommendation 3, no new entities go into Bridge.Data.Entities. P1 entities live in their respective blocks.
- **Central Package Management.** All package versions in `Directory.Packages.props`; no `Version=` in `<PackageReference>` elements.
- **Target framework.** `net11.0` per existing block convention (matches blocks-leases, blocks-tasks, etc.).
- **Flat apps/accelerators namespaces** ([ADR 0016](../../../docs/adrs/0016-app-and-accelerator-naming.md)) — `Sunfish.Bridge.*`. Packages use tier-prefixed namespaces: `Sunfish.Blocks.Subscriptions`, `Sunfish.Blocks.TenantAdmin`, `Sunfish.Blocks.BusinessCases`.
- **Pre-release private repo posture.** No external-contributor friction. Public release gated on LLC formation (memory: `project_sunfish_private_until_llc.md`).

### Sequencing

- **Tier 1 (prerequisite, completed 2026-04-20):** ADR 0015 accepted.
- **Tier 2 (parallel):** Scaffold the three blocks concurrently. No inter-block dependencies at contract level — each block only depends on Foundation contracts.
- **Tier 3 (sequential):** Wire Bridge shell — registration, `IBundleCatalog` seed loading, bundle-provisioning service, tenant-admin UI routing.
- **Tier 4 (verification):** Solution build green, tests green, kitchen-sink demo renders a bundle-activation flow.
- **Tier 5 (commit + push):** Atomic commit of the P1 bundle.

### Kill Criteria (UPF anti-pattern #11: zombie projects)

P1 cancellable if any of these fire:

1. **`IEntitlementResolver` contract cannot cleanly express edition-driven feature values** — if Stage 02 Architecture finds the contract needs breaking changes to express bundle entitlements, pause and open a `sunfish-api-change` intake before continuing.
2. **`ISunfishEntityModule` composition produces schema collisions across blocks** — if two P1 blocks both need `tenants` or similar shared table names and the prefix rule can't resolve it, pause and revisit ADR 0015.
3. **Bundle-activation UI requires UI primitives Sunfish does not yet have** — if `blocks-tenant-admin` can't render its Bundle-Activation UI with existing `Sunfish.UIAdapters.Blazor` primitives, pause the UI work and scope the missing primitives separately (not P1 blocker).

Absent those conditions, P1 runs to completion and then unblocks:
- Bridge.Data M1–M5 moves (audit.md move plan)
- ADR 0017 Web Components migration (which needs a credible multi-tenant shell to migrate)

## Next Steps

Proceed to **Stage 02 Architecture** (skipping Stage 01 Discovery — the architecture is sufficiently understood from ADR 0015 + the audit + the existing Foundation contracts; Discovery scope is absorbed by this intake). Stage 02 produces:

1. **Per-block design sketches** — for each of `blocks-subscriptions`, `blocks-tenant-admin`, `blocks-businesscases`:
   - Entity shapes + table names (module-prefixed per ADR 0015)
   - Service contracts (`I<Block>Service`)
   - In-memory reference implementation
   - DI registration shape
   - Integration with Foundation contracts (`ITenantContext`, `IBundleCatalog`, `IFeatureEvaluator`, `IEntitlementResolver`)
2. **Bridge-shell integration sketch** — Program.cs wiring order, bundle-provisioning service shape, tenant-admin routing, kitchen-sink demo page.
3. **Migration sequence plan** — one EF migration per block; do we generate via `dotnet ef migrations add` or hand-author?

**Expected Stage 02 output:** `02_architecture/output/p1-blocks-architecture-2026-04-20.md` (or landing date).

**Then Stage 06 Build** proceeds directly. Stage 03 (Package Design) is absorbed into Stage 02 since each block's design is tight; Stage 04 (Scaffolding) is not needed (no generator changes required to scaffold three blocks — they follow the blocks-leases template).

## Cross-References

- [ADR 0015 — Module-Entity Registration](../../../docs/adrs/0015-module-entity-registration.md) — The persistence pattern P1 blocks use.
- [ADR 0007 — Bundle Manifest Schema](../../../docs/adrs/0007-bundle-manifest-schema.md) — What `blocks-businesscases` resolves.
- [ADR 0009 — Foundation.FeatureManagement](../../../docs/adrs/0009-foundation-featuremanagement.md) — What `blocks-businesscases` extends via `IEntitlementResolver`.
- [bridge-data-audit.md](../../../_shared/engineering/bridge-data-audit.md) — The audit unblocked by this work.
- [`_shared/engineering/package-conventions.md`](../../../_shared/engineering/package-conventions.md) — Block packaging conventions.
- [`icm/pipelines/sunfish-feature-change/routing.md`](../../pipelines/sunfish-feature-change/routing.md) — Stage routing used here.
