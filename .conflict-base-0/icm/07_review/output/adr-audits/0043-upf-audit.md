# ADR 0043 UPF Audit — Unified Threat Model: Chain of Permissiveness

**Grade: A-** (high B floor, A on chain-naming + Revisit triggers; held below clean A by an under-specified delegation contract and one missing kill-trigger)

**Top finding:** ADR 0043 is a structurally excellent layered threat model — chain diagram, threat catalog T1-T5, prioritized HIGH/MEDIUM/LOW controls, seven Revisit triggers — but the cross-ADR threat-delegation pattern that downstream ADRs (e.g., 0049) already rely on is asserted by example in "What this ADR does NOT do" rather than codified as an explicit, indexable contract.

## Most-important amendment

Add a "Threat delegation contract" subsection naming the delegation rule (when a downstream ADR may say "audit-tier / crypto-tier / CRDT-tier threats are delegated to ADR X"), the registration mechanism (where the threat-tier index lives), and the explicit list of currently delegated tiers (crypto→0004, CRDT→0028, accelerator-deploy→0026/0031, audit-trail→0049). Without this, future security ADRs will reinvent the delegation phrasing case-by-case, the index will drift, and ADR 0049's claim that "0043 already practices cross-ADR threat delegation" will rest on a single negative-space paragraph rather than a contract.

## Stage 0 (Discovery & Sparring) findings

The ADR demonstrates strong Stage 0 hygiene: Option A/B/C/D analysis with explicit rejections, AHA-style discovery (the "chain is more permissive than worst-case-of-any-single-ADR" insight is exactly the framework's "fundamentally simpler approach" payoff), feasibility implicit in "shell function + PR template + workflow rewrite, no new infrastructure," and ROI explicit in "hours to write vs days plus reputational cost to recover." Existing-work check is solid (per-PR audit references #129/#130/#132/#133/#138 plus council review). Gaps: no explicit Factual Verification line for SLSA / OpenSSF Scorecard maturity claims, and the People Risk check (solo-maintainer fatigue, dispatcher-as-single-point-of-trust) is named in Decision Drivers but not separated from technical risk.

## Stage 1 — 5 CORE sections

**1. Context & Why** — Strong. The chain is named in 3 sentences in the Resolves block and elaborated with an ASCII diagram. Slightly long but the load-bearing diagram justifies it.

**2. Success Criteria** — Partial. HIGH/MEDIUM/LOW controls each have implicit success (the gap they close), but no measurable outcome (e.g., "median bypass-merge alerting latency < 5 min", "% of subagent PRs carrying valid audit-trail marker = 100"). No FAILED conditions or kill triggers per UPF Section 2 — the Revisit triggers are *posture-flip* triggers, not *kill* triggers. **Missing:** a kill trigger such as "if H1+H2+H3 are not shipped before the next high-velocity session, halt subagent dispatch."

**3. Assumptions & Validation** — Implicit only. Assumptions like "hardware-key 2FA is operationally relied upon" and "GITHUB_TOKEN scope is sufficient" are stated as TODAY mitigations but not in the UPF "Assumption → VALIDATE BY → IMPACT IF WRONG" form. Risk: reader cannot tell which TODAY claims are verified vs assumed.

**4. Phases** — Strong. HIGH (next session) / MEDIUM (2 weeks) / LOW (trigger-gated) is a clean phase structure with binary observable deliverables (kill-switch script exists, PR template field exists, SHA-pinned workflows). Each control has its own rationale and gap-closed mapping.

**5. Verification** — Weak on automated/ongoing observability. Manual review is implied (the council recommended this ADR). No automated verification on the controls themselves: no test asserts H2 marker presence, no monitor confirms M3 drift-detection runs, no dashboard tracks H3 circuit-breaker trip count. The 6-month forced re-read (Revisit #7) is good ongoing-observability discipline.

## Stage 2 — 7 checks + 21-AP scan

Cold Start Test: PASS — a fresh maintainer can read this and act. Anti-Pattern scan flags: **AP-3** (vague success criteria — see CORE-2 above), **AP-15** (premature precision — "default 30 minutes" for kill-switch flag has no calibration), **AP-21** (assumed facts without sources — "Hardware-key 2FA is assumed at the GitHub-account level" is exactly the pattern). Not flagged: AP-11 (zombie projects) — Revisit triggers are explicit; AP-1 (unvalidated assumptions) — assumptions are at least named, just not in UPF form. Discovery Consolidation Check: PASS — council review is referenced as the source. Plan Hygiene: PASS. Delegation Strategy: **partial** — delegation to other ADRs is the central feature but the contract is informal (see top finding).

## Amendments

| # | Severity | Amendment |
|---|---|---|
| 1 | Major | Add explicit "Threat delegation contract" subsection: registration mechanism, current delegated tiers (0004/0028/0026/0031/0049), and rule for new entries. Codifies what 0049 already assumes. |
| 2 | Major | Add measurable success criteria + kill trigger: e.g., "halt subagent dispatch if H1-H3 are not shipped before next high-velocity session"; quantify M3/M4 alerting latency. |
| 3 | Minor | Convert TODAY-mitigation claims (hardware-key 2FA, GITHUB_TOKEN scope, secret scanning) into UPF-form Assumptions table with VALIDATE BY + IMPACT IF WRONG. |
| 4 | Minor | Calibrate or footnote the "default 30 minutes" / "3 consecutive" / "50% over 6" thresholds — note they are starting heuristics, not evaluated values. |
| 5 | Minor | Add automated-verification line per HIGH control (test/lint/monitor that proves the control is live), distinct from the manual review gate. |
| 6 | Minor | Add a People Risk row separate from technical risk — solo-maintainer fatigue and dispatcher-as-single-trust-boundary deserve their own line. |
| 7 | Minor | Add T6 placeholder row reserving "audit-trail tampering" and pointing to ADR 0049, closing the loop the downstream ADR already opened. |
