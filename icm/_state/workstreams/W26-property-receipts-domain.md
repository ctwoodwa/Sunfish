---
sort_order: 25
number: 26
slug: property-receipts-domain
title: "Property-Receipts domain (cluster module)"
status: "design-in-flight"
status_cell: "`design-in-flight` (hand-off invalidated 2026-04-29 per ADR 0055/0056 reframe)"
owner: "research"
owner_cell: "research"
reference_cell: "`icm/_state/handoffs/property-receipts-stage06-handoff.md` (**REVOKED — do not implement**)"
---

## Notes

**Swept design-in-flight 2026-04-29 (CTO).** Original first-slice hand-off authored 2026-04-28 specified hardcoded C# types (`Receipt` + `ReceiptCategory` + `ReceiptLineItem` + `ReceiptSource` + `ReconciliationStatus`). Post-ADR 0055 (dynamic-forms substrate; PR #236) + ADR 0056 (Foundation.Taxonomy; PR #240) the architectural shape is wrong: receipts are admin-defined dynamic-form types backed by JSONB storage with schema-registry-driven shape, not hardcoded entities. Categories/sources/reconciliation-status become `TaxonomyClassification` references (per ADR 0056), not enums. Equipment FK rejoins as a `Coding` reference into the dynamic-forms substrate. **Sunfish-PM: do not implement the existing hand-off file** (kept for archival traceability; will be superseded when ADR 0055/0056 land + Phase 1 dynamic-forms scaffold lets a fresh hand-off be authored). Original first-slice scope (CRUD + audit + kitchen-sink seed + docs page) remains valid in shape; only the type-definition layer changes. New hand-off authoring is gated on ADR 0055 acceptance + ADR 0056 acceptance + dynamic-forms Phase 1 substrate landing.
