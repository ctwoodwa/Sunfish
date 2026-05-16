# Intake — WS-C/WS-D: ERPNext Native Integration Decision

**Date:** 2026-05-16
**From:** XO (research session)
**Status:** CO decision needed (defer until W#60 P3 ships)
**Related:** MASTER-PLAN Phase 2 WS-C (bank ingest) + WS-D (Stripe payments)

---

## Finding

The W#60 ERPNext composition pivot (approved 2026-05-11) changes the ownership of bank reconciliation and payment processing from Sunfish to ERPNext. This renders WS-C and WS-D workstreams as written in the 2026-04-27 Phase 2 intake potentially obsolete — but a CO decision is needed before closing them.

### WS-C — Bank ingest + Plaid reconciliation

**Original scope:** `providers-plaid` package in Sunfish; Plaid webhook → Sunfish Bridge → `blocks-accounting` bank transaction matching.

**ERPNext pivot impact:** ERPNext has a native bank reconciliation module. ERPNext supports bank feed import via:
1. Manual CSV upload (already usable by CO today)
2. Frappe Bank Integration (connects to open-banking APIs)
3. Third-party ERPNext apps for Plaid (community ecosystem)

**Recommended decision:**
- **Option A (SUPERSEDED):** Close WS-C; rely on ERPNext native bank feed. Sunfish React UI reads reconciled data from ERPNext API. Offline cache of recent statement data included in W#60 P3 Tauri SQLite store.
- **Option B (REDUCED):** Build a thin `providers-plaid` Sunfish package that routes Plaid tokens to ERPNext's bank import endpoint — 1-2 PRs, no new domain model. Only if ERPNext native bank feed has a gap (e.g., no Plaid support in CO's ERPNext version/region).

**XO recommendation:** Option A — defer final decision until W#60 P3 ships and CO can assess ERPNext bank reconciliation in the offline context. No Sunfish workstream unless ERPNext bank feed fails CO's needs.

---

### WS-D — Stripe payment forwarding

**Original scope:** `providers-stripe` package in Sunfish; Stripe webhook → Sunfish Bridge → `blocks-accounting` payment entry.

**ERPNext pivot impact:** ERPNext has a native Frappe Payment Gateway that includes Stripe. Stripe webhooks go directly to ERPNext:
- `POST /api/method/frappe.integrations.stripe.webhook` (ERPNext's built-in Stripe handler)
- ERPNext creates Payment Entry records automatically
- CO's React UI (W#60 P2) reads payment status from ERPNext via `GET /api/resource/Payment%20Entry`

**Recommended decision:**
- **Option A (SUPERSEDED):** Close WS-D; Stripe → ERPNext native. No Sunfish `providers-stripe` package needed. ADR 0051's "outbound-payment event forwarding" scope is met by ERPNext recording payments; Sunfish just displays them.
- **Option B (AUDIT ONLY):** If CO needs payment events in Sunfish's local event log for audit/offline reasons, build a thin Bridge webhook handler that mirrors Stripe `payment_intent.succeeded` events into the Sunfish audit trail (1 PR) alongside the ERPNext native integration.

**XO recommendation:** Option A — ERPNext natively handles Stripe. No Sunfish workstream unless audit log mirroring is required (and even then it's a 1-PR addition to W#60 P4 scope, not a separate workstream).

---

## CO action

After W#60 P3 (Tauri offline shell) ships and CO has validated the end-to-end offline experience:

1. **WS-C:** Does ERPNext bank reconciliation meet CO's needs? If yes → close WS-C. If no → scope a thin Plaid router (Option B).
2. **WS-D:** Does ERPNext native Stripe handle CO's payment tracking? If yes → close WS-D. If no → add audit mirroring to W#60 P4 scope.

No code work needed until CO confirms. XO will update MASTER-PLAN after CO decides.
