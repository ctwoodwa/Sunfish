# Wave 2 Cluster B Cascade Report

**Date:** 2026-04-25
**Cluster:** B (mixed — 2 Pattern A, 2 Pattern B)
**Plan:** [docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md](../../docs/superpowers/plans/2026-04-25-global-ux-reconciliation-and-cascade-loop-plan.md) (v1.3)
**Branch:** `global-ux/wave-2-cluster-cascade`
**Cluster commit SHA:** `6f58188364083097e52842b5595f7e3f32d36171`
**Token:** `wave-2-cluster-B`
**Verdict:** GREEN

---

## Scope

Four packages in the blocks-ops cluster — mixed-pattern per `wave-2-cluster-freeze.md`:

| Package | Pattern | DI seam | Files shipped |
|---|---|---|---|
| `packages/blocks-assets` | **B** (Razor SDK, no DI surface) | n/a — consumer wires | 3 (resx + resx + cs) |
| `packages/blocks-inspections` | **A** | `DependencyInjection/InspectionsServiceCollectionExtensions.cs` | 4 (resx + resx + cs + DI edit) |
| `packages/blocks-maintenance` | **A** | `DependencyInjection/MaintenanceServiceCollectionExtensions.cs` | 4 (resx + resx + cs + DI edit) |
| `packages/blocks-scheduling` | **B** (Razor SDK, no DI surface) | n/a — consumer wires | 3 (resx + resx + cs) |

Total: 14 file touches (12 creates + 2 edits).

---

## Per-package file list

### blocks-assets (Pattern B)

- `packages/blocks-assets/Resources/Localization/SharedResource.resx` — en-US, 8 keys, all `<comment>` start with `[scaffold-pilot — replace in Plan 6]`. Pilot string `action.save` = `Saving asset record…`.
- `packages/blocks-assets/Resources/Localization/SharedResource.ar-SA.resx` — ar-SA, 8 keys. `action.save` = `حفظ سجل الأصل`.
- `packages/blocks-assets/Localization/SharedResource.cs` — namespace `Sunfish.Blocks.Assets.Localization`; `public sealed class SharedResource { }`. XML doc notes Pattern B / consumer-wires.

No DI edit — Pattern B.

### blocks-inspections (Pattern A)

- `packages/blocks-inspections/Resources/Localization/SharedResource.resx` — en-US, 8 keys. Pilot string `action.save` = `Saving inspection record…`.
- `packages/blocks-inspections/Resources/Localization/SharedResource.ar-SA.resx` — ar-SA, 8 keys. `action.save` = `حفظ سجل التفتيش`.
- `packages/blocks-inspections/Localization/SharedResource.cs` — namespace `Sunfish.Blocks.Inspections.Localization`; `public sealed class SharedResource { }`.
- `packages/blocks-inspections/DependencyInjection/InspectionsServiceCollectionExtensions.cs` — added `using Microsoft.Extensions.DependencyInjection.Extensions;` and `using Sunfish.Foundation.Localization;`. Inside `AddInMemoryInspections`, added single line `services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));` (idempotent — `TryAdd` semantics). Updated XML doc to note the binding contribution and that the composition root still owns `services.AddLocalization()` (per Cluster A sentinel ratification).

### blocks-maintenance (Pattern A)

- `packages/blocks-maintenance/Resources/Localization/SharedResource.resx` — en-US, 8 keys. Pilot `action.save` = `Saving maintenance record…`.
- `packages/blocks-maintenance/Resources/Localization/SharedResource.ar-SA.resx` — ar-SA, 8 keys. `action.save` = `حفظ سجل الصيانة`.
- `packages/blocks-maintenance/Localization/SharedResource.cs` — namespace `Sunfish.Blocks.Maintenance.Localization`; `public sealed class SharedResource { }`.
- `packages/blocks-maintenance/DependencyInjection/MaintenanceServiceCollectionExtensions.cs` — same edit shape as inspections. Added two `using`s, single `TryAddSingleton` line, updated XML doc.

### blocks-scheduling (Pattern B)

- `packages/blocks-scheduling/Resources/Localization/SharedResource.resx` — en-US, 8 keys. Pilot `action.save` = `Saving schedule entry…`.
- `packages/blocks-scheduling/Resources/Localization/SharedResource.ar-SA.resx` — ar-SA, 8 keys. `action.save` = `حفظ إدخال الجدول`.
- `packages/blocks-scheduling/Localization/SharedResource.cs` — namespace `Sunfish.Blocks.Scheduling.Localization`; `public sealed class SharedResource { }`. XML doc references `SunfishScheduler` / `SunfishAllocationScheduler` / `SunfishCalendar` view-switcher.

No DI edit — Pattern B.

---

## Namespaces

| Package | Marker namespace |
|---|---|
| blocks-assets | `Sunfish.Blocks.Assets.Localization` |
| blocks-inspections | `Sunfish.Blocks.Inspections.Localization` |
| blocks-maintenance | `Sunfish.Blocks.Maintenance.Localization` |
| blocks-scheduling | `Sunfish.Blocks.Scheduling.Localization` |

