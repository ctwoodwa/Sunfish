# Plan 5 Task 3 Review — analyzer severity promotion (gap detection)

**Date:** 2026-04-26
**Report commit:** `debf1113`
**Branch:** `worktree-agent-a77ba1a9834c475ed`
**Reviewer:** Senior Code Reviewer (sub-agent)

---

## Per-criterion results

### (a) Gap-finding is real — **PASS**

`git grep -l` over `packages/analyzers/`:

| Diagnostic ID | Files matched |
|---|---|
| `SUNFISH_I18N_001` | `packages/analyzers/loc-comments/AnalyzerReleases.Unshipped.md`, `packages/analyzers/loc-comments/README.md`, `packages/analyzers/loc-comments/ResxCommentAnalyzer.cs`, `packages/analyzers/loc-comments/Sunfish.Analyzers.LocComments.csproj` |
| `SUNFISH_I18N_002` | (empty — no source defines it) |
| `SUNFISH_A11Y_001` | (empty — no source defines it) |

Result matches expectations exactly: only I18N_001 has analyzer source. The other two diagnostic IDs appear only in planning/spec documents, not in any analyzer descriptor. Gap is real.

### (b) SUNFISH_I18N_001 already at Error — **PASS**

`packages/analyzers/loc-comments/ResxCommentAnalyzer.cs` line 39:

```csharp
defaultSeverity: DiagnosticSeverity.Error,
```

Lines 37–38 carry an inline comment explicitly citing Plan 5 promotion intent. Severity is correctly set; nothing to promote.

### (c) Build-gate evidence — **PASS**

`dotnet build packages/foundation/Sunfish.Foundation.csproj --configuration Release -warnaserror 2>&1 | grep SUNFISH_` returned empty output. Foundation build emits zero `SUNFISH_*` analyzer diagnostics under `-warnaserror`, confirming the report's claim that the I18N_001 gate is satisfied for shipped resx files. Pre-existing toolchain noise (NETSDK1206, CS0162, PRI249) is environmental and not analyzer-attributable, as the report states.

### (d) Report-only commit hygiene — **PASS**

`git show debf1113 --name-only` returns exactly one path:

```
waves/plan-5/task-3-analyzer-severity-report.md
```

No analyzer source, no consumer package, no `Directory.Build.props` touched. Commit message contains the required token `plan-5-task-3` and a clear YELLOW rationale. Co-author trailer present.

---

## YELLOW vs GREEN evaluation

The subagent chose **YELLOW (option 1)** rather than **GREEN (option 2)** even though the task-as-defined has no remaining work. This framing is **correct** because:

1. The Plan 5 spec names three target diagnostics. Returning GREEN would imply all three are gated at Error severity, which is factually false — two of them simply do not exist yet, so they cannot be gated at any severity.
2. YELLOW + an explicit gap report surfaces the architectural fact that Plan 5's full intent (three Error-gated diagnostics) cannot be realised until I18N_002 and A11Y_001 are scaffolded. A future planner reading this verdict will see the unfinished surface area immediately.
3. The brief explicitly forbids in-scope-creep (option 3): scaffolding new analyzers from scratch was off-limits. The subagent honoured the trust boundary and did not invent code.
4. The commit is purely documentary, contains no source-file changes, and ships under a recognisable token — exactly the right shape for a "verification + gap" deliverable.

GREEN would have been a misreport; RED would imply a defect in shipped code, which is not the case. YELLOW is the only honest verdict.

---

## What was done well

- Trust boundary respected: subagent did not stray into scaffolding new analyzers.
- Build evidence is concrete and reproducible (commit hashes for PR #75 cited; build error categories itemised).
- Inline rationale comments in `ResxCommentAnalyzer.cs` (lines 32–38) cite Plan 5 promotion intent, making the severity decision auditable from the source itself.
- Report distinguishes cleanly between "diagnostic verified" and "diagnostic not implemented", avoiding any false-positive completion claim.

## Issues identified

None at the Critical or Important level. One Suggestion:

- **Suggestion:** When the follow-up plan scaffolds I18N_002 and A11Y_001, mirror the inline-comment rationale pattern used in `ResxCommentAnalyzer.cs` lines 32–38 so future reviewers can audit severity decisions without cross-referencing external plan docs.

---

## Recommended follow-up

- Scaffold `SUNFISH_I18N_002` analyzer in a separate plan (suggested package: `packages/analyzers/loc-translator-context/` or extend `loc-comments` with a second descriptor — architecture decision required).
- Scaffold `SUNFISH_A11Y_001` analyzer in a separate plan (new package: `packages/analyzers/a11y-*` — first analyzer in this domain, will need an `AnalyzerReleases.Shipped.md` baseline).
- Address baseline `NETSDK1206` (linux-musl RID), `CS0162` (kitchen-sink unreachable code), and `PRI249` (WinAppSDK resource indexing) errors so the `dotnet build Sunfish.slnx -c Release -warnaserror` gate is genuinely clean end-to-end. These are tracked separately from analyzer work.
- Update `docs/diagnostic-codes.md` (if it exists) and `docs/superpowers/plans/2026-04-24-global-first-ux-*` to mark I18N_002 and A11Y_001 as "spec-named, not yet implemented".

---

## Final verdict: GREEN

The reviewed deliverable (the YELLOW report itself) is correct, evidence-backed, and respects the trust boundary. The task verdict YELLOW is the right framing of the underlying state, and the report commit is hygienic. The reviewer's GREEN here means "the report and its YELLOW conclusion are accepted as a complete and accurate deliverable for Plan 5 Task 3 as scoped".
