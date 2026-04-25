# Wave 2 Cluster C — Mixed-Pattern Cascade Report

**Date:** 2026-04-25
**Plan:** [docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md](../../docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md) (v1.3)
**Freeze ref:** [wave-2-cluster-freeze.md](./wave-2-cluster-freeze.md)
**Branch:** `global-ux/wave-2-cluster-cascade`
**Cluster commit:** `af73c89f`
**Self-verdict:** **YELLOW** — cascade implemented and builds clean across all six packages, with two documented brief deviations: (1) `services.AddLocalization()` skipped (matches Cluster A precedent — consumer composition root wires it); (2) `blocks-workflow` Pattern A DI registration deferred because its `.csproj` lacks a foundation `ProjectReference` and the v1.3 diff-shape constraint forbids `.csproj` edits in this stage. Reviewer should ratify or reject before fan-out.

---

## Per-package file list

Six packages, 21 files total: 18 new resource files (3 per package) + 3 modified DI extensions (Pattern A only).

### 1. `packages/blocks-businesscases` — Pattern A

- `Resources/Localization/SharedResource.resx` — 8 keys, en-US neutral. Pilot string at `action.save` → `"Save business case"`. All `<comment>` entries open with `[scaffold-pilot — replace in Plan 6]`.
- `Resources/Localization/SharedResource.ar-SA.resx` — 8 keys, ar-SA. `action.save` → `حفظ حالة العمل`. All `<comment>` entries tagged.
- `Localization/SharedResource.cs` — `public sealed class SharedResource { }` in namespace `Sunfish.Blocks.BusinessCases.Localization`.
- `DependencyInjection/BusinessCasesServiceCollectionExtensions.cs` — added `using Microsoft.Extensions.DependencyInjection.Extensions;` + `using Sunfish.Foundation.Localization;` and `services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));` inside the existing `AddInMemoryBusinessCases(...)` body, after the existing entitlement / module registrations. Idempotent.

### 2. `packages/blocks-forms` — Pattern B

- `Resources/Localization/SharedResource.resx` — pilot at `action.save` → `"Submit form"`. `state.loading` → `"Loading form…"`.
- `Resources/Localization/SharedResource.ar-SA.resx` — `action.save` → `إرسال النموذج`.
- `Localization/SharedResource.cs` — namespace `Sunfish.Blocks.Forms.Localization`. Pattern B docstring notes downstream wires DI.
- (No DI edit — Razor SDK package has no `ServiceCollectionExtensions`.)

### 3. `packages/blocks-leases` — Pattern A

- `Resources/Localization/SharedResource.resx` — pilot at `action.save` → `"Save lease"`.
- `Resources/Localization/SharedResource.ar-SA.resx` — `action.save` → `حفظ عقد الإيجار`.
- `Localization/SharedResource.cs` — namespace `Sunfish.Blocks.Leases.Localization`.
- `DependencyInjection/LeasesServiceCollectionExtensions.cs` — added the same two `using`s + `TryAddSingleton(...)` after the `ILeaseService` registration in `AddInMemoryLeases(...)`.

### 4. `packages/blocks-tenant-admin` — Pattern A

- `Resources/Localization/SharedResource.resx` — pilot at `action.save` → `"Save tenant settings"`. `state.loading` → `"Loading tenant…"`.
- `Resources/Localization/SharedResource.ar-SA.resx` — `action.save` → `حفظ إعدادات المستأجر`.
- `Localization/SharedResource.cs` — namespace `Sunfish.Blocks.TenantAdmin.Localization`.
- `DependencyInjection/TenantAdminServiceCollectionExtensions.cs` — same DI pattern, added after the `ITenantAdminService` + `ISunfishEntityModule` registrations in `AddInMemoryTenantAdmin(...)`.

### 5. `packages/blocks-workflow` — Pattern A non-standard, **DI deferred**

