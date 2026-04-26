# Wave 1 Plan 4 Cluster C Report — Forms + Overlays bUnit-axe Cascade

**Branch:** `global-ux/wave-1-plan4-forms-overlays-clusterC`
**Token:** `wave-1-plan4-cluster-C`
**Status:** GREEN (6 passed, 1 skipped with documented a11y bug; 0 failures)
**Date:** 2026-04-25

---

## Scope

Per the brief, this cluster wires the bUnit-axe a11y harness to **3 Forms** + **4 Overlays**
= 7 components total, each in its own per-component test file under
`packages/ui-adapters-blazor-a11y/tests/Forms/` and `.../Overlays/`. Pattern mirrors the
existing harness sentinel (`FreshnessBadgeContractTests`) for file shape and the
`PilotMatrixTests` axe-runner approach for assertion shape: render via bUnit, host the
markup in a Playwright chromium page through `PlaywrightPageHost`, run `axe-core` against
it, assert zero moderate+ violations.

## Files added

### Forms (3)

| File | Component under test | Test count |
|---|---|---|
| `packages/ui-adapters-blazor-a11y/tests/Forms/SunfishLabelA11yTests.cs` | `SunfishLabel` | 1 |
| `packages/ui-adapters-blazor-a11y/tests/Forms/SunfishFieldA11yTests.cs` | `SunfishField` | 1 |
| `packages/ui-adapters-blazor-a11y/tests/Forms/SunfishTextBoxA11yTests.cs` | `SunfishTextBox` | 1 |

### Overlays (4)

| File | Component under test | Test count |
|---|---|---|
| `packages/ui-adapters-blazor-a11y/tests/Overlays/SunfishPopupA11yTests.cs` | `SunfishPopup` | 1 (skip — see findings) |
| `packages/ui-adapters-blazor-a11y/tests/Overlays/SunfishWindowA11yTests.cs` | `SunfishWindow` | 1 |
| `packages/ui-adapters-blazor-a11y/tests/Overlays/WindowActionButtonA11yTests.cs` | `WindowActionButton` | 1 |
| `packages/ui-adapters-blazor-a11y/tests/Overlays/WindowActionsA11yTests.cs` | `WindowActions` (in host `SunfishWindow`) | 1 |

**Total:** 7 files, 7 facts, 6 passing + 1 skipped.

## Component selection rationale

- **Forms**: chose the Container layer (`SunfishLabel`, `SunfishField`) plus the canonical
  bound-input control (`SunfishTextBox`) so the cluster covers the form-structure
  affordances (label/for binding, field grouping) and a real input control rendered with
  a value bound (per brief: "Forms: input bound to a value").
- **Overlays**: there are exactly 7 razor components under
  `Components/Overlays/` (1 popup + 6 window-family). The 4 chosen are the only
  combinations that produce independently-testable DOM:
  - `SunfishPopup` and `SunfishWindow` (top-level overlays with `Visible=true`).
  - `WindowActionButton` (renders standalone — its `[CascadingParameter]` is null-tolerant).
  - `WindowActions` (registration-only child that throws without a parent — exercised inside
    a host `SunfishWindow`, the realistic usage shape).
  The other three Window children (`WindowContent`, `WindowFooter`, `WindowTitle`) are
  pure registration shells that throw without a parent, so testing them adds no DOM
  coverage beyond what `SunfishWindow` already exercises — they are intentionally not in
  scope for this cluster.

## Test pattern

Each file follows the same shape:

1. `IClassFixture<Ctx>` per file — shared `BunitContext` + shared `PlaywrightPageHost` so
   Playwright chromium starts once per file, amortising the startup cost.
2. `Ctx.InitializeAsync()` resolves `PlaywrightPageHost.GetAsync()` (lazy singleton).
3. The fact renders the component with realistic params (Forms: `Value` + accessible name;
   Overlays: `Visible=true` so the opened-state markup is emitted).
4. `AxeRunner.RunAxeAsync(rendered.Markup, page)` injects axe-core into the hosted page
   and returns typed `AxeResult`.
5. Assertion filters violations to `Impact >= Moderate` and asserts zero. Failure messages
   surface the violated rule IDs so debug is straightforward.

