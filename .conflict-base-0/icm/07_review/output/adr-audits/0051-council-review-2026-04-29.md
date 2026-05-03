# ADR 0051 — Foundation.Integrations.Payments — Council Review

**Date:** 2026-04-29
**Reviewer:** Adversarial council (Stage 1.5 perspectives + 21-AP scan)
**Verdict:** **Accept with amendments**
**Quality grade:** **B (Solid)** — would lift to A with the four critical amendments below

---

## Top-line finding

The ADR is structurally well-shaped: it executes the Stage-0 sparring (three options, explicit rejections), holds the ADR 0013 line cleanly (Option C named and rejected), gets PCI SAQ-A discipline right *as a structural property* (no contract type carries PAN/CVV — compile-time impossible, not policy), and integrates with the audit substrate at the right tier. **However**, three load-bearing claims are under-specified or contradicted by the actual substrate code: (1) the ADR references `AuditCorrelation` as a parameter type that does not exist in `packages/kernel-audit/` — the real `AuditRecord` shape pairs `TenantId` with `AttestingSignatures`; (2) ADR 0004's algorithm-agility constraint that ADR 0049 explicitly flags as load-bearing for long-retention data classes is **not acknowledged anywhere** in this ADR, despite payment dispute trails being a canonical 7-year-retention class; (3) the `decimal` vs minor-units choice is decided on the basis of "match existing placeholder," which is the discovery-amnesia anti-pattern in miniature — the placeholder explicitly flagged itself as deferred work waiting for this very decision.

---

## Anti-pattern findings (which fired; severity)

| AP | Severity | Finding |
|---|---|---|
| **AP-19 Discovery amnesia** | **Critical** | ADR uses `AuditCorrelation` in 6 contract types but the kernel-audit substrate at `packages/kernel-audit/AuditRecord.cs` has no such type. The real shape is `(TenantId, EventId, OccurredAtUtc, EventTypeKey, PayloadJson, AttestingSignatures, …)`. Either `AuditCorrelation` is a new type this ADR is silently introducing (then say so + spec it) or the ADR has drifted from the substrate it claims to integrate with. |
| **AP-21 Assumed facts without sources** | **Critical** | ADR 0049 explicitly states *"Audit records are exactly the long-retention data class that needs algorithm-agility before format commitment"* and pins ADR 0004 as a hard prerequisite for any new audit-emitting subsystem. ADR 0051 emits 13 new audit record types and never mentions ADR 0004. Payment dispute records have a 7-year IRS-evidentiary retention horizon — this is exactly the class ADR 0049 warned about. |
| **AP-1 Unvalidated assumptions** | **Major** | OQ-P1 "decimal vs long minor-units" is decided in the body ("storing decimal directly … matches existing `Payment.Amount` field") on the basis of the placeholder field. The placeholder explicitly says rounding is deferred — invoking it as precedent is circular. Banking convention (long minor-units) avoids an entire class of decimal-rounding bugs that banker's-rounding mitigates but does not eliminate. |
| **AP-3 Vague success criteria** | **Major** | No measurable acceptance criteria. "Five Phase 2 consumers unblock" is a deliverable list, not a success metric. No SLO for charge round-trip latency, no error-budget for ACH-return reconciliation lag, no observability target for stuck `AwaitingScaChallenge` states. |
| **AP-18 Unverifiable gates** | **Major** | "PCI scope is structural, not policy" is asserted but there is no analyzer / banned-symbols rule that fails the build if a future contract addition introduces a `Pan` / `Cvv` / `CardNumber` / `Track2Data` field. ADR 0013's enforcement gate (workstream #14) covers vendor-namespace leakage, not PCI-data-shape leakage. The discipline is currently reviewer-enforced — same trap ADR 0013 just escaped. |
| **AP-11 Zombie projects (no kill criteria)** | Minor | Eight revisit triggers are named but none are measurable thresholds (e.g., "second provider abstraction-leak" has no definition of "leak"). |
| **AP-13 Confidence without evidence** | Minor | "HIGH" confidence in the self-audit is asserted on the grounds that "PCI SAQ-A discipline is well-understood industry pattern" — true at the policy level, but the claim that this *substrate* is structurally SAQ-A would benefit from a QSA or PCI-trained reviewer pass before acceptance. |

---

## Top 3 risks (highest impact first)

1. **`AuditCorrelation` type drift between ADR and substrate.** If sunfish-PM implements this ADR literally, they will discover at Stage-06 that the type doesn't exist and will either invent it ad-hoc (creating substrate-ADR drift identical to the ADR 0046 / `Foundation.Recovery` vs `Kernel.Security.Recovery` landmine the prior council audit flagged) or stall the workstream pending clarification. Cold-start test fails on this point.
2. **ADR 0004 algorithm-agility unhonored for a 7-year-retention class.** Payment dispute records written today against fixed Ed25519 `AttestingSignatures` will need migration when ADR 0004's dual-sign window opens. ADR 0049 *names* this exact scenario as the canonical case. ADR 0051 not acknowledging it means the substrate ships locked-in to a format ADR 0004 already commits to changing — guaranteed re-work cost.
3. **PCI scope discipline is structural at the contract layer but policy-only at the evolution layer.** No analyzer prevents a future PR from adding a `Pan` field to `PaymentMethodReference` or a webhook envelope. The "structural" claim only holds if the contract surface is frozen — which it explicitly isn't (forward-compat for multi-currency, recurring billing, etc. is a stated goal). Without a `BannedFieldNames` analyzer or PCI-shape architecture test, this regresses to the same "reviewers enforce" position ADR 0013 just abandoned.

