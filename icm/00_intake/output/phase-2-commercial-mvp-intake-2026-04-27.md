# Intake Note — Sunfish Business MVP Phase 2 (Commercial Operations Cycle)

**Date:** 2026-04-27
**Requestor:** Christopher Wood (BDFL) — first commercial-grade customer (own property-management business)
**Spec source:** Multi-turn architectural conversation, captured at `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_phase_2_commercial_scope.md`
**Pipeline variant:** `sunfish-feature-change`
**Predecessor phase:** Phase 1 (G1–G7) — tracked in `~/.claude/projects/-Users-christopherwood-Projects-Sunfish/memory/project_business_mvp_phase_1_progress.md`. G1–G6 mostly merged; G6 trustee orchestration in flight (PRs #178, #185); G7 conformance scan blocked on G6.

---

## Problem Statement

Phase 1 delivers the local-first substrate (Anchor + Bridge shells, sync, identity, key-loss recovery, conformance baseline). Phase 1 does NOT deliver an end-to-end commercial operations cycle that a real SMB customer can run their business on. The BDFL is positioned to be Sunfish's first commercial-grade customer — consolidating his actual property-management operation (4 property LLCs + 1 holding company + 1 property management company = 6 Sunfish tenants) off three existing SaaS tools (Wave Accounting, Rentler.com for lease holders, bank shared-access PDFs) onto Sunfish.

Phase 2 closes the gap from "Anchor opens and syncs" to "the BDFL can run his monthly cycle in Sunfish": rent collection, vendor payments, bank reconciliation, statement generation, audit-grade record-keeping, multi-actor delegation (BDFL + spouse co-ownership; bookkeeper for monthly reconciliation; tax advisor for annual prep), and consolidation of his existing fragmented toolchain.

The architectural primitives needed are largely already designed (ADRs 0004, 0008, 0013, 0021, 0028, 0031, 0032, 0046; `Foundation.Capabilities`, `Foundation.Macaroons`, `Foundation.PolicyEvaluator`, `Foundation.Reporting`, `Foundation.Integrations`). The work is scaffolding the audit-trail substrate (ADR 0049), shipping the first provider-adapters under ADR 0013's framework (zero `providers-*` packages exist today), extending blocks for accounting workflows, and orchestrating it all into a runnable monthly cycle.

Three signals make now the right moment:

1. **Phase 1 substrate is converging.** G1–G5 merged; G6 (`Foundation.Recovery` for primitive #48) in active flight; G7 conformance scan queued. The kernel + recovery + sync + identity primitives are stable enough to build commercial flows on top of.
2. **The architectural primitives Phase 2 needs already ship.** ADR 0013's `Foundation.Integrations`, ADR 0021's `Foundation.Reporting`, ADR 0028's `Kernel.Crdt`, ADR 0032's multi-team `TeamContext`, and the four-namespace authorization model documented in `packages/foundation/DECENTRALIZATION.md` are accepted and partially shipped.
3. **The first real-world customer exists.** The BDFL's own business is the canonical first-customer test of ADR 0046's recovery scheme, ADR 0032's multi-team Anchor, ADR 0013's provider-adapter pattern, and ADR 0008's multi-tenancy. Validates the design under real load before broader release.

---

## Scope Statement (Phase 2 only)

This intake covers the Phase 2 deliverables consolidated from a multi-turn architectural conversation (2026-04-27):

1. **ADR 0049 acceptance** — `Sunfish.Kernel.Audit` substrate (drafted, in review at `docs/adrs/0049-audit-trail-substrate.md`); load-bearing for G6 host integration AND every Phase 2 audit-emitting workstream
2. **ADR 0051** — `Foundation.Integrations.Payments` (payment-specific extensions to ADR 0013: money type, payment state machine, PCI scope discipline, refund authorization, ACH return handling, 3DS/SCA)
3. **ADR 0052** — outbound-messaging contracts (extension to `Foundation.Integrations`; today's contracts are inbound-webhook-only)
4. **First `providers-*` packages** (none exist today; Phase 2 ships the exemplars):
   - `providers-plaid` — banking transaction ingest (replaces bank shared-access PDF download workflow)
   - `providers-stripe` (or alt) — first payment processor (CC/debit/ACH for rent collection + vendor payment)
   - `providers-sendgrid` (or alt) — first outbound email (invoices + statements)
5. **Wave Accounting migration tool** — one-shot import (CSV / QBO format) into `blocks-accounting` per tenant; replaces existing Wave subscription
6. **`blocks-accounting` reconciliation workflow extension** — bank-line ↔ ledger-entry pairing, fuzzy matching, manual review queue
7. **Statement template + monthly job** — composes `Foundation.Reporting` (PDF) + outbound email (ADR 0052) + audit logging (ADR 0049); Quartz.NET behind a thin shim for scheduling (defers ADR 0053)
8. **Spouse co-ownership + 3-of-5 trustee setup** — configuration on existing primitives once `Foundation.Recovery` lands (gated on G6 host integration + ADR 0049 acceptance)
9. **Repair workflow basics** — 3-vendor quote → review → approve flow in `blocks-workflow`; capital-vs-expense classification + depreciation schedule in `blocks-tax-reporting`
10. **Tax-prep export-via-email** — annual one-shot file export from `blocks-tax-reporting` in industry-standard tax-software formats (QBO / QFX / OFX / IIF — exact set research-pending per Open Question 13) with CSV and PDF as universal fallbacks (already covered by ADR 0021's `ICsvExportWriter` and `IPdfExportWriter`); delivered via ADR 0052 outbound; audit-logged via ADR 0049's `IAuditTrail`. New format-writer contracts (`IQboExportWriter`, `IOfxExportWriter`, etc.) extend `Sunfish.Foundation.Reporting` per ADR 0021's pattern. Serves the tax advisor without requiring a portal; advisor consumes the export in their existing tax-prep software (ProSeries / Lacerte / UltraTax / Drake / ProSystem fx)
11. **Bookkeeper invitation flow + capability-driven UI feature trimming** — bookkeeper installs his own Anchor, receives a capability grant from BDFL, switches into BDFL's tenants with appropriately-trimmed feature set per ADR 0032's per-team plugin/block enablement

Phase 2 explicitly does NOT cover (deferred to Phase 3 or later):

- **Bridge lease-holder portal** — Rentler keeps running in Phase 2; lease-holder portal moves to Phase 3 (decided 2026-04-27 by BDFL)
- **ADR 0050 block-level REST + macaroon auth** — only needed when a portal lights up; no Phase 2 portal, no Phase 2 ADR 0050
- **ADR 0053 background-job scheduling** — Quartz behind a shim suffices for monthly statement jobs at this scale; revisit when scaling demands
- **`providers-twilio` SMS** — defer until lease-holder complaints about email-only reminders surface
- **BI / sales-readiness external API** — Phase 4+; no commercial customer demand in Phase 2 scope
- **Complex remodel project management** — `blocks-businesscases` extension if surfaced as a real need; basic repair workflow only in Phase 2

---

## Affected Sunfish Areas

Impact markers approximate; Stage 01 Discovery will refine.

| Area | Impact | Note |
|---|---|---|
| `packages/kernel-audit/` | **new** | Per ADR 0049 (proposed). Parallel to `Kernel.Ledger`, layered over kernel `IEventLog`. |
| `packages/foundation-recovery/` | **new** (Phase 1 carryover) | Scaffolds upon ADR 0046 + ADR 0049 acceptance; BDFL+spouse trustee setup is its first real consumer. |
| `packages/foundation-integrations/` | **affected** (heavy) | ADR 0052 adds outbound-messaging contracts; ADR 0051 adds payment-specific extensions. Today's contracts are inbound-webhook-only. |
| `packages/providers-plaid/` | **new** | First instance of ADR 0013's provider-adapter pattern. Banking transaction ingest. |
| `packages/providers-stripe/` (or alt) | **new** | First payment processor. PCI-scope-minimizing tokenization-first design. |
| `packages/providers-sendgrid/` (or alt) | **new** | First outbound email adapter. |
| `packages/blocks-accounting` | **affected** (heavy) | Reconciliation workflow extension (bank-line ↔ ledger-entry pairing, fuzzy matching, manual queue). Capital-vs-expense classification UX. Already has `Models/Payment.cs`, `Models/PaymentId.cs`. |
| `packages/blocks-rent-collection` | **affected** | Wire to `providers-stripe` for rent payment intake; wire to `providers-sendgrid` for invoice email. Block already has payment domain types. |
| `packages/foundation-reporting/` (per ADR 0021) | **affected** | New format-writer contracts (`IQboExportWriter` / `IOfxExportWriter` / `IQfxExportWriter` / `IIifExportWriter`) extend the existing reporting-pipeline pattern. Default implementations TBD per Open Question 13. |
| `packages/blocks-tax-reporting` | **affected** | Depreciation schedule support; tax-prep export endpoint emitting industry-standard formats with CSV + PDF fallbacks; audit binding via `IAuditTrail`. |
| `packages/blocks-workflow` | **affected** | 3-vendor quote → review → approve flow scaffold. |
| `packages/blocks-maintenance` | **possible** | Vendor record + invoice attachment + status timeline. May already cover most of this; verify in Discovery. |
| `tooling/migration-wave/` (new) | **new** | One-shot Wave Accounting CSV/QBO import tool. Not steady-state; lives under `tooling/`. |
| `accelerators/anchor/` | **affected** | 6-tenant workspace provisioning for the BDFL's setup; bookkeeper invitation UX; capability-driven UI feature trimming pattern. |
| `accelerators/bridge/` | **stable** (Phase 2) | Bridge work all deferred to Phase 3 per BDFL decision. |
| `Sunfish.slnx` | **affected** | New packages added: `kernel-audit`, `providers-plaid`, `providers-stripe`, `providers-sendgrid`, plus `tooling/migration-wave`. |
| `docs/adrs/` | **affected** | ADR 0049 acceptance; ADR 0051 + ADR 0052 drafts. |
| `docs/specifications/inverted-stack-package-roadmap.md` | **affected** | `Sunfish.Kernel.Audit` advances `book-committed` → `adr-accepted` → `scaffolded` → `shipped`. |

---

## Open Questions

1. **Money type representation.** Decimal precision, currency handling, rounding rules for `Foundation.Integrations.Payments`. `decimal` major-units or `long` minor-units? ISO 4217 currency codes or enum? Banker's rounding? Blocks ADR 0051. Discovery task.

2. **First payment processor selection.** Stripe is the natural choice given SMB audience + dominant DX + PCI scope reduction via Stripe Elements/Checkout. Confirm or override; the `providers-*` adapter is named accordingly. Architecture is vendor-neutral regardless.

3. **Multi-processor strategy posture.** Single processor (Stripe-only for Phase 2) or vendor-neutral from day one (ship contracts, defer all but one adapter)? Substrate-impl insulation discipline says the latter; cost matters. Recommend: ship one adapter under vendor-neutral contracts.

4. **PCI scope posture.** Tokenization-only (lowest scope, requires hosted forms) vs. server-side card capture (higher scope, more flexible)? For SMB this is almost always tokenization. Confirm.

5. **First email provider selection.** SendGrid, Postmark, Amazon SES — all viable. Postmark has the cleanest transactional-email DX; SendGrid has the broadest feature set; SES has the lowest cost. Discovery task; informs `providers-*` adapter.

6. **Per-tenant integration credentials.** Each LLC has its own bank account, possibly its own Stripe account, possibly its own email sender domain. ADR 0013's `CredentialsReference` is keyed per-provider. Verify whether the existing implementation handles `(tenant, provider)` composite keying; if not, a small ADR 0013 amendment may be needed.

7. **Capability-driven UI feature trimming.** The bookkeeper-via-Anchor model relies on UI surfaces respecting capability-graph queries. Verify whether existing blocks consistently check `ICapabilityGraph.QueryAsync` at component render time, or whether a `Foundation`-level UI-policy primitive is needed. May surface a `CONVENTIONS.md` deliverable rather than a new ADR.

8. **`AuditEventType` initial enum.** Per ADR 0049 open questions. Likely starts with: `KeyRecoveryInitiated`, `KeyRecoveryAttested`, `KeyRecoveryCompleted`, `CapabilityDelegated`, `CapabilityRevoked`, `PaymentAuthorized`, `PaymentCaptured`, `PaymentRefunded`, `BookkeeperAccess`, `TaxAdvisorAccess`, `IrsExportGenerated`. Discovery task.

9. **Reconciliation matching algorithm.** Exact-amount + date-window heuristic baseline; ML-assisted fuzzy match deferred. Confirm Phase 2 ships the heuristic; no ML.

10. **Wave Accounting export format.** Verify what Wave actually exports today (CSV per-account; QBO format option). Migration tool design depends on this. Discovery task.

11. **Holding-co consolidation reporting.** The 4 property LLCs roll up into the holding company for tax purposes. Does Phase 2 need a cross-tenant consolidation view, or does the BDFL handle this manually via export-and-aggregate? Likely manual in Phase 2 — flag for explicit decision.

12. **`AuditRecord` v0 → v1 migration plan.** Per ADR 0049 trust-impact, audit records are persisted as `v0` until ADR 0004's algorithm-agility refactor lands. Document the migration path before any audit data is considered forward-stable.

13. **Tax-prep export format research.** Industry-standard formats supported by tax-advisor software:
    - **QBO** — QuickBooks WebConnect (`.QBO`); OFX-based with Intuit identifiers; primary target for QuickBooks Desktop import
    - **QFX** — Quicken Financial Exchange; OFX-based; consumer-finance lineage
    - **OFX** — Open Financial Exchange (RFC-style spec, Intuit-led but open); broadly supported (Quicken, Banktivity, GnuCash, many tax tools)
    - **IIF** — Intuit Interchange Format; legacy plain-text; reads into QuickBooks Desktop
    - **CSV / PDF** — universal fallbacks (already shipped via ADR 0021)

    Specific format(s) to support in Phase 2 depends on the BDFL's tax advisor's tax-prep software (ProSeries, Lacerte, UltraTax, Drake, ProSystem fx). Recommend interviewing the advisor in Stage 01 Discovery to identify the canonical format. Architecture note: most QBO-importable tools accept OFX, so OFX as the primary format covers the broadest surface; QBO/QFX as closely-related variants are minor additions; IIF is legacy and probably skip unless the advisor specifically uses old QuickBooks Desktop. New format-writer contracts in `Sunfish.Foundation.Reporting` extend ADR 0021's pattern. Discovery task.

---

## Proposed First 3 Milestones

Phase 2 estimated 3–6 months solo at the BDFL's existing velocity. These are the first 3 internal milestones, in execution order:

### Milestone 1 — Audit substrate + payment/messaging ADRs (Weeks 1–3)

Output: ADR 0049 accepted; ADRs 0051 and 0052 drafted and accepted; `kernel-audit` scaffolded.

- Accept ADR 0049 (revised draft already in `docs/adrs/0049-audit-trail-substrate.md`); resolve open questions during the acceptance review
- Scaffold `packages/kernel-audit/` with `IAuditTrail` + `IAuditEventStream` + initial `AuditEventType` enum + `EventLogBackedAuditTrail` impl
- **Wire `IAuditTrail` into Phase 1 G6 host integration** (currently not started: "persist RecoveryEvents to per-tenant audit log") — this is the bridge between Phase 1 and Phase 2 and unblocks G7 conformance scan
- Draft ADR 0051 (`Foundation.Integrations.Payments`) — money type, payment state machine, PCI scope discipline, refund authorization, ACH return, 3DS/SCA
- Draft ADR 0052 (outbound-messaging contracts in `Foundation.Integrations`)
- Resolve Open Questions 1, 4, 6, 12 above (block ADR acceptance)

### Milestone 2 — First provider adapters + Wave migration (Weeks 4–8)

Output: `providers-plaid`, `providers-stripe` (or alt), `providers-sendgrid` (or alt), `tooling/migration-wave` shipped; first end-to-end flow runs against the BDFL's real (sandbox) accounts.

- Implement `providers-plaid` (banking ingest); wire to `blocks-accounting` per tenant; verify webhook idempotency + signature verification per ADR 0013
- Implement first payment processor adapter; wire to `blocks-rent-collection`; PCI-scope tokenization-only flow
- Implement first email adapter; wire to `blocks-rent-collection` + `blocks-accounting` for invoice + statement delivery
- Build `tooling/migration-wave` one-shot importer (CSV / QBO); land BDFL's actual Wave data in a sandbox tenant
- Smoke test: 1 sandbox tenant, real Plaid sandbox bank account, Stripe sandbox payment, SendGrid sandbox email — invoice round-trip through the system
- Resolve Open Questions 2, 3, 5, 9, 10 above

### Milestone 3 — Reconciliation + statements + spouse setup (Weeks 9–14)

Output: BDFL runs his actual monthly cycle (1 of his 4 properties) end-to-end on Sunfish; spouse co-ownership active; trustee setup complete.

- Reconciliation workflow extension in `blocks-accounting` — bank-line ↔ ledger-entry pairing, manual review queue for unmatched
- Statement template using `IPdfExportWriter` (PDFsharp+MigraDoc default per ADR 0021); monthly job via Quartz behind a thin shim
- Spouse principal provisioning (own Anchor install, own root keypair, capability grants on all 6 tenants)
- 3-of-5 trustee setup using `Foundation.Recovery` (after G6 lands): spouse + 4 designated trustees
- Paper-key generation + offline storage (BIP-39 phrase, joint safe deposit box)
- Bookkeeper invitation flow: bookkeeper installs Anchor, receives capability-trimmed access to BDFL's tenants
- Capability-driven UI feature trimming (Open Question 7) resolved — either confirmed implicit in existing blocks or formalized as a `CONVENTIONS.md`
- **Acceptance criterion:** "BDFL runs the monthly cycle for Property A in Sunfish — rent collection, bank reconciliation, statement generation, vendor payment — without touching Wave Accounting or his bank's PDF download portal" (Rentler stays for lease-holder side per Phase 2 scope)

After Milestone 3: subsequent milestones extend to Properties B–D, then holding co + management co; tax-advisor IRS export; repair workflow basics; depreciation schedules. These are addressed in subsequent stage artifacts, not pre-decided in this intake.

---

## Next Stage

Pending review of this intake by the BDFL.

If approved, proceed to **Stage 01 Discovery** — execute Milestone 1 above (ADR 0049 acceptance, `kernel-audit` scaffold, ADR 0051 + ADR 0052 drafting).

If revisions requested, update this intake in place and re-route.

---

## Coordination Notes

- This intake follows the `sunfish-feature-change` pipeline variant.
- Phase 2 builds on Phase 1 (G1–G7) without modifying it; Phase 1's G6 + G7 finish in parallel with Phase 2 Milestone 1.
- Memory references that drive this intake:
  - `project_phase_2_commercial_scope.md` — consolidated scope (the source for §Scope Statement)
  - `project_business_mvp_phase_1_progress.md` — Phase 1 progress (the source for §Predecessor phase)
  - `project_sunfish_reference_implementation.md` — Sunfish positioning context
- Cross-session needs filed via `icm/00_intake/` addressed to the responsible session.

---

## References

### Drafted artifacts produced in the originating conversation

- [ADR 0049](../../../docs/adrs/0049-audit-trail-substrate.md) — Audit-Trail Substrate (revised draft, status Proposed); load-bearing for Phase 2.

### Predecessor phase

- `project_business_mvp_phase_1_progress.md` — Phase 1 (G1–G7) progress; G6 trustee orchestration in flight.
- `icm/00_intake/output/business-mvp-phase-1-foundation-intake-2026-04-26.md` — Phase 1 originating intake (this intake's structural template).
- `icm/05_implementation-plan/output/business-mvp-phase-1-plan-2026-04-26.md` — Phase 1 implementation plan.

### Foundational ADRs Phase 2 builds on

- [ADR 0004](../../../docs/adrs/0004-post-quantum-signature-migration.md) — algorithm-agility migration (audit records depend on this for forward stability)
- [ADR 0008](../../../docs/adrs/0008-foundation-multitenancy.md) — `Foundation.MultiTenancy`
- [ADR 0013](../../../docs/adrs/0013-foundation-integrations.md) — `Foundation.Integrations` provider-adapter pattern (every Phase 2 provider builds on this)
- [ADR 0021](../../../docs/adrs/0021-reporting-pipeline-policy.md) — `Foundation.Reporting` document generation (statements, invoices, IRS export)
- [ADR 0028](../../../docs/adrs/0028-crdt-engine-selection.md) — substrate-impl insulation precedent
- [ADR 0031](../../../docs/adrs/0031-bridge-hybrid-multi-tenant-saas.md) — Bridge zone (Phase 3 work, deferred from Phase 2)
- [ADR 0032](../../../docs/adrs/0032-multi-team-anchor-workspace-switching.md) — multi-team Anchor (the BDFL's 6-tenant workspace switcher pattern)
- [ADR 0043](../../../docs/adrs/0043-unified-threat-model-public-oss-chain-of-permissiveness.md) — unified threat model (Phase 2 audit-tier delegation extends this)
- [ADR 0046](../../../docs/adrs/0046-key-loss-recovery-scheme-phase-1.md) — recovery scheme (Phase 2 spouse + trustee setup is the first real-customer test)

### Authorization architecture

- [`packages/foundation/DECENTRALIZATION.md`](../../../packages/foundation/DECENTRALIZATION.md) — four-namespace authorization model (Crypto, Capabilities, Macaroons, PolicyEvaluator) consumed throughout Phase 2.

### Existing block surfaces Phase 2 extends

- `packages/blocks-accounting/` — reconciliation workflow extension
- `packages/blocks-rent-collection/` — payment intake wiring; existing `Models/Payment.cs`
- `packages/blocks-tax-reporting/` — depreciation + IRS export
- `packages/blocks-workflow/` — 3-vendor quote approval flow
- `packages/blocks-maintenance/` — vendor record + invoice tracking (verify Discovery)

### Sunfish ICM

- `icm/CONTEXT.md` — pipeline overview
- `icm/_config/routing.md` — variant selection
- `icm/_config/deliverable-templates.md` — artifact standards
- `icm/pipelines/sunfish-feature-change/routing.md` — variant routing for this intake
