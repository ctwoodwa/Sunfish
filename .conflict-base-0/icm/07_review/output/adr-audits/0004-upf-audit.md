# ADR 0004 — Post-Quantum Signature Migration Plan — UPF Audit

**Grade: B (Solid)**
**Top-line finding:** A genuinely well-scoped position ADR that correctly identifies algorithm-agility (not PQ math) as the real v1 blocker; weakens against UPF only on FAILED-conditions, structured assumptions, and discovery consolidation with the format-v0 / ADR 0049 audit substrate.

## Most Important Amendment

**Major:** Add an explicit cross-reference to the format-v0 markers in the kernel-audit substrate (ADR 0049 / PR #190) and any other current consumers of `Signature.LengthInBytes == 64`, so the v1 algorithm-agility refactor inherits a complete blast-radius list rather than being rediscovered when the prerequisite PR opens. Also add a structured Assumptions table and FAILED / replanning triggers (e.g., "NIST withdraws ML-DSA," ".NET 10 ships without `System.Security.Cryptography` ML-DSA support," "cryptanalytic break against Dilithium published") so this ADR has kill criteria rather than drifting as a zombie position statement.

## Stage 0 Findings (Discovery)

The author clearly executed **Existing Work** (cites the shipped `Signature.cs` with line-accurate fact: 64-byte fixed-size, throws on mismatch), **Official Docs** (NIST FIPS 203/204/205 with DOI links, NSA CNSA 2.0), and **Factual Verification** (FIPS finalization date August 2024, NSA 2030 timeline, ML-DSA-65 sizes are correct). **AHA Effect** is implicitly hit — the central insight that "the prerequisite is the algorithm tag, not the PQ algorithm itself" is exactly the kind of reframing UPF's Check 0.9 rewards. Gaps: no explicit **Better Alternatives** comparison (e.g., why dual-sign vs. hybrid signatures à la X25519+Kyber-style composite, or vs. lazy upgrade with versioned envelope only), and no **ROI** sizing (storage cost of 50× signatures across event-sourced ledgers is quantifiable but not quantified).

## Stage 1 Findings (5 CORE)

1. **Context & Why:** Strong. Three-paragraph framing pins the actual problem (algorithm-locked `Signature` type) rather than the headline problem (PQ math). Within UPF's "max 3 sentences" guideline in spirit if not letter.
2. **Success Criteria + FAILED:** Partial. Phase 1/2/3 outcomes are observable, but no kill triggers, no replanning conditions, no "if X happens, this ADR is void." The 24-month transition window is stated as a minimum but with no upper bound and no condition for shortening or extending.
3. **Assumptions + VALIDATE BY + IMPACT IF WRONG:** Weak in form. Assumptions exist in prose (".NET 10+ is expected to provide..." / "NIST and NSA guidance does not require migration before 2030") but are not in the structured "Assumption → VALIDATE BY → IMPACT IF WRONG" format UPF requires. This is the single biggest UPF-form gap.
4. **Phases:** Good. Three named phases (opt-in / required / deprecated) with binary semantics (OR-logic vs AND-logic is explicit). Phase exit criteria are observable. Timeline is correctly labeled "illustrative" — avoids the timeline-fantasy anti-pattern.
5. **Verification:** Missing. No automated-test plan, no manual-review gates on the dual-sign rollout, and — most importantly — no ongoing observability for the dual-sign window (how does an operator see "X% of inbound events still Ed25519-only"?). For a multi-year transition this matters.

**Conditional sections present:** Risk Assessment (Negative/Trade-offs), Open Questions, References. **Missing but warranted:** Rollback Strategy (what if Phase 2 reveals a verifier bug after enforcement?), Reference Library (no link to .NET PQ tracking issues, BouncyCastle versions, or ADR 0049), Replanning Triggers, Security & Privacy detail on key rotation (called out as open question but not scoped).

## Stage 2 Findings (Anti-Patterns + Cold Start)

**Anti-patterns present:**
- **#3 Vague success criteria** (minor) — Phase 2's "minimum 24 months" has no defined ceiling and no event-driven exit condition.
- **#11 Zombie projects (no kill criteria)** (major) — no condition under which this ADR is superseded or withdrawn.
- **#13 Confidence without evidence** (minor) — ".NET 10+ is expected to provide first-class support" is sourced to nothing.
- **#19 Discovery amnesia** (major) — the format-v0 / ADR 0049 / kernel-audit dependency that the auditor was told is load-bearing is not mentioned. Future implementers must re-derive the consumer list.
- **#21 Assumed facts without sources** (minor) — "ML-DSA-65 is appropriate for government and commercial use" is reasonable but should cite NSA CNSA 2.0 mapping explicitly.

**Anti-patterns avoided cleanly:** #4 (rollback is implicit in dual-sign OR-logic), #12 (timeline labeled illustrative), #5 (does not end at deploy — defines deprecation tail).

**Cold Start Test:** A fresh agent told "implement ADR 0004" would correctly understand they cannot ship anything until the algorithm-agility refactor lands, would correctly default to ML-DSA-65, and would correctly stage dual-sign. They would NOT know which packages need updates (no consumer list), would NOT know what success looks like operationally (no observability), and would NOT know what would invalidate the plan. **Verdict: passes for direction, fails for execution-readiness.** Appropriate for an "Accepted" position ADR, borderline for one that claims to be a migration *plan*.

## Amendments Table

| # | Severity | Amendment |
|---|---|---|
| 1 | Major | Add Discovery Consolidation: cross-reference ADR 0049, kernel-audit format-v0 markers, and enumerate consumers of `Signature.LengthInBytes` / `Signature.FromBytes` so the v1 refactor inherits a complete blast-radius list. |
| 2 | Major | Add structured Assumptions table (Assumption → VALIDATE BY → IMPACT IF WRONG) covering: .NET 10 PQ support, NIST stability of ML-DSA, no cryptanalytic break, customer dual-sign storage tolerance. |
| 3 | Major | Add Replanning / Kill Triggers section: NIST withdrawal of ML-DSA, published cryptanalysis, .NET shipping without PQ APIs, NSA accelerating the 2030 timeline. |
| 4 | Minor | Add Verification subsection covering automated parity tests across algorithm tags, and operational observability of signature-algorithm mix during dual-sign window. |
| 5 | Minor | Quantify storage impact ("50× per event × N events/year × M-year retention = X GB") rather than "meaningful." |
| 6 | Minor | Define an upper bound or event-driven exit on the 24-month Phase 2 window. |
| 7 | Minor | Add Reference Library: dotnet/runtime tracking issue for ML-DSA, BouncyCastle.Cryptography minimum version, NSA CNSA 2.0 timeline page. |
