# ADR NNNN — <Short Title>

**Status:** Proposed | Accepted | Superseded by NNNN | Deprecated
**Date:** YYYY-MM-DD
**Resolves:** (optional — link to intake / discovery / open question this ADR resolves)

---

## Context

Why is this decision being made now? What problem does it solve? What are the relevant constraints? Keep this tight — three to five paragraphs.

---

## Decision drivers

- The forces shaping the decision (constraints, deadlines, priorities, quality attributes that matter)
- One bullet per driver; cite specifics (paper sections, prior ADRs, intakes)

---

## Considered options

### Option A — <name>

Summary. Pro / Con bullets. Verdict.

### Option B — <name>

Summary. Pro / Con bullets. Verdict.

### Option C — <name> [RECOMMENDED]

Summary. Pro / Con bullets. Verdict.

---

## Decision

**Adopt Option <X>.** State the decision crisply.

### Initial contract surface (when applicable)

```csharp
// Sketch of the public surface this ADR commits to
```

### Substrate / layering notes (when applicable)

How this decision composes with adjacent substrates (kernel, foundation, accelerators).

---

## Consequences

### Positive

- ...

### Negative

- ...

### Trust impact / Security & privacy (when applicable)

If the decision affects threat model, signature surface, capability scope, or data sensitivity boundaries — describe explicitly.

---

## Compatibility plan

How does this affect existing callers / consumers / packages? Migration path if any. Affected packages list.

---

## Implementation checklist

- [ ] Concrete tasks the implementer will execute
- [ ] One per row, observable / verifiable
- [ ] Use `[ ]` checkboxes so PRs can tick them off

---

## Open questions

Items not yet resolved that the implementer should flag if they hit. Each open question can become a follow-up ADR amendment or a separate intake.

---

## Revisit triggers

Conditions under which this ADR should be re-evaluated (e.g., "ADR 0004 algorithm-agility refactor begins" / "first regulated-SMB customer onboards" / "ADR 0028 substrate is replaced"). Without these, the ADR becomes a zombie (anti-pattern #11).

---

## References

### Predecessor and sister ADRs

- [ADR XXXX](./XXXX-...md) — what relationship

### Roadmap and specifications

- Paper §X.Y if applicable
- Roadmap entry if applicable
- Intake or discovery doc if applicable

### Existing code / substrates

- `packages/<...>/...cs` if applicable

### External

- RFC / standard / vendor doc if applicable

---

## Pre-acceptance audit (5-minute self-check)

Before flipping `Status:` to `Accepted`, run this checklist as the author. The cost is five minutes; the value is catching the most common Universal Planning anti-patterns before they ship.

- [ ] **AHA pass.** Considered ≥1 alternative simpler approach (Stage 0 Check 0.9). Documented why it was rejected. *(Anti-pattern #10: first idea remaining unchallenged.)*
- [ ] **FAILED conditions / kill triggers.** Named at least one condition under which this decision should be reversed or aborted. *(Anti-pattern #11: zombie projects with no kill criteria.)*
- [ ] **Rollback strategy.** What undoes this if it turns out wrong? At least one sentence. *(Anti-pattern #4: no rollback.)*
- [ ] **Confidence level.** HIGH / MEDIUM / LOW with one-line reason. Flags overconfidence early. *(Anti-pattern #13: confidence without evidence.)*
- [ ] **Anti-pattern scan.** Glanced at the 21-AP list in `.claude/rules/universal-planning.md`. None of the critical AP-1, AP-3, AP-9, AP-12, AP-21 apply.
- [ ] **Revisit triggers.** Named ≥1 condition under which this ADR should be re-evaluated. *(Anti-pattern #11 again — kill triggers without revisit triggers is a half-measure.)*
- [ ] **Cold Start Test.** Could a fresh contributor execute the implementation checklist from this ADR alone, without asking the author for clarification? If not, tighten the checklist. *(Stage 2 Check 5.)*
- [ ] **Sources cited.** Every load-bearing factual claim has a reference. *(Anti-pattern #21: assumed facts without sources.)*

If any of these are skipped, write a short justification in the ADR body or open a follow-up intake. Skipping the audit silently is itself an anti-pattern (#9: skipping Stage 0).

---

*This template enforces the lightweight Universal Planning Framework checks documented in [`.claude/rules/universal-planning.md`](../../.claude/rules/universal-planning.md). Copy this file when starting a new ADR; rename to `NNNN-<short-slug>.md` matching the next available number.*
