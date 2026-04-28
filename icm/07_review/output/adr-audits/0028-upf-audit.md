# ADR 0028 — CRDT Engine Selection — UPF Audit

**Grade: B (Solid)**
**Top finding:** The substrate-impl-insulation pattern is real and *validated by SPIKE-OUTCOME.md* (Loro deferred, YDotNet shipped without rippling through application code) — but the ADR itself was not amended after the substitution, leaving the recommendation text stale relative to the implemented reality.

---

## Most-important amendment

The ADR's Status header still reads "Accepted (2026-04-22) — Adopt Option B (Loro)" while the actual shipped backend is YDotNet (Option A). This is an *amendment in the file*, not just a downstream note: per UPF AP #19 (Discovery Amnesia), the ADR must record the realized decision so future readers do not re-litigate or assume Loro is in production.

---

## Stage 0 findings (Discovery & Sparring)

Stage 0 was performed to a high standard for an architectural ADR. **Existing Work** (Check 0.1) — prior `automerge-evaluation.md` is cited and its conclusion correctly carried forward. **Better Alternatives** (Check 0.3) — four candidates (Yjs, Loro, native .NET, sidecar) explicitly named with pro/con/license; first-idea-bias (AP #10) avoided. **Official Docs** (Check 0.4) — Loro and yrs repos cited; paper §9/§19 cited as the binding spec. **Feasibility** (Check 0.2) is the weak link: the ADR confidently picks Loro without surfacing that no maintained .NET binding exists — exactly what the spike then discovered. AP #21 (assumed facts without sources) and AP #1 (unvalidated assumptions) both apply: ".NET binding effort is comparable to yrs; we pay it once either way" is asserted without evidence and was falsified within a week. **AHA Effect** (Check 0.9) — the contract-isolation insight (`ICrdtDocument` wrapping the engine) is the simpler-approach win and is captured under "Compatibility plan."

---

## Stage 1 — per CORE section

1. **Context & Why** — Strong. Three sentences with paper citations; problem (CRDT growth as operational risk) is concrete.
2. **Success Criteria** — *Partial.* The Requirements list is measurable in spirit (binding, types, GC, encoding, license) but lacks **FAILED conditions / kill triggers / timeouts**. The 1-week spike has an implicit pass/fail but no explicit "if marshaling cost > X" or "if binding API surface < Y" thresholds. AP #3 (vague success criteria), AP #11 (zombie projects — no kill criteria) apply mildly.
3. **Assumptions & Validation** — *Implicit.* The ADR makes load-bearing assumptions (Loro .NET binding maturity; "comparable effort to yrs"; "ecosystem growth expected"; .NET 11 P/Invoke works on all three OSes) but does not use the "Assumption → VALIDATE BY → IMPACT IF WRONG" structure. The spike *did* validate them — but the ADR itself does not enumerate them. AP #1, AP #13 (confidence without evidence).
4. **Phases** — Adequate. Implementation checklist has scope-based tasks (spike → scaffold → property tests → stress tests → docs) and a binary fork (spike pass → Loro; spike fail → yrs). Gates are PASS/FAIL-shaped. No timelines beyond "1 week," which is appropriate per UPF "scope not hours" guidance.
5. **Verification** — Property-based tests at paper §15 Level 1 (convergence/idempotency/commutativity/monotonicity) and stress tests for growth are named. **Ongoing Observability is missing** — no production-monitoring story for CRDT document size, compaction efficacy, or GC behavior in deployed nodes. AP #5 (plan ending at deploy).

---

## Stage 2 — Meta-validation + AP scan

- **Delegation strategy** — Implicit (single-author ADR); fine for this artifact class.
- **Review gates** — Spike outcome explicitly gates Loro adoption; this is the strength of the ADR.
- **Cold Start Test** — A fresh agent reading the ADR alone would conclude Loro shipped. They would need to discover SPIKE-OUTCOME.md independently. **Fails Cold Start.**
- **Discovery Consolidation** — The spike's findings (YDotNet 0.6.0 client-ID divergence bug, uint32 mitigation, LoroCs API-gap blockers) are not back-propagated to the ADR. AP #19 (Discovery Amnesia).
- **AP scan hits:** #1 (unvalidated assumptions on binding maturity), #3 (no FAILED kill conditions), #5 (no observability post-deploy), #10 (first-idea — partial; alternatives WERE listed, but Loro recommendation was not stress-tested against binding-availability), #13 (confidence without evidence — "comparable effort"), #19 (Discovery Amnesia — ADR not updated post-spike), #21 (assumed facts on ecosystem growth without sources).
- **Strengths:** Compatibility plan (the `ICrdtDocument` contract) is the *load-bearing precedent* ADR 0049 inherits, and SPIKE-OUTCOME.md proves it works — engine swap from Loro→YDotNet did not ripple. This is the single most valuable thing the ADR established.

---

## Amendments

| # | Severity | Amendment |
|---|---|---|
| 1 | **Critical** | Update Status block + Decision section to reflect YDotNet-shipped reality. Add "Superseded-in-implementation: see SPIKE-OUTCOME.md (2026-04-22); Loro deferred, YDotNet shipped." Loro remains the long-term target per spike's revisit triggers. |
| 2 | **Major** | Add explicit Assumptions & Validation section in UPF format. At minimum: (a) "Loro has a maintained .NET binding" — VALIDATE BY spike — IMPACT: fall back to Yjs; (b) "P/Invoke marshaling cost is acceptable" — VALIDATE BY benchmark in spike — IMPACT: revisit sidecar; (c) "Library ecosystem will grow over 18 months" — VALIDATE BY quarterly review — IMPACT: re-evaluate. |
| 3 | **Major** | Add FAILED / kill-trigger conditions for the spike: explicit thresholds (binding API surface coverage %, marshaling latency budget, OS-platform coverage). Currently the spike's "fail" is undefined — only the actual run made it concrete. |
| 4 | **Major** | Add Ongoing Observability subsection: production telemetry for CRDT document size distribution, compaction-pass effectiveness, snapshot frequency, peer-divergence detection. Closes AP #5. |
| 5 | **Minor** | Cross-link this ADR from ADR 0049 and any other ADR that delegates to the substrate-impl-insulation precedent, so the inherited pattern is discoverable. |
| 6 | **Minor** | Document the YDotNet client-ID uint32 mitigation in the ADR (or link to SPIKE-OUTCOME.md §"Important finding") so the constraint is not lost in a non-ADR file. |
| 7 | **Minor** | Tighten "comparable effort" claim — replace with "binding effort is unknown until spike validates"; cite the spike's discovery that LoroCs API surface was insufficient. |

---

ADR 0028 audit complete; grade B; 7 amendments at 1 critical / 3 major / 3 minor. Findings at /Users/christopherwood/Projects/Sunfish/icm/07_review/output/adr-audits/0028-upf-audit.md.
