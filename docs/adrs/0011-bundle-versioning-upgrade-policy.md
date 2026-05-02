---
id: 11
title: Bundle Versioning and Upgrade Policy
status: Accepted
date: 2026-04-19
tier: foundation
concern:
  - version-management
  - commercial
  - operations
composes: []
extends: []
supersedes: []
superseded_by: null
amendments: []
---
# ADR 0011 — Bundle Versioning and Upgrade Policy

**Status:** Accepted
**Date:** 2026-04-19
**Resolves:** Formalize the versioning rules sketched in ADR 0007 and define the upgrade workflow Bridge surfaces to tenants when a bundle version advances.

---

## Context

ADR 0007 introduced the bundle manifest schema and declared a semver-based upgrade model at a high level. That sketch is enough to write manifests but not enough to ship the P3 bundle-store UX or the runtime provisioning service. Three concrete questions remain:

1. **What precisely is patch vs. minor vs. major** for a bundle manifest? Authors need a deterministic rule when they bump `version`.
2. **What does a tenant experience during an upgrade** — silent, opt-in, opt-out, forced? The answer differs by bump kind and by whether tenant customization is at risk.
3. **What happens to tenant-authored overlays** (template overlays, extension-field values, feature overrides) when the base bundle moves forward?

Without a codified policy, every bundle author invents their own. Auto-upgrades break tenants; explicit-opt-in upgrades stall across a customer base.

---

## Decision

### Semver rules for bundle manifests

A manifest's `version` follows strict semver. The following table is normative.

| Change | Bump |
|---|---|
| Documentation / prose changes (description, maturity, complianceNotes) | Patch |
| Adding a persona, seed workspace, or integration profile | Patch |
| Changing a feature default value | Patch **if** the change is conservative (safer default). Minor if permissive. |
| Adding an optional module | Minor |
| Adding an entry to `editionMappings` | Minor |
| Adding a `ProviderRequirement` with `required: false` | Minor |
| Adding a `ProviderRequirement` with `required: true` | Major |
| Removing an entry from `editionMappings` | Major |
| Removing or renaming a required or optional module | Major |
| Flipping a `ProviderRequirement.required` from false to true | Major |
| Removing a declared feature default that tenants rely on | Major |
| Changing `deploymentModesSupported` (removing a mode) | Major |
| Changing `category`, `key` | Major (treat as a new bundle; don't do this) |

Authors pick the highest bump any change in the diff requires.

### Tenant upgrade behavior by bump kind

| Bump | Tenant experience | Admin action | Template overlays |
|---|---|---|---|
| Patch | Silent auto-upgrade on provisioning refresh | None | Preserved verbatim |
| Minor | Auto-upgrade; newly-optional modules inactive until tenant opts in | Tenant admin sees "new in vX.Y.Z" notice; no mandatory action | Preserved verbatim; new fields/layouts appear only for tenants who activate the new optional module |
| Major | **Explicit opt-in** per tenant. Bundle store surfaces a migration preview | Tenant admin reviews migration report, accepts or postpones | Three-way merge proposed; conflicts flagged; admin approves resolution before overlay applies |

Major upgrades never auto-apply. The previous version remains loadable for tenants who have not migrated, up to the **deprecation deadline** below.

### Template overlay three-way merge (major upgrades)

When a major upgrade changes base template versions:

1. Given `baseOld`, `baseNew`, and `tenantOverlay` (which references `baseOld`).
2. Compute `Δ_overlay = diff(baseOld, baseOld + tenantOverlay)` — the overlay's intent.
3. Apply `Δ_overlay` onto `baseNew`. Where paths survive unchanged, apply cleanly.
4. Where `baseNew` has moved or removed a path the overlay modified, mark that overlay hunk as a **conflict**. Present to the admin: keep overlay hunk (may silently stop taking effect), drop overlay hunk, or author a new hunk against `baseNew`.
5. The resolved overlay references `baseNew` and is stored under a new overlay version.

This is the standard three-way merge (like git), executed on JSON Merge Patch documents.

### Deprecation and removal

| Status | Provisionable to new tenants | Existing tenants can remain | Duration |
|---|---|---|---|
| `Draft` | No | No | Author-only; never goes live |
| `Preview` | Opt-in | Yes | Until promoted to GA or abandoned |
| `GA` | Yes | Yes | Indefinite |
| `Deprecated` | No | Yes, with nag banner | At least **180 days** before next status change |
| `Archived` | No | No | Read-only historical access retained 1 year |

A bundle cannot move from `GA` directly to `Archived`; it must spend ≥180 days in `Deprecated` so tenants can migrate.

Major-version transitions are not status transitions. Both the old and new versions can be `GA` simultaneously. The old version is typically marked `Deprecated` after the new version has been `GA` for 90 days, signaling the deprecation countdown.

### Rollback policy

- **Automatic rollback** on provisioning failure: if applying an upgrade fails mid-way, the tenant is restored to the prior version with no data loss. Partial state is never persisted.
- **Manual rollback** after a successful upgrade: permitted for patch and minor upgrades within 7 days; not permitted for major upgrades (would require reverse three-way merge — out of scope).
- **Data never destroyed on rollback**. Newer records written under the new bundle version remain queryable; features not present in the rolled-back version are hidden from UI but their data is preserved.

### Entitlement revocation

Downgrading a tenant's edition or removing an optional module:

- **Does not delete tenant data** by default. Data becomes read-only / hidden in UI.
- **Does deactivate feature overrides** that belonged to the removed scope.
- **Admin action required**: tenant admin confirms the downgrade; system shows the impact (what becomes hidden).

Bundle authors declaring removal in a major bump must explicitly document migration paths for affected tenants. Bridge surfaces these alongside the upgrade preview.

### Manifest identity under upgrades

The manifest `key` never changes across versions. A bundle evolves; it does not fork identities. If a fork is genuinely needed (same business case, incompatible future direction), ship a **new bundle** with a distinct key and a migration path documented.

---

## Consequences

### Positive

- Bundle authors have a deterministic semver rule; review catches wrong bumps mechanically.
- Tenants never get surprised by a breaking upgrade.
- Template overlays survive base upgrades with an auditable merge, not a silent replacement.
- Deprecation windows give customers time to migrate without losing support.

### Negative

- Three-way merge tooling must exist before the first bundle major bump ships — a real engineering effort tracked as P3 follow-up.
- Deprecation timelines consume calendar space; breaking changes cannot ship in under six months of signaling.
- Manual rollback being disallowed for major upgrades may feel restrictive; the escape valve is forward-only fixes (a patch on the new major line).

### Follow-ups

1. **Three-way merge implementation** for template overlays — a small library in `Sunfish.Foundation.Catalog.Templates` or its eventual successor (`blocks-templating` per ADR 0010).
2. **Migration report generator** — reads old + new manifests, produces the diff + impact summary Bridge surfaces during major upgrades.
3. **Bundle store UX** in Bridge (P3 deliverable) — renders the migration report and orchestrates admin consent.
4. **Bundle lifecycle audit log** — every status transition, version bump, and tenant upgrade is recorded for compliance.
5. **CI lint** — the bundle-manifest CI step lints version bumps against the changed fields and rejects mismatches.

---

## References

- ADR 0007 — Bundle Manifest Schema (sketched the versioning idea).
- ADR 0005 — Type-Customization Model (template overlays and three-way merge motivation).
- Semver 2.0.0 specification.
- JSON Merge Patch (RFC 7396) — the overlay format whose three-way merge we are formalizing here.
