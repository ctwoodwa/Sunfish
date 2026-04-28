# Intake Note — Assets Domain Module (incl. Vehicle subtype + Mileage)

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build`.**
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (turns 4–5 — asset inventory with OCR; vehicle as asset subtype; mileage events).
**Pipeline variant:** `sunfish-feature-change`
**Parent:** [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)

---

## Problem Statement

Property owners are responsible for tracked assets — water heaters, HVAC, roofs, appliances, irrigation, etc. — each with cost basis, install date, warranty, service history, condition, expected remaining life, and disposal/replacement events. Today the BDFL has none of this captured systematically; serial numbers exist on stickers in basements and nowhere else.

A field-grade asset module turns property operations from spreadsheet-and-memory into a structured, queryable inventory. It also feeds the tax advisor (depreciation schedules), maintenance forecasting (warranty + remaining life), inspections (per-asset condition assessments), and work orders (FK target).

Vehicles are assets too. The BDFL's truck has a cost basis, depreciation schedule, business-use percentage, and service history identical in shape to a water heater's. **Mileage logs are vehicle lifecycle events.** Folding vehicles + mileage into the asset module avoids a parallel "vehicle" silo and reuses identical infrastructure.

## Scope Statement

### In scope

1. **`Asset` entity.** Tenant-scoped; FK Property + (optional) PropertyUnit; AssetClass discriminator (water heater | HVAC | appliance | roof | vehicle | …); make + model + serial + install_date + acquisition_cost + acquisition_source (links to Receipt) + location_in_property + photos + warranty metadata + expected_useful_life + disposal_date + disposal_reason.
2. **`AssetLifecycleEvent` log.** Append-only: `Installed`, `Serviced`, `Inspected`, `WarrantyClaimed`, `Replaced`, `Disposed`, `RatingChanged`. Each emits to ADR 0049 audit substrate.
3. **`Vehicle` AssetClass extension.** Vehicle-specific fields: VIN, license plate, base_mileage_at_acquisition, current_business_use_percentage, primary_driver.
4. **`Trip` entity (vehicle lifecycle events).** date + driver + start_odometer + end_odometer + computed miles + purpose + category (business / personal / charity / medical) + destination + (optional) FK linked_property_visit + (optional) gps_track_polyline_blob. **Phase 2.1a ships manual entry.** GPS auto-tracking is Phase 2.1c (separate ADR for mobile location capture posture).
5. **`AssetConditionAssessment` reference.** Created during inspections (sibling intake); read here for asset detail view ("last condition: fair, 2026-04-22").
6. **`blocks-assets` package.** New persistent block; entity registration per ADR 0015.
7. **iOS capture flow.** DataScannerViewController for serial/barcode + photo + asset-class picker + "add to existing asset OR create new" duplicate-prevention.
8. **Tax-advisor projection.** Cost basis + depreciation schedule per asset + total per property + total per tenant. Phase 2.2 reporting.

### Out of scope (handled elsewhere)

- Property entity → [`property-properties-intake-2026-04-28.md`](./property-properties-intake-2026-04-28.md)
- Inspection condition assessments → [`property-inspections-intake-2026-04-28.md`](./property-inspections-intake-2026-04-28.md)
- Receipt linking (asset acquisition cost evidence) → [`property-receipts-intake-2026-04-28.md`](./property-receipts-intake-2026-04-28.md)
- Work orders against assets → [`property-work-orders-intake-2026-04-28.md`](./property-work-orders-intake-2026-04-28.md)
- GPS auto-tracked mileage → Phase 2.1c (separate ADR)
- Tax-prep export of depreciation schedule → Phase 2.3 (Phase 2 commercial intake's `blocks-tax-reporting`)

---

## Affected Sunfish Areas

- `blocks-assets` (new)
- `foundation-persistence`, ADR 0015, ADR 0049
- `blocks-tax-reporting` (existing) — consumes assets for depreciation
- iOS capture flow (depends on iOS App intake)

## Acceptance Criteria

- [ ] All entities defined with XML doc + ADR 0014 adapter parity (read-side)
- [ ] AssetClass extensibility: kernel ships water-heater/HVAC/appliance/roof/vehicle/generic; future classes addable without schema migration
- [ ] iOS capture flow with DataScannerViewController OCR + duplicate detection
- [ ] Vehicle subtype + Trip entity fully functional (manual entry)
- [ ] Tax-advisor depreciation projection per asset
- [ ] kitchen-sink demo: 5 assets across 2 properties incl. one vehicle with 10 trips
- [ ] apps/docs entry covering asset lifecycle + vehicle/mileage subtype + OCR capture

## Open Questions

| ID | Question | Resolution |
|---|---|---|
| OQ-A1 | Asset hierarchy (parent-child for HVAC = compressor + air handler)? | Stage 02 — yes, optional `parent_asset_id`; flat for v1, hierarchy for complex assets |
| OQ-A2 | AssetClass: enum + extensibility hook, or string-tag with schema registry? | Stage 02 — recommend schema-registry-backed (per ADR 0001) |
| OQ-A3 | Trip GPS auto-tracking ADR scope | Defer to Phase 2.1c separate ADR |
| OQ-A4 | Vehicle business-use percentage: per tax year or per trip category aggregation? | Stage 02 — both: stored per tax year, computed from trip categories as cross-check |
| OQ-A5 | Vehicle as Asset vs separate Vehicle entity | Stage 02 — Asset with subtype recommended (this intake's choice); revisit if vehicles diverge significantly |

## Dependencies

**Blocked by:** Properties (sibling), ADR 0049, ADR 0015
**Blocks:** Inspections (AssetConditionAssessment FK), Work Orders (asset FK), Receipts (asset acquisition link), Phase 2.3 tax reporting

## Cross-references

- Sibling intakes: Properties, Inspections, Receipts, Work Orders, iOS App
- ADR 0001 (schema registry), ADR 0015, ADR 0049

## Sign-off

Research session — 2026-04-28
