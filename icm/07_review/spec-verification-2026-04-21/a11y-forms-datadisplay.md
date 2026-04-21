# A11y audit — Forms & Data Display — 2026-04-21

## Scope

`apps/kitchen-sink/Pages/Components/Forms/**/Accessibility/Demo.razor`
`apps/kitchen-sink/Pages/Components/DataDisplay/**/Accessibility/Demo.razor`

Only folders that actually contain an `Accessibility/` (or `KeyboardAndAccessibility/`) subfolder with a `Demo.razor` are in scope. The remaining Forms components (`Form`, `Validation`, `DropDownTree`, `FlatColorPicker`) and most Data Display components (`Barcodes`, `Carousel`, `Gantt`, `Grid`, `Heatmap`, `ListBox`, `Map`, `PivotGrid`, `Popover`, `Scheduler`, `Sparkline`, `Spreadsheet`, `TreeList`) only have an `Overview/` demo folder and no a11y demo yet — those are flagged below as gaps to be addressed by future work.

## Summary

| Component | Before | After | Changes |
|-----------|--------|-------|---------|
| Forms / SearchBox      | good | good | — Existing demo uses `role="search"` landmark, `aria-labelledby`, `aria-controls`, live-region results, `aria-describedby` hint, `KbdHint="/"`, disabled-announced variant, and a full keyboard reference table. |
| Forms / Select         | good | good | — Existing demo shows `SunfishLabel` + `aria-labelledby`, `aria-required` + `aria-describedby`, `aria-invalid` paired with a `role="alert"` error, disabled-announced variant, and a platform-keyboard reference table (type-ahead, Home/End, Arrow). |
| Data Display / Avatar  | good | good | — Image with descriptive `Alt`, image with empty `Alt=""` next to visible name, initials auto-exposed, interactive `<button>`-wrapped avatar with action-scoped `aria-label`, plus ARIA contract `<dl>`. |
| Data Display / Badge   | good | good | — Trigger + count pattern using wrapper `aria-label` and `aria-hidden` on children; avatar presence dot with `role="status"` + `aria-label`; standalone status badge; color-not-alone and WCAG AA contrast notes. |
| Data Display / Card    | good | good | — Two cards as named landmarks via `role="region"` + `aria-labelledby` pointing at header id; descriptive `SunfishCardImage` Alt; icon-action labelling guidance; keyboard/Tab-order discussion. |
| Data Display / DataGrid | good | good | — Native `<table>` semantics, `aria-sort`, `Navigable="true"` with arrow-key cell navigation, `aria-labelledby` to visible heading, checkbox selection column, keyboard reference (Tab, Arrow, Home/End, Space/Enter), aria-busy note. |
| Data Display / DataSheet | good | good | — Explicit `role="grid"` / `row` / `gridcell`, `aria-rowindex` / `aria-colindex`, `aria-readonly`, `aria-invalid` + `aria-errormessage`, live-region announcer, full Excel-style keyboard map (F2, Ctrl+V bulk paste, Ctrl+Home/End, Enter/Shift+Enter). |
| Data Display / List    | good | good | — Full ARIA listbox pattern: `role="listbox"`, `aria-multiselectable`, `aria-activedescendant` roving focus, `AriaLabel` vs `AriaLabelledBy` choice, `aria-disabled` propagation, keyboard reference, toolbar `role="toolbar"` note. |
| Data Display / ListView | good | good | — Shows the correct use of `role="listbox"` wrapper + per-item `role="option"` + `aria-selected` inside `ItemTemplate`; calls out browse-list (`role="list"`/`listitem`) vs selectable-list distinction; keyboard guidance for each mode. |
| Data Display / TreeView | good | good | — Canonical WAI-ARIA TreeView demo: `role="tree"`, `treeitem`, `group`, `aria-expanded`, `aria-selected`, tri-state `aria-checked` (true/false/mixed), full keyboard table (Arrow, Home/End, `*`, F2, Escape), expected screen-reader output block, and ARIA contract `<dl>`. |

**Count: 10 demos audited, 0 rewritten, 10 already good, 0 flagged for blocking engineering work.**

## Notes

### Demos rewritten
None. Every in-scope `Accessibility/Demo.razor` already demonstrates real accessibility features (ARIA attributes, keyboard shortcuts, focus/selection management, screen-reader expectations, landmarks, live regions) — not just "render the component." No trivial or empty placeholders were found.

### Coverage gaps (not rewrites — new demo files required, out of this audit's write-scope)

These Forms and Data Display components currently have only an `Overview/Demo.razor` and no `Accessibility/Demo.razor` yet. They should be created in a follow-up pass (tracked separately so the 12-agent parallel audit stays non-conflicting):

**Forms (4):**
- `Forms/DropDownTree` — needs an accessibility demo once combobox+tree ARIA is wired through (`aria-expanded`, `aria-controls`, `aria-activedescendant`, role="combobox" pattern).
- `Forms/FlatColorPicker` — needs a demo covering the non-text contrast / color-name pairing story for color swatch pickers.
- `Forms/Form` — needs a demo covering `<fieldset>`/`<legend>`, error summary, live-region submit announcements.
- `Forms/Validation` — needs a demo covering `aria-invalid`, `aria-errormessage`, and summary-vs-inline announcements.

**Data Display (13):**
`Barcodes`, `Carousel`, `Gantt`, `Grid`, `Heatmap`, `ListBox`, `Map`, `PivotGrid`, `Popover`, `Scheduler`, `Sparkline`, `Spreadsheet`, `TreeList` — each should get an `Accessibility/Demo.razor` sibling to its existing `Overview/Demo.razor` in a follow-up ICM task.

### Engineering work that would enable richer future demos (informational)

None of the existing demos is blocked by a missing API. A few observations worth logging for future work (no action this pass):

- `SunfishList` already exposes `AriaLabel` / `AriaLabelledBy` / `aria-activedescendant` internally; the demo reflects that contract accurately.
- `SunfishSelect` relies on the native `<select>` element, so platform a11y behaviour is inherited but cannot be fully parity-tested across OS screen readers without environment-specific notes — the demo correctly says "platform-dependent" for Enter/Space opening behaviour.
- `SunfishListView` does not itself emit listbox ARIA (by design: it is a lightweight repeater) — the demo correctly shows consumers how to upgrade it to a listbox. If we ever add a `SemanticMode="Listbox"` shortcut parameter, this demo should be revisited.

## Build verification

Full `dotnet build --nologo -v quiet` of `apps/kitchen-sink` — **0 errors, 1 pre-existing warning** (`CS0162 Unreachable code detected`) in a generated temp file, unrelated to this audit.

No files were modified during this audit pass.
