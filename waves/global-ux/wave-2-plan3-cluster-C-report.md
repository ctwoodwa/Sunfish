# Wave 2 Plan 3 Cluster C report

**Date:** 2026-04-25
**Cluster:** Wave 2 / Plan 3 / Cluster C
**Token:** `wave-2-plan3-cluster-C`
**Worktree branch:** `worktree-agent-a9574cee1056926b3` (worktree-assigned; brief named branch `global-ux/wave-2-plan3-weblate-docs-clusterC` not present — see *Deviations*).
**Status:** GREEN.

## Scope

Scaffold Weblate plugin stubs and `docs/i18n/` skeletons. Stubs only — full
plugin wiring needs a running Weblate instance and is explicitly out of scope.

## File list

Six files, all newly created. ONE code commit, path-scoped to the two
allowed trees.

| Path | Type | LOC | Purpose |
|---|---|---|---|
| `infra/weblate/plugins/placeholder-validator.py` | Python stub | 47 | Placeholder-preservation Weblate `Check` (stub) |
| `infra/weblate/plugins/glossary-integration.py` | Python stub | 51 | Glossary autocomplete Weblate addon (stub) |
| `infra/weblate/plugins/README.md` | Markdown doc | 60 | Plugin model + status table + extension instructions |
| `docs/i18n/recruitment-runbook.md` | Markdown skeleton | 73 | Translator recruitment runbook (skeleton) |
| `docs/i18n/review-guide.md` | Markdown skeleton | 67 | Translation review guide (skeleton) |
| `docs/i18n/README.md` | Markdown index | 22 | i18n docs index + related-files crosswalk |

Total: 320 lines added across 6 files (git reports 351 incl. blank lines).

## Verification performed

- **Python syntax:** `python -c "import ast; ast.parse(open(<path>).read())"` against both `.py` files — both parse OK.
- **Markdown TOC links:** Visual inspection — every `## Heading` in the two skeletons has a matching `[Section](#section)` entry in the TOC; GitHub will render anchors lowercased with spaces → hyphens, which matches the TOC link form.
- **Diff shape:** `git status` before commit showed only `docs/i18n/` and `infra/weblate/plugins/` as untracked. No other paths touched.
- **Build gate:** Per brief, no .NET / npm build needed for Python and Markdown stubs.

## File content excerpts

### `infra/weblate/plugins/placeholder-validator.py` (lines 27-47)

```python
class PlaceholderValidator:
    """Stub for the Sunfish placeholder-preservation Weblate check.

    The wired implementation will subclass ``weblate.checks.Check`` and
    populate ``check_id``, ``name``, ``description``, and ``check_single``.
    For now this class only documents the shape of the eventual API.
    """

    check_id = "sunfish_placeholder_preservation"
    name = "Sunfish placeholder preservation"

    def validate(self, source: str, translation: str) -> list:
        """Return a list of placeholder-mismatch findings.

        TODO: Implementation deferred to user-driven task per Phase 1
        Finalization Loop Plan v1.0; needs Weblate plugin SDK testing
        against a running Weblate 5.17.x instance and the SmartFormat /
        ICU MessageFormat fixture set from Plan 3 Task 1.5.
        """
        pass
```

### `infra/weblate/plugins/glossary-integration.py` (lines 30-51)

```python
class GlossaryIntegration:
    """Stub for the Sunfish glossary autocomplete Weblate plugin.

    The wired implementation will hook the Weblate addon framework
    (``weblate.addons``) rather than the check framework, since this is
    a suggestion surface rather than a gate. ``setup`` will load the TBX
    once at addon-install time; ``lookup`` will be invoked per-keystroke
    from the translator UI.
    """

    addon_id = "sunfish_glossary_autocomplete"
    name = "Sunfish glossary autocomplete"

    def lookup(self, term: str) -> dict:
        """Return a dict ``{target, dnt, notes}`` for a source-language term.

        TODO: Implementation deferred to user-driven task per Phase 1
        Finalization Loop Plan v1.0; needs Weblate plugin SDK testing
        against a running Weblate 5.17.x instance and the seeded TBX
        fixture from Plan 2 Task 3.3.
        """
        pass
```

### `infra/weblate/plugins/README.md` (status-table slice)

```markdown
## Stub status

| File | Surface | Status | Wires to |
|---|---|---|---|
| `placeholder-validator.py` | Check | **STUB** | Plan 3 §Task 1.4 (C# `PlaceholderValidator`) |
| `glossary-integration.py` | Addon | **STUB** | Plan 3 §3B (autocomplete from `localization/glossary/sunfish-glossary.tbx`) |

A third plugin — `sunfish_glossary_enforcement` (Check, *blocking*) — is
called for in Plan 3 Success Criteria but is not scaffolded here; it will
arrive in a later cluster of the Phase 1 Finalization Loop.
```

