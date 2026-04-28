# ADR 0013 — UPF Audit

**ADR:** 0013 — Foundation.Integrations + Provider-Neutrality Policy
**Audited:** 2026-04-28
**Grade:** **B-** (solid architectural ADR; under-specified policy enforcement and missing kill/verification criteria keep it below A)
**Top finding:** Provider-neutrality policy is declared as "reviewers enforce in PRs" but has no analyzer, test, or CI gate — the policy is unenforceable as written, which is the load-bearing risk given downstream Phase 2 commercial work (Plaid, Stripe, SendGrid, Twilio) cites this ADR as the seam.

## Most-important amendment

Add an **Enforcement** section that converts the two provider-neutrality rules into mechanical gates: a Roslyn analyzer (or `BannedSymbols.txt`) that fails the build when `blocks-*` projects reference vendor SDK namespaces, plus a unit-test convention that asserts no `using Stripe;` / `using Twilio;` / etc. in domain-module assemblies. Without enforcement, "reviewers reject violations" is rule-12 timeline-fantasy applied to humans — at the next staffing change the rule degrades silently.

## Stage 0 findings (Discovery & Sparring)

Stage 0 is **partially evidenced**. Existing Work is well-handled — the ADR explicitly cites `Foundation.FeatureManagement.IFeatureProvider` (ADR 0009) as the prior-art seam being generalized, and reuses `ProviderCategory` from ADR 0007 rather than reinventing vocabulary. Better Alternatives is **absent**: no consideration of (a) skipping Foundation.Integrations and letting each `blocks-*` define its own seam, (b) adopting OpenFeature-style provider model directly (it's mentioned as a reference but not weighed), or (c) using an existing OSS abstraction like Steeltoe / MassTransit transports. Feasibility is implicit-only — the contracts are simple enough that feasibility is uncontroversial, but the ADR does not record that judgment. AHA-effect check is missing; a credible simpler approach exists ("don't ship a meta-package; let the first real adapter drive the abstraction") and is not refuted.

## Stage 1 — 5 CORE sections

1. **Context & Why** — **Pass.** Three-paragraph framing is clear: multiple domains need the same plumbing, FeatureManagement already proved the pattern, vendor-neutrality requires a seam. Above the 3-sentence target but appropriate for an ADR.
2. **Success Criteria** — **Fail.** No measurable outcomes and no FAILED conditions. There is no statement of what "this ADR worked" looks like (e.g., "first three provider adapters land without modifying contracts," "zero `using Stripe;` references appear in `blocks-*`"). No kill triggers — if the contracts prove insufficient at the first real Stripe integration, the ADR has no defined replan path.
3. **Assumptions & Validation** — **Fail.** Several embedded assumptions are unmarked: that opaque `byte[]` cursors are sufficient (vs. structured), that registration-order dispatch is acceptable (vs. priority/topology), that `IReadOnlyList<string>` capabilities are enough until 3+ adapters exist, that signature verification belongs in adapters not Foundation. None use the "Assumption → VALIDATE BY → IMPACT IF WRONG" form.
4. **Phases** — **N/A-ish but weak.** ADRs are decisions, not plans, so phase gates do not apply directly; however the Follow-ups list (5 items) is effectively a phase plan and lacks PASS/FAIL gates, ordering, or owners. Follow-up #2 (secrets-management ADR) is flagged as a hard prerequisite for the first credential-bearing integration but is not assigned a trigger.
5. **Verification** — **Fail.** No automated verification (analyzer / banned-symbols / architecture test), no manual review checklist, no observability story for `IProviderHealthCheck` consumers. The ADR ships `ProviderHealthStatus` enum but does not say where status is surfaced or how staleness is detected.

**Conditional sections present:** Consequences (Positive/Negative), Follow-ups, References, layering diagram. **Missing & relevant:** Rollback Strategy, Risk Assessment, Security & Privacy (only credential-handling boundary is implicit), Dependencies & Blockers (Follow-up #2 is a blocker but not labeled), Replanning triggers.

## Stage 2 — Meta-validation + 21-AP scan

- **Cold Start Test:** Marginal. A fresh contributor could implement a new adapter from the contract list, but would not know the policy is enforced socially-only, nor when capabilities should graduate from `IReadOnlyList<string>` to a taxonomy.
- **Review gate placement:** None defined.
- **Discovery Consolidation:** Adequate — references ADR 0007, 0009, OpenFeature, `Microsoft.Extensions.Http.Resilience`.

**Anti-patterns triggered:**
- **AP-1 Unvalidated assumptions** — opaque cursor bytes, registration-order dispatch, capability-strings.
- **AP-3 Vague success criteria** — no measurable outcomes.
- **AP-4 No rollback** — no statement of how to retire/replace contracts if first real integration breaks them.
- **AP-11 Zombie projects (no kill criteria)** — Follow-ups have no triggers; #2 (secrets ADR) could perpetually slip.
- **AP-18 Unverifiable gates** — provider-neutrality policy lacks a mechanical gate.
- **AP-21 Assumed facts without sources** — claim that `Microsoft.Extensions.Http.Resilience` is "already pinned" is correct (verified against `Directory.Packages.props` context) but unsourced inline.

## Amendments

| # | Severity | Amendment |
|---|---|---|
| 1 | **Critical** | Add **Enforcement** section: Roslyn analyzer or `BannedSymbols.txt` rejecting vendor namespaces in `blocks-*` and `foundation-*`; architecture-test asserting `Sunfish.Providers.*` is the only assembly-prefix that may reference vendor SDKs. |
| 2 | **Major** | Add **Success Criteria** with 2–3 measurable outcomes and explicit FAILED conditions (e.g., "if first real adapter requires breaking changes to `WebhookEventEnvelope`, replan contracts before second adapter ships"). |
| 3 | **Major** | Reformat embedded assumptions (opaque cursor bytes, registration-order dispatch, capability strings, signature verification location) into the "Assumption → VALIDATE BY → IMPACT IF WRONG" table. |
| 4 | **Major** | Add **Follow-up triggers**: Follow-up #2 (secrets ADR) blocks first credential-bearing integration; Follow-up #4 (capability taxonomy) triggers at third payment adapter. Without triggers these are zombies. |
| 5 | **Minor** | Add **Better Alternatives Considered**: (a) per-block seams, (b) OpenFeature-only, (c) defer until first adapter — with one-line rejection rationale each. |
| 6 | **Minor** | Add **Verification** subsection covering automated analyzer tests, manual reviewer checklist, and where `IProviderHealthCheck` results are surfaced (Bridge admin? logs? metrics?). |
| 7 | **Minor** | Document **Rollback Strategy**: contracts are pre-1.0; what's the deprecation path if a contract proves wrong (obsolete-and-replace vs. break-and-major-bump)? |

**Files referenced:**
- `/Users/christopherwood/Projects/Sunfish/docs/adrs/0013-foundation-integrations.md`
- `/Users/christopherwood/Projects/Sunfish/docs/adrs/0007-bundle-manifest-schema.md`
- `/Users/christopherwood/Projects/Sunfish/docs/adrs/0009-foundation-featuremanagement.md`
- `/Users/christopherwood/Projects/Sunfish/packages/foundation-integrations/` (implementation present, no `providers-*` packages yet)
