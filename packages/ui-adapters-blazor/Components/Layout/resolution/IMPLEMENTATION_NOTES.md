# Implementation Notes: Layout Components

> **Note (2026-04-03):** `SunfishGrid` was renamed to `SunfishGridLayout`. Historical references to `SunfishGrid` in this document refer to the layout component now at `Layout/SunfishGridLayout.razor`.

## Design Decisions

### SunfishStack

1. **Provider handles direction, component handles alignment/sizing**: Simplified `ISunfishCssProvider.StackClass` to take only `StackDirection`. Alignment, spacing, width, and height are applied as inline styles by the component. This separates concerns: providers control theming and CSS framework mapping; components control layout logic.

2. **Two-axis alignment model**: Replaced single `Alignment` parameter with `HorizontalAlign`/`VerticalAlign`. The component maps these to `justify-content`/`align-items` based on the current `Orientation`:
   - Horizontal stack: HorizontalAlign → justify-content, VerticalAlign → align-items
   - Vertical stack: VerticalAlign → justify-content, HorizontalAlign → align-items

3. **Inline styles over CSS classes for arbitrary values**: `Spacing`, `Width`, `Height` accept arbitrary CSS values (e.g., "16px", "100%", "auto"). These cannot be mapped to predefined CSS classes, so inline styles are the correct approach.

## Approach

- Component builds inline styles via `StyleBuilder` in `BuildStackStyles()`
- `CombineStyles()` is NOT used because the component needs to append `Style` parameter inside `BuildStackStyles()` to avoid double-style attributes on the div
- The `when` guard on each `AddStyle` call prevents empty properties from being emitted

### SunfishGrid

1. **Dual-mode rendering**: SunfishGrid supports two layout modes:
   - **Flex container mode** (default): When no grid parameters are set, renders with provider's `GridClass()` and `CombineStyles()`. Works with existing `SunfishRow`/`SunfishColumn` children.
   - **CSS Grid mode**: When `Columns`, `Rows`, or child definitions are present, adds `display: grid` and grid template properties as inline styles.

2. **Child component registration via CascadingValue**: `SunfishGridLayoutColumn` and `SunfishGridLayoutRow` are data-holder components that register their Width/Height with the parent `SunfishGrid` via `CascadingParameter`. The parent cascades itself as a fixed value and children call `AddColumnDefinition`/`AddRowDefinition` in `OnInitialized`.

3. **String parameters override child definitions**: If `Columns="200px 1fr"` is set explicitly, it takes precedence over any `SunfishGridLayoutColumn` children. This gives users the simpler string shorthand option.

4. **SunfishGridLayoutItem uses inline grid positioning**: `grid-row` and `grid-column` CSS properties are set as inline styles, avoiding the need for provider-specific CSS classes.

5. **Reused StackAlignment enum**: Instead of creating a new `GridAlignment` enum, reused `StackAlignment` for `HorizontalAlign`/`VerticalAlign`. Values `Start`, `Center`, `End`, `Stretch` map cleanly to CSS Grid's `justify-items`/`align-items`.

## Code Notes

### Breaking Changes
- `Direction` parameter renamed to `Orientation`
- `Alignment` parameter removed, replaced by `HorizontalAlign`/`VerticalAlign`
- Default orientation changed from `Vertical` to `Horizontal`
- `ISunfishCssProvider.StackClass` signature changed: removed `StackAlignment` parameter
- Both Bootstrap and FluentUI providers updated
