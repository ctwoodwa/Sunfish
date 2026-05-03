---
id: NNNN
title: <Short Title>
status: Proposed
date: YYYY-MM-DD
tier: foundation | kernel | ui-core | adapter | block | accelerator | governance | policy | tooling | process
pipeline_variant: sunfish-feature-change | sunfish-api-change | sunfish-scaffolding | sunfish-docs-change | sunfish-quality-control | sunfish-test-expansion | sunfish-gap-analysis

# Optional — controlled vocabulary; see _FRONTMATTER.md
concern:
  - <tag>

# Optional — capabilities this ADR enables (kebab-case identifiers)
enables: []

# Optional — relationships (integer ADR numbers)
composes: []
extends: []
supersedes: []
superseded_by: null
deprecated_in_favor_of: null

# Optional — empty for ADRs with no amendments
amendments: []
---

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
- [ ] **Cited-symbol verification.** Every `Sunfish.*` symbol cited in the Decision section + Implementation checklist + Compatibility plan + cross-package wiring sections has been verified to exist at the cited name + namespace. Symbols that don't exist are either (a) renamed to match reality, OR (b) explicitly marked "introduced by this ADR" + added to Implementation checklist, OR (c) flagged with halt-condition pointing at the ADR-amendment that will ship them. *(Pattern observed across 5-of-5 substrate ADRs in the 2026-04-29 cohort: pre-acceptance audit asserted AP-21 doesn't apply while council review consistently found cited-symbol drift as the dominant failure mode. See `feedback_verify_cited_symbols_before_adr_acceptance` user memory + helper script below.)*
- [ ] **Anti-pattern scan.** Glanced at the 21-AP list in `.claude/rules/universal-planning.md`. None of the critical AP-1, AP-3, AP-9, AP-12, AP-21 apply. *(AP-21 is only honestly checkable AFTER cited-symbol verification above.)*
- [ ] **Revisit triggers.** Named ≥1 condition under which this ADR should be re-evaluated. *(Anti-pattern #11 again — kill triggers without revisit triggers is a half-measure.)*
- [ ] **Cold Start Test.** Could a fresh contributor execute the implementation checklist from this ADR alone, without asking the author for clarification? If not, tighten the checklist. *(Stage 2 Check 5.)*
- [ ] **Sources cited.** Every load-bearing factual claim has a reference. *(Anti-pattern #21 part 2: assumed facts without sources. Distinct from cited-symbol verification — that catches code-shape drift; this catches policy / regulatory / external-claim drift.)*

If any of these are skipped, write a short justification in the ADR body or open a follow-up intake. Skipping the audit silently is itself an anti-pattern (#9: skipping Stage 0).

### Cited-symbol verification helper (run before checking off the box above)

```bash
ADR=docs/adrs/00NN-<your-slug>.md

# 1. Print all Sunfish.* symbols cited in the ADR
grep -oE "Sunfish\.[A-Z][A-Za-z0-9.]+" "$ADR" | sort -u

# 2. For each one, check whether the short name exists as a defined type or namespace
for sym in $(grep -oE "Sunfish\.[A-Z][A-Za-z0-9.]+" "$ADR" | sort -u); do
  short=$(echo "$sym" | grep -oE "[^.]+$")
  if ! git grep -q -E "(class|record|interface|enum|namespace) +$short" packages/; then
    echo "MISSING: $sym (short: $short) — fix before acceptance"
  fi
done
```

Anything in `MISSING` list is either (a) genuinely missing (treat as AP-21 hit), (b) introduced by this ADR (mark explicitly + add to Implementation checklist), or (c) cited under a wrong name (rename to match reality).

Also grep for **cross-ADR claims** like `"per ADR 0XYZ T2 boundary"` — open the cited ADR + verify the claim matches the cited section. Cross-ADR claims have failed verification 5-of-5 in the 2026-04-29 cohort; treat them as guilty-until-proven-innocent.

---

*This template enforces the lightweight Universal Planning Framework checks documented in [`.claude/rules/universal-planning.md`](../../.claude/rules/universal-planning.md). Copy this file when starting a new ADR; rename to `NNNN-<short-slug>.md` matching the next available number.*
