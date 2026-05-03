# i18n Cascade Subagent Brief — Canonical Template

**Purpose:** Reusable brief template for dispatching i18n cascade subagents (one subagent per locale or per locale-batch). Maintainer copies this template, fills in the bracketed `<…>` placeholders, and pastes it into the subagent's opening message.

**Last updated:** 2026-04-26 (added Stage 2 back-translation validation requirement).

**Companion docs (do not duplicate — link to them from the rendered brief):**
- [`docs/runbooks/i18n-translation-validation.md`](../../../docs/runbooks/i18n-translation-validation.md) — 3-stage AI translation validation gate
- [`docs/runbooks/mat-setup-mac.md`](../../../docs/runbooks/mat-setup-mac.md) — MAT/Azure Translator setup on Mac
- [`i18n/coordinators.md`](../../../i18n/coordinators.md) — locale ownership roster
- [`i18n/locales.json`](../../../i18n/locales.json) — locale metadata
- [`tooling/Sunfish.Tooling.LocalizationXliff/`](../../../tooling/Sunfish.Tooling.LocalizationXliff/) — resx ↔ XLIFF 2.0 round-trip

---

## Why this template exists

Earlier i18n cascade briefs in this session (e.g., `a161ebd9064287da6`, `a362956926570c65a`) shipped translations directly from Stage 1 (AI translate) into the XLIFF without a validation step. This produced a small but non-zero rate of meaning drift (lost negation, swapped subject/object, dropped placeholders) that human reviewers caught during coordinator sign-off.

This template adds **Stage 2 back-translation validation** between translate and verify. Stage 3 cross-check is also referenced; full three-stage runs are encouraged for tier-1 locales but optional for `bake-in`-tier locales where the coverage floor is 40% and partial output is acceptable.

---

## Brief Template — copy from here

Below the `=== BEGIN ===` marker is the brief verbatim. Maintainer fills in the `<bracketed>` placeholders.

```
=== BEGIN ===

You are an i18n cascade subagent for Sunfish. Your job is to translate one
batch of UI strings from en-US into <TARGET_LOCALE> (BCP-47 tag), validate
the translations against drift, and open a PR with the result.

## Inputs

- Source locale: en-US
- Target locale: <TARGET_LOCALE>
- Batch name: <BATCH_NAME>            (e.g., 2026-04-fr-tier1-refresh)
- Source XLIFF: <SOURCE_XLIFF_PATH>   (typically obj/loc/<Project>.<locale>.xlf)
- Tier: <TIER>                        (complete | bake-in)
- Completeness floor: <FLOOR>%        (95 for complete, 40 for bake-in)
- Coordinator: <COORDINATOR_HANDLE>   (per i18n/coordinators.md)

## Locale-specific quality notes

<paste the relevant row(s) from
docs/runbooks/i18n-translation-validation.md §"Locale-specific quality notes">

## Stages

### Stage 1 — Translate

Use Claude (Sonnet/Opus). Translate every <source> in the input XLIFF whose
<target> is empty or whose state is "initial".

Prompt template: see docs/runbooks/i18n-translation-validation.md §Stage 1.

Output:
  waves/i18n/<BATCH_NAME>/stage-1-translations/<TARGET_LOCALE>.tsv
Format: key<TAB>source_en<TAB>target_<locale>

### Stage 2 — Back-translation validation  *** REQUIRED ***

For each translated key, back-translate the target back to en-US using a
*different* engine (DeepL preferred; if unavailable, re-prompt Claude with
explicit literal-translation instructions per the runbook).

For each key, score the drift between the back-translated text and the
original en-US source:

  - Green (≤10% drift) → no flag
  - Yellow (10–30%) → log only, no flag
  - Red (>30%) → flag

Output two files:

1) Side-by-side comparison (always written, all keys):
     waves/i18n/<BATCH_NAME>/stage-2-back-translations/<TARGET_LOCALE>.tsv
   Columns: key, source_en, target_<locale>, back_en, drift_score, flag

2) Validation flags (only flagged keys, written even if empty):
     waves/i18n/<BATCH_NAME>/validation-flags.md
   Markdown table of every flagged key across all locales in this batch
   (subagents append to this file; do not overwrite).

DO NOT block on flags. Continue to Stage 3 (or directly to Verify if Stage 3
is skipped) and open the PR. The flags file is for human review, not a
hard gate.

### Stage 3 — Cross-check  *** RECOMMENDED for tier=complete, OPTIONAL for tier=bake-in ***

If tier == "complete": run Stage 3 cross-check via Azure Translator using the
$AZURE_TRANSLATOR_KEY environment variable. Diff against Stage 1 output.

Output:
  waves/i18n/<BATCH_NAME>/stage-3-cross-check/<TARGET_LOCALE>.tsv

If tier == "bake-in": skip Stage 3 unless explicitly requested. Note the
skip in the validation report.

### Verify — XLIFF round-trip + completeness

1. Splice Stage 1 translations back into the source XLIFF as <target> entries
   with state="translated" (NOT state="final" — coordinator owns the
   final-promotion step).
2. Run the resx → XLIFF → resx round-trip via:
     dotnet build -t:SunfishXliffToResx -p:LocXliffLocale=<TARGET_LOCALE>
3. Verify completeness against <FLOOR>%. If below floor, list the missing
   keys in the validation report and stop — do not open a PR with sub-floor
   coverage.
4. Generate the per-locale validation report:
     waves/i18n/<BATCH_NAME>/validation-report-<TARGET_LOCALE>.md
   Use the template in docs/runbooks/i18n-translation-validation.md §"Output:
   per-locale validation report".

## Pull request

- Branch name: i18n/<BATCH_NAME>-<TARGET_LOCALE>
- Title: i18n(<TARGET_LOCALE>): <BATCH_NAME> cascade
- Body: link to validation-report-<TARGET_LOCALE>.md, link to
  validation-flags.md (if any flags), @-mention <COORDINATOR_HANDLE>.
- Labels: i18n, locale-<TARGET_LOCALE>
- Reviewers: <COORDINATOR_HANDLE>
- DO include: stage-1, stage-2, stage-3 outputs; validation report;
  validation flags; updated XLIFF; updated resx
- DO NOT include: secrets, API keys, machine-translation engine raw
  responses (only the parsed values)

## Constraints

- Stay on plain git (worktree-agent-* or i18n/* branch). DO NOT use `but` —
  cascade subagents run outside the GitButler workspace.
- Use `gh pr create` then `gh pr merge --auto --squash` if the PR has zero
  flags AND tier == "bake-in" AND coordinator has standing pre-approval per
  i18n/coordinators.md. Otherwise: open the PR but do NOT auto-merge.
- ICU placeholders ({0}, {name}, plural forms) must be preserved verbatim in
  every translation. If you cannot preserve a placeholder, drop the key from
  the batch and report it in validation-flags.md.
- Stay under your subscription token budget. Polish-cased / Japanese / Hindi
  cascades have historically been the most token-heavy.

## Self-cap

<MINUTES> minutes for this locale. If you can't finish in time:
1. Save partial output to waves/i18n/<BATCH_NAME>/.
2. Open a draft PR with what you have.
3. Report YELLOW with the % completed.

## Reporting back

Self-verdict:
  GREEN  → all keys translated, validated, PR opened
  YELLOW → partial; PR draft, % completed listed
  RED    → couldn't establish a viable Stage 1 (e.g., model refused, source
           XLIFF malformed)

Include in your final message:
  - PR number + URL
  - Branch name
  - Stage 1/2/3 file paths
  - Flag count + brief sample
  - Coordinator notification status

=== END ===
```

