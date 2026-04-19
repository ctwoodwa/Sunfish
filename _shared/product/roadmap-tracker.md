# Sunfish Planning-Phase Roadmap Tracker

**Status:** Living document
**Last reviewed:** 2026-04-19
**Purpose:** Consolidated status of planning-phase outputs (ADRs, Foundation packages, bundle manifests) and the roadmap through P6. Update every time an ADR merges, a package ships, or a phase gate closes.

---

## Overview

The planning phase produced ADRs 0005–0014, 7 Foundation-tier packages, 5 reference bundle manifests + a meta-schema, 1 audit doc, and 2 platform-spec reconciliation preambles. Every artifact is cross-referenced below. Net cost: ~10 focused planning iterations with build+test verification between each.

---

## ADRs shipped

| # | Title | Status | Artifact location |
|---|---|---|---|
| 0005 | Type-Customization Model (Typed vs. Dynamic Balance) | Accepted | [docs/adrs/0005-type-customization-model.md](../../docs/adrs/0005-type-customization-model.md) |
| 0006 | Bridge Is a Generic SaaS Shell, Not a Vertical App | Accepted | [docs/adrs/0006-bridge-is-saas-shell.md](../../docs/adrs/0006-bridge-is-saas-shell.md) |
| 0007 | Bundle Manifest Schema | Accepted | [docs/adrs/0007-bundle-manifest-schema.md](../../docs/adrs/0007-bundle-manifest-schema.md) |
| 0008 | Foundation.MultiTenancy Contracts + Finbuckle Boundary | Accepted | [docs/adrs/0008-foundation-multitenancy.md](../../docs/adrs/0008-foundation-multitenancy.md) |
| 0009 | Foundation.FeatureManagement (Flags vs. Entitlements vs. Editions) | Accepted | [docs/adrs/0009-foundation-featuremanagement.md](../../docs/adrs/0009-foundation-featuremanagement.md) |
| 0010 | Templates Module Boundary (Foundation.Catalog vs. blocks-templating) | Accepted | [docs/adrs/0010-templates-boundary.md](../../docs/adrs/0010-templates-boundary.md) |
| 0011 | Bundle Versioning and Upgrade Policy | Accepted | [docs/adrs/0011-bundle-versioning-upgrade-policy.md](../../docs/adrs/0011-bundle-versioning-upgrade-policy.md) |
| 0012 | Foundation.LocalFirst Contracts + Federation Relationship | Accepted | [docs/adrs/0012-foundation-localfirst.md](../../docs/adrs/0012-foundation-localfirst.md) |
| 0013 | Foundation.Integrations + Provider-Neutrality Policy | Accepted | [docs/adrs/0013-foundation-integrations.md](../../docs/adrs/0013-foundation-integrations.md) |
| 0014 | UI Adapter Parity Policy (Blazor ↔ React) | Accepted | [docs/adrs/0014-adapter-parity-policy.md](../../docs/adrs/0014-adapter-parity-policy.md) |
| **0015** | **Module-entity registration into Bridge DbContext** | **Pending** | Surfaced by Bridge.Data audit; needed before any Bridge.Data entity moves |

---

## Foundation packages created

All in `/packages/` and registered in `Sunfish.slnx`. Contracts-first with in-memory reference implementations. Each has its own test project and green-build verification.

| Package | Purpose | Test count |
|---|---|---|
| `packages/foundation-catalog` (extended P0) | Extension-field catalog, template definitions + overlays + merger, bundle manifest types, bundle catalog, manifest loader, meta-schema | 63 |
| `packages/foundation-multitenancy` (ADR 0008) | `TenantMetadata`, `ITenantContext`, `ITenantResolver`, `ITenantCatalog`, `ITenantScoped`/`IMustHaveTenant`/`IMayHaveTenant`, `InMemoryTenantCatalog` | 10 |
| `packages/foundation-featuremanagement` (ADR 0009) | `FeatureKey`/`FeatureValue`/`FeatureSpec`, `FeatureEvaluationContext`, `IFeatureCatalog`, `IFeatureProvider`, `IEntitlementResolver`, `IEditionResolver`, `DefaultFeatureEvaluator` (provider → entitlements → default) | 13 |
| `packages/foundation-localfirst` (ADR 0012) | `IOfflineStore`, `IOfflineQueue` + `OfflineOperation`, `ISyncEngine` + `SyncResult`/`SyncEvent`, `ISyncConflictResolver` + `LastWriterWinsConflictResolver`, `IDataExportService`, `IDataImportService` | 11 |
| `packages/foundation-integrations` (ADR 0013) | `ProviderDescriptor`, `IProviderRegistry`, `CredentialsReference`, `WebhookEventEnvelope` + handler + dispatcher, `SyncCursor` + store, `ProviderHealthStatus` + `IProviderHealthCheck` | 10 |

