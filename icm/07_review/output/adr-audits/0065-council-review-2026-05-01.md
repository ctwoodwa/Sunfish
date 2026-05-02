# Council Review — ADR 0065 (Wayfinder System + Standing Order Contract)

**Review date:** 2026-05-01
**Reviewer:** XO research session (in-thread; subagent stall pattern observed in this session — in-thread is more reliable)
**Review posture:** standard adversarial (6 perspectives) + WCAG/a11y + UPF v1.2 Stage 2 meta-validation + 21 anti-pattern scan
**Cohort batting average context:** 18-of-18 substrate amendments needed council fixes; structural-citation failure rate ~65% (improving from ~71% with §A0 discipline)

---

## Findings (10 total)

### F1 — Critical: §A0.1 wrongly states `Sunfish.Foundation.Capabilities.*` does not exist

**Perspective:** Skeptical Implementer (structural-citation correctness)
**Issue:** ADR §A0.1 says `Sunfish.Foundation.Capabilities.*` is "introduced by this ADR's Phase 1 build." Verification shows `packages/foundation/Capabilities/` exists with `Principal.cs`, `CapabilityAction.cs`, `CapabilityProof.cs`, `ICapabilityGraph.cs`, `InMemoryCapabilityGraph.cs`, `MutationResult.cs`, `CapabilityClosure.cs`, `CapabilityOp.cs`, `Resource.cs` — namespace `Sunfish.Foundation.Capabilities` (not a separate `foundation-capabilities` package; lives in the umbrella `foundation` package).
**Disposition:** Mechanical (Decision Discipline Rule 3 auto-accept). Update §A0.1 + §A0.2.
**Class:** A7-third-direction structural-citation failure that §A0 missed. Cohort batting average contributor.

### F2 — Major: Validation pipeline doesn't name the authority-check type

**Perspective:** Skeptical Implementer
**Issue:** §3 Validation pipeline `Authority = 300` line says "capability check via Sunfish.Foundation.Capabilities" but doesn't cite the specific type. Per existing surface, `ICapabilityGraph.HasCapability(Principal, CapabilityAction)` is the natural authority-check shape.
**Disposition:** Mechanical. Cite `ICapabilityGraph` explicitly.

### F3 — Minor: ICrdtEngine API not cited explicitly

**Perspective:** Pedantic Lawyer
**Issue:** §2 CRDT semantics says "materialized into a per-tenant Loro document under the document-key `wayfinder/standing-orders/{tenantId}`" — accurate but doesn't cite the actual API (`ICrdtEngine.CreateDocument(string documentId)` / `ICrdtEngine.OpenDocument(string documentId, ReadOnlyMemory<byte> snapshot)`).
**Disposition:** Mechanical. Add inline citation.

### F4 — Major: ADR 0009 amendment shape underspecified; Phase 5 estimate wrong

**Perspective:** The Manager + Devil's Advocate
**Issue:** Phase 5 says "Author ADR 0009 amendment (~1h)." That is structurally undersized — an ADR amendment authoring (per cohort precedent) is ~3-5h with council and ~5-8K words. Pulling it into W#42 also conflates substrate authoring with consumer authoring (the 5th-concept extension is a *consumer* of the contract this ADR defines; should be its own workstream). Additionally, the ADR doesn't specify *how* feature-management consumes Standing Orders (what API does ADR 0009 call into?).
**Disposition:** Non-mechanical (scoping decision). Recommendation: remove Phase 5 from W#42 implementation; file separate workstream row for ADR 0009 amendment authoring. Reduces W#42 to ~15-17h. CO confirmation needed.

### F5 — Major: Complex-JSON-schema accessibility unaddressed

**Perspective:** WCAG/a11y subagent
**Issue:** §7 says "form view is the accessible alternative" for the Monaco/CodeMirror JSON-edit view. But for *complex schemas* — array of nested records, polymorphic discriminators, conditional sub-schemas (JSON Schema `oneOf`/`anyOf`/`if`/`then`/`else`) — automatic form derivation from JSON Schema is non-trivial. The form view may degrade to a generic JSON tree-editor, which has its own a11y problems (deep keyboard navigation; SR announce-on-expand patterns). The ADR's "form view is canonical path" claim is brittle for complex shapes.
**Disposition:** Non-mechanical (additional contract specification). Recommendation: add §7.1 — "Complex-schema a11y fallback: when the form-view auto-derivation fails for a complex schema, the surface MUST present a chunked-disclosure structured form (each top-level path is a separate accessible region; nested objects expand under operator command, never auto-expand) and MUST announce structure changes via `aria-live='polite'`. The pure-JSON view remains available as escape hatch but is NOT the accessible-alternative." Defer concrete example to scaffolding stage.

### F6 — Minor: Rescission semantics for downstream-depended Standing Orders

**Perspective:** Pedantic Lawyer
**Issue:** §Decision and Phase 2 say "Reversible by `RescindAsync` within 30 days." But what about a Standing Order that has already been depended on (e.g., a license was generated under that policy; an operator was granted access; a payment was authorized)? Rescission must not *redact* the original event (audit immutability per ADR 0049). The contract should clarify: `RescindAsync` is itself a new event that nullifies the *future* effect of the rescinded order; it does NOT remove the audit record of the original issuance.
**Disposition:** Mechanical. Add clarifying sentence to §Decision §4 + §Consequences §Trust impact.

### F7 — Minor: Schema-registration analyzer mechanism underspecified

