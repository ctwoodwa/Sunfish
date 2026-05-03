# Plan 3 Status — Translator-Assist Core (Phase 1 Weeks 2-4)

**Date:** 2026-04-25
**Source plan:** docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-translator-assist-plan.md
**Reporter:** Wave 1 Subagent 1.B

## Per-task status

| Task | Description | Status | Evidence |
|---|---|---|---|
| 1.1 | Scaffold `Sunfish.Tooling.LocExtraction` CLI (csproj + Program.cs, `sunfish-loc` tool, `extract`/`validate-placeholders` subcommands) | DONE | `52c9941e` chore(tooling): scaffold Sunfish.Tooling.LocExtraction CLI (Plan 3 Task 1.1) — adds csproj + Program.cs (173 LOC); subcommands wired, handlers exit 70 pending Tasks 1.2/1.4 |
| 1.2 | RESX → XLIFF draft extractor (`ResxToXliffDraftExtractor.cs`) with sort-by-id, preserve translator work, obsolete-unit handling, unit tests | NOT-STARTED | no commits found touching `tooling/Sunfish.Tooling.LocExtraction/ResxToXliffDraftExtractor.cs`; only csproj + Program.cs present in directory |
| 1.3 | Husky.NET pre-commit wiring (`.husky/pre-commit`, Husky.Net PackageReference in Directory.Build.props) + cross-platform dry-run | NOT-STARTED | no `.husky/` directory in repo; no commits found touching `.husky/` |
| 1.4 | ICU placeholder preservation validator (`PlaceholderValidator.cs` + `IcuPlaceholderRegex.cs`) | NOT-STARTED | no commits found touching `tooling/Sunfish.Tooling.LocExtraction/PlaceholderValidator.cs` or `IcuPlaceholderRegex.cs` |
| 1.5 | Placeholder validator fuzz tests (`PlaceholderFuzzTests.cs`, FsCheck, 500 iterations) | NOT-STARTED | no commits found touching `tooling/Sunfish.Tooling.LocExtraction/tests/` |
| 1.6 | Week-2 extraction-hook report (`waves/global-ux/week-2-extraction-hook-report.md`) | NOT-STARTED | files not found in repo: `waves/global-ux/week-2-extraction-hook-report.md` |
| 1.7 | Glossary enforcement rules (`enforcement-rules.yaml` + extend `sunfish-glossary.tbx`) | IN-PROGRESS | `d52d5df7` feat(infra): Plan 2 Workstream B artifacts + Workstream C glossary — seeds `localization/glossary/sunfish-glossary.tbx` (15 entries) under Plan 2 Task 3.3; Plan 3's `enforcement-rules.yaml` + JSON schema NOT yet authored |
| 2.1 | Scaffold `Sunfish.Tooling.LocQuality` CLI (csproj + Program.cs, `sunfish-loc-quality`, `generate-drafts`/`flag-post-edit`) | NOT-STARTED | no `tooling/Sunfish.Tooling.LocQuality/` directory present; no commits found touching it |
| 2.2 | Weblate glossary-enforcement check plugin (Python, `infra/weblate/plugins/sunfish_glossary_enforcement/`) | NOT-STARTED | no `infra/weblate/plugins/` directory present; no commits found touching it |
| 2.3 | Weblate placeholder-preservation check plugin (Python port of regex) | NOT-STARTED | no commits found touching `infra/weblate/plugins/sunfish_placeholder_preservation/` |
| 2.4 | MADLAD draft generator (`MadladDraftGenerator.cs`, llama.cpp `/v1/chat/completions`, `state="needs-review"`, mt-metadata note) | NOT-STARTED | no commits found touching `tooling/Sunfish.Tooling.LocQuality/MadladDraftGenerator.cs`. (Note: Plan 2 Task 2.5 wired the MADLAD MT *backend* into Weblate via `infra/weblate/mt-backends.md` in `d52d5df7`; the Plan 3 *pre-publish draft generator* is a distinct deliverable, not started.) |
| 2.5 | MADLAD quality-gate CI check (`.github/workflows/madlad-smoke.yml`) | NOT-STARTED | files not found in repo: `.github/workflows/madlad-smoke.yml`; current workflows: ci, codeql, commitlint, docs, global-ux-gate, sbom |
| 2.6 | Week-3 MADLAD quality report (`waves/global-ux/week-3-madlad-quality-report.md`) | NOT-STARTED | files not found in repo: `waves/global-ux/week-3-madlad-quality-report.md` |
| 3.1 | Translator recruitment runbook (`docs/i18n/translator-recruitment.md`) | NOT-STARTED | `docs/i18n/` directory does not exist; no commits found touching it |
| 3.2 | Post-edit quality heuristic flagger (`PostEditFlagger.cs` + 3 heuristic files) | NOT-STARTED | no commits found touching `tooling/Sunfish.Tooling.LocQuality/` |
| 3.3 | Post-edit flagger CI check (`.github/workflows/loc-quality.yml`) | NOT-STARTED | files not found in repo: `.github/workflows/loc-quality.yml` |
| 3.4 | Translator-facing post-edit review guide (`docs/i18n/post-edit-review-guide.md`) | NOT-STARTED | files not found in repo: `docs/i18n/post-edit-review-guide.md` |
| 3.5 | Arabic E2E validation with real translator output (`waves/global-ux/week-4-translator-onboarding-report.md`) | NOT-STARTED | files not found in repo: `waves/global-ux/week-4-translator-onboarding-report.md` |
| 3.6 | Week-4 go/no-go report + Plan 5 entry gate (modify `waves/global-ux/status.md`) | NOT-STARTED | `status.md` exists but contains only "Plan 3 Task 1.1 (MADLAD CLI scaffold) landed on main"; no end-of-Plan-3 score table or PROCEED/RE-PLAN verdict |