Also extended in P0 (pre-planning-phase):

| Package | Additions |
|---|---|
| `packages/foundation` | `Sunfish.Foundation.Extensibility` — `ExtensionFieldKey`, `IHasExtensionData`, `ExtensionDataBag` (+ 7 tests) |

**Total new planning-phase tests:** ≥107, all green.

---

## Bundle catalog

Five reference manifests embedded in `Sunfish.Foundation.Catalog` under `Bundles/` logical-name prefix:

| Bundle | Key | Category | Modes supported | Status |
|---|---|---|---|---|
| Property Management | `sunfish.bundles.property-management` | Operations | Lite, SelfHosted, HostedSaaS | Draft |
| Asset Management | `sunfish.bundles.asset-management` | Operations | Lite, SelfHosted, HostedSaaS | Draft |
| Project Management | `sunfish.bundles.project-management` | Operations | Lite, SelfHosted, HostedSaaS | Draft |
| Facility Operations | `sunfish.bundles.facility-operations` | Operations | Lite, SelfHosted, HostedSaaS | Draft |
| Acquisition / Underwriting | `sunfish.bundles.acquisition-underwriting` | Diligence | SelfHosted, HostedSaaS (Lite intentionally unsupported) | Draft |

Meta-schema: [`Schemas/bundle-manifest.schema.json`](../../packages/foundation-catalog/Schemas/bundle-manifest.schema.json). All 5 bundles pass meta-schema validation.

---

## Operational doc outputs

| Doc | Purpose |
|---|---|
| [`_shared/engineering/adapter-parity.md`](../engineering/adapter-parity.md) | Parity matrix + exception register (ADR 0014) |
| [`_shared/engineering/bridge-data-audit.md`](../engineering/bridge-data-audit.md) | Bridge.Data inventory + move plan (8 of 9 entities are vertical-leakage) |
| Platform spec §4.7 + §6 reconciliation preambles | Reframes PM-vertical language as PM-bundle, technical content unchanged |
| Root `README.md`, `accelerators/bridge/README.md`, `accelerators/bridge/PLATFORM_ALIGNMENT.md` | Corrected per ADR 0006 |

---

## Phase status

### P0 — Customization plumbing

**Complete.** Foundation.Catalog + extension primitives shipped; reference template end-to-end test exercises base + overlay + merger.

### P1 — Tenancy, features, bundles, Bridge shell

**Ready to start.** Every blocker is resolved:

- ADR 0006 + README corrections landed (platform identity)
- ADR 0007 + meta-schema + 5 reference bundles (bundle shape and seeds)
- ADR 0008 + Foundation.MultiTenancy (tenancy contracts)
- ADR 0009 + Foundation.FeatureManagement (entitlement + flag seam)

**Outstanding before P1 ships:**

- **`blocks-subscriptions`** (new domain module) — plans, editions, subscriptions, bundle add-ons, usage meters
- **`blocks-tenant-admin`** (new domain module) — provisioning, user/role basics, bundle activation UI
- **`blocks-businesscases`** (new domain module) — resolves `tenant → bundle(s) → modules + features`; the bundle-manifest-backed `IEntitlementResolver`
- **Bridge shell evolution** — generic tenant shell + bundle provisioning service consuming `IBundleCatalog` + `IFeatureEvaluator`
- **ADR 0015** — module-entity registration pattern (blocks from Bridge.Data audit)

### P2 — Missing generic domain modules (Tier 1)

**Blocked by P1 + ADR 0015.** 11 modules queued: `blocks-contacts`, `blocks-crm`, `blocks-communications`, `blocks-documents`, `blocks-diligence`, `blocks-invoicing`, `blocks-billing`, `blocks-reconciliation`, `blocks-reporting` (contracts), `blocks-searchworkspace`, `blocks-dataexchange`, plus `blocks-projects` (first target for Bridge.Data move per audit doc).

Recommended tactic: contracts + thin runtime for all 11, deep runtime only for modules the PM bundle activates first.