---

## Maintainer checklist before dispatching

- [ ] Filled in all `<bracketed>` placeholders in the brief.
- [ ] Confirmed `$AZURE_TRANSLATOR_KEY` is in the subagent's environment (Stage 3).
- [ ] Confirmed `$DEEPL_AUTH_KEY` is set if using DeepL for Stage 2.
- [ ] Coordinator (per `i18n/coordinators.md`) notified that a PR is incoming and given an SLA.
- [ ] Source XLIFF was regenerated from current en-US resx (run `dotnet build -t:SunfishResxToXliff` at HEAD before dispatch).
- [ ] Batch directory `waves/i18n/<BATCH_NAME>/` exists.
- [ ] If running multiple locales in parallel, each subagent writes to its own `<TARGET_LOCALE>.tsv` files but they all share `validation-flags.md` — ensure they append, not overwrite.

---

## Schema for `validation-flags.md`

The shared flags file across a batch. Subagents append; nobody overwrites.

```markdown
# Validation Flags — <BATCH_NAME>

| Locale | Key | Source (en-US) | Stage 1 translation | Back-translation | Drift | Stage 3 disagrees? | Action |
|---|---|---|---|---|---|---|---|
| de-DE | app.error.fileLocked | The file is locked by another user | Die Datei ist von einem anderen Benutzer gesperrt | The file was blocked from another user | 35% | no | escalate |
| pl-PL | app.button.editProfile | Edit profile | Edytuj profil | Edit a profile | 12% | no | log only |
```

Coordinator processes this file at sign-off time and either:
1. Approves the AI translation as-is (promote to `state="final"`),
2. Edits the translation in the XLIFF and promotes,
3. Escalates to a paid human translator (always-human surface or unresolvable disagreement).

---

## Why Stage 2 was added (2026-04-26)

Cascades dispatched before this update produced ~1.5–4% rate of "ship-able by AI but actually wrong" translations on tier-1 commercial locales (sample: French and German cascades, ~3,000 keys each, ~50–120 errors caught at coordinator review).

Adding Stage 2 back-translation surfaced **~80% of those errors automatically** in dry-run testing, reducing coordinator review burden from "every key" to "flagged keys + 5% spot-check". The runtime cost is ~2x Stage 1 (one extra translation pass) and ~$0 in API cost (DeepL/Azure free tiers cover it).

Stage 3 catches an additional ~5–10% of errors that survive Stage 2 (typically: errors the same engine would make on both forward and back translation). It is recommended for tier-1 locales where the cost of a wrong production string is high; optional for `bake-in`-tier locales where partial coverage is the explicit goal.

---

## See also

- [`docs/runbooks/i18n-translation-validation.md`](../../../docs/runbooks/i18n-translation-validation.md)
- [`docs/runbooks/mat-setup-mac.md`](../../../docs/runbooks/mat-setup-mac.md)
- [`i18n/coordinators.md`](../../../i18n/coordinators.md)
- [`i18n/locales.json`](../../../i18n/locales.json)
- [`tooling/Sunfish.Tooling.LocalizationXliff/`](../../../tooling/Sunfish.Tooling.LocalizationXliff/)
