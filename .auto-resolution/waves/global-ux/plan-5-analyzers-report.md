# Plan 5 — Analyzers Scaffold + Close-out Report

**Date:** 2026-04-25
**Branch:** `feat/plan-5-analyzers-and-closeout` (worktree-isolated; not pushed)
**Token:** `plan-5-analyzers-and-closeout`
**Verdict:** **GREEN** — all build/test gates clean; both analyzer/tool scaffolded; close-out doc authored

---

## 1. Analyzer / tool list

| Diagnostic | Implementation | Path | Severity (initial) |
|---|---|---|---|
| `SUNFISH_I18N_002` | Roslyn analyzer | `packages/analyzers/loc-unused/UnusedResourceAnalyzer.cs` | Warning |
| `SUNFISH_A11Y_001` | Node MSBuild-style check | `tooling/Sunfish.Tooling.A11yStoriesCheck/check.mjs` | Warning (report-only in workflow) |

Both register on `analyzer-promotion-cascade` follow-up (separate PR) for Error promotion.

---

## 2. Test counts

| Suite | Count | Result | Duration |
|---|---|---|---|
| `Sunfish.Analyzers.LocUnused.Tests` (xunit) | 3 | 3 passed, 0 failed | 357 ms |
| `Sunfish.Tooling.A11yStoriesCheck/tests/check.test.mjs` (`node --test`) | 3 | 3 passed, 0 failed | 475 ms |
| **Total** | **6** | **6 / 6 passed** | **832 ms** |

### Test scenarios per brief

**SUNFISH_I18N_002 (`UnusedResourceAnalyzerTests.cs`):**
- ✅ Test (a): unreferenced key emits diagnostic — both keys in resx absent from source → 2 diagnostics emitted.
- ✅ Test (b): referenced-via-indexer key does NOT emit — `localizer["X"]` pattern recognized.
- ✅ Test (c): referenced-via-GetString key does NOT emit — `.GetString("X")` pattern recognized.

**SUNFISH_A11Y_001 (`check.test.mjs`):**
- ✅ Component WITH sibling `*.stories.ts` → no finding; exit 0.
- ✅ Component WITHOUT sibling `*.stories.ts` → finding emitted; exit 1 with `--fail-on-missing`.
- ✅ Mixed components (some with, some without) → only missing one flagged; exit 0 in report-only mode.

---

## 3. Live coverage check (real ui-core surface)

```
$ node tooling/Sunfish.Tooling.A11yStoriesCheck/check.mjs
SUNFISH_A11Y_001: scanned 3 component(s); 0 missing sibling stories.
```

All 3 Plan 4 pilot components (button / dialog / syncstate-indicator) already
have sibling stories. The gate is green on the current Phase 1 surface in
report-only mode.

---

## 4. What was deferred

| Item | Why deferred | Tracked in |
|---|---|---|
| Promote `SUNFISH_I18N_002` to Error severity | Separate cascade PR — needs `.editorconfig` + `Directory.Build.props` cascade per Plan 2 Task 4.3 pattern; out of scope for scaffolding-only PR | `waves/global-ux/plan-5-closeout.md` §5 follow-up #1 |
| Flip `--fail-on-missing` on `a11y-stories-check` workflow job | Same — same cascade follow-up PR | Same |
| `.razor` (Blazor adapter) coverage in a11y-stories-check | v2 follow-up; razor stories live in different harness | `tooling/Sunfish.Tooling.A11yStoriesCheck/README.md` §"Scope (v1)" |
| React adapter coverage in a11y-stories-check | v2 follow-up; needs Plan 6 input on whether adapter coverage is redundant with ui-core's | Same |
| Wire `loc-unused` into `Directory.Build.props` cascade (per Plan 2 Task 4.3 pattern) | Mirrors I18N_001 cascade pattern; separate PR after Phase 1 surface verified clean against Warning-mode rule | `waves/global-ux/plan-5-closeout.md` §5 follow-up #1 |
| Plan 5 Task 9: 10-run p95 measurement | Per brief: "deferred to manual; not blocked on this PR" | `waves/global-ux/plan-5-closeout.md` §5 follow-up #2 |