The `axe-core` JS bundle is auto-discovered from the repo's pnpm node_modules
(`axe-core@4.11.3`); CI overrides via `SUNFISH_AXE_CORE_PATH` if needed.

## A11y bug findings

### `SunfishPopup` — `aria-dialog-name` (skipped, NOT fixed per brief)

When rendered with `FocusTrap=true`, `SunfishPopup` emits `role="dialog"` and
`aria-modal="true"` on the popup root, but does **not** set `aria-label` or
`aria-labelledby`. axe-core (rule `aria-dialog-name`, WCAG 4.1.2 Name/Role/Value, impact
`serious`) reports the dialog has no accessible name.

The test is marked
`[Fact(Skip = "axe violation: aria-dialog-name — popup root needs aria-label or aria-labelledby")]`
with an inline comment documenting the violation. Per the brief, Cluster C does not fix
component bugs — the finding is logged here for the next remediation pass to triage.

**Suggested fix (out of scope for this cluster):** add an `AriaLabel` parameter to
`SunfishPopup` and emit it as `aria-label` when `FocusTrap=true` (or accept an
`AriaLabelledBy` for the case where the popup wraps a heading). Mirrors the contract
already enforced on `SunfishNodeHealthBar` (see `NodeHealthBarContractTests`).

No bugs found in any other tested component.

## Infrastructure note (not a component bug)

`SunfishWindow` and the host-window scenario for `WindowActions` inject three internal
interfaces from `Sunfish.UIAdapters.Blazor.Internal.Interop` (`IElementMeasurementService`,
`IDragService`, `IResizeInteractionService`). The new test assembly
(`Sunfish.UIAdapters.Blazor.A11y.Tests`) does not have an `InternalsVisibleTo` entry on
`Sunfish.UIAdapters.Blazor`, so the interface symbols are not directly nameable here.
Per the brief's diff-shape constraint (no csproj edits), the tests resolve the interface
types through reflection on the host assembly and register `NSubstitute.Substitute.For`
proxies — which works because `ui-adapters-blazor.csproj` already grants
`InternalsVisibleTo` to `DynamicProxyGenAssembly2` (the proxy generator NSubstitute uses
under the hood).

This keeps the cluster's diff scope strictly to the two test directories with no edits
to component or csproj files.

## Build + test gate (brief-mandated)

```text
$ dotnet build packages/ui-adapters-blazor-a11y/Sunfish.UIAdapters.Blazor.A11y.csproj
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ SUNFISH_AXE_CORE_PATH="/c/Projects/sunfish/node_modules/.pnpm/axe-core@4.11.3/node_modules/axe-core/axe.min.js" \
    dotnet test packages/ui-adapters-blazor-a11y/tests/tests.csproj \
    --filter "FullyQualifiedName~Forms|FullyQualifiedName~Overlays" --no-build

[xUnit.net 00:00:01.21]     Sunfish.UIAdapters.Blazor.A11y.Tests.Overlays.SunfishPopupA11yTests.SunfishPopup_VisibleWithFocusTrap_ZeroAxeViolations [SKIP]
  Skipped Sunfish.UIAdapters.Blazor.A11y.Tests.Overlays.SunfishPopupA11yTests.SunfishPopup_VisibleWithFocusTrap_ZeroAxeViolations [1 ms]

Passed!  - Failed:     0, Passed:     6, Skipped:     1, Total:     7, Duration: 526 ms
```

## Diff shape

Two new directories, seven new files, zero modifications to existing files:

```
packages/ui-adapters-blazor-a11y/tests/Forms/
  SunfishFieldA11yTests.cs
  SunfishLabelA11yTests.cs
  SunfishTextBoxA11yTests.cs
packages/ui-adapters-blazor-a11y/tests/Overlays/
  SunfishPopupA11yTests.cs
  SunfishWindowA11yTests.cs
  WindowActionButtonA11yTests.cs
  WindowActionsA11yTests.cs
```

No csproj edits, no component edits, no edits to existing tests, no other paths touched.

## Verdict

**GREEN** — 7-component cascade landed under one commit, build gate clean, test gate green
(6 pass + 1 skipped/documented). One real a11y bug surfaced and routed to a future
remediation pass per brief instructions; no other a11y violations in the cluster.