All four match the inferred convention `Sunfish.Blocks.<X>.Localization` and the foundation/Bridge precedent.

---

## Build excerpts

`dotnet build <package>.csproj --nologo` for all four packages. All GREEN, 0 errors, 0 `SUNFISH_I18N_001` warnings. The single warning per build is the pre-existing `NETSDK1206` for the linux-x64-musl RID on `YDotNet.Native.Linux` — entirely unrelated to localization, present on the baseline.

```
Sunfish.Blocks.Assets       -> bin\Debug\net11.0\Sunfish.Blocks.Assets.dll       Build succeeded.   1 Warning(s)   0 Error(s)   00:00:20.66
Sunfish.Blocks.Inspections  -> bin\Debug\net11.0\Sunfish.Blocks.Inspections.dll  Build succeeded.   1 Warning(s)   0 Error(s)   00:00:11.06
Sunfish.Blocks.Maintenance  -> bin\Debug\net11.0\Sunfish.Blocks.Maintenance.dll  Build succeeded.   1 Warning(s)   0 Error(s)   00:00:10.67
Sunfish.Blocks.Scheduling   -> bin\Debug\net11.0\Sunfish.Blocks.Scheduling.dll   Build succeeded.   1 Warning(s)   0 Error(s)   00:00:10.99
```

The `SUNFISH_I18N_001` analyzer (wired via `Directory.Build.props` cascade per commit `d4dc625e`) inspected every `<data>` entry across the 8 new resx files and produced no findings — every entry carries a non-empty `<comment>` per spec §3A.

---

## Pattern A vs. Pattern B handling

**Pattern A (inspections, maintenance).** Followed v1.3 brief and Cluster A sentinel ratification:

- Single line added inside the existing `Add<X>(...)` method:
  ```csharp
  services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));
  ```
  Used `TryAddSingleton` per this brief's directive; equivalent to Cluster A's `services.TryAdd(ServiceDescriptor.Singleton(...))` — both paths land on the same `ServiceDescriptor` and are idempotent across multiple `Add<X>()` calls.
- **No `services.AddLocalization()` call.** Cluster A sentinel finding ratified by reviewer: class libraries don't take a hard PackageReference on `Microsoft.Extensions.Localization`; the composition root owns that call. Documented in updated XML doc on the registration method.
- `using` lines added in canonical order (already-sorted): `Microsoft.Extensions.DependencyInjection`, then `Microsoft.Extensions.DependencyInjection.Extensions` (new), then `Sunfish.Blocks.<X>.Services`, then `Sunfish.Foundation.Localization` (new).

**Pattern B (assets, scheduling).** Per v1.3 brief amendment in the cluster freeze: no DI surface exists, so no DI edit. `.resx` + marker class only. Marker class XML doc explicitly notes "Pattern B package — Razor SDK with no DI surface; downstream consumers wire the binding in their composition root." Cluster E will wire the consumer-side binding for these in `apps/kitchen-sink`.

---

## Diff-shape constraint compliance

Touched ONLY the paths enumerated in the brief — no `.csproj`, README, sample, or other files. `.resx` files were auto-embedded by the Razor / NET SDK default conventions; no project file changes required (consistent with foundation, Bridge, and Cluster A precedents).

`git show --stat 6f581883` confirms the 14 changed paths fall entirely within:

- `<package>/Resources/Localization/SharedResource{,.ar-SA}.resx` (×8)
- `<package>/Localization/SharedResource.cs` (×4)
- `<package>/DependencyInjection/<X>ServiceCollectionExtensions.cs` (×2, Pattern A only)

---

## Deviations

**None.** All deliverables executed per brief:

- Marker class visibility: `public sealed` (matches foundation; per cluster freeze amendment §1).
- Pattern B handling: skipped DI edit and documented in marker XML doc + this report (per cluster freeze amendment §2).
- `blocks-workflow` non-standard path amendment (§3): not relevant to Cluster B (no workflow package in scope).
- Pilot strings: each Pattern B/A package's `action.save` carries package-scoped phrasing (assets: "Saving asset record…"; inspections: "Saving inspection record…"; maintenance: "Saving maintenance record…"; scheduling: "Saving schedule entry…"). Other 7 keys carry generic-but-package-scoped phrasing so the bundle is shippable as-is per Plan 2 Task 3.5.
- ar-SA pilots: locale-appropriate translations of each English pilot (e.g., `حفظ سجل الأصل` for assets); other 7 ar-SA keys match the brief's spec verbatim.

---

## Summary

GREEN. Cluster B cascade complete: 14 file touches across 4 packages, 1 path-scoped commit, all four packages build clean, no `SUNFISH_I18N_001` warnings, no `.csproj` or out-of-scope changes. Cluster A's sentinel pattern (no consumer-coupled `AddLocalization()`) preserved. Pattern A and Pattern B paths exercised cleanly in a single mixed cluster.

**Cluster commit:** `6f58188364083097e52842b5595f7e3f32d36171`
**Report commit:** (this file — separate commit)
**Verdict:** GREEN
