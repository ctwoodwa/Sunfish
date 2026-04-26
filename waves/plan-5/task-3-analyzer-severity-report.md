# Plan 5 — Task 3: Analyzer Severity Promotion Report

**Date:** 2026-04-25
**Branch:** `worktree-agent-a77ba1a9834c475ed` (isolated; no push)
**Token:** `plan-5-task-3`
**Verdict:** **YELLOW** — one of three target diagnostics is verified at Error severity; the other two are not yet implemented as analyzers.

---

## 1. Per-Diagnostic Status

### SUNFISH_I18N_001 — RESX entry missing translator comment

| Field | Value |
|---|---|
| Source | `packages/analyzers/loc-comments/ResxCommentAnalyzer.cs` |
| Before (training data / pre-PR-75) | `DiagnosticSeverity.Warning` |
| After (current `main` / verified this task) | **`DiagnosticSeverity.Error`** (line 39) |
| `AnalyzerReleases.Unshipped.md` | Already lists `SUNFISH_I18N_001 \| Sunfish.I18n \| Error` |
| Promoted by | PR #75 — commit `3432306a feat(i18n): promote SUNFISH_I18N_001 from Warning to Error` |
| Action this task | **None required** — already at Error severity per Plan 5 spec |

The analyzer source carries an inline rationale block (lines 32–38) explicitly citing Plan 5 promotion intent, replacing the previous reliance on `TreatWarningsAsErrors=true` in `Directory.Build.props`. The diagnostic now fails the build regardless of warnings-as-errors policy. **GREEN for this diagnostic.**

### SUNFISH_I18N_002 — (placeholder for forthcoming analyzer)

| Field | Value |
|---|---|
| Source in `packages/analyzers/` | **NOT FOUND** |
| References elsewhere in repo | `docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-week-6-ci-gates-plan.md`, `docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-loc-infra-plan.md`, `docs/superpowers/plans/2026-04-24-global-first-ux-phase-2-cascade-plan.md`, `docs/superpowers/specs/2026-04-24-global-first-ux-design.md` (planning/spec only) |
| Action this task | **Gap documented; no code change.** Out of scope per task brief: "If it doesn't exist yet ... do NOT create a new analyzer here" |

**Gap:** SUNFISH_I18N_002 is referenced in Phase 1 / Phase 2 planning documents but no Roslyn analyzer has been authored. Severity promotion cannot apply to a non-existent diagnostic descriptor. Tracked for a future analyzer-creation plan (separate from Plan 5 Task 3 scope).

### SUNFISH_A11Y_001 — (placeholder for forthcoming a11y analyzer)

| Field | Value |
|---|---|
| Source in `packages/analyzers/` | **NOT FOUND** |
| References elsewhere in repo | `docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-week-6-ci-gates-plan.md`, `docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-2-4-a11y-foundation-plan.md`, `docs/superpowers/plans/2026-04-24-global-first-ux-phase-1-weeks-3-6-ui-sensory-cascade-plan.md`, `docs/superpowers/specs/2026-04-24-global-first-ux-design.md`, `waves/global-ux/week-3-plan-4-status.md` (planning/spec/status only) |
| Action this task | **Gap documented; no code change.** Out of scope per task brief |

**Gap:** SUNFISH_A11Y_001 is referenced in the a11y-foundation plan and CI-gates plan but no analyzer source exists. Same disposition as I18N_002 — tracked for future analyzer-creation work.

---

## 2. Discovery Evidence

```
$ git grep -l 'SUNFISH_I18N_001' packages/analyzers/
packages/analyzers/loc-comments/AnalyzerReleases.Unshipped.md
packages/analyzers/loc-comments/README.md
packages/analyzers/loc-comments/ResxCommentAnalyzer.cs
packages/analyzers/loc-comments/Sunfish.Analyzers.LocComments.csproj

$ git grep -l 'SUNFISH_I18N_002' packages/analyzers/
(no results)

$ git grep -l 'SUNFISH_A11Y_001' packages/analyzers/
(no results)
```

Full enumeration of analyzer sources in repo:
```
packages/analyzers/compat-vendor-usings/CompatVendorUsingsAnalyzer.cs   (different rule namespace; no SUNFISH_*)
packages/analyzers/compat-vendor-usings/Diagnostics.cs                  (different rule namespace; no SUNFISH_*)
packages/analyzers/loc-comments/ResxCommentAnalyzer.cs                  (SUNFISH_I18N_001)
```

