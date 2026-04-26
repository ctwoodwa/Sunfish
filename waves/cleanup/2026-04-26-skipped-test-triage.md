# Skipped-test triage — session 2026-04-26 (followup to PR #135 debt audit)

**Author:** subagent (test-triage)
**Scope:** All 22 `Fact(Skip)` / `Theory(Skip)` markers catalogued in
`waves/cleanup/2026-04-26-followup-debt-audit.md` §1a–§1f.
**Method:** read each test, verify Skip premise still holds against current main,
classify, take action.
**Self-cap:** 75 minutes.

---

## Executive verdict

| Disposition | Count | Action taken |
|---|---|---|
| RESOLVED-UPSTREAM (skip already removed) | 1 | None — verified clean |
| FIX-LATER (real prod bug or buildable fixture, but scoped follow-up) | 5 | Skip message rewritten with tracking + unblocker + ETA |
| KEEP-SKIPPED (definition-only, environmental, or designer-blocked — intentional) | 11 | Skip message rewritten to mark intent clearly |
| FIX-NOW | 0 | n/a |
| DELETE | 0 | n/a |
| **Total** | **17 actionable** + 5 phantom | — |

> "22" reconciliation: the audit's §1 summary reports "20 Fact(Skip) + 2 Theory(Skip) = 22"
> but its detail tables enumerate exactly 17 distinct file:line entries (1 prod-race + 2 axe +
> 10 DataDisplay + 1 Gantt-pair counted as 2 + 1 environmental + 2 palette). The 5 missing
> entries trace to Cluster A (5 SplitButton/Chip skips) which were FIXED before this
> triage by PR #103/#112 — those skips no longer exist in the tree. Reconciled below.

---

## Per-test disposition

### §1a — Real production bug (1 test) → **RESOLVED-UPSTREAM**

| File:line (audit) | Disposition | Notes |
|---|---|---|
| `packages/kernel-lease/tests/FleaseLeaseCoordinatorTests.cs:204` | RESOLVED | PR #118 (`fca9a28c fix(kernel-lease): real Release broadcast race`) merged. The skip is GONE; line 204 is now a normal `[Fact]`. No action needed. |

### §1b — Real a11y bug, /AI/ folder (2 tests) → **FIX-LATER**

| File | Disposition | Notes |
|---|---|---|
| `packages/ui-adapters-blazor-a11y/tests/AI/SunfishChatA11yTests.cs:48` | FIX-LATER | Production bug confirmed in `Components/AI/SunfishChat.razor:53` — `<div aria-label="…">` violates axe `aria-prohibited-attr`. **Why not FIX-NOW:** task brief restricts diff to test files; production fix would touch `SunfishChat.razor` (small) and is owed a dedicated PR per audit §8.4. **Skip message rewritten** to name unblocker (`role="status"` on the bubble) + audit reference. |
| `packages/ui-adapters-blazor-a11y/tests/AI/SunfishAIPromptA11yTests.cs:61` | FIX-LATER | Production bug confirmed — `.sf-aiprompt__history-button` lacks WCAG 2.2 SC 2.5.8 (target-size 24×24) styling. **Why not FIX-NOW:** fix lives in 3 SCSS theme files (Material/Bootstrap/FluentUI) + their built CSS — too broad for this triage PR. **Skip message rewritten** to name unblocker (`min-inline-size`/`min-block-size >=24px`) + audit reference. |

### §1c — Tracked-fixture skips, DataDisplay (10 tests) → mixed FIX-LATER / KEEP-SKIPPED

Two sub-categories:

**(a) FIX-LATER — buildable fixture, just not built yet (3 tests):**

| File | Disposition | Unblocker |
|---|---|---|
| `tests/DataDisplay/DataGrid/SunfishDataGridA11yTests.cs:34` | FIX-LATER | Extract `MockDownloadService` + typed-row fixture builder under `tests/Fixtures/DataGridFixture.cs`. One PR; unblocks DataGrid root. |
| `tests/DataDisplay/DataGrid/SunfishDataSheetA11yTests.cs:28` | FIX-LATER | `tests/Fixtures/DataSheetFixture.cs` (sample columns + typed rows). Same workstream as above. |
| `tests/DataDisplay/Gantt/SunfishGanttA11yTests.cs:33` | FIX-LATER | `tests/Fixtures/GanttFixture.cs` extracted from kitchen-sink demo; also unblocks the dependencies test below. |

**(b) KEEP-SKIPPED — definition-only by design (7 tests):**

These 7 components are cascading definition components that register with a parent host and
render no isolated DOM. They cannot be exercised in isolation by axe, so the skip is permanent
unless the test pattern itself changes (test through parent harness instead).

| File | Component class | Parent harness that will cover it |
|---|---|---|
| `tests/DataDisplay/DataGrid/SunfishGridColumnA11yTests.cs:26` | `SunfishGridColumn<TItem>` | `SunfishDataGridA11yTests` |
| `tests/DataDisplay/DataGrid/SunfishTreeListColumnA11yTests.cs:27` | TreeList column def | `SunfishTreeList…A11yTests` |
| `tests/DataDisplay/DataGrid/SunfishDataSheetColumnA11yTests.cs:27` | DataSheet column def | `SunfishDataSheetA11yTests` |
| `tests/DataDisplay/DataGrid/SunfishPivotGridColumnFieldA11yTests.cs:26` | Pivot column field | `SunfishPivotGrid…A11yTests` |
| `tests/DataDisplay/DataGrid/SunfishPivotGridMeasureFieldA11yTests.cs:26` | Pivot measure field | `SunfishPivotGrid…A11yTests` |
| `tests/DataDisplay/DataGrid/SunfishPivotGridRowFieldA11yTests.cs:26` | Pivot row field | `SunfishPivotGrid…A11yTests` |
| `tests/DataDisplay/Gantt/SunfishGanttDependenciesA11yTests.cs:27` | Gantt dependency def | `SunfishGanttA11yTests` |

