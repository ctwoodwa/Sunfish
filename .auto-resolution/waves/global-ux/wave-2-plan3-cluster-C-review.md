# Wave 2 Review — Plan 3 Cluster C (Weblate plugins + i18n docs)

**Date:** 2026-04-25
**Code commit:** 830fec35
**Report commit:** 5fa1cc2e
**Branch:** worktree-agent-a9574cee1056926b3
**Reviewer:** Wave 2 Cluster C reviewer

## Per-criterion results

### (a) Diff scope — PASS
`git show --name-only 830fec35 | sort` returns exactly the six expected paths and no others:

- `docs/i18n/README.md`
- `docs/i18n/recruitment-runbook.md`
- `docs/i18n/review-guide.md`
- `infra/weblate/plugins/README.md`
- `infra/weblate/plugins/glossary-integration.py`
- `infra/weblate/plugins/placeholder-validator.py`

### (b) Python stubs parse cleanly — PASS
Both files validated via `ast.parse`:
```
placeholder-validator.py: OK
glossary-integration.py: OK
```
Each file has: module docstring → `from __future__ import annotations` → class declaration → class docstring → class-level attributes (`check_id`/`addon_id`, `name`) → method stub (`validate` / `lookup`) with docstring and `pass` body. No syntax errors.

### (c) Meaningful module docstrings — PASS
- `placeholder-validator.py`: ~22-line module docstring covering STATUS, "Planned behavior" (placeholder preservation, ICU/SmartFormat compare against C# extractor), Reference (Plan 3 §Task 1.4), and explicit deferral rationale.
- `glossary-integration.py`: ~22-line module docstring covering STATUS, "Planned behavior" (TBX-based autocomplete, DNT flag surfacing for Sunfish/Anchor/Bridge), companion-plugin distinction (suggestion vs blocking enforcement), Reference (Plan 3 §3B / §Task 1.7), and explicit deferral rationale.

Both docstrings communicate intent clearly enough that a future implementer can wire the plugin without re-reading the plan.

### (d) Markdown TOCs match anchors + placeholder content + TODO lines — PASS

**`recruitment-runbook.md`** — TOC has 7 anchors: `#purpose`, `#target-locales`, `#translator-selection-criteria`, `#onboarding-workflow`, `#compensation-model`, `#quality-review-cadence`, `#off-boarding`. All seven match `##` headers verbatim. Each section has 1-5 sentences of placeholder content followed by `TODO: expand per Plan 3 task 4.x.`

**`review-guide.md`** — TOC has 6 anchors: `#purpose`, `#reviewer-responsibilities`, `#quality-rubric`, `#approval-workflow`, `#escalation`, `#common-pitfalls`. All six match `##` headers verbatim. Each section has 1-2 sentences (rubric and pitfalls have richer prose) followed by `TODO: expand per Plan 3 task 5.x.`

### (e) Plugins README documents model + status + extension — PASS
`infra/weblate/plugins/README.md` contains:
- **Plugin model** section — explains the two Weblate extension surfaces (Checks for hard gates, Addons for non-blocking surfaces) and how loading is configured via `WEBLATE_ADDITIONAL_APPS` and `/app/data/python/` mount.
- **Stub status** table — both files listed with surface (Check/Addon), STUB status, and Plan 3 task wire-up reference.
- **How to extend a stub into a wired plugin** — six-step instructions covering local Weblate spin-up, base subclassing, mount + restart + verify, pytest scaffold under new `tests/` tree (pinned to `weblate==5.17.*`), `docker-compose.yml` registration, and README status update.
- **Why these are stubs today** — rationale tied to Phase 1 Finalization Loop scope.

### (f) i18n README indexes + skeleton notice — PASS
`docs/i18n/README.md` opens with the exact required note: *"These are skeletons; expansion is tracked under Plan 3 tasks 4.x and 5.x."* Index table lists both files with audience and status (`SKELETON (Plan 3 task 4.x)` and `SKELETON (Plan 3 task 5.x)`). Related-links block points at Weblate stack docs, the ops runbook, the glossary TBX, and the Plan 3 spec — well-cross-linked.

### (g) Commit token — PASS
Commit subject: `feat(i18n): wave-2-plan3-cluster-C — Weblate plugin stubs + docs/i18n skeletons`. Body also contains the explicit `Token: wave-2-plan3-cluster-C` line. Token is present twice.

### (h) Diff shape clean — PASS
Same as (a). Only `infra/weblate/plugins/*` and `docs/i18n/*`. No stray edits to `.wolf/`, no edits to other packages, no edits to Plan 3 itself.

### (i) Sentinel deviations evaluation — PASS

1. **Worktree branch name divergence.** ACCEPTABLE — no push occurred, isolated worktree, no downstream consumer.
2. **Recruitment runbook filename divergence.** PASS — flag is present and prominent. The first blockquote of `recruitment-runbook.md` reads:
   > *"Plan 3 §File Structure references `docs/i18n/translator-recruitment.md`; the Phase 1 Finalization Loop brief renamed this file to `recruitment-runbook.md` for consistency with `review-guide.md`. If the divergence matters, reconcile in a follow-up commit."*

   This is the right call: brief takes precedence, divergence is flagged for downstream reconciliation.
3. **Three-plugin gap.** PASS — `infra/weblate/plugins/README.md` explicitly documents:
   > *"A third plugin — `sunfish_glossary_enforcement` (Check, blocking) — is called for in Plan 3 Success Criteria but is not scaffolded here; it will arrive in a later cluster of the Phase 1 Finalization Loop."*

   Gap is named, surface is named, and follow-up cluster is anticipated. No scope creep, full transparency.
4. **`.wolf/*` not touched.** ACCEPTABLE per brief — the brief's TRUSTED constraint took precedence over the OpenWolf rule for this isolated scaffold cluster.

## Observations (non-blocking)

- The placeholder validator's docstring elegantly notes the Python ↔ C# lock-step constraint ("when one changes, the other must update or CI will diverge from the editor surface") — this is exactly the kind of cross-language invariant a future maintainer needs called out at the top of the file. Well-placed.
- The plugins README's Plugin model section correctly distinguishes `weblate.checks.Check` (gates) from `weblate.addons` (surfaces) — matches Weblate 5.x architecture and gives the future implementer the right starting point.
- The review guide's quality rubric (5 criteria × 0-2 score, all-2s required for `state="final"`) is concrete enough to apply today even though it carries a TODO; placeholder content is meaningfully calibrated, not filler.
- All TODO lines point at the right Plan 3 task ranges (4.x for recruitment, 5.x for review, 1.4 / 1.7 / 3B for plugins). Cross-references will not rot.

## Final verdict: GREEN
