# ADR 0046 — Anchor Key-Loss Recovery Scheme (Phase 1) — UPF Audit

**Grade: B (Solid)**
**Top-line finding:** A well-reasoned sub-pattern selection ADR with clean four-option Stage-0 sparring, but it is structurally a *position* document — it lacks the Phases / Verification / Assumptions table UPF requires of a *plan*, and there is real implementation drift (the code lives in `packages/kernel-security/Recovery/`, not the `Foundation.Recovery` package the ADR and downstream ADR 0049 both name).

## Most Important Amendment

**Critical:** Reconcile the package-name drift. The ADR text speaks of `Sunfish.Foundation.Recovery` (echoed in ADR 0049 §Decision drivers and §Compatibility plan), but the shipped Phase-1 implementation is `Sunfish.Kernel.Security.Recovery` under `packages/kernel-security/Recovery/`. Either amend ADR 0046 with a "Package placement" section that ratifies the kernel-security home (and patch ADR 0049's references), or open an api-change-pipeline migration ticket to move the recovery package — silently letting the docs and code disagree on a security-critical primitive's tier (kernel vs foundation) is exactly the discovery-amnesia anti-pattern that bites future contributors.

## Stage 0 Findings (Discovery)

The author clearly executed **Better Alternatives** (4 named options A/B/C/D with explicit pros/cons and verdicts — the strongest UPF dimension here), **Existing Work** (cites the kernel-security substrate primitives Ed25519/X25519/SqlCipher/RootSeedProvider as load-bearing), and **Constraints** (P7 kill-trigger from `concept-index.yaml`, Phase 1 scope bound). **AHA Effect** is implicit and well-deployed — the reframe from "pick one sub-pattern" to "stack 4 sub-patterns so each leg covers a different failure mode" is exactly Check 0.9's intent. Gaps: no **Factual Verification** of the cited external patterns (Argent's 3-of-5 default is asserted; Apple iCloud Keychain's recovery primitive is named without a spec link; BIP-39 24-word claim is right but uncited), no **ROI / People Risk** analysis of the trustee-coordination UX cost (the Negative section names the risk but doesn't size it), and no **Official Docs** trail to BIP-39 or the relevant NIST guidance on threshold signatures.

## Stage 1 Findings (5 CORE)

