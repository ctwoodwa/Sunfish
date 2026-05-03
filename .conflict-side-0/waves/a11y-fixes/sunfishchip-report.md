# SunfishChip a11y triple-violation — fix report

**Token:** `fix-a11y-sunfishchip-triple`
**Branch:** `worktree-agent-a1ad44ca15e02946b` (isolated worktree, no push)
**Code commit:** `bd7a1d292789192aa63d15d0c23f03c2062fa36a`
**Status:** GREEN

## Scope

One architectural fix that resolves the three entangled axe violations the Wave 1
Buttons cascade flagged on `SunfishChip` (per `wave-1-plan4-cluster-A-report.md`).

| # | Rule | Severity | Status |
|---|------|----------|--------|
| 1 | `aria-required-parent` | Critical | RESOLVED |
| 2 | `nested-interactive`   | Serious  | RESOLVED |
| 3 | `target-size` (WCAG 2.2) | Serious  | RESOLVED |

## Files changed

- `packages/ui-adapters-blazor/Components/Buttons/Chip/SunfishChip.razor`
- `packages/ui-adapters-blazor-a11y/tests/Buttons/SunfishChipA11yTests.cs`

No sibling components were touched; `SunfishChipSet.razor` continues to render its
own internal markup (does not consume `SunfishChip` as a child) and is therefore
unaffected by the chip's structural change.

## Architectural fix

`SunfishChip` now picks one of four render modes based on a new `Listbox` parameter
(default `false`) plus its existing `Removable` / `Selectable` / `OnClick` state:

1. **Listbox mode** (`Listbox=true`) — root is a non-interactive
   `<span role="option" aria-selected>`. Selection / keyboard handling is the
   parent `role="listbox"` container's responsibility (roving tabindex, etc.),
   so the option itself carries no `@onclick` or `tabindex`. This satisfies
   `aria-required-parent` *for the case where consumers actually want option
   semantics* and avoids `nested-interactive` because the option is no longer
   marked interactive itself.

2. **Removable standalone** — root is a non-interactive `<div>` wrapper
   containing two sibling `<button>` elements: `sf-chip__action` (main label,
   carries selection / click) and `sf-chip__remove` (× control). Sibling buttons
   instead of nested buttons — `nested-interactive` cannot fire. The remove
   button carries inline `min-width:24px; min-height:24px; padding:0 4px;` to
   meet WCAG 2.2 §2.5.8 target-size minimum.

3. **Interactive standalone** (Selectable or `OnClick.HasDelegate`, not removable) —
   root is a single semantic `<button type="button">` with `aria-pressed` for
   selection state. No `role="option"` leaks outside a listbox container.

4. **Decorative standalone** (no Selectable, no OnClick, no Removable) — root is
   a plain `<span>` with no role; purely a styled label.

## Why this resolves all three at once

- **aria-required-parent:** mode 1 is the only mode that emits `role="option"`,
  and it's only entered when the consumer opts in via `Listbox=true`. Modes 2-4
  emit no `role="option"`, so a chip rendered outside a listbox can never
  trigger the rule.
- **nested-interactive:** in the only mode that previously caused this violation
  (Removable), the chip root is now a non-interactive `<div>` wrapping two
  sibling `<button>` elements. Neither button is nested inside another
  interactive widget.
- **target-size:** the remove button enforces ≥24×24 CSS px via inline
  `min-width` / `min-height`. Inline styles are used so the rule passes regardless
  of the host site's stylesheet load order.

## API surface

One new public parameter on `SunfishChip`:

```csharp
[Parameter] public bool Listbox { get; set; }
```

Default `false`. Backward compatible for all standalone usages: chips that were
clicked from the consumer side now render as `<button>` instead of
`<span role="option" tabindex>`, which is a strict accessibility improvement.
Existing consumers that depended on the literal `role="option"` markup outside
a listbox container were already triggering an axe critical and need to set
`Listbox=true` (and provide a parent `role="listbox"`) to keep that semantics.

## Verification

- `dotnet build packages/ui-adapters-blazor/Sunfish.UIAdapters.Blazor.csproj`
  — exit 0, 0 errors, 5 warnings (all pre-existing NETSDK1206 RID warnings,
  unrelated to this change).
- `dotnet test packages/ui-adapters-blazor-a11y/tests/tests.csproj
  --filter "FullyQualifiedName~SunfishChip"` — **6 passed, 0 failed, 0 skipped**
  (3 SunfishChip default/selected/removable + 3 SunfishChipSet theory cases).
  All three previously-skipped Fact annotations are now active.

## Diff shape compliance

Two files modified. No sibling components, no shared CSS, no other test projects.

## Out of scope (noted, not addressed)

- `SunfishChipSet.razor` itself renders chip-options inline rather than via
  `SunfishChip`, and does so correctly inside a `role="listbox"` container —
  but its inline option markup carries `@onclick` on the option, which would
  trigger the same `nested-interactive` axe rule when `Removable=true`. That
  is a separate fix on a sibling component; per the diff-shape constraint it
  was deliberately not touched here.