**Perspective:** Skeptical Implementer
**Issue:** Phase 3 says "Schema-registration analyzer: warn at build time on `AddSunfish*()` calls without `AtlasSchemaDescriptor`." But what mechanism — Roslyn analyzer? Source generator? Compile-time reflection? Roslyn is right answer (source-generator side-effects don't cleanly produce diagnostics).
**Disposition:** Mechanical. Specify "Roslyn analyzer (`Sunfish.Wayfinder.Analyzers` package; severity Warning)".

### F8 — Minor: Phase estimate optimistic per cohort precedent

**Perspective:** The Manager
**Issue:** ~16-18h across 6 phases averages ~2.5-3h per phase. Cohort precedent (W#34/W#40/W#41 substrates) ran 3-5h per phase consistently. Phase 3 (Atlas projector + search index + analyzer + 4 perf tests) is realistically 4-6h on its own.
**Disposition:** Mechanical. Update estimate range to "~18-25h" (more honest); split Phase 3 into 3a (projector + search basics) + 3b (analyzer + perf tests) if it ships in pieces.

### F9 — Minor: Search latency target may be optimistic in cold-projection state

**Perspective:** Devil's Advocate
**Issue:** "P95 ≤ 100ms over 10K settings" — JetBrains achieves this with compiled in-memory schemas. Sunfish's Atlas projector projects per-tenant from a CRDT log; cold-cache projection latency is unknown. The 100ms target may be unachievable under partition recovery + cold-projection.
**Disposition:** Mechanical. Restate as "P95 ≤ 200ms cold-projection; P95 ≤ 100ms with warm projection cache."

### F10 — Minor: EN 301 549 version unspecified

**Perspective:** Pedantic Lawyer
**Issue:** §References cites "EN 301 549" without version. Current normative is **v3.2.1 (2021)**.
**Disposition:** Mechanical. Update reference.

---

## UPF v1.2 Stage 2 — 7 meta-validation checks

| Check | Result | Note |
|---|---|---|
| 1. Delegation strategy clarity | PASS | W#42 hand-off authoring is sequenced post-CO acceptance; consumer ADRs (~0066/~0067/~0068) are separate workstreams. |
| 2. Research needs identification | PASS | §Open questions §6 names empirical research need (search latency under load) before Phase 3 close. |
| 3. Review gate placement | PASS | Pre-merge council canonical (this review); post-Phase-3 perf-tests gate; per-phase WCAG/a11y subagent gate. |
| 4. Anti-pattern scan (21 patterns) | See below | 3 hits (AP1 unvalidated P95 target → F9; AP14 wrong detail distribution → F4; AP16 hallucinated estimates → F8). All addressed. |
| 5. Cold Start Test | PASS | A fresh agent could read this ADR + W#34 discovery + intake and execute Phase 1 without further context. |
| 6. Plan Hygiene Protocol | PASS | Status: Proposed; review file authored; mechanical fixes applied; non-mechanical findings flagged for CO discretion. |
| 7. Discovery Consolidation Check | PASS | W#34 discovery referenced + cited at §Context, §1, §6. |

**21 anti-pattern scan:** 3 hits (AP1, AP14, AP16); all surfaced in F-findings; mechanical fixes applied for AP1 (F9) and AP16 (F8); non-mechanical for AP14 (F4) deferred to CO.

---

## Mechanical-fix list (Decision Discipline Rule 3 auto-accept)

Applied in this PR's amendment commit:

1. **F1**: §A0.1 + §A0.2 corrected — `Sunfish.Foundation.Capabilities.*` exists in `packages/foundation/Capabilities/`.
2. **F2**: §3 Validation pipeline cites `ICapabilityGraph.HasCapability(Principal, CapabilityAction)` for authority check.
3. **F3**: §2 CRDT semantics cites `ICrdtEngine.CreateDocument`/`OpenDocument` API.
4. **F6**: §4 Audit emission + §Trust impact clarify rescission semantics (rescind is a new event, not a redaction; audit records of original issuance are immutable).
5. **F7**: Phase 3 specifies Roslyn analyzer (`Sunfish.Wayfinder.Analyzers` package; severity Warning).
6. **F8**: Estimate updated to ~18-25h; Phase 3 split into 3a + 3b.
7. **F9**: Search-latency target restated as "P95 ≤ 200ms cold-projection; P95 ≤ 100ms warm".
8. **F10**: EN 301 549 cited with v3.2.1 (2021).

## Non-mechanical findings (CO discretion)

- **F4**: Phase 5 (ADR 0009 amendment) should be split into separate workstream. **Recommendation:** remove Phase 5 from W#42; file separate workstream row for ADR 0009 amendment authoring (~3-5h XO time + council). Awaiting CO sign-off.
- **F5**: Complex-schema a11y contract addition (§7.1 chunked-disclosure structured form). **Recommendation:** add as part of this PR's amendment. Awaiting CO sign-off.

---

## Cohort discipline log

- §A0 self-audit caught: 0-of-1 structural-citation failures (F1 missed by §A0; council found).
- Council caught: 1 structural-citation failure (F1) + 9 other findings.
- Cohort batting average updated: 19-of-19 substrate ADRs now needed council fixes (council remains canonical defense; §A0 alone insufficient — confirmed again).

## Verdict

**Mechanical fixes (F1, F2, F3, F6, F7, F8, F9, F10)** apply via Decision Discipline Rule 3. Push amendment commit to PR #479.

**Non-mechanical (F4, F5)** awaiting CO disposition before applying.

Council recommends: PR #479 is mergeable after mechanical-fix amendment lands AND CO disposes F4 + F5 (either applies per recommendation or chooses different path).
