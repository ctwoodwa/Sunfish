# Wave 2 Cluster E Report — apps/kitchen-sink

**Date:** 2026-04-25
**Cluster role:** Composition root (special-case); canary alongside Cluster D1.
**Plan:** [docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md](../../docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md) (v1.3)
**Branch:** `global-ux/wave-2-cluster-cascade`
**Cluster commit SHA:** `33ec91fe`
**Self-verdict:** GREEN

---

## Files created / edited

| Path | Op | Notes |
|---|---|---|
| `apps/kitchen-sink/Resources/Localization/SharedResource.resx` | NEW | 8 keys (severity.*, action.*, state.loading); en-US neutral; pilot strings flavoured for kitchen-sink ("Saving demo…"). Every `<comment>` starts with `[scaffold-pilot — replace in Plan 6]`. |
| `apps/kitchen-sink/Resources/Localization/SharedResource.ar-SA.resx` | NEW | Same 8 keys; canonical ar-SA translations per brief; `[scaffold-pilot]` token preserved. |
| `apps/kitchen-sink/Localization/SharedResource.cs` | NEW | `public sealed class SharedResource { }` in namespace `Sunfish.KitchenSink.Localization`. Marker-only; matches foundation visibility (`public`, not `internal`) per cluster-freeze brief amendment. |
| `apps/kitchen-sink/Program.cs` | EDIT | Added `using` directives + `services.AddLocalization()` and `services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>))` block. Placed immediately after `AddSunfish().AddSunfishInteropServices()` so the localizer is registered before any provider that might consume it during request-scope construction. |

No `.csproj` edit was required — `Microsoft.NET.Sdk.Web` provides `Microsoft.Extensions.Localization` transitively (Bridge confirms this same pattern uses `AddLocalization` without an explicit `PackageReference`). The strict v1.3 diff-shape constraint holds.

## Diff shape verification

`git diff --stat HEAD~1 HEAD` for the cluster commit shows exactly 4 paths, all under `apps/kitchen-sink/`:

```
apps/kitchen-sink/Localization/SharedResource.cs                       | 28 ++++
apps/kitchen-sink/Program.cs                                           | 12 ++
apps/kitchen-sink/Resources/Localization/SharedResource.ar-SA.resx     | 60 ++++++
apps/kitchen-sink/Resources/Localization/SharedResource.resx           | 76 ++++++++
```

Path-scoped `git add apps/kitchen-sink/Resources/ apps/kitchen-sink/Localization/ apps/kitchen-sink/Program.cs` was used (no unscoped `git add`); other in-flight changes from Cluster D1 (ui-adapters-blazor, ui-core) and unrelated `.wolf/` updates remained unstaged.

## Build gate

```
dotnet build apps/kitchen-sink/Sunfish.KitchenSink.csproj --nologo
```

Result excerpt:

```
  Sunfish.KitchenSink -> C:\Projects\sunfish\apps\kitchen-sink\bin\Debug\net11.0\Sunfish.KitchenSink.dll

Build succeeded.
    2 Warning(s)
    0 Error(s)

Time Elapsed 00:00:20.54
```

Both warnings are pre-existing and unrelated to localization:
- `CS0162` unreachable code in a generated demo source (existing repo state).
- `NETSDK1206` linux-x64-musl RID compatibility note from a transitive YDotNet dependency.

**No `SUNFISH_I18N_001` warnings.** The analyzer (wired via Directory.Build.props in commit `d4dc625e`) consumed both the en-US and ar-SA resx and accepted every `<data>` entry — the `[scaffold-pilot — replace in Plan 6]` token satisfies the non-empty `<comment>` rule per spec §3A + ADR 0034.

## Composition-root rationale

Per Cluster A's sentinel ratification, Pattern B packages (ui-core, blocks-tasks, blocks-forms, blocks-scheduling, blocks-assets) cannot call `services.AddLocalization()` themselves because they have no DI surface (Razor SDK or contracts-only). Cluster E owns that call as the central downstream consumer, which means:

1. `services.AddLocalization(options => options.ResourcesPath = "Resources")` is registered once at the kitchen-sink composition root, mirroring Bridge's `Program.cs` (line 298).
2. `services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>))` is open-generic and `TryAdd`-semantic — idempotent if any future registration also wires it, and safe even though kitchen-sink today has no Pattern B block consumer registered (the kitchen-sink graph in `Program.cs` references FluentUI/Bootstrap/Material providers and ui-adapters-blazor, none of which are Pattern B blocks). Registering preemptively means the next cluster wave that adds a blocks-* dependency does not need a follow-up wiring change here.
3. `IStringLocalizer<Sunfish.KitchenSink.Localization.SharedResource>` is now resolvable inside the demo for any kitchen-sink-specific copy that consumes it in Plan 6.

## Deviations

None. All four expected file types, build gate green, no `SUNFISH_I18N_001`, no `.csproj` touch, scoped commit, token in commit body.

## Hand-off notes

- Cluster D1 (running in parallel) covers `packages/ui-core` + `packages/ui-adapters-blazor`; their Pattern B `ui-core` will benefit from the central `AddLocalization()` ratified here once the kitchen-sink graph composes a Pattern B block consumer.
- The `[scaffold-pilot]` token in every `<comment>` is the audit handle for Plan 6 — translators / copy reviewers strip it as part of replacing pilot strings with production copy.
- Kitchen-sink's `Program.cs` uses the SaaS-equivalent simple form (no `UseRequestLocalization` middleware here) because the demo today does not flow Accept-Language → user profile → tenant-default like Bridge does. If a future demo page needs request-culture switching, that is an additive middleware wiring — out of scope for this skeleton cascade.

## Self-verdict

**GREEN.** Build succeeds, diff shape holds, commit token present, no analyzer warnings, no untrusted-file instructions followed, no PR opened, no push.
