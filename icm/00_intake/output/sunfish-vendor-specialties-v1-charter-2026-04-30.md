# Starter Taxonomy Charter v1.0 — `Sunfish.Vendor.Specialties@1.0.0`

**Status:** Draft (sunfish-PM authored as part of W#18 Phase 6; awaiting CO sign-off)
**Date:** 2026-04-30
**Composes:** [ADR 0056 — Foundation.Taxonomy substrate](../../../docs/adrs/0056-foundation-taxonomy-substrate.md), [ADR 0058 — Vendor onboarding posture](../../../docs/adrs/0058-vendor-onboarding-posture.md)
**Author:** sunfish-PM session (W#18 Phase 6 hand-off)
**ICM stage:** 00_intake (taxonomy product charter)
**Pipeline variant:** sunfish-feature-change

---

## Purpose

`Sunfish.Vendor.Specialties@1.0.0` enumerates the trades + service categories that vendors offer. Replaces the existing `VendorSpecialty` enum in `blocks-maintenance` per ADR 0058 cross-package wiring — every existing enum value is preserved as a v1.0 root node so consumers migrating from the enum see no semantic regression.

Authoritative regime — Sunfish ships the canonical seed; civilians may **clone** to derive their own variant or **extend** with locally-scoped sub-specialty nodes (e.g., a property-management firm with a custom "Pool Service" specialty), but cannot **alter** the Sunfish-shipped node set.

## Identity

- **System:** `Sunfish.Vendor.Specialties@1.0.0`
- **Owner:** Sunfish (authoritative)
- **Governance regime:** Authoritative

## Migration from `VendorSpecialty` enum

The 11 existing `VendorSpecialty` enum values become the 11 anchor root nodes (codes lower-kebab-cased from PascalCase). Sub-categories layered as children where the trade has meaningful sub-distinctions. Net node count: 11 root + 19 children = 30 nodes.

| Enum value | v1.0 node code |
|---|---|
| `GeneralContractor` | `general-contractor` |
| `Plumbing` | `plumbing` |
| `Electrical` | `electrical` |
| `HVAC` | `hvac` |
| `Landscaping` | `landscaping` |
| `Painting` | `painting` |
| `Roofing` | `roofing` |
| `PestControl` | `pest-control` |
| `Appliances` | `appliances` |
| `Cleaning` | `cleaning` |
| `Other` | `other` |

Phase 1 of W#18 migrates `Vendor.Specialty` (singular enum) → `Vendor.Specialties` (list of `TaxonomyClassification`). Existing call-sites map enum value → `new TaxonomyClassification(system: "Sunfish.Vendor.Specialties@1.0.0", code: "<kebab>")`.

## Root nodes (parent: null) — 11 anchors

| code | display | description |
|---|---|---|
| `general-contractor` | General Contractor | Broad-capability contractor handling multi-trade jobs (renovation, multi-system repair). |
| `plumbing` | Plumbing | Plumbing installation, repair, and maintenance. |
| `electrical` | Electrical | Electrical wiring, fixture installation, panel work. |
| `hvac` | HVAC | Heating, ventilation, air conditioning installation + repair. |
| `landscaping` | Landscaping | Grounds maintenance, lawn care, tree work. |
| `painting` | Painting | Interior + exterior painting. |
| `roofing` | Roofing | Roof installation, repair, gutter work. |
| `pest-control` | Pest Control | Extermination + ongoing pest management. |
| `appliances` | Appliances | Appliance installation + repair. |
| `cleaning` | Cleaning | Janitorial + cleaning services. |
| `other` | Other | Catch-all when no more specific specialty fits. |

## Children — `plumbing` sub-specialties

| code | parent | display | description |
|---|---|---|---|
| `plumbing.water-heater` | `plumbing` | Plumbing — Water Heater | Tank + tankless water heater install/repair/replace. |
| `plumbing.drain-cleaning` | `plumbing` | Plumbing — Drain Cleaning | Hydrojetting, snake, drain unclogging. |
| `plumbing.pipe-repair` | `plumbing` | Plumbing — Pipe Repair | Leak repair, repipe, copper/PEX replacement. |

## Children — `electrical` sub-specialties

| code | parent | display | description |
|---|---|---|---|
| `electrical.panel` | `electrical` | Electrical — Panel | Service-panel install, upgrade, replace. |
| `electrical.lighting` | `electrical` | Electrical — Lighting | Fixture install, recessed lighting, low-voltage. |
| `electrical.ev-charger` | `electrical` | Electrical — EV Charger | Level-2 EV-charger installation. |

## Children — `hvac` sub-specialties

| code | parent | display | description |
|---|---|---|---|
| `hvac.central` | `hvac` | HVAC — Central System | Central AC + furnace + heat pump install/service. |
| `hvac.minisplit` | `hvac` | HVAC — Mini-Split | Ductless mini-split install + service. |
| `hvac.duct` | `hvac` | HVAC — Duct Work | Ductwork install, sealing, cleaning. |

## Children — `landscaping` sub-specialties

| code | parent | display | description |
|---|---|---|---|
| `landscaping.tree-service` | `landscaping` | Landscaping — Tree Service | Tree pruning, removal, stump grinding. |
| `landscaping.irrigation` | `landscaping` | Landscaping — Irrigation | Sprinkler/drip system install, repair, winterize. |
| `landscaping.snow-removal` | `landscaping` | Landscaping — Snow Removal | Plowing + de-icing (cold-climate jurisdictions). |

## Children — `roofing` sub-specialties

| code | parent | display | description |
|---|---|---|---|
| `roofing.shingle` | `roofing` | Roofing — Shingle | Asphalt + composite shingle install/repair. |
| `roofing.flat-roof` | `roofing` | Roofing — Flat Roof | EPDM, TPO, modified-bitumen flat-roof systems. |
| `roofing.gutter` | `roofing` | Roofing — Gutter | Gutter install, repair, leaf-guard. |

## Children — `cleaning` sub-specialties

| code | parent | display | description |
|---|---|---|---|
| `cleaning.move-out` | `cleaning` | Cleaning — Move-Out | Deep clean for vacating tenants. |
| `cleaning.recurring` | `cleaning` | Cleaning — Recurring | Weekly/biweekly common-area + unit cleaning. |
| `cleaning.carpet` | `cleaning` | Cleaning — Carpet | Carpet shampoo, steam, stain treatment. |
| `cleaning.window` | `cleaning` | Cleaning — Window | Interior + exterior window cleaning. |

## Out of v1.0 (deferred to v1.1 minor or v2.0 major)

- **Highly-specialized sub-trades** — restoration (water/fire/mold remediation), foundation, structural engineering, asbestos abatement. These are typically licensed-class distinctions; deferred until property-mgmt MVP demand materializes.
- **Insurance-specific specialties** — claims-adjuster vendors, public adjusters. Defer to a separate `Sunfish.Insurance.Specialties` taxonomy when the insurance-claim workflow lands.
- **Service-area metadata** — geographic radius, license-state coverage. These are vendor *attributes*, not specialty taxonomy nodes; they live on `Vendor` records directly.

## Versioning

- **MAJOR** (2.0.0) — any node `code` removed or renamed
- **MINOR** (1.1.0) — new nodes added (additive only); e.g., adding `restoration` root or `plumbing.gas-line` child
- **PATCH** (1.0.1) — display revisions, parent-reorganization within same `code` set, tombstones with successor mappings

Tombstoning is the deprecation marker; removing a tombstoned node requires a major bump.

## Cross-references

- ADR 0056 (Foundation.Taxonomy substrate) — the shape this charter slots into
- ADR 0058 (Vendor onboarding posture) — drives `Vendor.Specialties` field migration in W#18 Phase 1
- Existing `VendorSpecialty` enum (`packages/blocks-maintenance/Models/VendorSpecialty.cs`) — preserved as 11 root nodes
- Sibling charter: [`Sunfish.Equipment.Classes@1.0.0`](./starter-taxonomies-v1-charters-2026-04-29.md) — same Authoritative-regime + property-tier convention
