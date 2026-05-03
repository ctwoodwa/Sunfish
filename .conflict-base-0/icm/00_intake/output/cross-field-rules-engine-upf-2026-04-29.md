# UPF — Cross-Field Rules Engine for Dynamic Forms

**Stage:** 00 Intake / 01 Discovery hybrid (UPF Stages 0–2 driving an architectural choice)
**Status:** Research + recommendation complete; awaiting CEO sign-off on the rule-language choice
**Date:** 2026-04-29
**Author:** CTO (research session)
**Triggered by:** CEO directive 2026-04-29 — "Cross-field rules needs a universal planning session" (in the dynamic-forms primitives discussion).
**Resolves:** Open question from dynamic-forms discussion — how does Sunfish express conditional / computed / cross-field validation rules that travel as data, sync between devices, and execute on multiple platforms?
**Companion:** Provider research (PR #229), Permissions UPF (PR #230), OSS primitives research (PR #231), Contact use-enum UPF + Taxonomy substrate intake (PR #234); forthcoming dynamic-forms substrate ADR (final synthesis).

---

## Stage 0 — Discovery & Sparring

### Check 0.1 — Existing Work

Sunfish substrate that touches rule evaluation:

- **`blocks-businesscases`** — entitlement resolver with rule-evaluation infrastructure already; runs against bundle activations
- **`Foundation.FeatureManagement`** (ADR 0009) — `IFeatureProvider` evaluates feature-flag rules; OpenFeature-aligned
- **`Sunfish.Foundation.Rule.Engine.EventBridge`** — exists per package list; rule engine bridge for event-bus integration (haven't audited; flag to read)
- **`blocks-forms`** — `SunfishValidation` for field-level validation; doesn't currently support cross-field
- **JSON Schema validation (already adopted in OSS primitives research)** — handles type/required/range/regex/format
- **ADR 0028 (CRDT engine)** — rules-as-data sync via CRDT primitives

Sunfish is not greenfield on rule evaluation — three substrates already exist. The question is **which one extends to handle dynamic-forms cross-field rules**, or whether a new layer is warranted.

### Check 0.4 — Industry Research

Researched 14 systems / specifications across rule engines, validation libraries, and form-builders.

#### JSON Schema Draft 2020-12

Built-in conditional + dependent constructs:

```json
{
  "if": {"properties": {"state": {"const": "TX"}}},
  "then": {"properties": {"property_tax_field": {"const": null}}}
}
```

Plus `dependentSchemas` (apply schema if field present), `dependentRequired` (require fields if other fields present), `not` (negate).

- **Pro:** Standardized; tooling everywhere; integrates with existing JSON Schema validation
- **Pro:** Declarative; serializable; portable
- **Con:** Limited expressiveness — no arithmetic, no cross-record traversal, no computed values
- **Con:** Verbose for complex rules

#### JsonLogic

Minimal JSON-encoded rule language. ~50 operators. Designed for "rules that can travel between server and browser":

```jsonlogic
{
  "and": [
    {"<=": [{"var": "scheduled_date"}, {"var": "completed_date"}]},
    {"==": [{"var": "lease.status"}, "Active"]}
  ]
}
```

- **Pro:** Tiny spec (one page); easy to learn; ~10+ language interpreters (C#, JS, Python, Swift, Java, Go, Ruby, Rust)
- **Pro:** Composes with JSON Schema (rules referenced via `$ref` or stored alongside)
- **Pro:** Known stable; used by 1Password, Stripe internal, others
- **Con:** Lisp-style prefix syntax (operator first; arguments second) — not Excel-familiar
- **Con:** Limited entity-graph traversal; `var` accesses dotted paths but doesn't traverse references
- **Con:** No arithmetic for monetary/measurement compounds out-of-box (extensible but custom)

#### FHIRPath

Domain-specific path + expression language for FHIR. Designed for traversing entity graphs + expressing invariants:

```fhirpath
Patient.contact.where(relationship.coding.code = 'BP').name.given.first()
```

- **Pro:** Designed for graph traversal + invariants (the hard parts of cross-field rules)
- **Pro:** Strongly typed; integrates with FHIR's type system
- **Pro:** Mature; widely used in healthcare; .NET library exists
- **Con:** Designed for FHIR specifically; would need adaptation for Sunfish's type system
- **Con:** Less mainstream than JsonLogic; smaller community
- **Con:** More complex syntax for simple rules

#### Salesforce Formula Language

Excel-style formulas with Sunfish-relevant operators (logical, math, text, date):

```
IF(AND(NOT(ISBLANK(Equipment__c.WarrantyExpiry__c)), Equipment__c.WarrantyExpiry__c < TODAY()),
   "Warranty expired", "")
```

- **Pro:** Familiar to non-developer authors (Excel users get it instantly)
- **Pro:** Comprehensive operator set; well-documented
- **Pro:** Used in Salesforce Validation Rules (cross-field), Formula Fields (computed), and Workflow Rules
- **Con:** Proprietary (no formal spec); each clone slightly different
- **Con:** No cross-platform interpreter standard
- **Con:** Object-relational; tied to Salesforce metadata model

#### Microsoft Power Apps / Power Fx

Excel-like formula language; open-spec'd:

```powerfx
If(IsBlank(Equipment.WarrantyExpiry) || Equipment.WarrantyExpiry < Today(),
   Notify("Warranty expired"))
```

- **Pro:** Excel-familiar
- **Pro:** Microsoft open-sourced the spec + reference implementation
- **Pro:** Supports both formulas (computed) and validation rules
- **Con:** .NET-coupled (not yet broadly cross-platform interpreters)
- **Con:** Targeting Microsoft Power Platform; Sunfish is .NET but not Power-specific

#### SurveyJS Expressions

Lightweight conditional expressions for form questions:

```
{question1} contains "Yes" and {question2} > 5
```

- **Pro:** Tiny; designed for form-state evaluation
- **Pro:** JS interpreter ships with library; .NET interpreter possible
- **Con:** Limited operators; can't handle entity-graph traversal
- **Con:** SurveyJS-specific (not standard)

Used for `visibleIf`, `enableIf`, `requiredIf`, `setValueExpression`. Good model for *form rendering* conditions; not enough for full validation.

#### Pydantic (Python)

Validation library with cross-field validators via `@model_validator`:

```python
@model_validator(mode='after')
def check_dates(self) -> 'Booking':
    if self.start_date >= self.end_date:
        raise ValueError('end_date must be after start_date')
    return self
```

- **Pro:** Strongly typed; integrates with type system
- **Con:** Code-as-rule; doesn't sync as data
- **Con:** Python-specific (Sunfish is .NET)

#### Yup (JS validation)

Schema-builder library with `.when()` for conditional:

```js
yup.string().when('state', {is: 'TX', then: schema.notRequired(), otherwise: schema.required()})
```

- **Pro:** Familiar to JS/TS developers
- **Pro:** Code-driven; type-safe
- **Con:** Code-as-rule; doesn't sync as data
- **Con:** JS-specific

#### Casbin

RBAC/ABAC policy engine with declarative policy:

```casbin
p, vendor, work_order:[id], read
p, vendor, work_order:[id], update_status
g, alice, vendor
```

- **Pro:** Mature; cross-platform (Go, Java, Node, .NET, Python, Rust)
- **Pro:** Policy-as-data; storage-backed
- **Con:** Built for access control, not field validation
- **Con:** Adapter would be a stretch for cross-field rules

#### OPA (Open Policy Agent) + Rego

Policy engine; Rego is declarative logic-programming-style language:

```rego
allow {
  input.user.role == "vendor"
  input.action == "update_status"
  time.now_ns() < data.work_order[input.work_order_id].deadline
}
```

- **Pro:** Cloud-native standard; broadly adopted
- **Pro:** Powerful expressiveness
- **Con:** Heavy; introduces another moving part
- **Con:** Rego learning curve significant

#### Drools / Easy Rules / NRules

Java/JVM rule-engine ecosystem with classical RETE-network rule evaluation:

```java
when
  $w: WorkOrder(status == "Completed", $cost: totalCost > 5000)
then
  insert(new ApprovalRequired($w));
```

- **Pro:** Industry-grade; handles complex rule networks
- **Con:** Massive learning curve
- **Con:** JVM-centric (.NET ports exist but smaller community)
- **Con:** Code-as-rule (Java/.NET)

#### Schematron (XML-rule lang)

XML-document validation via XPath assertions:

```xml
<rule context="lease">
  <assert test="end_date > start_date">end_date must follow start_date</assert>
</rule>
```

- **Pro:** Mature standard
- **Con:** XML-centric; aging tech stack
- **Con:** Rare outside compliance/legal/healthcare-document spaces

#### Notion / Airtable Formulas

Spreadsheet-like formula language for computed properties:

```airtable
IF({Equipment Type}="Vehicle", {VIN Required}=TRUE, FALSE)
```

- **Pro:** Excel-familiar
- **Pro:** Used by ~100M+ users via Notion/Airtable
- **Con:** Proprietary; each platform's slightly different
- **Con:** Limited cross-record reasoning

#### Excel / Google Sheets Formulas

The granddaddy. Universal familiarity.

- **Pro:** Anyone has used it
- **Con:** Cell-relative semantics don't fit entity-record world

### Check 0.3 — Better Alternatives (the AHA effect)

Surveying the 14 sources surfaces **three orthogonal concerns Sunfish has been bundling under "cross-field rules":**

1. **Field-level conditional state** (visibility, required, read-only) — runs at form-render time; cheap; needs to be portable
2. **Cross-field validation** (e.g., `start < end`) — runs at form-submit; can't allow invalid persistence
3. **Computed fields** (e.g., `total = subtotal + tax`) — derived values; must update reactively as inputs change

**The AHA: these three need different mechanisms with different trade-offs, not one unified rule engine.**

#### Three-tier proposal

| Concern | Mechanism | Reasons |
|---|---|---|
| **Field-level conditional state** (visibility / required / readonly per field) | **Lightweight expression language (JSON Schema if/then/else + `dependentRequired`)** | Standard, portable, cheap to evaluate at render time, no arithmetic needed |
| **Cross-field validation** (multi-field invariants like date ranges, computed comparisons, cross-record references) | **JsonLogic with custom operators for entity-graph traversal** | Cross-platform interpreters exist; declarative; data-syncable; extensible for Sunfish's needs |
| **Computed fields** (derived values that update reactively) | **Excel-style formula language (Power Fx-aligned spec)** | Familiar to admin authors; well-spec'd; Microsoft has open-sourced reference implementation |

This three-tier separation matches how the industry actually solves this in practice — Salesforce has Validation Rules (tier 2) + Formula Fields (tier 3) + UI conditional logic (tier 1) as distinct features.

### Three-tier vs unified — six approaches

| # | Approach | Mechanism | Trade-offs |
|---|---|---|---|
| **A** | Three-tier (recommended) | JSON Schema if/then/else + JsonLogic + formula language | Right tool per concern; some learning surface; ~3 mental models |
| **B** | Unified JsonLogic | One language for all three tiers | Single mental model; weaker for computation + form-render |
| **C** | Unified formula language | Excel/Power Fx for all three tiers | Familiar; might be heavier than needed for simple visibility rules |
| **D** | Code-as-rule (C# delegates) | Imperative .NET methods | Most expressive; can't sync; can't run in browser/iOS |
| **E** | OPA + Rego | Policy engine for all rule types | Heavy; cloud-native culture; learning curve |
| **F** | FHIRPath-derived | Entity-graph-aware language | Strong for cross-record reasoning; weaker for computation |

### ROI Analysis

| Approach | Phase 1 build cost | Cross-platform | Author ergonomics | Long-term flexibility |
|---|---|---|---|---|
| **A — Three-tier** | Medium-high (need 3 interpreters, but each is small) | Excellent | Tier-appropriate; each tier is right tool | Excellent |
| B — Unified JsonLogic | Low (one interpreter) | Excellent | Awkward for computation | Medium |
| C — Unified formula | Medium (Power Fx requires .NET-only interpreter; cross-platform pending) | Limited (.NET-only short-term) | Excellent (Excel-familiar) | Good |
| D — Code-as-rule | Lowest (just C#) | Poor (no browser/iOS) | Good for devs; unusable for admins | Limited |
| E — OPA/Rego | Highest (engine + spec + adapter) | Excellent | Steep learning | Excellent (overkill) |
| F — FHIRPath | Medium-high | Limited (.NET + JS exist) | Steep | Strong for graph |

**ROI winner: Approach A (three-tier).** Higher initial build cost than B but each tier uses the right tool, simpler authoring, and the layers can evolve independently.

### Updates / Constraints / People Risk

- **Constraint:** Rules must travel as data (CRDT-syncable, cluster cross-device sync requirement)
- **Constraint:** Rules must execute in browser (Photino+Blazor / web), .NET (Anchor desktop), Swift (future iOS) — no .NET-only options
- **Constraint:** Admin-defined types in v1 means non-developer authors will write rules
- **People risk:** No dedicated rule-author; admin authors are also other-things-authors
- **People risk:** Excel-style formula language is the no-code sweet spot; tier 3 (computed fields) needs this regardless of tier 1/2 choice

---

## Stage 1 — The Plan

### 1.1 Context & Why (≤3 sentences)

Sunfish dynamic forms need cross-field rules — conditional visibility, multi-field validation, computed values — that travel as data, sync via CRDT, and run on web, .NET, and (eventually) Swift. Industry research surfaces 14 approaches; the AHA insight is that **three orthogonal concerns (form-render conditionals, validation, computed fields) need three different mechanisms** rather than one unified rule engine. **CTO recommends Approach A — three-tier with JSON Schema if/then/else + JsonLogic + Power-Fx-aligned formula language**, as the structurally cleanest answer for Sunfish's local-first, multi-platform, admin-authored-rules use case.

### 1.2 Success Criteria (with FAILED conditions)

**Success:**
- Tier 1 (form-render conditionals): JSON Schema `if/then/else` + `dependentRequired` + `dependentSchemas` evaluates per-field visibility / required / readonly at render time
- Tier 2 (cross-field validation): JsonLogic + Sunfish custom operators (`reference`, `path`, `compound`) handles invariants like `start < end`, `total = subtotal + tax`, `equipment.warranty.expires < today()`
- Tier 3 (computed fields): Power-Fx-aligned formula language for derived values, with .NET reference interpreter shipping in v1
- All 3 tiers' rule definitions serialize as JSON; CRDT-syncable; cross-platform
- Server-side validation re-runs all 3 tiers (don't trust client)
- Authoring UX provides syntax help for each tier
- Schema registry stores rule definitions alongside type definitions
- Rule changes audit-emit per ADR 0049

**FAILED conditions (kill triggers):**
- Tier 2 JsonLogic + custom operators expressively insufficient for >5% of property-management cases → escalate to FHIRPath (Approach F) or Power-Fx unification (Approach C)
- Tier 3 formula language can't ship cross-platform interpreter in v1 timeframe → defer Tier 3 to v2; tiers 1+2 launch first
- Three-tier learning surface overwhelms admin authors → consolidate to fewer tiers
- Browser-side performance regression — rule evaluation exceeds 50ms p95 per form load — pre-compile rules; cache per-tenant; if still bad, push to server-side eval

### 1.3 Assumptions & Validation

| Assumption | Validate by | Impact if wrong |
|---|---|---|
| Three concerns (visibility / validation / computation) are genuinely orthogonal | Sketch 10 representative property-management rule cases; categorize each into a tier; confirm none span multiple tiers | If they overlap significantly, unified approach wins |
| JsonLogic + 3-5 custom operators covers cross-field validation needs | Implement 5 representative validation rules in JsonLogic; assess expressiveness | If JsonLogic falls short, escalate to FHIRPath |
| Power Fx open-source reference implementation is cross-platform-feasible (browser via Blazor; iOS via cross-compile) | Investigate Microsoft.PowerFx NuGet package + browser viability | If .NET-only, defer Tier 3 OR pick formula alternative |
| Rule evaluation latency stays under 50ms p95 for typical form loads (10-30 fields with 5-15 rules) | Benchmark on representative form | If too slow, pre-compile rules + cache |
| Schema-registry storage of rule definitions composes with CRDT-sync | Verify with kernel-crdt substrate | If sync conflicts arise on rule updates, additional design |

### 1.4 Phases (binary gates)

**Phase 1 — Tier 1 (form-render conditionals).**
- JSON Schema 2020-12 if/then/else + dependentRequired + dependentSchemas adopted as standard
- Form rendering engine evaluates these at render time
- Authoring UX: schema editor exposes conditional construction (dropdown or inline JSON)
- PASS gate: 5+ representative form-render conditions work end-to-end (vendor-only fields hide for tenant; advanced section opens when checkbox set; field becomes required when state=TX)
- FAIL gate: JSON Schema's expressiveness insufficient for representative cases → unusual; defer to JsonLogic-based tier-1

**Phase 2 — Tier 2 (cross-field validation).**
- JsonLogic interpreter ships in `Sunfish.Foundation.RuleEngine` package (or extends existing rule-engine substrate)
- Custom operators added: `path` (entity-relative reference traversal); `compound` (Money/Period/etc. arithmetic); `today` / `now` / `tenant` / `actor` (context primitives)
- Server-side: validation runs through interpreter on submit
- Browser-side: same interpreter runs in Photino/web; via Blazor WebAssembly compilation OR JsonLogic's existing JS reference interpreter
- PASS gate: 8+ representative validation rules work cross-platform (date sequences; cross-entity invariants; conditional required; numeric arithmetic with Money compounds)
- FAIL gate: expressiveness insufficient; rules need full programming language → escalate to Approach F (FHIRPath) or C (Power Fx unified)

**Phase 3 — Tier 3 (computed fields / formulas).**
- Power Fx (or selected alternative) reference interpreter ships in `Sunfish.Foundation.Formulas` (separate from RuleEngine to keep concerns clean)
- Computed fields are first-class on schema definitions: `field_X.formula = "subtotal + (subtotal * tax_rate)"`
- Reactive evaluation: when an input changes, dependent computed fields update
- Server-side authoritative; client-side optimistic
- PASS gate: 5+ representative computed fields work (Money-compound math; date arithmetic; reference-traversal-based aggregations)
- FAIL gate: cross-platform interpreter not feasible in v1 timeframe → defer Tier 3 to v2; ship Tiers 1+2 only

**Phase 4 — Authoring UX integration.**
- Schema editor exposes per-tier authoring affordances:
  - Tier 1: visual condition builder ("show if X = Y")
  - Tier 2: rule-list editor with operator palette
  - Tier 3: formula bar (Excel-style with autocomplete)
- Inline syntax help; preview-evaluation against sample data
- PASS gate: BDFL can author 3 representative rules in <5 min total
- FAIL gate: authoring UX too complex; admin can't compose rules → simplify or revisit tier separation

**Phase 5 — Audit emission + observability.**
- Rule changes (create / edit / delete) emit to ADR 0049 audit substrate
- Rule evaluation failures (validation reject) emit with actor + record + rule id
- Authoring + execution audit-trail enables compliance defense
- PASS gate: representative audit events emit; rate-limited; searchable
- FAIL gate: audit emission breaks form-load performance

### 1.5 Verification

- **Automated:** unit tests per tier; integration tests for cross-tier interactions; cross-platform parity tests (.NET + JS interpreters produce identical results for same rule + same data)
- **Manual:** CEO walks through 3 representative scenarios per tier — vendor-portal field visibility (Tier 1); date-range invariant (Tier 2); equipment-life formula (Tier 3)
- **Ongoing observability:** rule-evaluation latency monitored per tier; rule-failure rate per tenant; audit emission verified

---

## Stage 1.5 — Adversarial Hardening

Six perspectives stress-test Approach A.

### Outside Observer

> "Three different rule languages? That's not 'a rule engine,' that's three rule engines. Why are you proposing complexity?"

Real concern. The three-tier proposal *is* more complex than one engine. The justification: the three concerns have genuinely different requirements (render-time speed vs validation expressiveness vs computational fluency), and the industry has converged on three separate mechanisms (Salesforce Validation Rules + Formula Fields + UI Conditionals) precisely because no single engine handles all three well. Unifying would either use the wrong tool for one tier (Tier 1 over-engineered with JsonLogic; Tier 3 under-served with JSON Schema) or pick a heavyweight that's wrong for everything (OPA/Drools).

That said, the three-tier complexity is reduced by the fact that **users typically only interact with ONE tier per task** — admin authoring a form uses Tier 3 formulas; admin authoring a validation uses Tier 2; admin authoring conditional visibility uses Tier 1. They don't need to learn all three at once.

### Pessimistic Risk Assessor

> "What's the worst-case failure mode? An admin author writes a rule that references a non-existent field?"

Each tier has type-checking surface:
- Tier 1: schema-registry-aware validator at authoring time; flags "this if-condition references a field that doesn't exist on this type"
- Tier 2: JsonLogic interpreter does runtime checks; bad references → error logged + form rejects with clear message
- Tier 3: formula language has variable-binding analysis at parse time; unresolvable references rejected before save

Worst-case: admin adds a field to type X; rule referencing that field's old name in type Y silently breaks. Mitigation: schema-registry change cascade emits "rule depends on X.field that was renamed; please review" notifications.

Plus: rule-evaluation failures are non-fatal at the user layer (form shows validation message; computed field shows "—" with tooltip); nothing crashes.

### Pedantic Lawyer

> "Compliance scenarios — FCRA adverse-action requires specific field-validation logic; right-of-entry requires 48-hour-elapsed checks. Will the engine support these?"

Yes:
- FCRA adverse-action: Tier 2 validation `if loan_decision = "decline" and decline_reason IN [credit_score, background_check] then notice_letter_required = true`
- Right-of-entry: Tier 2 `if appointment.scheduled_at - now() < 48h and not entry_notice.acknowledged then transition_to(blocked_by_compliance)`

Both work in JsonLogic. The harder case is **history-bound rules** — "must wait N days from notice" requires the rule to access prior audit-trail events. That's beyond standalone rule evaluation; needs a query-against-audit-log primitive, which JsonLogic could expose as a custom operator (`audit_event_count_since(work_order, "EntryNoticeSent", 48h) > 0`).

Recommend adding to Phase 2 PASS gate: "audit-substrate-query custom operator works for representative compliance rules."

### Skeptical Implementer

> "JsonLogic + custom operators sounds nice but every custom operator means writing parallel code in C# AND the browser-side interpreter. How does that stay in sync?"

Fair point. Mitigation:
- Define operators in a portable spec (e.g., as YAML/JSON definition with reference test cases)
- Use existing JsonLogic libraries' extension mechanism in each language
- CI runs cross-platform parity test suite — same operator + same input must produce same output in both .NET and JS interpreters
- Operator additions are rare events (probably <1/quarter once mature); the maintenance overhead is small

This is the "operator coverage" risk. Cap initial Phase 2 to ~10 custom operators (keep surface small).

### The Manager

> "BDFL wants to send a form to a plumber. Are we building too much again?"

For BDFL's immediate use cases (Tier 1 vendor portal visibility), Phase 1 alone suffices. For range validation (Tier 2 lease start < end), Phase 2 alone suffices. **For BDFL's MVP day 1, Phases 1 + 2 are enough.** Phase 3 (computed fields) becomes meaningful when reporting / accounting flows mature — Phase 2.2-2.3.

Aggressive scope cut for v1: **Phases 1 + 2 + 4 (authoring UX) + 5 (audit). Defer Phase 3 (computed fields) to Phase 2.2.** Reduces ~7 weeks → ~4-5 weeks.

CEO override welcome.

### Devil's Advocate

> "Why not just use C# delegates? Sunfish is a .NET project; the rules can be code; problem solved."

C# delegates fail on three counts:
1. **Don't sync as data.** Code can't be CRDT-synced; would require devs to deploy rule changes.
2. **Don't run in browser.** Photino+Blazor renders forms in a webview; needs in-browser rule eval.
3. **Don't run on iOS** (future). Swift/SwiftUI form host can't run .NET rules.

C# delegates work for the *internal* rules Sunfish-platform itself ships (Stage 06 hardcoded validation in shipped types), but admin-authored rules in v1 (per CEO directive) require data-as-rule. Code-as-rule fails the multi-platform requirement.

---

## Stage 2 — Meta-Validation

### Check 1 — Delegation strategy

Phases 1-2 are CTO + research-session work for spec design + interpreter selection. Phases 3 (formula tier) defers to v2 unless aggressive scope cut is rejected. Phase 4 (authoring UX) is COB after Phases 1-2 substrate ships. Phase 5 (audit) is COB.

### Check 2 — Research needs

14 sources researched. Sufficient.

### Check 3 — Review gate placement

CEO review at:
- End of Phase 1 (Tier 1 conditionals working)
- End of Phase 2 (Tier 2 validation working; cross-platform parity demonstrated)
- End of Phase 3 (if not deferred)
- End of Phase 4 (BDFL authors representative rules)
- End of Phase 5 (audit trail complete)

### Check 4 — Anti-pattern scan (21-AP list)

- AP-1: 5 named assumptions with validation steps ✓
- AP-2: 5 phases with binary gates ✓
- AP-3: 8 success criteria + 4 FAILED conditions ✓
- AP-4: rollback = revert tier(s); rollback to schema-only validation; never gets stuck ✓
- AP-9: Stage 0 with 14 industry sources ✓
- AP-10: AHA challenged unified-engine framing ✓
- AP-11: kill triggers explicit ✓
- AP-13: confidence calibrated; estimate honest ✓
- AP-19: existing rule-engine substrate explicitly cataloged + leveraged ✓
- AP-21: each industry-source claim cited ✓

**Critical APs: none fired.**

### Check 5 — Cold Start Test

Phase 1 (JSON Schema if/then/else) is well-known; fresh contributor can implement. Phase 2 (JsonLogic with custom operators) needs ~1 reading session on JsonLogic spec; manageable. Phase 3 (Power Fx) needs more learning; aligned with deferral. Acceptable.

### Check 6 — Plan Hygiene

- Three-tier separation documented as the load-bearing concept
- Phases sized realistically with binary gates
- Cross-references to existing substrate explicit

### Check 7 — Discovery Consolidation

Stage 0 → Stage 1 flows clearly:
- Industry research (Check 0.4) → 6-approach bake-off (Check 0.3 AHA) → ROI (Check 0.5) → recommendation
- Existing rule-engine substrate (Check 0.1) → Phase 2 reuses where possible
- Cross-platform constraint (Check 0.6) → eliminated C# delegates + Power Fx (until cross-platform)

---

## Quality rubric self-check

- C (Viable): All 5 CORE + ≥1 CONDITIONAL ✓
- B (Solid): C + Stage 0 + FAILED conditions + Confidence + Cold Start ✓
- A (Excellent): B + 6-perspective sparring + review gates + reference list + replanning triggers ✓

**Confidence: HIGH.** 14 industry sources researched; three-tier matches industry's empirical convergence; phases sized realistically.

**Replanning triggers:**
- Custom operator coverage exceeds 15-20 (means JsonLogic isn't expressive enough; FHIRPath or Power Fx unification warranted)
- Cross-platform interpreter parity drift in CI (one platform's eval diverges from another's; need stricter spec)
- Authoring UX rejected by BDFL on usability grounds (consolidate tiers)
- Compliance scenario requires audit-log query that JsonLogic can't express

---

## Decisions for CEO

1. **Adopt Approach A (three-tier)?** Default = yes; reasons in ROI analysis.
2. **Aggressive scope cut: Phases 1+2+4+5 in v1; defer Phase 3 (computed fields) to v2?** Default = yes (saves ~2-3 weeks; matches BDFL MVP needs); override = include Phase 3 in v1.
3. **Tier-1 mechanism: stick with JSON Schema if/then/else, or use JsonLogic for tier 1 too (collapsing to two-tier)?** Default = JSON Schema (standard, portable); override = JsonLogic if conditional logic gets complex.
4. **Tier-2 interpreter: JsonLogic primary + custom operators, or escalate to FHIRPath now?** Default = JsonLogic; override = FHIRPath if Phase 2 PASS gate cases reveal expressiveness gaps.
5. **Tier-3 formula language: Power Fx or alternative?** Default = Power Fx (Microsoft-open-sourced; reference impl available); override = pin alternative; or defer entire tier per question 2.
6. **Sequence:** integrate cross-field rules into the dynamic-forms substrate ADR (next turn — final synthesis), or ship as standalone rule-engine ADR? Default = bundle into dynamic-forms ADR (single coherent landing).

---

## Cross-references

- ADR 0009 (Foundation.FeatureManagement) — existing rule substrate; informs but doesn't replace
- `Sunfish.Foundation.Rule.Engine.EventBridge` (existing package; needs audit) — possible substrate to extend
- ADR 0028 (CRDT engine) — rule definitions sync via CRDT
- ADR 0049 (Audit Trail) — rule lifecycle + execution failures emit
- OSS primitives research (PR #231) — JSON Schema as validation core
- Permissions UPF (PR #230) — Approach F's "field-level annotations" tier integrates with cross-field rules at the field-condition level
- Contact use-enum UPF (PR #234) — overlapping concern (assignments could compose with rules)
- Forthcoming dynamic-forms substrate ADR — consumes this UPF as the rules-engine sub-system
- [`taxonomy-management-substrate-intake-2026-04-29.md`](./taxonomy-management-substrate-intake-2026-04-29.md) — sibling intake; rules can reference taxonomy nodes via `Coding`

## Sign-off

CTO (research session) — 2026-04-29
