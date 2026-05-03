# ADR 0053 (Work Order Domain Model) — Council Review

**Reviewer:** research session (adversarial council, UPF Stage 1.5)
**Date:** 2026-04-29
**Subject:** ADR 0053 v. 2026-04-28 + 3 same-day amendments (A1 Equipment rename, A2 state-machine composition, A3 affected-packages revision)
**Companion artifacts read:** ADR text; `packages/blocks-maintenance/Services/TransitionTable.cs`; `packages/blocks-maintenance/Services/InMemoryMaintenanceService.cs` (transition rules); `packages/blocks-maintenance/Models/WorkOrder.cs`; `packages/blocks-maintenance/Models/WorkOrderStatus.cs`; reconciliation review workstream #19.

---

## 1. Verdict

**Accept with amendments.** The architectural core is sound and the substrate composition (ADR 0028 lease, 0032 macaroon, 0049 audit, 0051 payments, 0052 messaging, 0054 signatures) is principled. But the A2 amendment ("compose existing TransitionTable") understates a non-trivial state-set conflict that, if left implicit, will fire at Stage 06 build and cost more than catching it now. Four targeted amendments below close the gap; none alter Option A.

---

## 2. Anti-pattern findings

| AP | Severity | Where it fires |
|---|---|---|
| **AP-1 Unvalidated assumption** | **High** | A2 asserts the cluster's 13 states "map onto" the existing 8-state enum, but the existing transition table has **5 states the ADR's diagram drops** (`Sent`, `Accepted`, `Scheduled`, `Cancelled` spelled with British "ll") and the ADR introduces **9 states the existing table doesn't have** (`Scoped`, `AssignedToVendor`, `AppointmentProposed`, `AppointmentConfirmed`, `AwaitingSignOff`, `Invoiced`, `Paid`, `Closed`, `Disputed`). Net union is ~17 states, not 13. The ADR does not name that delta. |
| **AP-3 Vague success criteria** | Med | Implementation checklist says "13-state table with valid transitions enumerated" but A2 says compose existing 8-state table. Which? Stage 06 implementer cannot tell from the ADR alone. |
| **AP-9 Skipping Stage 0 / first-idea-unchallenged** | Low | Option A vs B vs C is real triangulation; survives. |
| **AP-13 Confidence without evidence** | Med | "Cluster cost reduction: ~6–8 hours new-block scaffold avoided; replaced by ~3–4 hours of extension PRs" — the extension PRs include reconciling 5 dropped + 9 added states, renaming `Cancelled→Canceled` (or not), and migrating an `internal` `TransitionTable<TState>` (currently `internal sealed`) into a public/composable surface. 3–4h is optimistic. |
| **AP-18 Unverifiable gate** | Med | "subtype-coherence with existing audit record types verified" — no acceptance criterion for "coherent." |
| **AP-19 Missing tool fallbacks** | Low | CP-class lease unavailable path documented; appointment fallback to manual confirmation is not specified. |
| **AP-21 Assumed facts without sources** | Low | "~100ms typical for distributed lease acquire" — no source; should cite ADR 0028 measured latency or mark as estimate. |

No critical AP fires. AP-1 + AP-3 are the load-bearing ones; both flow from the same root cause (A2 underspecifies the state-set merge).

---

## 3. Top 3 risks