**Why not DELETE these 7:** they exist for symmetric component inventory (every public
component has a tests file) per audit §8.5. Removing them would silently drop an inventory
slot. They also serve as a discoverability marker: future contributors searching for
`SunfishPivotGridRowField` find a tests file and a clear note about how that component is
covered through its parent.

### §1d — Environmental (1 test) → **KEEP-SKIPPED**

| File | Disposition | Notes |
|---|---|---|
| `accelerators/bridge/tests/Sunfish.Bridge.Tests.Integration/HealthCheckTests.cs:11` | KEEP-SKIPPED | Aspire `DistributedApplicationTestingBuilder` requires a real container runtime (Podman/Docker). CI agents are headless without Docker-in-Docker. Per audit §9 ("DO NOT TOUCH"). **Skip message rewritten** to mark intent + dev-box re-enable instructions. |

### §1e — Designer-blocked (2 tests) → **KEEP-SKIPPED**

| File | Disposition | Notes |
|---|---|---|
| `tooling/Sunfish.Tooling.ColorAudit/tests/SyncStatePaletteAuditTests.cs:67` | KEEP-SKIPPED | Light-palette CVD distinguishability awaits human designer decision per `waves/global-ux/week-2-cvd-palette-audit.md`. Per audit §9. |
| `tooling/Sunfish.Tooling.ColorAudit/tests/SyncStatePaletteAuditTests.cs:92` | KEEP-SKIPPED | Dark-palette same — worst pair: protanopia healthy↔conflict ΔE=2.18. ETA: Plan-6 design wave. |

Both skip messages rewritten with concrete unblocker (designer decision needed) + ETA.

### §1f — Streaming parking-lot → no test code

The audit references `docs/superpowers/plans/2026-04-18-platform-phase-C-input-modalities.md:1096`
which is a documentation note about Phase C, not a `Fact(Skip)`. No test exists yet to triage.
**No action.**

---

## What changed in this PR (test files only)

14 test files modified — every change is **the Skip-string text + xmldoc remarks**. No test
behaviour changes. No production-code changes.

```
accelerators/bridge/tests/Sunfish.Bridge.Tests.Integration/HealthCheckTests.cs
packages/ui-adapters-blazor-a11y/tests/AI/SunfishAIPromptA11yTests.cs
packages/ui-adapters-blazor-a11y/tests/AI/SunfishChatA11yTests.cs
packages/ui-adapters-blazor-a11y/tests/DataDisplay/DataGrid/SunfishDataGridA11yTests.cs
packages/ui-adapters-blazor-a11y/tests/DataDisplay/DataGrid/SunfishDataSheetA11yTests.cs
packages/ui-adapters-blazor-a11y/tests/DataDisplay/DataGrid/SunfishDataSheetColumnA11yTests.cs
packages/ui-adapters-blazor-a11y/tests/DataDisplay/DataGrid/SunfishGridColumnA11yTests.cs
packages/ui-adapters-blazor-a11y/tests/DataDisplay/DataGrid/SunfishPivotGridColumnFieldA11yTests.cs
packages/ui-adapters-blazor-a11y/tests/DataDisplay/DataGrid/SunfishPivotGridMeasureFieldA11yTests.cs
packages/ui-adapters-blazor-a11y/tests/DataDisplay/DataGrid/SunfishPivotGridRowFieldA11yTests.cs
packages/ui-adapters-blazor-a11y/tests/DataDisplay/DataGrid/SunfishTreeListColumnA11yTests.cs
packages/ui-adapters-blazor-a11y/tests/DataDisplay/Gantt/SunfishGanttA11yTests.cs
packages/ui-adapters-blazor-a11y/tests/DataDisplay/Gantt/SunfishGanttDependenciesA11yTests.cs
tooling/Sunfish.Tooling.ColorAudit/tests/SyncStatePaletteAuditTests.cs
```

---

## New skip-message convention

Every rewritten skip message follows this shape:

```
[DISPOSITION] (category): one-line summary. Unblocker: concrete next step.
See waves/cleanup/2026-04-26-followup-debt-audit.md §X + (this triage doc).
```

Where DISPOSITION is one of `FIX-LATER` / `KEEP-SKIPPED`. This makes future audits trivial:
`grep -r "FIX-LATER" --include "*A11yTests.cs"` returns the actionable backlog directly.

---

## Follow-up dispatch recommendations (not in this PR)

1. **AI components: production fix PR.** Two small edits: (a) `SunfishChat.razor:53` add
   `role="status"` to typing bubble; (b) AIPrompt theme SCSS — set `min-inline-size`/`min-block-size`
   to >=24px on `.sf-aiprompt__history-button`. After landing, flip the 2 §1b skips to active.
2. **DataGrid fixture builder PR.** Author `MockDownloadService` + `tests/Fixtures/DataGridFixture.cs`.
   Unblocks the 3 FIX-LATER tests in §1c (a). One non-trivial PR; high leverage.
3. **GanttFixture extraction.** Pull from `apps/kitchen-sink` Gantt demo. Smaller scope; one PR.
4. **ColorAudit palette.** Designer-driven; not subagent work.
5. **HealthCheck.** No action — intentional environmental skip.

---

## Verdict

**GREEN** — all 22 catalogued markers triaged; 14 test files now carry actionable skip
messages with named unblockers, ETAs, and audit references. Zero production-code changes.
Zero test behaviour changes.
