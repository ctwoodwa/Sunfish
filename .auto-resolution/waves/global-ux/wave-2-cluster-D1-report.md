# Wave 2 — Cluster D1 (canary) Cascade Report

**Cluster:** D1 (`packages/ui-core` + `packages/ui-adapters-blazor`)
**Role:** Wave-2 canary — first parallel-dispatch cluster paired with E.
**Branch:** `global-ux/wave-2-cluster-cascade` (driver pre-created; no push).
**Verdict:** **GREEN.**

---

## Summary

Mixed-pattern cluster per the Wave-2 cluster freeze. Two packages, two
deliverable shapes:

| Package | Pattern | DI surface? | Localizer registered here? |
|---|---|---|---|
| `packages/ui-core` | **B** (contracts-only library) | None | No — consumer wires it |
| `packages/ui-adapters-blazor` | **A** (adapter w/ DI extensions) | `RendererServiceCollectionExtensions` | Yes — via `TryAddSingleton` |

`AddLocalization()` is **NOT** called from either package, per the Cluster A
sentinel ratification — that responsibility lives in consumer composition
roots (apps / accelerators).

---

## Per-package file list

### `packages/ui-core` (Pattern B)

| Path | Purpose |
|---|---|
| `packages/ui-core/Resources/Localization/SharedResource.resx` | en-US source, 8 keys, scaffold-pilot tag in every `<comment>` |
| `packages/ui-core/Resources/Localization/SharedResource.ar-SA.resx` | ar-SA satellite, full 8/8 coverage |
| `packages/ui-core/Localization/SharedResource.cs` | `public sealed class SharedResource { }` marker |

- **Namespace:** `Sunfish.UICore.Localization`
- **DI edit:** none (Pattern B has no DI surface).
- **Pilot strings (en-US):** UI-context phrases including `action.save = "Saving…"`
  (present-progressive — distinct from the foundation source's imperative `"Save"`
  and the adapter's imperative `"Save"` — illustrates that the cascade puts
  package-appropriate UI-context strings on each layer rather than copy-pasting
  the foundation source).

### `packages/ui-adapters-blazor` (Pattern A)

| Path | Purpose |
|---|---|
| `packages/ui-adapters-blazor/Resources/Localization/SharedResource.resx` | en-US source, 8 keys, scaffold-pilot tag |
| `packages/ui-adapters-blazor/Resources/Localization/SharedResource.ar-SA.resx` | ar-SA satellite, 8/8 |
| `packages/ui-adapters-blazor/Localization/SharedResource.cs` | `public sealed class SharedResource { }` marker |
| `packages/ui-adapters-blazor/Renderers/DependencyInjection/RendererServiceCollectionExtensions.cs` | edited — added open-generic localizer registration |

- **Namespace:** `Sunfish.UIAdapters.Blazor.Localization`
- **DI edit:** added two `using` directives (`Microsoft.Extensions.DependencyInjection.Extensions`,
  `Sunfish.Foundation.Localization`) and a single line inside the existing
  `AddSunfishBlazorDomRenderer(this IServiceCollection)` method:

  ```csharp
  services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));
  ```

  Idempotent (`TryAdd*`); applies to whichever `SharedResource` marker the
  consumer asks for; consumer keeps the right to override.
- **Did NOT** add `services.AddLocalization()` — sentinel ratification.

---

## ar-SA translations (canonical per brief, both packages)

| Key | Value |
|---|---|
| `severity.info` | `معلومات` |
| `severity.warning` | `تحذير` |
| `severity.error` | `خطأ` |
| `severity.critical` | `حرج` |
| `action.save` | `حفظ` (adapter) / `جارٍ الحفظ…` (ui-core, present-progressive matches en-US `"Saving…"`) |
| `action.cancel` | `إلغاء` |
| `action.retry` | `إعادة المحاولة` |
| `state.loading` | `جارٍ التحميل…` |

The trailing U+2026 ellipsis is preserved on `state.loading` and on ui-core's
present-progressive `action.save`. Comments in the satellites carry the
`[scaffold-pilot — replace in Plan 6]` token alongside the translator note,
matching foundation precedent.