---

## Top 3 strengths (what the ADR gets right)

1. **Stage-0 sparring is honest and Option C is held.** Three named options, explicit rejection rationale, Stripe-shape leakage (Option C) named *and* rejected with direct reference to ADR 0013's second rule. This is the cleanest provider-neutrality discipline in the recent ADR cohort.
2. **PCI SAQ-A as compile-time property is the right framing.** "No contract type carries PAN/CVV, therefore compile-time impossible to persist" is structurally stronger than policy-based scope claims. It's the right architectural shape even if the evolution-layer enforcement gap (risk #3) needs closing.
3. **State machine fidelity (ACH return + dispute + SCA challenge) at substrate tier.** Modeling NACHA R-codes, dispute response SLAs, and SCA challenge resume *as substrate concerns* — not adapter quirks — is the correct call. Future Adyen/Square adapters will pressure-test this, but the current shape will absorb provider variance without leaking into block code. `ScaChallengeAffordance` (AutoFollow / DeferToCaller / RejectIfRequired) is a well-considered three-way switch.

---

## Specific amendments (Accept-with-amendments)

| # | Severity | Amendment |
|---|---|---|
| 1 | **Critical** | Define `AuditCorrelation` explicitly: either (a) spec it as a new type this ADR introduces (with field list + nullability + relationship to `AuditRecord.EventId`) or (b) replace every `AuditCorrelation Audit` parameter with the actual `AuditRecord` shape from `packages/kernel-audit/AuditRecord.cs`. Cold-start contributors must not have to guess. |
| 2 | **Critical** | Add an "ADR 0004 algorithm-agility coupling" subsection acknowledging that the 13 payment audit record types inherit ADR 0049's pre-agility constraint. State explicitly whether payment audit records ship before or after the `Signature`-envelope refactor, and if before, the migration plan. |
| 3 | **Critical** | Add a `BannedFieldNames` / Roslyn analyzer rule (or architecture test) that fails the build if `Sunfish.Foundation.Integrations.Payments.*` types ever introduce a field named `Pan`, `Cvv`, `Cvv2`, `CardNumber`, `Track1Data`, `Track2Data`, or matching regex. Make the "structural PCI scope" claim mechanically enforceable, same pattern ADR 0013's workstream #14 used. |
| 4 | **Major** | Move OQ-P1 (decimal vs long minor-units) from "open question" to "decided" with a real rationale that does not invoke the placeholder field as precedent. If decimal stands, name the test that proves 1¢-accuracy across the full range of Phase 2 charge amounts ($1–$10,000) under banker's rounding. |
| 5 | **Major** | Add a Verification subsection: charge round-trip SLO, ACH-return reconciliation lag budget, stuck-`AwaitingScaChallenge` observability metric, idempotency-key collision detection. |
| 6 | **Major** | Convert OQ-P5 (ACH return monitoring window) and OQ-P6 (SCA challenge resume) from "Stage 02 to decide" to in-ADR decisions — both are load-bearing for substrate shape, not Stage-02 policy. 90-day NACHA window and challenge-expiry cron both need to be substrate contracts. |
| 7 | Minor | Add Reference Library links: NACHA R-code list (current), Stripe SCA flow doc, EU PSD2 RTS. |
| 8 | Minor | Quantify revisit triggers: e.g., "second provider abstraction-leak" → "Adyen integration requires a non-additive change to `IPaymentGateway` or `ChargeStatus`". |

---

## Quality rubric grade

**B (Solid).** Earns C trivially (5 CORE present, multiple CONDITIONAL sections, no AP-2/-4/-5 critical violations). Earns B via Stage-0 evidence + Cold Start Test + Confidence Level + Revisit Triggers + Rollback. Falls short of A on three counts: (i) AP-19 substrate-vocabulary drift (`AuditCorrelation`) is a critical Cold-Start failure mode; (ii) AP-21 ADR-0004 algorithm-agility coupling is omitted despite ADR 0049 naming it as load-bearing for new audit emitters; (iii) Verification section is absent — no SLOs, no observability targets, no analyzer for the structural PCI claim. The four critical-tier amendments are tractable (none require redesign, all are scope clarifications) and would lift this to A on a re-review.

**Recommendation to CTO:** Accept with amendments 1–4 mandatory before Stage-02 entry; amendments 5–8 may land during Stage-02. The three critical-severity items are not architectural objections — they are scope/vocabulary clarifications that close real Cold-Start landmines.