### P3 — Bundle composition + Bridge bundle store

**Blocked by P2.** 5 bundles + Tier-2 modules as each bundle demands (`blocks-projects`, `blocks-vendors`, `blocks-procurement`, `blocks-locations`, `blocks-units`, `blocks-reservations`, `blocks-support`, `blocks-identityaccess`, `blocks-bankpayments`, `blocks-adminbackoffice`).

### P4 — Local-first + deployment modes

**Contracts ready** via ADR 0012 + Foundation.LocalFirst. **Implementation blocked** by need for SQLite-backed `IOfflineStore` adapter, module-level export/import contributor contracts, and a reference lite-mode app.

### P5 — Integration adapters

**Contracts ready** via ADR 0013 + Foundation.Integrations. **Implementation blocked** by need for first concrete provider adapter (likely Stripe or a payment-gateway reference) and a secrets-management contract ADR.

### P6 — React adapter + CLI + docs site

**Parity policy defined** (ADR 0014). **Implementation blocked** by need for a dedicated ADR confirming React scaffolding choices (reconciler, CSS strategy, state primitives) before component work begins.

---

## Open decisions / pending ADRs

| Item | Owner | Blocks |
|---|---|---|
| **ADR 0015** — Module-entity registration pattern (DbContext per block vs. shared Bridge DbContext) | Platform team | Bridge.Data moves, all P2 modules, P1 Bridge shell |
| Secrets-management contract (how `CredentialsReference` resolves) | Platform team | First credential-bearing integration in P5 |
| External entity mapping contract (`IExternalMapping`) | Platform team | First pull integration in P5 |
| Blazor form renderer choice (native vs. `compat-telerik`) | UI track | `blocks-diligence` demo in P2 |
| Three-way merge tooling for template overlays | Foundation.Catalog maintainer | First major bundle version bump in P3 |
| React scaffolding (reconciler, CSS, state) | UI track | P6 start |
| Templates extraction criteria — recheck after 3rd template kind lands | Foundation.Catalog maintainer | Criteria per ADR 0010 |
| Platform spec full phrasing pass | Doc author | After P1 bundle provisioning service lands |

---

## Risks surfaced during planning

1. **P2 scope.** 11 new domain modules is the longest stretch. Build thin-but-complete for all; deep runtime only for modules PM bundle requires first.
2. **Bridge.Data vertical-leakage.** 8 of 9 current entities are project-management domain. Moves deferred to P2 when target blocks exist; meanwhile freeze new Entity additions in `Bridge.Data.Entities`.
3. **Two `ITenantContext` interfaces.** Old (`Foundation.Authorization`) + new (`Foundation.MultiTenancy`) coexist. Migration tracked as ADR 0008 follow-up.
4. **TenantId namespace.** Still in `Foundation.Assets.Common`; move to `Foundation.MultiTenancy` deferred to avoid cross-repo breakage.
5. **Federation retrofit.** Unknown effort until P4 discovery spike; federation packages preserved unchanged until then.
6. **React adapter slippage.** Each month of slippage decays the "framework-agnostic" claim. Consider starting minimal React adapter in parallel with P3 if capacity allows.

---

## How to keep this document current

- After every ADR merge: add its row; link the doc; update any phase it unblocks.
- After every Foundation-tier package ships: add its row with test count; cross-link.
- After every bundle manifest lands: add its row; note bundle status.
- After every audit or recon doc: add its row under operational doc outputs.
- After every phase gate opens: update phase status; strike "blocked by" conditions that have resolved.

This tracker is the single entry point for "what is the state of the Sunfish platform planning work?" Keep it honest.

---

## References

- [docs/adrs/README.md](../../docs/adrs/README.md) — ADR index
- [Sunfish.slnx](../../Sunfish.slnx) — solution file (authoritative package list)
- [packages/foundation-catalog/Manifests/Bundles/](../../packages/foundation-catalog/Manifests/Bundles/) — bundle seeds
- [packages/foundation-catalog/Schemas/bundle-manifest.schema.json](../../packages/foundation-catalog/Schemas/bundle-manifest.schema.json) — bundle meta-schema
- [_shared/engineering/bridge-data-audit.md](../engineering/bridge-data-audit.md) — Bridge.Data move plan
- [_shared/engineering/adapter-parity.md](../engineering/adapter-parity.md) — UI adapter parity register
