# Wave 0 Workflow Followup Report

**Date:** 2026-04-25
**Branch:** `global-ux/wave-0-workflow-followup`
**Wave:** Phase 1 Finalization Loop, Wave 0
**Closes:** Wave 2 Cluster C YELLOW deviation tracked in [`waves/global-ux/week-3-cascade-coverage-report.md`](./week-3-cascade-coverage-report.md) §"Packages deferred → `packages/blocks-workflow` — Pattern A DI deferred"

Token: `wave-0-workflow-followup`

---

## Verdict

GREEN. Build passes with 0 warnings, 0 errors, no `SUNFISH_I18N_001`. Diff-shape constraint honored (only the two scoped files in the code commit). `blocks-workflow` is now full Pattern A — resources + marker + DI registration.

---

## File diff summary

### `packages/blocks-workflow/Sunfish.Blocks.Workflow.csproj` (+3 lines)

A new `<ItemGroup>` containing the foundation `ProjectReference` was appended adjacent to the existing `<PackageReference>` group (no existing `<ProjectReference>` ItemGroup existed):

```xml
  <ItemGroup>
    <ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />
  </ItemGroup>
```

### `packages/blocks-workflow/src/WorkflowServiceCollectionExtensions.cs` (+3 lines)

Added two `using` directives in alphabetical position and one `TryAddSingleton` line inside the existing `AddInMemoryWorkflow` method body:

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;   // added
using Sunfish.Foundation.Localization;                        // added
...
    services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));   // added
```

**Cosmetic-cref restoration:** Not applicable. The existing XML doc comment on `AddInMemoryWorkflow` only references `<see cref="InMemoryWorkflowRuntime"/>` and `<see cref="IWorkflowRuntime"/>` (workflow-internal types). There was no plain-text `<c>IStringLocalizer&lt;T&gt;</c>` fallback to restore — the Cluster C subagent never inserted one in this file. Hence no `using Microsoft.Extensions.Localization;` was needed and none was added (avoiding an unused-using warning).

Total: 6 insertions, 0 deletions across 2 files (matches `git commit` summary).

---

## Commit SHAs

| Commit | SHA | Files |
|---|---|---|
| Code | `a201f3d828f0c2abdab4128aab57168fe3205a09` | `Sunfish.Blocks.Workflow.csproj`, `WorkflowServiceCollectionExtensions.cs` |
| Report | (this commit, captured below) | `waves/global-ux/wave-0-workflow-followup-report.md` |

---

## `dotnet build` output excerpt

```
$ dotnet build packages/blocks-workflow/Sunfish.Blocks.Workflow.csproj
  Determining projects to restore...
  Restored C:\Projects\sunfish\packages\blocks-workflow\Sunfish.Blocks.Workflow.csproj (in 334 ms).
  3 of 4 projects are up-to-date for restore.
  Sunfish.Analyzers.LocComments -> ...\Sunfish.Analyzers.LocComments.dll
  Sunfish.Tooling.LocalizationXliff -> ...\Sunfish.Tooling.LocalizationXliff.dll
  Sunfish.Foundation -> ...\Sunfish.Foundation.dll
  Sunfish.Blocks.Workflow -> ...\Sunfish.Blocks.Workflow.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:06.62
```

The `SUNFISH_I18N_001` analyzer (Sunfish.Analyzers.LocComments) loaded and ran (visible building before the workflow project) — emitted no warnings on this package. Foundation built as a transitive consequence of the new ProjectReference.

NETSDK1057 preview-SDK informational messages are present (project-wide property of the .NET 11 preview toolchain, unrelated to this change).

---

## Diff-shape audit

```
$ git show --stat HEAD~  # the code commit
 packages/blocks-workflow/Sunfish.Blocks.Workflow.csproj          | 3 +++
 packages/blocks-workflow/src/WorkflowServiceCollectionExtensions.cs | 3 +++
 2 files changed, 6 insertions(+)
```

Only the two scope files. No other blocks-* packages, no foundation, no accelerators, no apps, no `_shared/`, no ICM, no docs (other than the separate report commit per brief). `git add` was path-scoped — pre-existing untracked/modified `.wolf/*` files were left unstaged.

---

## Deviations

One minor deviation from the per-file deliverable wording, documented for transparency:

**Cosmetic doc-cref restore not performed (no-op vs. instructed action).** The brief said: "if there's an XML doc comment that uses `<c>IStringLocalizer&lt;T&gt;</c>` (plain-text fallback the Cluster C subagent inserted to avoid CS1574 when foundation wasn't referenced), restore it to `<see cref="IStringLocalizer{T}"/>`." Reading the file before editing showed the Cluster C subagent never added such a fallback comment to `WorkflowServiceCollectionExtensions.cs` — the only doc-cref in the file references `IWorkflowRuntime`, which is workflow-internal and resolvable without foundation. Restoring a non-existent comment would mean fabricating prose; not doing so is the correct interpretation of "if … restore it" (conditional). Net: no `using Microsoft.Extensions.Localization;` needed either (it would have been unused).

No other deviations.

---

## Self-verdict: GREEN

- Build passes (0 warnings, 0 errors). ✓
- No `SUNFISH_I18N_001` warnings on `blocks-workflow`. ✓
- `blocks-workflow` now matches Pattern A canonical shape from `blocks-accounting`. ✓
- Diff-shape constraint honored — only the two scoped files in the code commit, plus this report in a separate commit. ✓
- Wave 2 Cluster C YELLOW deviation closed.
