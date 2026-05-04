# Intake — Ship's Office Content Aggregation Surface

**Date:** 2026-05-01
**Requestor:** XO research session (synthesis output of W#35 Ship Architecture discovery §5.6 + §8.7)
**Request:** New ADR specifying the Ship's Office content aggregation UI — cross-document-type browse + search + edit + version diff for the Scribe role.
**Pipeline variant:** `sunfish-feature-change`
**Stage:** 00 — pending CO promotion to active

---

## Problem Statement

The W#35 Ship Architecture discovery (§5.6) identifies Ship's Office as Partial coverage: per-document-type substrates exist (W#21 SignatureEnvelope built 2026-04-30; W#22 LeaseDocumentVersion built 2026-04-30; W#18 W9Document built 2026-04-30; ADR 0007 bundle manifests; ADRs 0055 + 0056 dynamic forms + taxonomies inform templates). What's missing: the cross-document-type aggregation surface that surfaces these as a unified Ship's Office where the Scribe operates.

## Predecessor

- **W#21** — kernel-signatures substrate (`SignatureEnvelope`)
- **W#22** — `LeaseDocumentVersion` append-only versioning + per-party signatures
- **W#18** — `W9Document` storage with EncryptedField TIN
- **ADR 0007** — bundle manifest schema
- **ADR 0055** — dynamic forms substrate
- **ADR 0056** — Foundation.Taxonomy
- **W#34 ~ADR 0067** — Atlas integration-config (provider config docs adjacent)

The per-document-type substrates are solid; the aggregation surface is the gap.

## Scope

- **Cross-document-type browse + search UI** — list all docs across W9 / leases / signatures / bundle manifests / templates / kitchen-sink seeds / apps/docs
- **Scribe role definition** + permission tuple (canonical role registration in Shared Design System ADR)
- **Template + bundle-manifest authoring UX** — composes ADR 0007 + ADR 0055
- **Apps/docs content editing surface** — markdown editor + preview + publish flow
- **Document-version diff UX** — composes Stripe-style diff-preview from W#34 §B.3
- **WCAG 2.2 AA conformance** — document-content surfaces (long-form reading, diff UX, tables) require dedicated review per W#35 §9.5; reading-mode toggle (font size / line spacing / serif-vs-sans); table-of-contents for long documents

## Industry prior-art

- DocuSign / Adobe Sign — document repository UX
- Google Drive / Notion — document browse + search
- GitHub PR diff view — version diff UX
- Confluence / Notion — markdown editing + preview

## Dependencies and Constraints

- **Hard prerequisite**: W#35 ~ADR Shared Design System
- **Soft prerequisite**: W#34 ~ADR 0065 (Wayfinder + Standing Order — Ship's Office docs may be referenced in Standing Orders)
- **Effort estimate**: medium (~10–14h)
- **Council review posture**: pre-merge canonical + WCAG/a11y subagent (document-content surfaces require dedicated review)

## Affected Areas

- ui-core: Ship's Office surface contract
- ui-adapters-blazor / ui-adapters-react: per-adapter rendering
- accelerators/anchor + accelerators/bridge: per-zone rendering (Bridge has more multi-tenant content; Anchor has tenant-local)

## Downstream Consumers

- All Sunfish admins (Scribe role)
- W#22 Leasing Pipeline — lease-document-version review surface
- W#21 signature workflow surface
- W#18 W9 document review surface
- Phase 2 commercial MVP — content management for tenants

## Next Steps

Promote to active workstream when CO confirms; proceed to Stage 01 Discovery after Shared Design System ADR lands.

## Cross-references

- Parent discovery: `icm/01_discovery/output/2026-05-01_ship-architecture.md` §5.6 + §8.7