1. **Context & Why:** Strong. Two paragraphs; pins the actual problem (P7 ownership + #48 unspecified sub-pattern combo for Phase 1). Within UPF spirit.
2. **Success Criteria + FAILED:** Partial — pass for criteria, fail for kill triggers. The P7 failed-conditions clause is *quoted* but the ADR itself never declares its own kill criteria (e.g., "this ADR is voided if a real recovery incident produces post-mortem evidence X"). Revisit triggers exist but are vague (e.g., "a real customer scenario hits the trustee-coordination problem") with no measurable threshold.
3. **Assumptions + VALIDATE BY + IMPACT IF WRONG:** Missing in form. Several load-bearing assumptions are baked into the prose without structured framing — e.g., "5 trustees with 3 quorum is the right tradeoff," "7-day grace period beats attacker timelines," "BIP-39 paper-key adoption will exceed historical crypto-wallet baselines because of clearer messaging," "trustee Ed25519 signing keys can be re-rotated without rebuilding quorum." Each deserves a row. The 7-day grace claim in particular is consequential and unsourced.
4. **Phases:** Absent. The ADR is a position decision without Phase 1 rollout phases, observable per-phase deliverables, or binary gates. UPF's "Phases" CORE section is essentially deferred to the Phase 1 G6 plan in `project_business_mvp_phase_1_progress` memory. For a Phase 1 in active multi-PR build (#176/#177/#178/#185), the absence of an in-ADR rollout sequence is the second-largest UPF gap.
5. **Verification:** Absent. No automated-test plan (the implementation has property tests `RecoveryCoordinatorTests`, `RecoveryDisputeTests`, `TrusteeRecordTests`, `PaperKeyDerivationTests` — but the ADR doesn't require them or define what they prove), no manual-review gate for the Trustee Setup wizard UX, no observability for "recovery initiated but never completed" stuck states. For a security-critical scheme this is a real omission.

**Conditional sections present:** Risk Assessment (Negative consequences), Revisit Triggers, References. **Missing but warranted given context:** Security & Privacy threat model (no explicit attacker model — what is "trustee-collusion attack" defending against, exactly?), Rollback Strategy (what if the social-recovery flow has a verifier bug post-ship?), Reference Library (Argent / iCloud Keychain / BIP-39 named without links), Legal & Compliance (audit-trail evidentiary claim is asserted; not analyzed).

## Stage 2 Findings (Anti-Patterns + Cold Start)

**Anti-patterns present:**
- **#19 Discovery amnesia** (critical) — the package home contradicts ADR 0049's downstream claim and the actual shipped code; auditor caught this only by inspecting `packages/kernel-security/Recovery/`. Future implementers must rediscover.
- **#11 Zombie projects (no kill criteria)** (major) — Revisit Triggers exist but are observation-driven, not measurable; nothing in the ADR can void it.
- **#3 Vague success criteria** (major) — "user can recover from forgotten password OR lost laptop OR lost trustees" is a list of capabilities, not testable acceptance criteria. No quantified UX target (e.g., "5-of-5 trustee setup completes in <10 min for 80% of users"), no observability target.
- **#13 Confidence without evidence** (minor) — "Argent / Apple-Keychain pattern alignment makes the UX familiar" is asserted; no usability research cited.
- **#21 Assumed facts without sources** (minor) — 7-day grace period, 3-of-5 quorum default, BIP-39 24-word — all stated as defaults without citation to the source pattern (Argent's actual default is 3-of-3 minimum; iCloud's recovery is contact-based with different math).
- **#1 Unvalidated assumptions** (minor) — paper-key adoption assumption is named in Negative but not validated.

**Anti-patterns avoided cleanly:** #10 (first idea was challenged — 4 named options with clean elimination), #5 (consequences extend past Decision into post-MVP extensibility), #12 (no specific dates).

**Cold Start Test:** A fresh agent told "implement ADR 0046" would correctly select sub-patterns 48a+48c+48e+48f, would correctly default to 3-of-5 + 7-day + BIP-39 24-word, and would correctly route audit events to a future Kernel.Audit. They would NOT know the package belongs in `kernel-security/Recovery/` rather than `foundation-recovery/` (the actual code disagrees with the doc), would NOT know what tests prove correctness, and would NOT have a defined attacker model to design against. **Verdict: passes for direction, fails for execution-readiness.**

## Amendments Table

| # | Severity | Amendment |
|---|---|---|
| 1 | Critical | Reconcile package home: either add a "Package placement" section ratifying `Sunfish.Kernel.Security.Recovery` (and patch ADR 0049's `Foundation.Recovery` references) or commit to a migration ADR. The doc/code mismatch is a security-critical landmine. |
| 2 | Major | Add structured Assumptions table (Assumption → VALIDATE BY → IMPACT IF WRONG) covering: 7-day grace beats attacker timelines, 3-of-5 quorum, paper-key adoption rate, trustee key-rotation independence, BIP-39 wordlist suffices vs custom wordlist. |
| 3 | Major | Add Phases section with binary gates per sub-pattern (48a coordinator landed → 48f audit-trail bound → 48e grace-period scheduler → 48c paper-key derivation → host integration). Reference G6 progress markers explicitly. |
| 4 | Major | Add explicit attacker model: trustee collusion, lost-device attacker holding old keys during grace, attestation-replay between recovery sessions, paper-key shoulder-surfing — and which sub-pattern defends against each. |
| 5 | Major | Add Verification subsection naming the property tests that prove correctness (chain-hash continuity, dispute window enforcement, quorum determinism, paper-key derivation determinism) plus production observability for stuck-state recoveries. |
| 6 | Major | Add measurable kill triggers, e.g., "this ADR is voided if a real recovery incident demonstrates X in post-mortem" — replace the vague "may need UX adjustment" wording. |
| 7 | Minor | Add Reference Library with links: Argent social-recovery spec, iCloud Keychain recovery whitepaper, BIP-39 spec, Vitalik's social-recovery essay. |
| 8 | Minor | Quantify UX targets: trustee setup completion time, paper-key generation completion rate, expected dispute-window false-positive rate. |
