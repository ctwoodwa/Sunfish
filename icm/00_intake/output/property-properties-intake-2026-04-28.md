# Intake Note — Properties Domain Module

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build` and a hand-off file appears in `icm/_state/handoffs/`.**
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28.
**Pipeline variant:** `sunfish-feature-change`
**Parent:** [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
**Position in cluster:** Spine #1 — root entity for the property-operations vertical.

---

## Problem Statement

Sunfish has no first-class `Property` entity. Phase 2 commercial scope is anchored on a 6-tenant property-management business (4 property LLCs + 1 holding co + 1 property management LLC), and every other property-operations module — assets, inspections, leases, work orders, receipts, mileage destinations, public listings — references "the property this is about." Without a canonical `Property` record, those modules either denormalize property metadata (model + serial of a water heater stored alongside "address: 123 Main St, unit 2B") or invent ad-hoc references that don't compose.

Adding `Property` as a domain module gives the cluster its root: every other module FK's to `Property` (or to one of its child entities), `Foundation.Multitenancy` scoping rules apply naturally (one property belongs to one tenant), and reporting pivots cleanly (P&L per property, asset list per property, inspection cadence per property, vendor expense per property).

`Property` is also the first record class to surface the **multi-unit question**: a fourplex is one parcel with four leasable units; a single-family rental is one parcel with one unit. The data model has to handle both without either feeling forced.

## Scope Statement

### In scope (this intake)

1. **`Property` entity definition.** Address, parcel/APN, ownership entity (which tenant LLC), acquisition date, acquisition cost, structure attributes (sqft, year built, beds/baths total), notes/photos, soft-delete on disposition.
2. **`PropertyUnit` child entity.** For multi-unit properties: per-unit attributes (unit number, sqft, beds/baths, current lease ref, market rent). Single-unit properties have one implicit `PropertyUnit` whose attributes mirror the parent.
3. **`PropertyOwnershipRecord` event log.** Acquisition / disposition / refinance / transfer events; immutable; sourced for tax basis and depreciation.
4. **`blocks-properties` package.** New persistent block; `ISunfishEntityModule` registration per ADR 0015; persistence via `foundation-persistence`.
5. **CRUD surface for Anchor + Bridge.** Owner web cockpit (separate intake) consumes; iOS app consumes for picker/lookup; public listings (separate intake) consumes a redacted projection.
6. **Tenant binding.** Every `Property` belongs to exactly one Sunfish tenant (an LLC). The tenant-identity work in `tenant-id-sentinel-pattern-intake-2026-04-28.md` applies; a property cannot be `TenantId.Default`.

### Out of scope (this intake — handled elsewhere)

- Asset inventory per property → [`property-assets-intake-2026-04-28.md`](./property-assets-intake-2026-04-28.md)
- Lease tracking per unit → [`property-leases-intake-2026-04-28.md`](./property-leases-intake-2026-04-28.md)
- Inspection history → [`property-inspections-intake-2026-04-28.md`](./property-inspections-intake-2026-04-28.md)
- Work-order history → [`property-work-orders-intake-2026-04-28.md`](./property-work-orders-intake-2026-04-28.md)
- Public listing surface → [`property-public-listings-intake-2026-04-28.md`](./property-public-listings-intake-2026-04-28.md)
- Cost-basis depreciation calc → Phase 2 commercial intake's `blocks-tax-reporting`
- Cross-property reporting (consolidated P&L, etc.) → Phase 2.2 (covered in Phase 2 commercial intake)

---

## Affected Sunfish Areas

| Layer | Item | Change |
|---|---|---|
| Foundation | `foundation-multitenancy` | Property is canonical tenant-bound entity; validates ADR 0008 + tenant-id-sentinel work |
| Foundation | `foundation-persistence` | New entity registration via ADR 0015 `ISunfishEntityModule` |
| Blocks | `blocks-properties` (new) | Primary deliverable |
| Blocks | `blocks-tax-reporting` (existing) | Consumes property → cost basis for depreciation; no schema change here |
| ADRs | ADR 0008 (multi-tenancy) | Property is the canonical tenant-bound demonstration |
| ADRs | ADR 0015 (module-entity registration) | Property registers as canonical example |
| ADRs | ADR 0049 (audit-trail substrate) | Acquisition / disposition / transfer events are first-class audit records |

---

## Acceptance Criteria

- [ ] `Property` and `PropertyUnit` entities defined in `blocks-properties` with full XML doc + ADR 0014 adapter parity contracts
- [ ] `PropertyOwnershipRecord` event log entity defined (immutable; append-only)
- [ ] CRUD endpoints / handlers for both entities, tenant-scoped per ADR 0008
- [ ] Adapter parity tests: Blazor + React both render a Property detail page sourced from the same kernel modules
- [ ] kitchen-sink demo: at least 2 properties, one single-unit + one multi-unit, populated for the Phase 2 commercial scope tenants
- [ ] apps/docs entry under "blocks" describing the property domain
- [ ] JSDoc / XML comments on all public APIs
- [ ] Migration path documented for the BDFL's current property records (currently in spreadsheet / Wave / Rentler) — one-shot import script under `tooling/`

---

## Open Questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-P1 | Multi-unit modeling: separate `PropertyUnit` child entity (recommended) vs. flat `Property` with `unit_count` and per-unit data denormalized into JSON column. | Stage 02 design — recommend separate child entity. |
| OQ-P2 | Geographic identifiers: store full address only, or also lat/lng / parcel polygon GeoJSON? Lat/lng useful for showings (mapping); polygon useful for inspections (boundary verification). | Stage 02 design — start with structured address + lat/lng; defer polygon. |
| OQ-P3 | Multi-tenant ownership: a holding-co LLC owns 4 child LLCs that each own properties. Which tenant does a property belong to — the child LLC or the holding co? | Resolve via parent intake's "Phase 2 commercial scope" actor model + `tenant-id-sentinel-pattern-intake-2026-04-28.md`. Likely: child LLC owns property; holding co tenant has cross-tenant read via `TenantSelection`. |
| OQ-P4 | Property "soft delete" on disposition: hide from active list but retain for historical reports? | Yes; standard soft-delete pattern with `disposed_at` + reason. |
| OQ-P5 | Photo storage: same blob-ingest pipeline as receipts/inspections? Or property "hero image" treated specially? | Same pipeline; recommend a `primary_photo_blob_ref` field for listing/dashboard usage. |

---

## Dependencies

**Blocked by:** None directly — Property is a leaf in the dependency graph. (Tenant identity work in `tenant-id-sentinel-pattern-intake-2026-04-28.md` is concurrent; both can proceed.)

**Blocks:**
- Assets, Inspections, Leases, Work Orders, Receipts, Public Listings, Owner Cockpit, Mileage destinations — all FK to `Property`

**Cross-cutting open questions consumed:** OQ1 (multi-actor permissions), OQ13 (toolchain replacement scope) from INDEX.

---

## Pipeline Variant Choice

`sunfish-feature-change` — new feature-block delivering a new domain module. Adapter parity required. kitchen-sink demo + apps/docs mandatory. Standard 9-stage flow with no skipped stages.

---

## Cross-references

- Parent: [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)
- Phase 2 commercial: [`phase-2-commercial-mvp-intake-2026-04-27.md`](./phase-2-commercial-mvp-intake-2026-04-27.md)
- Tenant identity: [`tenant-id-sentinel-pattern-intake-2026-04-28.md`](./tenant-id-sentinel-pattern-intake-2026-04-28.md)
- ADR 0008 multi-tenancy
- ADR 0015 module-entity registration
- ADR 0049 audit-trail substrate

---

## Sign-off

Research session — 2026-04-28