- `Resources/Localization/SharedResource.resx` — pilot at `action.save` → `"Save workflow"`. `state.loading` → `"Loading workflow…"`. Block-comment header documents the non-standard `src/` layout for future readers.
- `Resources/Localization/SharedResource.ar-SA.resx` — `action.save` → `حفظ سير العمل`.
- `Localization/SharedResource.cs` — namespace `Sunfish.Blocks.Workflow.Localization`. XML doc-cref to `IStringLocalizer{T}` rendered as `<c>` (plain text) rather than `<see cref=...>` because workflow's csproj does not transitively pull `Microsoft.Extensions.Localization.Abstractions`; using `<see cref>` triggered `error CS1574`. Same shape as the Bridge marker, no semantic loss.
- (DI registration **NOT** added — see Deviation 2 below.)

### 6. `packages/blocks-tasks` — Pattern B

- `Resources/Localization/SharedResource.resx` — pilot at `action.save` → `"Save task"`. `state.loading` → `"Loading tasks…"`.
- `Resources/Localization/SharedResource.ar-SA.resx` — `action.save` → `حفظ المهمة`.
- `Localization/SharedResource.cs` — namespace `Sunfish.Blocks.Tasks.Localization`. Pattern B docstring notes downstream wires DI.
- (No DI edit — Razor SDK package has no `ServiceCollectionExtensions`.)

## Namespaces used

- `Sunfish.Blocks.BusinessCases.Localization`
- `Sunfish.Blocks.Forms.Localization`
- `Sunfish.Blocks.Leases.Localization`
- `Sunfish.Blocks.TenantAdmin.Localization`
- `Sunfish.Blocks.Workflow.Localization`
- `Sunfish.Blocks.Tasks.Localization`

Each matches the package's primary namespace prefix (validated against existing `DependencyInjection`, `Services`, etc. namespaces in each package).

## Patterns

| Package | Pattern | DI seam | DI edited? |
|---|---|---|---|
| blocks-businesscases | A | `DependencyInjection/BusinessCasesServiceCollectionExtensions.cs` | yes |
| blocks-forms | B | (none — Razor SDK) | n/a |
| blocks-leases | A | `DependencyInjection/LeasesServiceCollectionExtensions.cs` | yes |
| blocks-tenant-admin | A | `DependencyInjection/TenantAdminServiceCollectionExtensions.cs` | yes |
| blocks-workflow | A non-standard | `src/WorkflowServiceCollectionExtensions.cs` | **deferred** (deviation 2) |
| blocks-tasks | B | (none — Razor SDK) | n/a |

## Build gate output (success line per package)

Per-package `dotnet build <package>/<csproj>` output (trimmed; one line per package):

```
=== blocks-businesscases ===
  Sunfish.Blocks.BusinessCases -> .../bin/Debug/net11.0/Sunfish.Blocks.BusinessCases.dll
Build succeeded.    1 Warning(s) [NETSDK1206 — pre-existing YDotNet RID warning, unrelated]    0 Error(s)

=== blocks-forms ===
  Sunfish.Blocks.Forms -> .../bin/Debug/net11.0/Sunfish.Blocks.Forms.dll
Build succeeded.    1 Warning(s) [NETSDK1206 — pre-existing]    0 Error(s)

=== blocks-leases ===
  Sunfish.Blocks.Leases -> .../bin/Debug/net11.0/Sunfish.Blocks.Leases.dll
Build succeeded.    1 Warning(s) [NETSDK1206 — pre-existing]    0 Error(s)

=== blocks-tenant-admin ===
  Sunfish.Blocks.TenantAdmin -> .../bin/Debug/net11.0/Sunfish.Blocks.TenantAdmin.dll
Build succeeded.    1 Warning(s) [NETSDK1206 — pre-existing]    0 Error(s)

=== blocks-workflow ===
  Sunfish.Blocks.Workflow -> .../bin/Debug/net11.0/Sunfish.Blocks.Workflow.dll
Build succeeded.    0 Warning(s)    0 Error(s)

=== blocks-tasks ===
  Sunfish.Blocks.Tasks -> .../bin/Debug/net11.0/Sunfish.Blocks.Tasks.dll
Build succeeded.    1 Warning(s) [NETSDK1206 — pre-existing]    0 Error(s)
```