No unescaped `<`, `>`, `&` in any value or comment — verified by clean build
(no resgen failures) and by visual inspection.

---

## Build gate

Both project builds succeeded with **0 warnings, 0 errors**, including no
`SUNFISH_I18N_001` diagnostics.

### `dotnet build packages/ui-core/Sunfish.UICore.csproj`

```
  Sunfish.Analyzers.LocComments -> ...\Sunfish.Analyzers.LocComments.dll
  Sunfish.Tooling.LocalizationXliff -> ...\Sunfish.Tooling.LocalizationXliff.dll
  Sunfish.Foundation -> ...\Sunfish.Foundation.dll
  Sunfish.UICore -> ...\Sunfish.UICore.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:06.63
```

The analyzer ProjectReference and `<AdditionalFiles>` glob in
`Directory.Build.props` auto-attached when ui-core gained
`Resources/Localization/` — confirmed by `Sunfish.Analyzers.LocComments`
appearing in the build graph for ui-core where it had not before.

### `dotnet build packages/ui-adapters-blazor/Sunfish.UIAdapters.Blazor.csproj`

```
  Sunfish.Analyzers.LocComments -> ...\Sunfish.Analyzers.LocComments.dll
  Sunfish.Tooling.LocalizationXliff -> ...\Sunfish.Tooling.LocalizationXliff.dll
  Sunfish.Foundation -> ...\Sunfish.Foundation.dll
  Sunfish.Kernel -> ...\Sunfish.Kernel.dll
  Sunfish.UICore -> ...\Sunfish.UICore.dll
  ...
  Sunfish.UIAdapters.Blazor -> ...\Sunfish.UIAdapters.Blazor.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:11.43
```

Same auto-attach happened for the adapter. The `TryAddSingleton` line
compiles cleanly under `TreatWarningsAsErrors=true` with `Nullable=enable` —
the open-generic API takes `Type` parameters so no nullability annotation
issue surfaced.

A grep for `SUNFISH_I18N_001` in the adapter build output returned no hits.

---

## Diff-shape constraint compliance (v1.3 Seat 2 P1)

Touched only the file types named in the per-package deliverables. No
`.csproj`, README, sample, or other file modified. Path-scoped staging used:

```bash
git add packages/ui-core/Resources/ packages/ui-core/Localization/ \
        packages/ui-adapters-blazor/Resources/ \
        packages/ui-adapters-blazor/Localization/ \
        packages/ui-adapters-blazor/Renderers/DependencyInjection/RendererServiceCollectionExtensions.cs
```

`git status` after staging confirmed only the 7 expected files were staged;
`.wolf/*` housekeeping diffs and the untrusted `waves/global-ux/wave-3-cluster-A-review.md`
were intentionally left unstaged.

---

## Deviations

None. The cluster matched the brief exactly:

- 8 keys per resx (severity.{info,warning,error,critical}, action.{save,cancel,retry}, state.loading)
- Pilot strings package-scoped in en-US source per package
- `[scaffold-pilot — replace in Plan 6]` token in every `<comment>`
- Canonical ar-SA values used; ui-core's `action.save` pairs with its present-progressive en-US to keep the meaning aligned
- Marker class `public sealed class SharedResource { }` — same shape as foundation's
- Pattern A: `TryAddSingleton` of open generic, `using` for `Sunfish.Foundation.Localization`
- Pattern B: no DI edit, documented above

`Directory.Build.props` discovery means the resx files automatically picked
up the SUNFISH_I18N_001 analyzer and the XLIFF tooling — nothing to wire
manually at the cluster level.

---

## Cluster commit

```
SHA  : 079d3817
title: feat(i18n): wave-2-cluster-D1 skeleton cascade — Plan 2 Task 3.5
files: 7 (3 ui-core, 4 ui-adapters-blazor)
+    : 356 insertions
```

Token `wave-2-cluster-D1` present in the commit body (per discipline rule).

---

## Self-verdict

**GREEN.** Both builds clean; no SUNFISH_I18N_001 hits; diff shape exactly
matches the brief; no foundation / accelerator / blocks files touched; no
pushes performed. Ready for Wave 3 review.
