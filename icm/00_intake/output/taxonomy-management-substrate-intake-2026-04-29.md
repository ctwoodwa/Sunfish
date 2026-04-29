# Intake Note — Taxonomy Management Substrate (versioned product model with lineage)

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM (COB): do not build until status flips to `ready-to-build` and a hand-off file appears in `icm/_state/handoffs/`.**
**Status owner:** CTO (research session)
**Date:** 2026-04-29
**Requestor:** Christopher Wood (BDFL / CO)
**Spec source:** CEO directive 2026-04-29 — the spec section captured below is essentially CEO's authored vision for the substrate.
**Pipeline variant:** `sunfish-feature-change` (with mandatory new ADR; foundation-tier substrate)
**Resolves:** OSS primitives research open question #4 — "Coding+CodeableConcept sufficient, or build taxonomy substrate above?" — **answered: build the substrate.** This intake captures the substrate spec and surfaces architectural decisions.
**Companion:** [`oss-primitive-types-research-2026-04-29.md`](../../01_discovery/output/oss-primitive-types-research-2026-04-29.md) (PR #231); forthcoming dynamic-forms substrate ADR.

---

## Problem Statement

Sunfish's primitive type catalog (PR #231) proposed `Coding` + `CodeableConcept` as compound primitives for taxonomy entries — a triple of (system, code, display). The shape is correct for **referencing** taxonomy entries from data records, but it intentionally punts on the harder questions: **who owns taxonomies, how do they version, how do they evolve, who can fork them, how do consumers stay aligned over time?**

CEO surfaced these as the actually-load-bearing concerns 2026-04-29. Sunfish needs a **Taxonomy Management Substrate** that treats taxonomies as first-class versioned products with lineage, not just as static lookup tables. Without this substrate:

- Equipment classification (`Equipment.kind`) becomes an enum baked into code; can't evolve per tenant
- Vendor specialty taxonomies are a free-for-all of inconsistent strings
- Inspection deficiency categories diverge across implementers
- Compliance / regulatory codes (FCRA classifications, NACHA codes, jurisdiction-specific rent-control taxonomies) don't have a stable identity for audit
- Cross-tenant analytics are impossible because nobody can map "this water heater class" in tenant A to "this water heater class" in tenant B
- Marketplace + community-contributed taxonomies don't have a place to live

The substrate is a foundation-tier feature, not a primitive. Substrate enables `Coding` (the lightweight reference primitive) to point at meaningful, governed taxonomy nodes.

---

## CEO's Spec (verbatim, captured 2026-04-29)

> **Core idea: taxonomies as versioned products with lineage**
>
> - Each taxonomy is a first‑class, versioned artifact: `taxonomyId`, semantic version, status (draft/published/deprecated), owner, and metadata.
> - A "derived" taxonomy records its ancestry (`baseTaxonomyId`, `baseVersion`) but then evolves independently.
> - Assets store classifications as stable references like `{ taxonomyId, taxonomyVersion, nodeId }`, so you always know exactly which taxonomy and version was used.
>
> **Authoritative vs marketplace roles**
>
> - Authoritative owners (corporate/government/standards bodies) publish central, governed taxonomies; they control changes, versions, and deprecations.
> - Marketplace participants (civilian/SMB/prosumer) can browse available taxonomies—including authoritative ones—and fork/clone/extend them into their own derived taxonomies.
>
> **Derived taxonomy operations**
>
> - **Clone:** copy an existing taxonomy into a new one with its own identity and owner; it can diverge freely while keeping a one‑time lineage pointer back to the source.
> - **Extend:** build on top of a base taxonomy, adding new nodes and metadata while preserving inherited structure and codes; the system tracks which nodes are inherited vs owned.
> - **Alter:** allow structural changes (move, split, merge, rename nodes) in the derived taxonomy, while still tracking which original nodes they came from for mapping/migration.
>
> **Different regimes by vertical**
>
> - **Corporate/government tenants:**
>   - Use authoritative taxonomies as the primary source.
>   - Any derivatives must maintain mappings back to the base for compliance and cross‑org analytics.
>   - Local changes are more constrained and validated.
> - **Civilian tenants:**
>   - Start from best‑practice template taxonomies (e.g., PMI‑styled project management, business templates) they can use out‑of‑the‑box or customize.
>   - Have the option to create fully free‑form taxonomies when templates don't fit.
>   - Mapping back to standards is encouraged but not strictly required, except where they opt into it.
>
> **Free‑form vs template‑based civilian taxonomies**
>
> - **Template‑based:**
>   - Curated, documented taxonomies shipped as marketplace "products."
>   - Users can install, then clone/extend them, preserving lineage to enable optional mapping to the original.
> - **Free‑form:**
>   - Users can create arbitrary trees/graphs/tags without imposed standards for maximum flexibility.
>   - The platform still uses the same underlying object model so later mapping or promotion to a template is possible.
>
> **Collaboration and local‑first / CRDT alignment**
>
> - Taxonomy authoring is not broadly multi‑user collaborative in the same document; instead:
>   - Authoritative taxonomies are typically edited in controlled environments and published as immutable snapshots for clients.
>   - Tenant‑owned derived taxonomies can use CRDTs or local‑first storage so a small group of authorized maintainers can edit offline and sync safely.
>   - Regular users mostly see snapshots; they classify assets via stable references, while taxonomy owners maintain and publish new versions.
>
> **Governance and policy surface**
>
> - The platform exposes a common engine but configures behavior by tenant/vertical:
>   - Which taxonomies they can see and adopt.
>   - Whether they can fork/clone/extend authoritative sources.
>   - How strict mappings back to those sources must be.
> - This lets you satisfy both extremes: strict, auditable taxonomies for corporate/government; flexible, template‑driven and free‑form taxonomies for civilians.

---

## Scope Statement

### In scope (this intake; foundation-tier substrate)

1. **`TaxonomyDefinition` entity** — versioned taxonomy artifact:
   - `taxonomy_id` (URI; e.g., `taxonomy://sunfish.dev/property-management/equipment-classes`)
   - `semantic_version` (e.g., `2.1.0`)
   - `status` enum (`Draft | Published | Deprecated | Withdrawn`)
   - `owner` (IdentityRef — tenant or external authoritative source)
   - `lineage` (optional `{base_taxonomy_id, base_version, derivation_kind: Clone|Extend|Alter}`)
   - `tree` (the actual node graph; CRDT-backed for tenant-owned editing)
   - `metadata` (description, license, locale variants)
2. **`TaxonomyNode` entity** — entry in the tree:
   - `node_id` (stable; persists across version bumps within a taxonomy)
   - `code` (canonical machine identifier, e.g., `WaterHeater`)
   - `display_text` (`InternationalizedText` per OSS research)
   - `parent_node_id` (optional)
   - `inherited_from` (optional — tracks if this node came from a base taxonomy via Extend)
   - `mapped_from` (optional — for Alter operations; tracks the original node this was derived from)
   - `status` enum (`Active | Deprecated | Withdrawn`)
   - `attributes` (key-value metadata per node)
3. **`TaxonomyClassification` reference** — what records use to point at a taxonomy node:
   - `{taxonomy_id, taxonomy_version, node_id}` triple — stable, version-pinned
   - This is the `Coding` primitive's actual storage shape
4. **Derived-taxonomy operations** — clone / extend / alter:
   - `Clone(source_taxonomy_id, source_version) → new_taxonomy_id` — full copy with new identity
   - `Extend(base_taxonomy_id, base_version) → derived_taxonomy_id` — additive; inherited nodes preserved
   - `Alter(derived_taxonomy_id, operation: {Move, Split, Merge, Rename})` — structural change with mapping preservation
5. **Vertical regime configuration** — per-tenant policy:
   - Which taxonomies tenant can view (`visible_taxonomies: TaxonomyId[]`)
   - Which tenant can fork/clone/extend (`derivation_permissions: PerTaxonomyPermission[]`)
   - Mapping strictness (`mapping_required_back_to_base: bool` per derived taxonomy)
6. **Marketplace surface** — bundle taxonomies as `MarketplaceProduct` (composing `Foundation.Catalog.Bundles` per ADR 0007):
   - Browse / install / preview
   - Versioned releases
   - Optional payment / licensing (deferred; v2)
7. **Snapshot publishing** — authoritative taxonomies publish immutable version snapshots; clients consume snapshots:
   - Server-side: `PublishTaxonomyVersionAsync` produces a frozen artifact
   - Client-side: subscriber pulls latest snapshot on a defined cadence
8. **CRDT-backed tenant-owned taxonomy editing** — uses ADR 0028 `Kernel.Crdt` substrate for collaborative offline editing by authorized maintainers; sync semantics inherit from the kernel's CRDT layer.
9. **Lineage queries** — given a derived taxonomy, walk back to the base; given a node, find what it was mapped from.
10. **Audit emission per ADR 0049** — every taxonomy change (create, version-bump, node add/edit/deprecate, clone, extend, alter) is a first-class audit event with attribution.
11. **`Sunfish.Foundation.Taxonomy` package** — new foundation-tier substrate (sibling to `Foundation.Catalog`).
12. **New ADR**: "Taxonomy Management Substrate — versioned product with lineage."

### Out of scope (this intake — handled elsewhere or deferred)

- Specific taxonomies (Property Management equipment classes, vendor specialties, etc.) — those are *content* shipped as marketplace products on the substrate
- Marketplace billing / paid taxonomies — Phase 2.x or later
- Visual taxonomy editor UI — Phase 2.1+ (substrate ships first; UI on Bridge as separate workstream)
- Cross-tenant taxonomy federation (one tenant subscribes to another tenant's taxonomy) — Phase 3+
- AI-assisted taxonomy mapping (ML to suggest mappings between similar taxonomies) — much later
- Standards-body integration (FHIR ValueSet, ICD-10, SNOMED, NAICS) — adapter pattern follows after substrate ships
- Taxonomy-driven validation (e.g., "Equipment.kind must reference a TaxonomyNode in this approved list") — covered by cross-field rules UPF + dynamic-forms substrate ADR

### Explicitly NOT in scope (deferred indefinitely or out of scope)

- Multi-axis classification (an equipment item belonging to multiple taxonomies for different facets) — assets have a list of `TaxonomyClassification`; no special multi-axis machinery beyond that
- Taxonomy comparison / diff tooling (UI for visualizing changes between versions) — Phase 3+
- Public-facing taxonomy registry (Sunfish-as-marketplace-host beyond same-platform tenants) — separate business decision
- Taxonomy translation / localization workflow (translation of node displays into 30+ locales) — `InternationalizedText` primitive supports it; the workflow is its own concern

---

## Affected Sunfish Areas

| Layer | Item | Change |
|---|---|---|
| Foundation | `Sunfish.Foundation.Taxonomy` (new) | Primary deliverable |
| Foundation | `Sunfish.Foundation.Catalog` | Marketplace integration; existing bundle catalog gains taxonomy product type |
| Foundation | `foundation-recovery` (workstream #15 ✓ built) | Authoritative-source publishing + signature; CRDT-backed editing leverages existing recovery primitives |
| Kernel | `kernel-crdt` (ADR 0028) | CRDT-backed tree editing for tenant-owned taxonomies |
| Kernel | `kernel-audit` (ADR 0049) | Audit emission for taxonomy lifecycle events |
| ADRs | New "Taxonomy Management Substrate" ADR | Primary architectural deliverable |
| ADRs | ADR 0001 (Schema Registry Governance) | Cross-reference; taxonomies are governed alongside schemas |
| ADRs | ADR 0007 (Bundle Manifest Schema) | Extension: bundles can declare TaxonomyProduct entries |
| ADRs | ADR 0049 (Audit Trail Substrate) | Confirm 6-8 new audit record types for taxonomy lifecycle events |
| ADRs | Forthcoming dynamic-forms substrate ADR | Reference — `Coding` primitive's storage is `TaxonomyClassification` |
| Cluster | Property-ops cluster (workstreams #16-#30) | Equipment classes / vendor specialties / deficiency categories all consume taxonomies via `TaxonomyClassification` |

---

## Acceptance Criteria

- [ ] New ADR drafted, council-reviewed, accepted
- [ ] `Sunfish.Foundation.Taxonomy` package scaffolded with `TaxonomyDefinition`, `TaxonomyNode`, `TaxonomyClassification`, derivation operations
- [ ] CRDT-backed tenant-owned taxonomy editing works (multi-maintainer offline-then-sync demonstrated)
- [ ] Snapshot publishing produces immutable versioned artifacts
- [ ] Lineage queries (walk to base; find mapped-from) return correct results across multi-derivation chains
- [ ] Vertical regime configuration applies per-tenant per-taxonomy
- [ ] Marketplace integration: at least one starter taxonomy (Property-Management Equipment Classes v1.0) publishes as a marketplace product
- [ ] Audit emission for all 6-8 lifecycle event types
- [ ] Forms substrate `Coding` primitive resolves through TaxonomyClassification at render time
- [ ] kitchen-sink demo: tenant clones the Property-Management Equipment Classes taxonomy, adds a "SmartThermostat" node, and classifies a piece of equipment using the derived taxonomy
- [ ] apps/docs entry covering substrate + governance model + civilian vs corporate regimes

---

## Open Questions

| ID | Question | Resolution path |
|---|---|---|
| OQ-T1 | **Semantic versioning scheme.** Major.Minor.Patch — what triggers each? Recommend: Major = breaking (node removal, code rename); Minor = additive (new nodes, new metadata fields); Patch = display-only (translation updates, description rewrites). | ADR; mirror common-sense semver for taxonomies |
| OQ-T2 | **Snapshot identity.** Should snapshots be content-addressed (hash of canonical bytes) or version-addressed (taxonomy_id + version)? Both have merits; lineage tracking benefits from content-addressing. | Stage 02 design |
| OQ-T3 | **Authoritative source identity.** For taxonomies published by external organizations (FHIR ValueSet, NAICS), how do we represent the publisher's identity? Adapter pattern? | Defer to standards-body integration phase (post-substrate-ship) |
| OQ-T4 | **Migration ergonomics.** When tenant Alters their derived taxonomy (move/split/merge nodes), how do existing classifications referencing the old node-IDs migrate? Mapping table + auto-update? Manual review? | Stage 02 design — recommend automatic with a "review changes before commit" step |
| OQ-T5 | **Marketplace billing posture.** Is the v1 marketplace free-only? Paid taxonomies (e.g., third-party regulatory taxonomies licensed for fee)? | CEO decision; default = free-only in v1; paid is Phase 2.x |
| OQ-T6 | **Cross-version classification queries.** "Find all equipment classified as a 'WaterHeater' regardless of which taxonomy version was current at classification time" — does the substrate support this natively, or does the consumer write equivalence-graph traversal? | Stage 03 design — recommend native via cross-version equivalence index |
| OQ-T7 | **Civilian default templates.** Which template taxonomies ship in v1? CEO mentioned "PMI-styled project management, business templates." For property management cluster v1, the load-bearing one is "Property-Management Equipment Classes" (water heater, HVAC, appliance, etc.). What else? | CEO directive; v1 = Property-Management Equipment Classes + Vendor Specialties + Inspection Deficiency Categories. Others on demand. |
| OQ-T8 | **Tree vs DAG vs forest.** Should taxonomies be strict trees (single parent per node), DAGs (multi-parent allowed), or forests (multiple roots)? CEO spec says "trees/graphs/tags" — implying flexibility. Recommend: tree as primary shape with optional `also_categorized_under: NodeId[]` for multi-axis cases. | Stage 02 design |
| OQ-T9 | **Free-form vs template — same data shape?** CEO spec says yes ("the platform still uses the same underlying object model"). Confirm in design: free-form taxonomy is just a tenant-owned taxonomy with `lineage = null`. | Stage 02 design — confirmed in spec |
| OQ-T10 | **Per-vertical governance configuration.** How does Sunfish know whether a tenant is "corporate/government" vs "civilian" for governance regime selection? Tenant tier flag? Bundle subscription? Per-tenant config? | CEO decision — recommend per-tenant config flag (`tenant.governance_tier ∈ {Civilian, Enterprise, Authoritative}`); defaults to Civilian. |

---

## Dependencies

**Blocked by:**
- ADR 0028 (CRDT engine) — accepted; substrate exists
- ADR 0049 (Audit Trail Substrate) — accepted (PR #190 + Tier 1 retrofit)
- ADR 0001 (Schema Registry Governance) — accepted
- ADR 0007 (Bundle Manifest Schema) — accepted
- Foundation.Recovery (workstream #15) — built (PR #223); needed for authoritative-source signatures
- Forthcoming dynamic-forms substrate ADR — must reference Taxonomy substrate as `Coding` primitive's underlying engine; sequence the ADRs together

**Blocks:**
- Property-ops cluster Equipment classification (cluster workstream #24 → uses Equipment Classes taxonomy)
- Vendor specialty classification (cluster workstream #18)
- Deficiency category classification (cluster workstream #25 Inspections)
- Phase 2 commercial intake's tax categorization (NAICS / IRS Schedule E categories)
- Future regulatory compliance work (FCRA / NACHA / state-specific landlord-tenant codes)

---

## Pipeline Variant Choice

`sunfish-feature-change` with mandatory new ADR. Foundation-tier substrate; affects multiple cluster modules; ADR is the architectural anchor. Stage 02 (architecture) + Stage 03 (package design) mandatory; Stage 04 (scaffolding) substantial.

---

## Estimated effort

| Phase | Work | Estimate |
|---|---|---|
| ADR drafting | "Taxonomy Management Substrate" with adversarial Stage 1.5 hardening | ~2 turns CTO |
| Substrate scaffolding | `Sunfish.Foundation.Taxonomy` package with entities + operations | ~3-5 days COB |
| CRDT integration | tenant-owned taxonomy editing via `kernel-crdt` | ~3-5 days COB |
| Snapshot publishing | authoritative-source publish-and-subscribe flow | ~2-3 days COB |
| Marketplace integration | bundle-based distribution; v1 starter taxonomy | ~3 days COB |
| Audit emission | 6-8 lifecycle event types | ~1-2 days COB |
| Lineage queries | walk-to-base; find-mapped-from; cross-version equivalence | ~2-3 days COB |
| Vertical regime config | per-tenant policy framework | ~2-3 days COB |
| Forms-substrate integration | `Coding` primitive resolves through TaxonomyClassification | ~1-2 days COB (after dynamic-forms ADR ships) |
| Documentation + apps/docs entry | Substrate README + governance guide | ~1 day CTO |
| **Total** | | **~3-5 weeks COB + ~2-3 turns CTO** |

Substantial. Slots into the dynamic-forms substrate ADR's broader 12-16 week MVP scope.

---

## Cross-references

- ADR 0001 (Schema Registry Governance), ADR 0007 (Bundle Manifest Schema), ADR 0028 (CRDT Engine), ADR 0049 (Audit Trail), ADR 0046 (Recovery — for authoritative-source signing)
- Forthcoming "Taxonomy Management Substrate" ADR
- [`oss-primitive-types-research-2026-04-29.md`](../../01_discovery/output/oss-primitive-types-research-2026-04-29.md) — `Coding` + `CodeableConcept` primitives consumed by this substrate
- [`contact-use-enum-upf-2026-04-29.md`](./contact-use-enum-upf-2026-04-29.md) — sibling intake (CEO directive same day)
- [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md) — cluster modules consume taxonomies for equipment classes / vendor specialties / deficiency categories

## Sign-off

CTO (research session) — 2026-04-29