---

## 5. slnx changes

```diff
   <Folder Name="/analyzers/loc-comments/">
     <Project Path="packages/analyzers/loc-comments/Sunfish.Analyzers.LocComments.csproj" />
     <Project Path="packages/analyzers/loc-comments/tests/Sunfish.Analyzers.LocComments.Tests.csproj" />
   </Folder>
+  <Folder Name="/analyzers/loc-unused/">
+    <Project Path="packages/analyzers/loc-unused/Sunfish.Analyzers.LocUnused.csproj" />
+    <Project Path="packages/analyzers/loc-unused/tests/Sunfish.Analyzers.LocUnused.Tests.csproj" />
+  </Folder>
```

One folder + 2 project entries added. Note: the Node-tool (a11y-stories-check)
does not appear in slnx since it's not a .NET project; it's invoked from the
GitHub workflow only.

---

## 6. Workflow change

`.github/workflows/global-ux-gate.yml`:

1. **Path triggers:** added `tooling/Sunfish.Tooling.A11yStoriesCheck/**`.
2. **New job `a11y-stories-check`** (after `locale-completeness`, before `a11y-storybook`):
   - Runs `node --test tooling/Sunfish.Tooling.A11yStoriesCheck/tests/check.test.mjs`
     for the tool's own fixture tests.
   - Runs `node tooling/Sunfish.Tooling.A11yStoriesCheck/check.mjs` (no
     `--fail-on-missing`) for report-only coverage of the live ui-core surface.
3. **Aggregator `needs:` list:** added `a11y-stories-check` between
   `locale-completeness` and `a11y-storybook`.

No removal or modification of any pre-existing job; existing workflow contract
preserved.

---

## 7. Build evidence

**Analyzer build (clean Release):**
```
$ dotnet build packages/analyzers/loc-unused/Sunfish.Analyzers.LocUnused.csproj --configuration Release
  Sunfish.Analyzers.LocUnused -> .../bin/Release/netstandard2.0/Sunfish.Analyzers.LocUnused.dll
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:03.15
```

(One initial RS1032 Roslyn-analyzer-style warning about a trailing period in
`messageFormat`; resolved by removing the trailing period to match Roslyn's
single-sentence-no-period convention.)

---

## 8. Diff-shape compliance

Per brief's restricted file set:

- [x] `packages/analyzers/loc-unused/` — 7 NEW files
- [x] `tooling/Sunfish.Tooling.A11yStoriesCheck/` — 3 NEW files
- [x] `Sunfish.slnx` — 1 folder + 2 project entries added
- [x] `.github/workflows/global-ux-gate.yml` — 1 new job + 1 path trigger + 1 needs-list update
- [x] `waves/global-ux/plan-5-closeout.md` — NEW
- [x] `waves/global-ux/plan-5-analyzers-report.md` — NEW (this file)
- [x] No other paths touched.

Note: chose Node-tool route for SUNFISH_A11Y_001 per brief's preference
("**Choose (a) — author this as an MSBuild task in `tooling/Sunfish.Tooling.A11yStoriesCheck/`
instead**"). No `packages/analyzers/a11y-stories/` directory created.

---

## 9. Self-verdict

**GREEN.**

- Both deliverable analyzers/tools scaffolded and tested green (6 / 6 passing).
- Live scan against real Phase 1 surface (ui-core/src/components/) confirms 0
  missing-stories findings — gate is green on the current cascade.
- Build is clean (0 errors, 0 warnings after RS1032 fix).
- Workflow YAML edits are surgical (one added job, one path trigger, one needs-list
  insertion) — no risk of revert noise against the actively evolving file.
- Close-out doc captures Plan 5's 8-of-9 task land status, both deferrals (Task 9
  measurement + analyzer-error-promotion cascade), and explicit Plan 6 hand-off.
- Stayed under wall-clock self-cap.
