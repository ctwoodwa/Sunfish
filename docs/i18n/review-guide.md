# Translation review guide

> **STATUS: SKELETON.** Wave-2-Plan3-Cluster-C scaffold. Each section below
> is a one-line placeholder; full content lands per Plan 3 task 5.x.

## Table of contents

- [Purpose](#purpose)
- [Reviewer responsibilities](#reviewer-responsibilities)
- [Quality rubric](#quality-rubric)
- [Approval workflow](#approval-workflow)
- [Escalation](#escalation)
- [Common pitfalls](#common-pitfalls)

## Purpose

This guide tells reviewers what to look for when approving translator
output in Weblate, how to score against the Sunfish quality rubric, and
when to escalate. It pairs with `recruitment-runbook.md` (translator
side) so both populations work from the same standards.

TODO: expand per Plan 3 task 5.x.

## Reviewer responsibilities

Reviewers run the second-pass eye on translator drafts: confirm placeholder
preservation, glossary fidelity, register / tone consistency with the
locale's existing Sunfish corpus, and absence of MT artifacts. Reviewers
are the gate between translator draft and `state="final"` in Weblate.

TODO: expand per Plan 3 task 5.x.

## Quality rubric

Five criteria, each scored 0-2 (0 = fail, 1 = needs revision, 2 = pass):

1. **Accuracy** — meaning preserved without over-/under-translation.
2. **Placeholder fidelity** — all `{name}`, `{count, plural, ...}`, and SmartFormat tokens preserved with correct nesting.
3. **Glossary adherence** — DNT terms (Sunfish, Anchor, Bridge) untranslated; domain terms (Block, Cascade) capitalized correctly.
4. **Register & tone** — matches the locale's prior Sunfish corpus; no shift between formal / informal voice mid-component.
5. **Naturalness** — reads as native, not as MT post-edit; no calque or awkward word order.

A segment passes review only with all five at 2. Any 1 returns to draft
with reviewer comments; any 0 returns to draft with mandatory translator
re-read of the relevant glossary entry or rubric clause.

TODO: expand per Plan 3 task 5.x.

## Approval workflow

Reviewer opens the translator's pending segments in Weblate, scores each
against the rubric, leaves inline comments for any sub-2 score, and either
marks `state="final"` (all 2s) or returns to draft (any sub-2). Bulk
approval of more than 20 segments at once is forbidden — the rubric must
be applied per-segment.

TODO: expand per Plan 3 task 5.x.

## Escalation

Disagreements between translator and reviewer that cannot be resolved in
the Weblate comment thread within three business days escalate to the
locale lead; if no locale lead exists or the locale lead is one party,
escalate to the translator coordinator. Persistent disagreement on the
rubric itself (not on a specific segment) escalates to the BDFL via the
Plan 3 owner.

TODO: expand per Plan 3 task 5.x.

## Common pitfalls

- **Approving MT output verbatim** — MADLAD drafts are starting points, not final translations; check for hallucinations and locale-specific awkwardness before approving.
- **Missing the placeholder regression** — a dropped `{count}` in a plural branch is a runtime crash, not a cosmetic issue; never wave one through.
- **Glossary drift** — an Arabic translator who uses a slightly different rendering of "Workspace" than the agreed glossary entry creates a corpus-wide consistency problem; correct early or it propagates.
- **Register inconsistency** — formal / informal mixing within a single component (e.g., German `Sie` vs. `du`) reads as broken; review the whole component, not just the changed segments.
- **Reviewer fatigue** — bulk-approving 50 segments because they "look fine" defeats the rubric; if a reviewer is fatigued, hand the queue to another reviewer.

TODO: expand per Plan 3 task 5.x.