1. **State-set merge ambiguity (highest impact).** Existing `WorkOrderStatus` enum is 8 values: `Draft, Sent, Accepted, Scheduled, InProgress, OnHold, Completed, Cancelled`. ADR 0053 lists 13 + 3 side branches. There is no name-level overlap on 9 of the 13 (Scoped, AssignedToVendor, AppointmentProposed, AppointmentConfirmed, AwaitingSignOff, Invoiced, Paid, Closed, Disputed) and the existing `Sent`/`Accepted`/`Scheduled` map ambiguously to ADR's `AssignedToVendor`/`AppointmentProposed`/`AppointmentConfirmed`. Spelling: existing `Cancelled` (UK) vs ADR `Canceled` (US) is a breaking enum rename. Stage 06 will discover this; Stage 02 should resolve it. Impact: re-opening ADR mid-build, churn on 8 cluster intakes that depend on the state vocabulary.
2. **Existing `WorkOrder` record schema mismatch.** Existing `Models/WorkOrder.cs` is a positional record with `decimal EstimatedCost` / `decimal? ActualCost` — ADR adds `Money? TotalCost` (ADR 0051). Existing `MaintenanceRequestId RequestId` is the source FK; ADR replaces with polymorphic source via first audit event. Migrating positional → init-only with ~10 new fields and dropping `RequestId` is an api-change-shape modification, not a chore-class extension. ADR should flag this explicitly, ideally as an api-change pipeline variant.
3. **`TransitionTable<T>` is `internal sealed`.** A2 says "compose" the existing class. It cannot be composed across assemblies as-is; cluster's appointment-aware transitions live in extension code that can't reach `internal`. Either lift to `public` (small surface change but real), introduce an `IWorkOrderStateMachine` that wraps it (which the ADR already proposes — but then "composes" is wrong; it's "replaces with a new interface that delegates to a refactored guard"), or move the transition table into kernel. None of those is free.

---

## 4. Top 3 strengths

1. **CP/AP per record class is principled and paper-§6.3-faithful.** The lease scope `(VendorId, slot-window) ∧ (PropertyId, slot-window)` is exactly the right primitive for double-booking prevention; reusing ADR 0028's existing lease (vs reinventing) is correct composition.
2. **Polymorphic source via first audit event is defensible.** Rejecting nullable-FK proliferation + discriminator field in favor of "the source is the first event in the stream" aligns with ADR 0049 substrate semantics, supports new source types without schema migration, and preserves provenance forever. The slight read-path indirection is the right trade.
3. **Macaroon-based vendor magic-link with capability scope `(VendorId, WorkOrderId, {view, status-update, photo-upload, sign-off})` + 7d TTL + revoke-on-Close** is exactly what ADR 0032 was built for. No bespoke vendor-auth, no identity-bearing token leakage. Threat model per ADR 0043 holds.

---

## 5. Required amendments (Accept-with-amendments)

- **A4 (state-set reconciliation table).** Add a sub-section under A2 enumerating the 8 existing states + 13 ADR states + 3 side branches in a single table; for each, mark `KEEP / REPLACE-WITH-X / NEW`. Resolve `Cancelled`↔`Canceled` spelling explicitly (recommend keep `Cancelled` to avoid a breaking rename; note US-spelling preference is cosmetic).
- **A5 (TransitionTable visibility).** Document the `internal sealed` → `public` (or kernel-relocated) move as part of the cluster work, with a one-line rationale. Mark this as a *tiny* api-change (the type was `internal`, so no public surface breaks).
- **A6 (WorkOrder record migration is api-change-shape).** State explicitly that the existing positional record's field set changes (drop `RequestId`, replace `decimal EstimatedCost` with `Money? TotalCost`, add ~10 init-only fields). Rebadge the cluster integration commit as an api-change pipeline variant for this package even though it's pre-v1.
- **A7 (effort estimate honesty).** Replace "~3–4 hours of extension PRs" with a more grounded figure that accounts for state-set reconciliation, record migration, transition-table refactor, audit-emission threading, and 11 new audit-record types. Suggested: "~12–18 hours; primary risk is state-set reconciliation."

Optional but encouraged:
- **A8 (lease-unavailable UX fallback).** One sentence on what the user sees when CP-lease can't acquire — "scheduler shows 'slot unavailable, retry or pick another'." Closes AP-19.
- **A9 (audit-coherence acceptance criterion).** Define "coherent with ADR 0049 vocabulary" as: "uses existing `IAuditRecord` + `AuditCorrelation` types; new record subtypes named `WorkOrder*` per existing convention; verified by running the kernel-audit conformance test against the new types." Closes AP-18.

---

## 6. Quality rubric grade

**B (Solid), borderline-A.**

Rationale:
- All 5 CORE sections present (Context, Decision drivers, Considered options, Decision, Consequences). ✓
- Multiple CONDITIONAL sections present (Compatibility plan, Open questions, Revisit triggers, References, Pre-acceptance audit). ✓
- Stage 0 evidence is real (three options triangulated, not first-idea). ✓
- FAILED conditions / kill triggers are concrete and externally observable. ✓
- Confidence Level stated (HIGH) — but I'd downgrade to MEDIUM given the AP-1 finding. ✓ structurally, ✗ calibratively.
- Cold Start Test stated — but a fresh contributor reading ADR + amendments **cannot** scaffold without first reading `TransitionTable.cs`. The ADR itself acknowledges this in A2 ("Stage 02 implementation must (1) read TransitionTable.cs"). That's an A→B downgrade: the ADR knows it isn't self-sufficient.
- Reference Library + Knowledge Capture present. ✓
- Replanning triggers explicit (7 named). ✓

To reach **A**, close A4 (reconciliation table makes the ADR self-sufficient) and recalibrate confidence.

---

## 7. Reviewer's bottom line for the CTO

ADR 0053 is doing the right architectural thing. The substrate composition is faithful to the paper; the CP/AP carving is correct; the audit-emission discipline is exactly the kind of coordination keystone the cluster needs. **The same-day amendments (A1, A2, A3) caught the right problem (don't introduce a new entity that already exists) but underspecified the merge.** Accept after A4–A7 land. Estimated rewrite cost: 1–2 hours of ADR editing, 0 code changes. Worth doing before flipping to Accepted because eight cluster intakes downstream depend on the state vocabulary being final.

If A4–A7 don't land within ~1 working day, the right move is **Reject and re-propose** rather than letting the ambiguity ship to Stage 06.
