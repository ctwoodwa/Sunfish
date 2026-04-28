# ADR 0021 UPF Audit — Document and Report Generation Pipeline

**Grade:** B (Solid)
**Top finding:** Rigorous on contract design and license rationale; lacks verification mechanics, FAILED conditions, and a Phase-2 extension protocol — gaps that matter now that `IQboExportWriter` / `IOfxExportWriter` extend the pattern.

## Most-important amendment

Add an "Extension protocol" subsection that pins the rules new format-writer contracts (per Phase 2: QBO, OFX, QFX, IIF) must follow to remain contract-compliant — namespace, semantic-content DTO style, no library-type leakage, default-adapter license bar (MIT/Apache-2.0, pure managed, no revenue gate), parity-test requirement. Without this, downstream ADRs (0051+ era contracts) will silently drift from 0021's intent because the policy is currently encoded only as five concrete interfaces, not as a generative rule.

## Stage 0 findings (Discovery)

Stage 0 work is mostly visible in Context: existing-work survey is comprehensive (PDFsharp/QuestPDF/NPOI/ClosedXML/Telerik/Syncfusion/Aspose/GemBox), feasibility is established (pattern proven in ADRs 0013/0014), license diligence is sharp (QuestPDF $1M gate, Apache-2.0 vs MIT distinguished). Missing: vendor URLs lack date stamps (license terms are versioned), no explicit Better Alternatives weigh-off (WeasyPrint, DinkToPdf, IronPDF) with reject rationale, no ROI/People-Risk analysis around the "commercial adapters are expected community contributions" delegation assumption. AHA candidate not engaged: a single HTML→all-formats pipeline could collapse PDF/DOCX/PPTX surface area (Playwright adapter hints at it).

## Stage 1 — CORE sections

**1. Context & Why** — Strong. Three realities frame the decision crisply. Slightly over the 3-sentence guideline but the domain warrants it; readable.

**2. Success Criteria** — Weak. No measurable outcomes ("zero revenue-gated dependencies in default deployment" is implicit but not stated as a verifiable criterion), no FAILED conditions, no kill triggers. What would invalidate this ADR? E.g., "if PDFsharp goes unmaintained for >18 months" or "if observable-equivalence parity proves infeasible across 3+ formats" — none stated.

**3. Assumptions & Validation** — Missing as a discrete section. Assumptions are embedded in prose ("commercial adapters are expected community contributions", "observable equivalence is acceptable", "PDFsharp+MigraDoc API is good enough for default") but lack the "Assumption → VALIDATE BY → IMPACT IF WRONG" structure. The community-contribution assumption is the highest-risk one and is unvalidated.

**4. Phases** — Absent. ADR doesn't specify rollout/migration phases. Given the in-flight `PlaywrightPdfExportWriter` rename and the need to promote `IPdfExportWriter` from Blazor adapter to Foundation, a phased plan (split → promote → ship default → ship opt-in) would harden the change. Consequence section's "Kitchen-sink must demonstrate the contract" hints at deliverables but isn't a binary-gate phase.

**5. Verification** — Weak. Parity-test pattern referenced via ADR 0014, but no automated tests named, no observable-equivalence fixture set defined, no ongoing observability story (how do we detect a default adapter falling behind in fidelity?). "Documented per-adapter known-differences is a follow-up deliverable" is acknowledged as debt without an owner or trigger.

## Stage 2 findings (Meta-validation + 21-AP scan)

**Delegation strategy:** "Expected community contributions" for commercial adapters is delegation-without-contract (AP #7) and blind-trust (AP #8). State the SLA offered to contribution authors and the fallback if no vendor adapter materializes.

**Anti-pattern hits:** AP #1 unvalidated assumptions (license terms, community contribution); AP #3 vague success criteria; AP #4 no rollback (promoting `IPdfExportWriter` to Foundation has no reverse plan); AP #11 zombie risk (no adapter staleness kill criteria); AP #18 unverifiable gates ("observably equivalent" without fixture); AP #21 assumed facts without dated sources.

**Cold-start test:** Fresh agent could implement a new *default* adapter from this ADR but could not confidently add `IQboExportWriter` (Phase 2 case) — extension protocol is implicit. Pass with caveat.

**Plan hygiene / discovery consolidation:** Well cross-referenced (0007/0013/0014, sustainability.md, compatibility-policy.md). Discovery consolidated in prose, not extractable as a checklist.

## Amendments

| # | Severity | Amendment |
|---|---|---|
| 1 | **Major** | Add "Extension protocol" rules so Phase 2 `IQboExportWriter` / `IOfxExportWriter` / `IQfxExportWriter` / `IIifExportWriter` ADRs (or implementations) inherit the no-leakage / pure-managed-default / parity-test bar generatively, not by analogy. |
| 2 | Major | Add explicit FAILED / kill-trigger conditions (e.g., default-adapter abandonment >18 months, parity infeasibility threshold, license-term change in QuestPDF). |
| 3 | Major | Reformat embedded assumptions into "Assumption → VALIDATE BY → IMPACT IF WRONG" table. Highlight community-contribution assumption as the riskiest. |
| 4 | Major | Add Verification subsection naming the parity-test fixture set, observable-equivalence assertions, and the trigger for cataloguing per-adapter known-differences. |
| 5 | Minor | Add phased rollout (split-from-Blazor → promote-to-Foundation → ship-defaults → ship-opt-ins) with binary gates. |
| 6 | Minor | Datestamp the QuestPDF license-terms reference (terms are versioned; readers two years out need to know what was true on 2026-04-20). |
| 7 | Minor | Add an "Alternatives considered and rejected" stub covering WeasyPrint/IronPDF/DinkToPdf and the HTML→all-formats AHA candidate. |
| 8 | Minor | Document maintainer expectations / SLA for community-contributed commercial adapters; specify what happens if no contributor materializes. |
