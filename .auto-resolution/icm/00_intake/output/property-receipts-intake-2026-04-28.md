# Intake Note — Receipts Domain Module

**Status:** `design-in-flight` — Stage 00 intake. **sunfish-PM: do not build against this intake until status flips to `ready-to-build`.**
**Status owner:** research session
**Date:** 2026-04-28
**Requestor:** Christopher Wood (BDFL)
**Spec source:** Multi-turn architectural conversation 2026-04-28 (turn 3 — receipt capture in the field).
**Pipeline variant:** `sunfish-feature-change`
**Parent:** [`property-ops-INDEX-intake-2026-04-28.md`](./property-ops-INDEX-intake-2026-04-28.md)

---

## Problem Statement

Property operations generate receipts continuously: vendor invoices, supply runs, service-call payments, capital purchases. Capturing them in the field — before they get lost in a glove compartment — is the difference between a defensible cost basis and "I think we paid about $3,500 for that water heater."

A receipt has multiple roles: cost-basis evidence (asset acquisition), payment evidence (vendor 1099 supporting documentation), expense categorization (P&L line item), tax-deduction evidence (tax advisor consumes annually). The same captured artifact threads through all of these.

This module provides the Receipt entity, the iOS capture flow (photo + OCR for amount/date/vendor extraction), and the cross-domain links (FK to Asset for cost-basis, FK to WorkOrder for completion artifact, FK to Payment via ADR 0051).

## Scope Statement

### In scope

1. **`Receipt` entity.** Tenant + (optional) FK Property + (optional) FK Asset (acquisition basis) + (optional) FK WorkOrder (completion artifact) + vendor_ref + amount + currency + transaction_date + capture_date + payment_method + category (depreciable / repair-and-maintenance / supplies / utilities / etc.) + photo_blob_ref + extracted_text_ocr + source (mobile-capture | email-attachment | manual-entry) + reconciliation_status (pending | matched | unmatched).
2. **`ReceiptLineItem` child entity** (optional, for itemized receipts where breakdown matters).
3. **`blocks-receipts` package.**
4. **iOS capture flow.** Photo capture + on-device OCR (Vision framework) + auto-fill amount/date/vendor + manual correction + categorization + asset/work-order link picker.
5. **Email-attachment ingestion path.** Receipt arrives as an email attachment → inbound parsing (per Messaging Substrate intake) → routed to receipts inbox → owner triages.
6. **Reconciliation hook.** Receipt → Payment matching (when ADR 0051 / Phase 2 commercial bank reconciliation lands).

### Out of scope

- Bank-line ↔ ledger pairing (full reconciliation) → Phase 2 commercial intake's `blocks-accounting` reconciliation deliverable
- Asset entity → [`property-assets-intake-2026-04-28.md`](./property-assets-intake-2026-04-28.md)
- Work Order entity → [`property-work-orders-intake-2026-04-28.md`](./property-work-orders-intake-2026-04-28.md)
- Vendor entity → [`property-vendors-intake-2026-04-28.md`](./property-vendors-intake-2026-04-28.md)

---

## Affected Sunfish Areas

- `blocks-receipts` (new)
- `foundation-persistence`, ADR 0015, ADR 0049
- iOS capture flow (depends on iOS App intake)
- ADR 0051 (Payments) for matching

## Acceptance Criteria

- [ ] Receipt entity defined; full XML doc + adapter parity
- [ ] iOS capture flow end-to-end with OCR auto-fill
- [ ] Email-attachment ingestion path operational (Messaging Substrate dependency)
- [ ] Asset/WorkOrder link picker on capture
- [ ] Reconciliation projection: receipt amount vs payment amount; mismatch flagging
- [ ] kitchen-sink demo: 5 receipts captured across 2 properties (mobile + email-attachment paths)
- [ ] apps/docs entry covering capture flows + categorization + cost-basis evidence

## Open Questions

| ID | Question | Resolution |
|---|---|---|
| OQ-R1 | OCR engine: iOS native Vision (free, on-device) vs cloud OCR (Textract, Google Vision)? | Stage 02 — Vision native (offline-first; free; sufficient quality for receipts) |
| OQ-R2 | Receipt deduplication (same receipt photographed twice, or photo + email-attachment of same receipt) | Stage 03 — content-hash + amount/date/vendor heuristic flagging |
| OQ-R3 | Multi-currency support | Stage 02 — capture currency field; defer FX conversion to reporting layer |
| OQ-R4 | Receipt retention policy (IRS keeps for 7 years; data-tenancy laws vary) | Stage 02 — default 10-year retention; per-tenant override |

## Dependencies

**Blocked by:** Properties, Assets, Vendors, Messaging Substrate, iOS App
**Blocks:** Phase 2 reconciliation, Phase 2.3 vendor 1099 reporting, Phase 2.3 tax-advisor export

## Cross-references

- Sibling intakes: Assets, Vendors, Work Orders, Messaging Substrate, iOS App
- ADR 0015, ADR 0049, ADR 0051 (Payments)

## Sign-off

Research session — 2026-04-28