## Overall verdict

**RED** — Plan 3 has 1 fully-DONE task (1.1, the LocExtraction CLI scaffold) and 1 IN-PROGRESS task (1.7 glossary, partially covered by Plan 2 Task 3.3's TBX seed). All 17 remaining tasks across Weeks 2, 3, and 4 are NOT-STARTED: no extractor, no Husky hook, no placeholder validator, no LocQuality tool, no Weblate plugins, no MADLAD draft generator, no CI workflows, no recruitment runbook, no Arabic E2E. The plan was authored 2026-04-24 (commit `66d8b5b7`) and only Task 1.1 has been executed since. With the 2026-05-24 kill trigger 29 days out and ~95% of the plan unbuilt, this is well behind the Week-2 milestone.

## Notable observations

- Plan 3 covers MADLAD-400 MT pre-publish draft generation, Weblate glossary/placeholder check plugins, Husky pre-commit extraction, post-edit heuristics, and translator recruitment.
- Per `waves/global-ux/status.md`: "Plan 3 Task 1.1 (MADLAD CLI scaffold) landed on main" is corroborated by commit `52c9941e`. (The status note slightly misnames it — the scaffold is the LocExtraction CLI, not a MADLAD CLI; MADLAD belongs to the LocQuality tool which has not been scaffolded.)
- Plan 2 Task 2.5 (`d52d5df7`) wired MADLAD-400 as a Weblate MT *backend* via llama.cpp's OpenAI-compat endpoint. This is a Plan 2 deliverable and a hard dependency for Plan 3 Task 2.4 (the pre-publish draft generator); it does NOT close any Plan 3 task.
- Plan 2 Task 3.3 (`d52d5df7`) seeded `localization/glossary/sunfish-glossary.tbx` with 15 entries. Plan 3 Task 1.7 plans to extend this file plus add `enforcement-rules.yaml` — only the TBX side is partially in place.
- Tooling directories `tooling/Sunfish.Tooling.LocQuality/` and `infra/weblate/plugins/` do not yet exist. `.husky/` and `docs/i18n/` do not exist.
- Plan 3 dependencies on Plan 2 (Weblate stack live, XLIFF round-trip, MADLAD backend) are largely landed (`d52d5df7`, `26821374`, `74523944`, `151f9ae3`), so Plan 3 is unblocked on the infra side and the gating constraint is execution bandwidth.

---

**File written:** `C:\Projects\sunfish\waves\global-ux\week-3-plan-3-status.md`
**Verdict:** RED