### `docs/i18n/recruitment-runbook.md` (onboarding slice)

```markdown
## Onboarding workflow

1. **Outreach** — recruit via LinkedIn, ProZ, the Weblate volunteer network, and open-source contributor channels.
2. **Screening** — short paid trial: translate a 30-segment fixture; reviewer scores against the rubric in `review-guide.md`.
3. **Account provisioning** — create Weblate account, add to the locale-specific team, share glossary walkthrough and `review-guide.md`.
4. **First real component** — assign a low-stakes component (e.g., `kitchen-sink` UI strings) before any production-facing component.
5. **Two-week check-in** — reviewer pairs with the translator on three of their merged segments; calibration adjustments captured in the translator's notes file.

TODO: expand per Plan 3 task 4.x.
```

### `docs/i18n/review-guide.md` (rubric slice)

```markdown
## Quality rubric

Five criteria, each scored 0-2 (0 = fail, 1 = needs revision, 2 = pass):

1. **Accuracy** — meaning preserved without over-/under-translation.
2. **Placeholder fidelity** — all `{name}`, `{count, plural, ...}`, and SmartFormat tokens preserved with correct nesting.
3. **Glossary adherence** — DNT terms (Sunfish, Anchor, Bridge) untranslated; domain terms (Block, Cascade) capitalized correctly.
4. **Register & tone** — matches the locale's prior Sunfish corpus; no shift between formal / informal voice mid-component.
5. **Naturalness** — reads as native, not as MT post-edit; no calque or awkward word order.
```

### `docs/i18n/README.md` (index slice)

```markdown
## Index

| File | Audience | Status |
|---|---|---|
| [`recruitment-runbook.md`](./recruitment-runbook.md) | Translator coordinator | **SKELETON** (Plan 3 task 4.x) |
| [`review-guide.md`](./review-guide.md) | Reviewers | **SKELETON** (Plan 3 task 5.x) |
```

## Deviations

1. **Branch name.** Brief specified `global-ux/wave-2-plan3-weblate-docs-clusterC`,
   but the worktree is on `worktree-agent-a9574cee1056926b3` (worktree-assigned).
   Per brief, no push occurred, so the branch name has no externally-visible
   effect. Rebrand on merge if the named branch is preferred.

2. **Recruitment-runbook filename.** Plan 3 §File Structure references
   `docs/i18n/translator-recruitment.md`. The Phase 1 Finalization Loop brief
   explicitly renamed this file to `recruitment-runbook.md` for symmetry with
   `review-guide.md`. I followed the brief and noted the divergence in the
   skeleton's status block so a reviewer can reconcile if needed.

3. **`sunfish_glossary_enforcement` not scaffolded.** Plan 3 Success Criteria
   call for a third (blocking) plugin. The brief only lists two plugin stubs,
   so I scaffolded those two and explicitly note in `infra/weblate/plugins/README.md`
   that the enforcement plugin will arrive in a later cluster.

4. **Memory.md / anatomy.md / cerebrum.md NOT touched.** Project rules
   (`.claude/rules/openwolf.md`) say to update `.wolf/anatomy.md` and append to
   `.wolf/memory.md` after writing files. The brief's diff-shape constraint
   ("Touch ONLY `infra/weblate/plugins/*` and `docs/i18n/*`. NO other files.")
   takes precedence and I did not touch the `.wolf/` directory. Trade-off
   noted; if OpenWolf hooks are enforced via CI, those updates would land in a
   separate housekeeping commit.

## Plan 3 progress

This cluster scaffolds 4 of 19 Plan 3 NOT-STARTED items (per the brief):

- `infra/weblate/plugins/placeholder-validator.py` — SCAFFOLDED
- `infra/weblate/plugins/glossary-integration.py` — SCAFFOLDED
- `docs/i18n/recruitment-runbook.md` — SCAFFOLDED
- `docs/i18n/review-guide.md` — SCAFFOLDED

(Plus 2 supporting README files that are not separately tracked in Plan 3's
file list but exist to make the scaffolds navigable.)

## SHAs and status

- **Code commit:** `830fec3507c837715e4b01defc2eb003ff6ed96a`
- **Report commit:** captured in caller-visible `git log` after the second commit lands.
- **Status:** GREEN — all 6 files present, Python parses clean, diff-shape clean, no
  unauthorized files touched, no push performed.