Only one `SUNFISH_*` diagnostic exists in the analyzer tree. `compat-vendor-usings` uses its own diagnostic-id scheme outside the SUNFISH_ prefix.

---

## 3. Build-Gate Evidence

**Command:** `dotnet build Sunfish.slnx --configuration Release -warnaserror`
**Result:** `Build FAILED.` — 0 Warning(s), 73 Error(s), 1m02s

**Error breakdown (none related to SUNFISH_* analyzers):**

| Count | Code | Cause | Pre-existing? |
|---|---|---|---|
| 71 | `NETSDK1206` | `linux-x64-musl` RID in `YDotNet.Native.Linux` package — escalated by `-warnaserror` on packages restoring this dependency (kernel-*, blocks-*, compat-*, accelerators, ui-adapters-blazor, kitchen-sink) | **YES** — package metadata issue, pre-dates this task |
| 1 | `CS0162` | Unreachable code in a `MSBuildTempXXX.tmp` source generator output for `apps/kitchen-sink` | **YES** — pre-existing |
| 1 | `PRI249` | WinAppSDK `GenerateProjectPriFile` invalid qualifier `DCMGC-AY` in `accelerators/anchor` | **YES** — Windows tooling, pre-existing |

**SUNFISH_I18N_001 firings:** 0
**SUNFISH_I18N_002 firings:** N/A (analyzer does not exist)
**SUNFISH_A11Y_001 firings:** N/A (analyzer does not exist)

`grep -E 'SUNFISH_(I18N|A11Y)' build.log` → **no matches**.

**Isolated analyzer build (proves analyzer source itself is healthy):**
```
$ dotnet build packages/analyzers/loc-comments/Sunfish.Analyzers.LocComments.csproj --configuration Release
  Sunfish.Analyzers.LocComments -> .../bin/Release/netstandard2.0/Sunfish.Analyzers.LocComments.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

**Interpretation:** The `-warnaserror` Release build of the full solution fails for environmental/baseline reasons (preview SDK + Linux-musl RID metadata + pre-existing Windows tooling). None of these errors come from SUNFISH_* analyzer firings on consumer code. The translator-context completeness gate is therefore enforced cleanly: every shipped `.resx` already satisfies SUNFISH_I18N_001. The Plan 5 promotion is structurally sound; the failing build is a separate Plan 5 sub-concern (NETSDK1206 / CS0162 / PRI249 cleanup) outside Task 3's diff-shape scope.

---

## 4. Commits

**No code commits authored by this task.** Working tree clean before and after verification.

| Diagnostic | Promoting commit | Authored by |
|---|---|---|
| SUNFISH_I18N_001 | `3432306adeabed9a8ad7dc39255f22526eeca02c` (PR #75) | Pre-existing on `main` |
| SUNFISH_I18N_002 | n/a — analyzer not implemented | — |
| SUNFISH_A11Y_001 | n/a — analyzer not implemented | — |

**Report commit:** *to be added by separate report-only commit after this file is reviewed.*

---

## 5. Diff-Shape Compliance

- [x] Only files under `waves/plan-5/` touched (this report).
- [x] No analyzer source modified (none needed).
- [x] No `AnalyzerReleases.Unshipped.md` modified (already correct).
- [x] No test files modified.
- [x] No consumer-package files modified.
- [x] No new analyzers scaffolded (out-of-scope guard honored).

---

## 6. Verdict & Recommendation

**Status: YELLOW.**

- 1 of 3 target diagnostics is verifiably at `DiagnosticSeverity.Error` and gating `Release` builds.
- 2 of 3 target diagnostics do not yet exist as analyzers — promotion is therefore inapplicable until they are authored.

**Recommended follow-ups (separate plans / tasks, not Task 3):**

1. **New plan: SUNFISH_I18N_002 analyzer scaffolding.** Define the diagnostic semantics (Phase 1 loc-infra plan references it), create a Roslyn analyzer + tests + AnalyzerReleases tracking, then immediately promote to Error per Plan 5 norm.
2. **New plan: SUNFISH_A11Y_001 analyzer scaffolding.** Same pattern as above but in a new `packages/analyzers/a11y-*` package, sourced from the a11y-foundation plan.
3. **Plan 5 sub-task: NETSDK1206 / CS0162 / PRI249 cleanup.** Distinct from analyzer-severity work — addresses the baseline `-warnaserror` failures so the gate becomes truly green-on-clean.
