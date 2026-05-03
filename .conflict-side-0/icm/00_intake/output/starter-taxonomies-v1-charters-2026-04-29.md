# Starter Taxonomy Charters v1.0 (Sunfish-shipped, Authoritative regime)

**Status:** Draft (CTO; awaiting CO sign-off + ADR 0056 acceptance)
**Date:** 2026-04-29
**Composes:** [ADR 0056 — Foundation.Taxonomy substrate](../../../docs/adrs/0056-foundation-taxonomy-substrate.md) (PR #240)
**Author:** research session (CTO)
**ICM stage:** 00_intake (taxonomy product charters; one charter per starter taxonomy)
**Pipeline variant:** sunfish-feature-change (per starter taxonomy, when implementation hand-offs are written)

---

## Purpose

ADR 0056 defines the Foundation.Taxonomy substrate (versioned product model + lineage) and enumerates 5 starter taxonomies that ship with v1.0 of the substrate. This document charters those 5 taxonomies as concrete products: identity, governance regime, node list, parent/child structure, deprecation policy, and downstream-consumer-impact-on-version-bump.

Each charter is a Stage-00 intake artifact that can be promoted to a Stage-06 hand-off once ADR 0056 is Accepted and Foundation.Taxonomy Phase 1 (registry contract + node persistence + Coding/CodeableConcept primitives) has landed.

The 5 starter taxonomies + their immediate unblock:

| Taxonomy | Unblocks |
|---|---|
| `Sunfish.Signature.Scopes` v1.0 | ADR 0054 (Electronic Signatures) Pattern E amendment + 6 council-review amendments |
| `Sunfish.Equipment.Classes` v1.0 | Property cluster equipment work (taxonomy-backed instead of hardcoded) |
| `Sunfish.Vendor.Specialties` v1.0 | Vendor Onboarding ADR (cluster INDEX-queued) |
| `Sunfish.Inspection.DeficiencyCategories` v1.0 | Inspections EXTEND (already shipped scaffolding via PR #222) |
| `Sunfish.Contact.UseContexts` v1.0 | Contact-use UPF (PR #234) — FHIR-aligned use enum operationalization |

**Governance regime for all 5:** Authoritative (Sunfish-shipped; civilians may **clone** to derive their own taxonomy with separate identity, but cannot **alter** the Sunfish-shipped node set; can **extend** with locally-scoped child nodes that do not collide with Sunfish-managed identifiers).

---

## Charter conventions (apply to all 5)

### Identity

```
{Vendor}.{Domain}.{TaxonomyName}@{Version}
```

- **Vendor:** `Sunfish` for these 5 (authoritative)
- **Domain:** capitalized domain noun (`Signature`, `Equipment`, `Vendor`, `Inspection`, `Contact`)
- **TaxonomyName:** capitalized plural noun (`Scopes`, `Classes`, `Specialties`, `DeficiencyCategories`, `UseContexts`)
- **Version:** semver string (`1.0.0` for v1.0)

Each taxonomy's stable identity for `Coding`/`TaxonomyClassification` references:

```
system: "Sunfish.Signature.Scopes@1.0.0"
code:   "{node-stable-code}"   // see per-taxonomy charter below
display: "{node-default-display}"
```

### Node identity

- `code` is **stable forever** within a major version. Renaming a code = breaking change = major version bump.
- `display` may be revised within a major version (revisions tracked per ADR 0049 audit; `display_history` field on TaxonomyNode preserves prior labels for audit trail and UI rendering of historical records).
- `parent` may be revised within a major version (taxonomy reorganization is breaking only if `code` changes).
- `tombstoned: true` is the deprecation marker — not deletion. Tombstoned nodes still resolve (return their last-known `display` + `tombstoned_at` + optional `successor_code`); records pinned to the tombstoned `(system, code, version)` triple remain valid for audit.

### Versioning rules (semver applied to taxonomy products)

- **MAJOR (`2.0.0`):** any node `code` removed or renamed; any structural change that breaks consumers pinned to the prior major version
- **MINOR (`1.1.0`):** new nodes added (additive only)
- **PATCH (`1.0.1`):** display revisions, parent-reorganization within the same `code` set, tombstones with successor mappings

Consumers reference `system@version`. Version pinning is mandatory for compliance / audit-trail records (per ADR 0049 + 0054). Civilian dynamic-form definitions may pin to `system` alone (latest minor in the major series), accepting forward-compatibility risk.

### Deprecation policy

Tombstoning a node (within a major version) requires:
1. `tombstoned_at` timestamp
2. `successor_code` (optional but recommended) — a node within the same taxonomy that should replace it
3. `deprecation_reason` (free text)
4. ADR 0049 audit record (`DeprecateNode` event type)

Removing a tombstoned node requires a major version bump.

---

## Charter 1: `Sunfish.Signature.Scopes@1.0.0`

**Purpose:** Enumerate the legal/operational scopes a captured signature may attest to. ADR 0054's `SignatureScope` (Pattern E adopted 2026-04-29 via CO approval; PR #237) becomes `IReadOnlyList<TaxonomyClassification>` referencing nodes in this taxonomy.

**Owner:** Sunfish (authoritative)
**Governance regime:** Authoritative
**FHIR alignment:** mirrors `Signature.type` (FHIR R5 — multiple `Coding`); these are the Sunfish-shipped seed values, not the FHIR `urn:iso-astm:E1762-95:2013` set (which is also referenceable via a separately-charterable `HL7.Signature.Type` taxonomy bundle in a future release).

### Root nodes (parent: null)

| code | display | description |
|---|---|---|
| `lease-execution` | Lease Execution | Tenant or landlord signing a lease document with binding intent. |
| `lease-amendment` | Lease Amendment | Signature attesting to a revision of an existing lease (rider, addendum, modification). |
| `inspection-acknowledgment` | Inspection Acknowledgment | Acknowledgment of an inspection report's findings (not necessarily agreement). |
| `inspection-disagreement` | Inspection Disagreement | Acknowledgment of report receipt with explicit disagreement noted. |
| `move-in-checklist` | Move-In Checklist | Tenant signature on move-in condition checklist. |
| `move-out-checklist` | Move-Out Checklist | Tenant signature on move-out condition checklist. |
| `notary-jurat` | Notary Jurat | Notarized affidavit (signer swore truth of contents under oath). |
| `notary-acknowledgment` | Notary Acknowledgment | Notary attesting to identity of signer (no oath). |
| `witness-attestation` | Witness Attestation | Third-party witness confirming signing event. |
| `vendor-acceptance` | Vendor Acceptance | Vendor signing acceptance of work order or service agreement. |
| `payment-authorization` | Payment Authorization | Authorization of a specific payment (ACH, wire, recurring debit). |
| `consent-background-check` | Consent — Background Check | Applicant consent to FCRA-compliant background screening. |
| `consent-credit-check` | Consent — Credit Check | Applicant consent to FCRA-compliant credit screening. |
| `consent-disclosure` | Consent — Disclosure | Acknowledgment of receipt of mandated disclosure (lead-paint, fair housing, etc.). |
| `right-of-entry-notice` | Right-of-Entry Notice | Tenant acknowledgment of landlord notice to enter. |
| `delivery-receipt` | Delivery Receipt | Signature confirming delivery of physical item or document. |
| `general-acknowledgment` | General Acknowledgment | Catch-all when no more specific scope fits (cluster intake explicit). |

### Children (lease-execution → renewal/origination distinction)

| code | parent | display | description |
|---|---|---|---|
| `lease-origination` | `lease-execution` | Lease Origination | First execution of a lease for a unit (vs renewal). |
| `lease-renewal` | `lease-execution` | Lease Renewal | Execution of a lease that follows a prior lease for same parties + unit. |

### Children (inspection-acknowledgment → trigger taxonomy)

| code | parent | display | description |
|---|---|---|---|
| `inspection-acknowledgment-annual` | `inspection-acknowledgment` | Annual Inspection Acknowledgment | Annual property inspection report. |
| `inspection-acknowledgment-move-in` | `inspection-acknowledgment` | Move-In Inspection Acknowledgment | Pre-occupancy inspection report. |
| `inspection-acknowledgment-move-out` | `inspection-acknowledgment` | Move-Out Inspection Acknowledgment | Post-occupancy inspection report. |
| `inspection-acknowledgment-post-repair` | `inspection-acknowledgment` | Post-Repair Inspection Acknowledgment | Post-completion verification of work-order resolution. |
| `inspection-acknowledgment-jurisdictional` | `inspection-acknowledgment` | Jurisdictional Inspection Acknowledgment | City/county/state-mandated inspection. |

### Out of v1.0 (deferred to v1.1 minor or v2.0 major)

- HL7 `urn:iso-astm:E1762-95:2013` standard scope set (charters separately as `HL7.Signature.Type@1.0.0` bundle)
- Healthcare-specific scopes (HIPAA authorizations, etc.) — defer to future vertical
- E-signature platform-specific scopes (DocuSign envelope types) — handled at adapter layer, not taxonomy

### Why these 17+7 nodes specifically

Property cluster scope drove this list. Each node maps to a real signature event in the property MVP user stories:
- Lease execution / amendment / renewal (tenant onboarding + ongoing modification)
- Inspection scopes (move-in, move-out, annual, post-repair) — covers ADR 0054's "what was signed" question for property workflows
- Notary scopes (jurat vs acknowledgment) — covers notarized leases + signature-required forms
- Right-of-entry — covers the queued Right-of-Entry Compliance ADR
- Background/credit/disclosure consent — covers FCRA workflows in the leasing pipeline
- Payment authorization — covers ADR 0051 (Foundation.Integrations.Payments) workflows
- Vendor acceptance — covers ADR 0053 (Work-Order Domain Model) workflows

**Cross-cutting compliance:** every node enables a stable audit reference for compliance review (FCRA, state landlord-tenant law, NACHA payment regs).

---

## Charter 2: `Sunfish.Equipment.Classes@1.0.0`

**Purpose:** Class taxonomy for physical equipment in property management (water heaters, HVAC units, smoke detectors, etc.). Cluster's `Equipment.EquipmentClass` (currently a hardcoded enum in `blocks-property-equipment` per PR #213/#216) pivots to a `Coding` reference into this taxonomy. Tenants may extend with locally-scoped child nodes (e.g., a custom solar-panel class).

**Owner:** Sunfish (authoritative)
**Governance regime:** Authoritative
**Industry alignment:** loose alignment with NAHMA / NAA / IREM facility-management classification conventions; not a direct copy of any single industry standard (those are typically licensed and vendor-specific).

### Root nodes (parent: null) — equipment categories

| code | display | description |
|---|---|---|
| `plumbing` | Plumbing | Plumbing equipment (water heaters, pumps, fixtures). |
| `hvac` | HVAC | Heating, ventilation, and air-conditioning equipment. |
| `electrical` | Electrical | Electrical distribution + protection equipment. |
| `appliance` | Appliance | Major appliances (refrigerators, ovens, dishwashers, washers, dryers). |
| `safety` | Safety Equipment | Smoke detectors, CO detectors, fire extinguishers, sprinkler heads. |
| `access-control` | Access Control | Locks, keys, door hardware, garage openers. |
| `energy-management` | Energy Management | Smart thermostats, solar inverters, battery systems, EV chargers. |
| `landscape` | Landscape Equipment | Sprinklers, lawn equipment, exterior lighting. |
| `structural` | Structural | Foundation, roof, exterior cladding (durable equipment-class assets). |
| `other` | Other | Catch-all when no specific class applies (intentional escape hatch). |

### Children (plumbing → water heater types)

| code | parent | display |
|---|---|---|
| `water-heater-tank` | `plumbing` | Water Heater (Tank) |
| `water-heater-tankless` | `plumbing` | Water Heater (Tankless) |
| `water-heater-heat-pump` | `plumbing` | Water Heater (Heat Pump) |
| `water-heater-solar` | `plumbing` | Water Heater (Solar) |
| `water-pump` | `plumbing` | Water Pump |
| `sump-pump` | `plumbing` | Sump Pump |
| `water-softener` | `plumbing` | Water Softener |

### Children (hvac → unit types)

| code | parent | display |
|---|---|---|
| `furnace-gas` | `hvac` | Furnace (Gas) |
| `furnace-electric` | `hvac` | Furnace (Electric) |
| `boiler` | `hvac` | Boiler |
| `central-ac` | `hvac` | Central Air Conditioner |
| `heat-pump-air-source` | `hvac` | Heat Pump (Air-Source) |
| `heat-pump-ground-source` | `hvac` | Heat Pump (Ground-Source / Geothermal) |
| `mini-split` | `hvac` | Mini-Split |
| `evaporative-cooler` | `hvac` | Evaporative Cooler (Swamp) |
| `air-handler` | `hvac` | Air Handler |
| `thermostat` | `hvac` | Thermostat |

### Children (electrical → distribution + protection)

| code | parent | display |
|---|---|---|
| `electrical-panel` | `electrical` | Electrical Panel (Main / Sub) |
| `circuit-breaker` | `electrical` | Circuit Breaker |
| `gfci-outlet` | `electrical` | GFCI Outlet |
| `arc-fault-breaker` | `electrical` | Arc-Fault Breaker |
| `surge-protector` | `electrical` | Whole-House Surge Protector |
| `generator-backup` | `electrical` | Backup Generator |

### Children (safety → device types)

| code | parent | display |
|---|---|---|
| `smoke-detector` | `safety` | Smoke Detector |
| `co-detector` | `safety` | Carbon Monoxide Detector |
| `combo-smoke-co-detector` | `safety` | Smoke + CO Combo Detector |
| `fire-extinguisher` | `safety` | Fire Extinguisher |
| `sprinkler-head` | `safety` | Fire Sprinkler Head |
| `radon-mitigation` | `safety` | Radon Mitigation System |

### Children (energy-management — for solar/EV/battery distinctions)

| code | parent | display |
|---|---|---|
| `solar-panel` | `energy-management` | Solar Panel |
| `solar-inverter` | `energy-management` | Solar Inverter |
| `battery-storage` | `energy-management` | Battery Storage System |
| `ev-charger-l2` | `energy-management` | EV Charger (Level 2) |
| `ev-charger-dcfc` | `energy-management` | EV Charger (DC Fast) |
| `smart-thermostat` | `energy-management` | Smart Thermostat |

### Out of v1.0 (deferred)

- Commercial-only equipment (rooftop unit, chiller, cooling tower, building automation system) — will appear in v1.1 if/when commercial property management enters scope
- Multi-family-specific (boiler-room, central laundry, elevator, pool equipment) — defer to v1.1
- Detailed appliance subclasses (oven type, refrigerator type) — civilians may extend locally

---

## Charter 3: `Sunfish.Vendor.Specialties@1.0.0`

**Purpose:** Specialty taxonomy for vendor onboarding. Each vendor record references one or more specialty nodes (vendor doing both plumbing + HVAC = two `TaxonomyClassification` entries). Tenants extend with locally-scoped child nodes for niche specialties.

**Owner:** Sunfish (authoritative)
**Governance regime:** Authoritative
**Industry alignment:** standard contractor-license categories (e.g., NASCLA, state contractor-board categories).

### Root nodes (parent: null)

| code | display | description |
|---|---|---|
| `plumbing` | Plumbing | Pipes, fixtures, water heaters, drains. |
| `electrical` | Electrical | Wiring, panels, circuits, fixtures. |
| `hvac` | HVAC | Heating, cooling, ventilation. |
| `roofing` | Roofing | Roof installation, repair, inspection. |
| `painting` | Painting | Interior and exterior painting. |
| `flooring` | Flooring | Carpet, tile, hardwood, vinyl. |
| `landscaping` | Landscaping | Lawn, trees, sprinklers, hardscape. |
| `pest-control` | Pest Control | Insect, rodent, termite, wildlife. |
| `cleaning` | Cleaning | Move-out cleaning, common-area cleaning, carpet cleaning. |
| `general-contractor` | General Contractor | Licensed GC for permitted multi-trade work. |
| `handyman` | Handyman | Unlicensed multi-trade for minor work. |
| `appliance-repair` | Appliance Repair | Refrigerator, oven, dishwasher, washer, dryer. |
| `locksmith` | Locksmith | Lock installation, rekeying, lockouts. |
| `garage-door` | Garage Door | Installation, repair, opener service. |
| `pool-spa` | Pool / Spa | Pool maintenance, equipment, chemicals. |
| `inspector` | Inspector | Licensed inspector (general, structural, mold, radon, etc.). |
| `appraiser` | Appraiser | Licensed real-estate appraiser. |
| `notary` | Notary | Notary public services. |
| `legal` | Legal Services | Real-estate attorney, eviction counsel. |
| `accounting` | Accounting / Tax | Bookkeeper, CPA, tax preparer. |
| `other` | Other Specialty | Catch-all for niche specialties (locally extensible). |

### Children — common subdivisions

| code | parent | display |
|---|---|---|
| `roofing-asphalt` | `roofing` | Roofing — Asphalt Shingle |
| `roofing-metal` | `roofing` | Roofing — Metal |
| `roofing-tile` | `roofing` | Roofing — Tile |
| `roofing-flat` | `roofing` | Roofing — Flat (TPO/EPDM) |
| `pest-termite` | `pest-control` | Pest Control — Termite |
| `pest-rodent` | `pest-control` | Pest Control — Rodent |
| `pest-wildlife` | `pest-control` | Pest Control — Wildlife |
| `inspector-general` | `inspector` | Inspector — General Property |
| `inspector-structural` | `inspector` | Inspector — Structural |
| `inspector-mold` | `inspector` | Inspector — Mold |
| `inspector-radon` | `inspector` | Inspector — Radon |
| `inspector-sewer` | `inspector` | Inspector — Sewer / Septic |

### Out of v1.0 (deferred)

- Commercial-only specialties (commercial fire suppression, commercial elevator, etc.) — defer to v1.1
- Highly regional specialties (e.g., snowplow, hurricane shutter) — civilians extend locally
- Trade-license metadata (license number, expiry, jurisdiction) is **not** taxonomy data — it's vendor-record data referencing this taxonomy

---

## Charter 4: `Sunfish.Inspection.DeficiencyCategories@1.0.0`

**Purpose:** Deficiency category taxonomy for inspection reports. Replaces the inspection cluster's hardcoded `DeficiencyCategory` enum approach.

**Owner:** Sunfish (authoritative)
**Governance regime:** Authoritative
**Industry alignment:** loose alignment with HUD UPCS (Uniform Physical Condition Standards) deficiency categories.

### Root nodes — top-level area

| code | display | description |
|---|---|---|
| `site` | Site | Site-level deficiencies (drainage, fencing, lighting). |
| `building-exterior` | Building Exterior | Exterior cladding, foundation, roof. |
| `common-areas` | Common Areas | Shared corridors, stairs, lobbies. |
| `unit-interior` | Unit Interior | Inside an individual rented unit. |
| `systems` | Building Systems | HVAC, plumbing, electrical, fire/life-safety systems. |
| `health-safety` | Health & Safety | Issues that pose immediate hazard regardless of category. |

### Children — common deficiencies (each parent gets a `health-safety` cross-reference where relevant)

| code | parent | display |
|---|---|---|
| `site-drainage` | `site` | Site — Drainage / Standing Water |
| `site-walkway` | `site` | Site — Walkway / Step Hazard |
| `site-fence` | `site` | Site — Fence / Gate Damaged |
| `exterior-roof` | `building-exterior` | Exterior — Roof Condition |
| `exterior-cladding` | `building-exterior` | Exterior — Cladding / Siding |
| `exterior-foundation` | `building-exterior` | Exterior — Foundation Crack / Settlement |
| `exterior-window` | `building-exterior` | Exterior — Window Damage |
| `unit-floor` | `unit-interior` | Unit — Flooring Damage |
| `unit-wall` | `unit-interior` | Unit — Wall / Ceiling Damage |
| `unit-paint` | `unit-interior` | Unit — Paint Condition |
| `unit-window` | `unit-interior` | Unit — Window Damage |
| `unit-appliance` | `unit-interior` | Unit — Appliance Issue |
| `systems-electrical` | `systems` | Systems — Electrical Issue |
| `systems-plumbing` | `systems` | Systems — Plumbing Leak / Issue |
| `systems-hvac` | `systems` | Systems — HVAC Performance |
| `systems-water-heater` | `systems` | Systems — Water Heater |
| `health-mold` | `health-safety` | Health — Mold / Mildew |
| `health-pest` | `health-safety` | Health — Pest Infestation |
| `health-lead-paint` | `health-safety` | Health — Lead Paint Disturbance |
| `health-asbestos` | `health-safety` | Health — Asbestos Disturbance |
| `health-co-risk` | `health-safety` | Health — Carbon Monoxide Risk |
| `health-fire-safety` | `health-safety` | Health — Fire/Life-Safety Issue (smoke detector, sprinkler, exit) |

### Out of v1.0 (deferred)

- Severity rating taxonomy (separate concern; numeric Rating per OSS primitives research)
- Resolution/remediation taxonomy (separate concern; could charter as `Sunfish.Inspection.RemediationActions@1.0.0` later)
- HUD UPCS exact mirror (some categories are HUD-only; not Sunfish-MVP-relevant)

---

## Charter 5: `Sunfish.Contact.UseContexts@1.0.0`

**Purpose:** Contact use-context taxonomy for `ContactPoint` records (email/phone/SMS). Operationalizes the FHIR-aligned `use` enum that emerged from PR #234 Contact-Use UPF + Pattern G architecture.

**Owner:** Sunfish (authoritative)
**Governance regime:** Authoritative
**FHIR alignment:** mirrors `ContactPoint.use` enum codes (`home`, `work`, `temp`, `old`, `mobile`) plus operational additions for property management.

### Root nodes (parent: null) — FHIR-mirrored core

| code | display | description |
|---|---|---|
| `home` | Home | A communication contact point at the person's home. |
| `work` | Work | A communication contact point at the person's place of work. |
| `mobile` | Mobile | A telecommunication device that moves with the individual. |
| `temp` | Temporary | A temporary contact point (replaces existing during a known time window). |
| `old` | Old | This contact point is no longer in use (kept for historical/audit purposes). |

### Operational additions (Sunfish-specific; civilian-extensible — children of `work`)

| code | parent | display |
|---|---|---|
| `work-leasing-office` | `work` | Work — Leasing Office |
| `work-property-manager` | `work` | Work — Property Manager (direct) |
| `work-on-call` | `work` | Work — On-Call (after-hours emergency) |

### Operational additions (children of `home`)

| code | parent | display |
|---|---|---|
| `home-emergency-contact` | `home` | Home — Emergency Contact |
| `home-co-leaseholder` | `home` | Home — Co-Leaseholder |

### Out of v1.0 (deferred)

- Provider-specific contexts (e.g., `medical`, `legal`, `accountant`) — defer to v1.1 if/when those use cases solidify; civilians may extend locally as `home`/`work` children
- Channel-preference taxonomy (e.g., "prefer SMS over email for after-hours") — separate concern; not taxonomy-suitable, lives on `ContactPoint.preference` field

---

## Cross-charter patterns

### Code-naming convention

All codes use `kebab-case-ascii-only`. Reserved characters: `-`, `a-z`, `0-9` only. No spaces, underscores, or unicode. This guarantees safe URL embedding + safe JSON property naming + alignment with FHIR FHIR `Coding.code` conventions.

### Display-naming convention

`Title Case With Capital Words And Em-Dashes For Sub-Categorization` (e.g., `Inspector — Mold`). Em-dash separator (` — `) for parent-child display when context lacks structural rendering.

### Parent-child semantics

- Parent node = broader category; child = narrower
- Multi-parent (DAG) **not supported** in v1.0 — strict tree
- Cross-references handled via the consuming-record's classification list (a record may carry multiple `TaxonomyClassification`s from different paths)

### Audit + governance

All 5 starter taxonomies follow ADR 0049 audit (8 record types):
- `CreateDefinition` (one event per starter taxonomy on substrate genesis)
- `PublishVersion` (one event per major/minor release)
- `AddNode` (per node added in minor releases)
- `DeprecateNode` (tombstone events)
- `ResolveClassification` (per record-resolution; downstream of consuming records)
- (Clone/Extend/Alter not expected for Sunfish-shipped authoritative; civilians cloning trigger those on their own derivation)

### Versioning cadence (forecast)

| Taxonomy | v1.0 → v1.1 trigger | v1.x → v2.0 trigger |
|---|---|---|
| `Sunfish.Signature.Scopes` | adding HL7 mapped scope set (deferred) | renaming or removing core node (e.g., `lease-execution`) |
| `Sunfish.Equipment.Classes` | commercial property management entry | rebase to NAHMA/IREM standard mid-major |
| `Sunfish.Vendor.Specialties` | new licensed-trade emergence | reorg of root categories |
| `Sunfish.Inspection.DeficiencyCategories` | jurisdictional mandate addition | rebase to HUD UPCS mid-major |
| `Sunfish.Contact.UseContexts` | provider-vertical entry (medical/legal) | breaking FHIR `use` enum upstream change |

---

## Acceptance gates

Before each starter taxonomy ships:

1. **CO sign-off** (out-of-band) on this charter (one or all 5 in a single review pass)
2. **ADR 0056 Accepted** (PR #240; in flight at time of writing)
3. **Foundation.Taxonomy Phase 1 landed** (registry contract + node persistence + Coding/CodeableConcept primitives)
4. **Implementation hand-off authored** (one per taxonomy or bundled)

The charter is a Stage-00 intake; the implementation is a Stage-06 hand-off; Stage-01/02/03/04/05 are accelerated for shipped seed data per the `sunfish-feature-change` variant routing (intake → architecture → implementation-plan → build → release; no scaffolding stage needed).

---

## Open questions

1. **Should `Sunfish.Contact.UseContexts` be operational additions OR a separate taxonomy?** PR #234's UPF concluded operational additions belong as children of FHIR-mirrored core. Confirmed in this charter (children of `work`/`home` for property-MVP-relevant operational uses).

2. **Should `Sunfish.Inspection.DeficiencyCategories` mirror HUD UPCS exactly?** Decision: loose alignment for v1.0; strict UPCS mirror would require licensing review + may not match property-MVP needs. Revisit in v1.1 if a regulated tenant requires UPCS compliance.

3. **Should `Sunfish.Vendor.Specialties` carry license-category metadata?** Decision: no. License metadata is per-vendor-record data (license #, expiry, jurisdiction); the taxonomy carries the specialty type only.

4. **Should starter taxonomies ship as one bundled product or 5 separate products?** Decision: **5 separate products** with their own version cadences. Each evolves independently (signature scopes evolve faster than equipment classes; etc.). Bundled distribution is a marketplace concern, not an identity concern.

5. **Should civilian extensions co-exist with Sunfish-shipped nodes in the same taxonomy?** Decision: **no for v1.0** — civilian extensions create a derivation (Clone semantics in ADR 0056) with separate identity. Sunfish-shipped taxonomies remain pristine. Future: a `Custom` extension regime that allows local-scope child nodes (under a reserved namespace prefix like `_civilian-{tenant-slug}-`) without forcing Clone — defer to v1.1 if pressure emerges.

---

## Implementation runway (Phase 1 — gated on ADR 0056 acceptance)

1. Encode all 5 charters as JSON seed data per the Foundation.Taxonomy substrate's persistence schema
2. Author the substrate's TaxonomySeed loader (one-shot at fresh-tenant initialization)
3. Wire up the substrate's `ITaxonomyRegistry.RegisterCorePackage()` call for each starter
4. Acceptance test: every starter taxonomy resolves successfully via `ITaxonomyResolver.Resolve(system, code, version)` with non-null `TaxonomyNode`
5. Acceptance test: tombstoning a non-shipped node fails (Authoritative regime guard)
6. Acceptance test: cloning a Sunfish-shipped taxonomy creates a new TaxonomyDefinition with separate identity + `TaxonomyLineage` record pointing back to the source

After Phase 1 ships, ADR 0054's 7 amendments become authorable (Sunfish.Signature.Scopes is the seed for the Pattern E TaxonomyClassification reference).