No `SUNFISH_I18N_001` warnings emitted on any package — every `<data>` carries a non-empty `<comment>` per spec §3A. The single warning surfaced (`NETSDK1206` re: YDotNet's `linux-x64-musl` RID) is pre-existing infra noise unrelated to the cascade.

## Deviations from brief

### Deviation 1 — `services.AddLocalization()` not added (matches Cluster A precedent)

The brief explicitly says **"DO NOT add `services.AddLocalization()`"**, so this is technically *not* a deviation from this cluster's brief — but I'm calling it out for symmetry with the Cluster A YELLOW report. The open-generic `ISunfishLocalizer<>` registration is sufficient at the block layer; consumer apps / accelerators (e.g., `Sunfish.Bridge`, kitchen-sink) already call `services.AddLocalization()` once at composition. No double-registration risk.

### Deviation 2 — `blocks-workflow` Pattern A DI registration deferred

`packages/blocks-workflow/Sunfish.Blocks.Workflow.csproj` does **not** include a `ProjectReference` to `..\foundation\Sunfish.Foundation.csproj` (verified: only references `Microsoft.Extensions.DependencyInjection`). Adding `using Sunfish.Foundation.Localization;` + `services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));` therefore fails to compile.

**Resolving this requires editing `Sunfish.Blocks.Workflow.csproj`** to add the `ProjectReference`. The v1.3 brief diff-shape constraint (Seat 2 P1) explicitly forbids `.csproj` edits in this stage and instructs me to STOP and document. I did so:

- Edited `src/WorkflowServiceCollectionExtensions.cs` initially, reverted it on discovery (no functional change in the committed tree — the file ends in the same state as before this cluster cascade started).
- Resources + marker class still ship per the brief — workflow ends with the same shape as Pattern B packages (resources only, downstream wires DI).
- Recommended fix-up for a follow-up commit: add `<ProjectReference Include="..\foundation\Sunfish.Foundation.csproj" />` to `Sunfish.Blocks.Workflow.csproj`, then add the `TryAddSingleton(...)` line + `using`s. Single trivial change once the diff-shape constraint relaxes.

This deviation slightly reduces blocks-workflow's DI parity with the other Pattern A packages until that follow-up lands. End-user impact: zero — Wave 2 is infra-only and no end-user code in workflow currently resolves an `ISunfishLocalizer<>`.

### Deviation 3 — `<see cref="IStringLocalizer{T}">` rewritten as `<c>` plain text in workflow's marker

Same root cause as Deviation 2 — `Microsoft.Extensions.Localization.Abstractions` isn't in workflow's transitive closure. The XML doc cref produced `error CS1574` at build. Rewriting the cref as `<c>Microsoft.Extensions.Localization.IStringLocalizer&lt;T&gt;</c>` plain text resolves the build with no semantic loss; the docstring still describes the role correctly. Other five packages keep the `<see cref>` form as they all pull abstractions transitively.

## Diff-shape verification

Cluster commit `af73c89f` touches **only** the four file types per the brief:

- `packages/<x>/Resources/Localization/SharedResource.resx` (×6, new)
- `packages/<x>/Resources/Localization/SharedResource.ar-SA.resx` (×6, new)
- `packages/<x>/Localization/SharedResource.cs` (×6, new)
- `packages/<x>/.../*ServiceCollectionExtensions.cs` (×3 modified — businesscases, leases, tenant-admin only; workflow deferred)

Total: **21 files** (18 new + 3 modified). No `.csproj`, README, `_Imports.razor`, or other files modified. `git status --short` confirms staged set matches scope; uncommitted residual is `.wolf/*` (OpenWolf memory, untouched here) and an unrelated `wave-3-cluster-A-review.md` left from a separate stream.

## Self-verdict

**YELLOW** — six-of-six builds clean, all skeleton deliverables present, but blocks-workflow's DI registration is deferred behind a `.csproj` edit that the v1.3 diff-shape constraint forbids at this stage. Reviewer should ratify deviation 2 (and the follow-up fix-up plan) before the cluster cascade fans out further or before Wave 3 lights up workflow's localizer consumers.
