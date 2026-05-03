# Workstream #NN — <Subject> — Stage 06 hand-off

<!--
Sunfish Stage 06 hand-off template. Pattern proven across W#19 / W#20 /
W#21 / W#22 / W#27 / W#28 / W#30. Replace bracketed values; delete
sections that don't apply.

Authoring rules:
- Verify cited Sunfish.* symbols exist (per
  feedback_verify_cited_symbols_before_adr_acceptance) BEFORE finalizing
- Phases are binary PASS/FAIL gates; don't ladder them with vague
  acceptance criteria
- Each Phase has a short `PR title:` line so sunfish-PM commits with
  consistent prefixes
- Halt-conditions are SPECIFIC scenarios (not "if something goes wrong");
  must be named for each cross-substrate dependency
-->

**Workstream:** #NN (<one-line scope>)
**Spec:** [ADR 00XX](../../docs/adrs/00XX-<slug>.md) (<status>; amendments <if any>)
**Pipeline variant:** `sunfish-feature-change` | `sunfish-api-change` (per ADR amendment if breaking)
**Estimated effort:** N–M hours focused sunfish-PM time
**Decomposition:** N phases shipping as ~M separate PRs
**Prerequisites:** <list with ✓ for shipped + status for in-flight>

<!-- Optional one-paragraph note: what's in scope for THIS hand-off;
     what's deferred to follow-ups. -->

---

## Scope summary

<!-- 5-10 bullet list of what this hand-off ships. Mirror the ADR's
     Decision section + amendments. Cite specific package / type names
     that have been verified to exist (or to be introduced by this hand-off). -->

1. **<Component A>** — <one-line>
2. **<Component B>** — <one-line>
3. ...

**NOT in scope** (deferred to follow-up hand-offs): <bulleted; explicit>

---

## Phases

### Phase 1 — <Subject> (~Nh)

<!-- Per-phase template:
     - One-paragraph what + why
     - Specific files / types to create / modify
     - Code sketches if helpful (XML doc + nullability + required)
     - **Gate:** binary PASS/FAIL condition
     - **PR title:** suggested commit subject -->

<Description>

**Gate:** <binary condition>

**PR title:** `<type>(<scope>): <subject> (<W#NN Phase X / ADR 00NN Ax>)`

### Phase 2 — <Subject> (~Nh)

...

### Phase N — Ledger flip (~0.5h)

Update `icm/_state/active-workstreams.md` row #NN → `built`. Append last-updated entry.

**PR title:** `chore(icm): flip W#NN ledger row → built`

---

## Total decomposition

| Phase | Subject | Hours |
|---|---|---|
| 1 | <subject> | N–M |
| 2 | <subject> | N–M |
| ... | ... | ... |
| N | Ledger flip | 0.5 |
| **Total** | | **N–M h** |

---

## Halt conditions

<!-- Per `feedback_co_class_decision_filter` + the no-destructive-git rule:
     name SPECIFIC scenarios that should trigger a cob-question-* beacon
     instead of attempting to resolve in-session. Each halt-condition
     should reference a specific symbol / package / external dependency
     that might be missing or broken. -->

- **<Specific scenario 1>** → write `cob-question-*` beacon naming the gap; halt
- **<Specific scenario 2>** → ...
- **<Specific scenario 3>** → ...

---

## Acceptance criteria

<!-- Cumulative; everything that must be true for the workstream to flip
     to `built`. Mirror the Phases but as a single checklist. -->

- [ ] <criterion 1>
- [ ] <criterion 2>
- [ ] All tests pass; build clean
- [ ] Ledger row #NN → `built`

---

## References

<!-- Required: ADR being implemented; cluster intake; companion ADRs/hand-offs. -->

- [ADR 00XX](../../docs/adrs/00XX-<slug>.md) — substrate spec
- [Cluster intake](../../icm/00_intake/output/<intake-file>.md) — original scope
- <Related hand-offs / ADRs>
