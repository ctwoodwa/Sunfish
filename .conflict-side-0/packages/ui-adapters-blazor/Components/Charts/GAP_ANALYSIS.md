# Charts Gap Analysis

## Table of Contents

- [SunfishChart Gap Analysis](#marilochartgap-analysis)
- [SunfishChartSeries Gap Analysis](#marilochartseries-gap-analysis)

---

# SunfishChart Gap Analysis

## Summary

SunfishChart has substantial gaps relative to its spec. The spec describes a rich charting component with events, tooltips, axis configuration child components, drilldown, pan/zoom, stacked series, trendlines, templates, rendering modes, multiple axes, accessibility roles, CSS variable theming, and a `Refresh()` method. The current implementation is a minimal SVG-rendering component that supports only `Width`, `Height`, `Title`, and `ChildContent` parameters, with inline SVG generation. Most of the spec's features are not yet implemented.

---

## Spec → Code Gaps

### Parameters

| # | Spec Feature | Status | Severity | Details |
|---|---|---|---|---|
| 1 | `Width` parameter (`string`) | **Implemented** | -- | Defaults to `"100%"`. Spec matches. |
| 2 | `Height` parameter (`string`) | **Implemented** | -- | Defaults to `"300px"`. Spec matches. |
| 3 | `Class` parameter (inherited from base) | **Implemented** | -- | Available via `CombineClasses()`. Spec references `Class` in the parameters table. |
| 4 | `Transitions` parameter (`bool?`) | **Not implemented** | **Medium** | Spec documents a `Transitions` parameter to control chart animations. No such parameter exists in code. |
| 5 | `RenderAs` parameter (`RenderingMode`) | **Not implemented** | **Medium** | Spec (`rendering-modes.md`) describes SVG vs Canvas rendering modes via `RenderAs`. Not implemented. |

### Child Components / Tags

| # | Spec Feature | Status | Severity | Details |
|---|---|---|---|---|
| 6 | `<ChartTitle>` with `Text`, `Description`, `Position` | **Not implemented** | **High** | Spec shows `<ChartTitle Text="..." Description="..." Position="...">` as a child component with accessibility support. Code uses a simple `Title` string parameter and renders a plain `<div>`. No `Description` (for screen readers), no `Position`, no `<ChartSubtitle>`. |
| 7 | `<ChartLegend>` with `Position`, `Visible`, nested customization tags | **Not implemented** | **High** | Spec describes a full `<ChartLegend>` child component with `Position`, `Visible`, `<ChartLegendTitle>`, `<ChartLegendItem>`, marker customization. Code renders a hard-coded inline legend div with no configurability. |
| 8 | `<ChartSeriesItems>` container | **Not implemented** | **Medium** | Spec wraps `<ChartSeries>` tags inside `<ChartSeriesItems>`. Code uses `SunfishChartSeries` directly as children via `CascadingValue` without a container component. |
| 9 | `<ChartCategoryAxes>` / `<ChartCategoryAxis>` | **Not implemented** | **High** | Spec describes `<ChartCategoryAxis Categories="..." Name="..." Type="...">`. Code derives categories from series data only -- no axis configuration is possible. |
| 10 | `<ChartValueAxes>` / `<ChartValueAxis>` / `<ChartXAxes>` / `<ChartYAxes>` | **Not implemented** | **High** | Spec describes value axis configuration with `Max`, `Color`, titles, and positioning. Not implemented. |
| 11 | `<ChartTooltip>` and `<ChartSeriesTooltip>` | **Not implemented** | **High** | Spec describes both common and per-series tooltips with `Visible`, `Background`, `Color`, and `<Template>` support. No tooltip rendering exists in code. |
| 12 | `<ChartSettings>` / `<NoDataTemplate>` | **Not implemented** | **Low** | Spec describes a no-data template when all series are empty. Code silently returns nothing. |
| 13 | `<ChartSeriesStack>` (stacked series) | **Not implemented** | **Medium** | Spec describes simple stack, named stack, and stack-100% modes for bar/column/line/area. Not implemented. |

### Events

| # | Spec Feature | Status | Severity | Details |
|---|---|---|---|---|
| 14 | `OnSeriesClick` event | **Not implemented** | **High** | Spec describes `ChartSeriesClickEventArgs` with `DataItem`, `Category`, `Percentage`, `SeriesIndex`, `SeriesName`, `SeriesColor`, `CategoryIndex`. No click handling exists. |
| 15 | `OnAxisLabelClick` event | **Not implemented** | **Medium** | Spec describes `ChartAxisLabelClickEventArgs`. Not implemented. |
| 16 | `OnLegendItemClick` event | **Not implemented** | **Medium** | Spec describes `ChartLegendItemClickEventArgs` with `PointIndex`, `SeriesIndex`, `Text`. Not implemented. |
| 17 | `OnDrilldown` event | **Not implemented** | **Medium** | Spec describes drilldown with breadcrumb navigation. Not implemented. |

### Methods

| # | Spec Feature | Status | Severity | Details |
|---|---|---|---|---|
| 18 | `Refresh()` method | **Not implemented** | **Medium** | Spec documents `Refresh()` for programmatic re-render (e.g., after container resize). Not exposed. |
| 19 | `ResetDrilldownLevel()` method | **Not implemented** | **Low** | Depends on drilldown feature. Not implemented. |

### Advanced Features

| # | Spec Feature | Status | Severity | Details |
|---|---|---|---|---|
| 20 | Multiple axes support | **Not implemented** | **Medium** | Spec describes associating series with different Y/X axes. |
| 21 | Date axis support | **Not implemented** | **Medium** | Spec describes `ChartCategoryAxisType.Date` with aggregation. |
| 22 | Pan and zoom | **Not implemented** | **Medium** | Spec describes pan/zoom configuration. |
| 23 | Plot bands | **Not implemented** | **Low** | Spec describes colored axis range highlights. |
| 24 | Trendlines | **Not implemented** | **Low** | Spec describes linear trendline and moving average trendline series. |
| 25 | Label templates and formatting | **Not implemented** | **Medium** | Spec describes `<ChartSeriesLabels>` with `Visible`, `Format`, `Template`, and ARIA templates. |
| 26 | CSS variable theming (`--kendo-chart-*`) | **Not implemented** | **Low** | Spec describes CSS variable customization. Code uses hard-coded inline styles. |
| 27 | Accessibility: `role=graphics-document` | **Not implemented** | **High** | Spec requires `role="graphics-document document"` on the chart container and keyboard navigation. Code renders a plain `<div>` with no ARIA roles. |

---

## Code → Spec Gaps

| # | Implemented Feature | Documented? | Severity | Details |
|---|---|---|---|---|
| 1 | `Title` as a direct string parameter | **Partially** | **Medium** | Spec uses `<ChartTitle Text="...">` child component. Code uses a simple `[Parameter] public string? Title` which is a different API shape. |
| 2 | Hard-coded default color palette (`DefaultColors` array) | **No** | **Low** | Code defines 8 default colors. Spec does not document these specific defaults. |
| 3 | Inline SVG rendering (server-side SVG generation) | **No** | **Medium** | Code generates SVG entirely in C# via `RenderTreeBuilder`. The spec implies a client-side charting engine (with JS interop for tooltips, pan/zoom, etc.). This is a fundamental architectural difference. |
| 4 | Bar and Column types treated identically | **No** | **Medium** | Code renders both `ChartSeriesType.Bar` and `ChartSeriesType.Column` as vertical columns. Spec distinguishes Bar (horizontal) from Column (vertical). |
| 5 | Fixed SVG viewBox dimensions (600x300 cartesian, 300x300 pie) | **No** | **Low** | Implementation detail. Not documented. |

---

## Recommended Changes

| # | Priority | Action | Details |
|---|---|---|---|
| 1 | **High** | Implement `<ChartTooltip>` and per-series tooltip | Most impactful missing UX feature. Users cannot see data values on hover. |
| 2 | **High** | Implement `OnSeriesClick` event | Critical for interactive chart use cases (drilldown, detail views). |
| 3 | **High** | Add ARIA `role="graphics-document"` and keyboard navigation | Accessibility compliance gap. |
| 4 | **High** | Implement `<ChartCategoryAxis>` with `Categories` parameter | Enables independent axis configuration separate from series data. |
| 5 | **High** | Implement `<ChartLegend>` as a configurable child component | Replace hard-coded legend with positionable, toggleable legend. |
| 6 | **Medium** | Fix Bar vs Column rendering | Bar should render horizontally; Column vertically. Currently both render vertically. |
| 7 | **Medium** | Replace `Title` string parameter with `<ChartTitle>` child component | Align API shape with spec. The `Description` property is needed for accessibility. |
| 8 | **Medium** | Add `Transitions` parameter | Enable/disable chart animations. |
| 9 | **Medium** | Implement `<ChartSeriesItems>` container | Aligns with spec's component tree structure. |
| 10 | **Low** | Implement stacked series support | Stack, named stack, and 100% stack modes. |
| 11 | **Low** | Add CSS variable theming support | Replace hard-coded colors with CSS custom properties. |

---

## Open Questions / Ambiguities

1. **Architectural approach**: The spec implies a client-side rendering engine (JS interop for tooltips, pan/zoom, animations). The current implementation generates SVG server-side via `RenderTreeBuilder`. Should the implementation move to a JS interop model, or should the spec be revised to match a server-side SVG approach?

2. **`<ChartSeriesItems>` container vs direct children**: The spec wraps series in `<ChartSeriesItems>`. The current code uses `CascadingValue` with direct `SunfishChartSeries` children. Is the container component required for API compatibility, or is the simpler approach acceptable?

3. **Rendering modes (SVG vs Canvas)**: The spec describes `RenderAs` with `RenderingMode.SVG` and `RenderingMode.Canvas`. If the server-side SVG approach is kept, Canvas mode may not be feasible. Clarify intended scope.

4. **Chart type coverage**: The spec documents 19 chart types (Line, Bar, Column, Area, Pie, Donut, Scatter, ScatterLine, Bubble, Candlestick, OHLC, Heatmap, Waterfall, RangeArea, RangeColumn, RangeBar, RadarLine, RadarColumn, RadarArea). The `ChartSeriesType` enum defines only 7 (Line, Bar, Column, Area, Pie, Donut, Scatter). Are the remaining 12 types planned? What is the priority order?

5. **`Refresh()` method**: With server-side SVG rendering, does `Refresh()` simply call `StateHasChanged()`, or does it need additional logic?

---

---

# SunfishChartSeries Gap Analysis

## Summary

SunfishChartSeries implements the minimal viable surface: `Name`, `Data`, `Field`, `CategoryField`, `Type`, and `Color`. The spec describes a far richer component with numerous additional parameters, child components for tooltips, labels, markers, legend items, notes, error bars, and stacking configuration.

---

## Spec → Code Gaps

### Parameters

| # | Spec Feature | Status | Severity | Details |
|---|---|---|---|---|
| 1 | `Name` parameter | **Implemented** | -- | Default `"Series"`. Spec matches. |
| 2 | `Data` parameter (`IEnumerable<object>?`) | **Implemented** | -- | Spec shows `Data` accepting `List<object>` and model collections. |
| 3 | `Field` parameter | **Implemented** | -- | Used for value property name in data model. |
| 4 | `CategoryField` parameter | **Implemented** | -- | Used for category property name in data model. |
| 5 | `Type` parameter (`ChartSeriesType`) | **Implemented** | -- | Default `Line`. Supports 7 enum values. |
| 6 | `Color` parameter (`string?`) | **Implemented** | -- | Custom color override for the series. |
| 7 | `XField` / `YField` for scatter/bubble charts | **Not implemented** | **High** | Spec uses `XField` and `YField` for numerical chart types. Code only supports `Field` + `CategoryField`. Scatter charts cannot plot properly with two numeric axes. |
| 8 | `Visible` parameter | **Not implemented** | **Medium** | Spec shows `Visible` parameter for toggling series visibility (used with legend click). |
| 9 | `<ChartSeriesTooltip>` child component | **Not implemented** | **High** | Spec describes per-series tooltip with `Visible`, `Background`, `Color`, and `<Template>`. |
| 10 | `<ChartSeriesLabels>` child component | **Not implemented** | **Medium** | Spec describes data point labels with `Visible`, `Format`, `Template`, and ARIA support. |
| 11 | `<ChartSeriesMarkers>` child component | **Not implemented** | **Low** | Spec shows marker customization (type, size, background). |
| 12 | `<ChartSeriesLegendItem>` child component | **Not implemented** | **Low** | Spec shows per-series legend item customization with markers and highlight. |
| 13 | `<ChartSeriesStack>` child component | **Not implemented** | **Medium** | Spec describes `Enabled`, `Group`, and 100% stack via a child tag. |
| 14 | `<ChartSeriesNotes>` child component | **Not implemented** | **Low** | Spec mentions series notes. |
| 15 | Simple data binding (`Data` as `List<object>` of primitives) | **Not implemented** | **Medium** | Spec shows `Data="@(new List<object>() { 10, 2, 7, 5 })"` without `Field`/`CategoryField`. Code requires both `Field` and `CategoryField` since it uses reflection to extract values. Simple numeric lists will fail with reflection errors. |
| 16 | `SizeField` for bubble charts | **Not implemented** | **Low** | Spec's bubble chart type requires a third dimension. Not applicable until Bubble type is added. |
| 17 | `Gap` and `Spacing` for bar/column series | **Not implemented** | **Low** | Spec describes gap/spacing configuration. Code uses hard-coded 70%/30% ratios. |

### Missing Chart Types (in enum but spec-documented)

| # | Spec Type | In Enum? | In Rendering Code? | Severity |
|---|---|---|---|---|
| 18 | ScatterLine | No | No | **Medium** |
| 19 | Bubble | No | No | **Medium** |
| 20 | Candlestick | No | No | **Low** |
| 21 | OHLC | No | No | **Low** |
| 22 | Heatmap | No | No | **Low** |
| 23 | Waterfall | No | No | **Low** |
| 24 | RangeArea | No | No | **Low** |
| 25 | RangeColumn | No | No | **Low** |
| 26 | RangeBar | No | No | **Low** |
| 27 | RadarLine / RadarColumn / RadarArea | No | No | **Low** |

---

## Code → Spec Gaps

| # | Implemented Feature | Documented? | Severity | Details |
|---|---|---|---|---|
| 1 | `IDisposable` implementation (removes series from parent on dispose) | **No** | **Low** | Internal lifecycle detail. Not typically documented. |
| 2 | Reflection-based data extraction (`GetDataPoints()`) | **No** | **Low** | Internal implementation. Uses `Type.GetProperty()` to read `Field` and `CategoryField` from data items. |
| 3 | `CascadingParameter` for `ParentChart` | **No** | **Low** | Component registration pattern. Not typically documented for end users. |
| 4 | Inherits `SunfishComponentBase` (has `Class`, `Style`, `AdditionalAttributes`) | **No** | **Low** | `SunfishChartSeries` renders no DOM element of its own, so these base-class parameters have no effect. This is arguably a bug -- it inherits a base class designed for components with DOM output, but is a code-only component. |

---

## Recommended Changes

| # | Priority | Action | Details |
|---|---|---|---|
| 1 | **High** | Add `XField` and `YField` parameters | Required for scatter/numerical charts per spec. Without these, scatter charts use categorical placement instead of proper XY plotting. |
| 2 | **High** | Support simple data binding (primitive lists) | Detect when `Data` contains primitives and skip reflection. Currently throws when `Field` is empty and data is `List<object>` of `int`/`double`. |
| 3 | **Medium** | Add `Visible` parameter | Needed for legend-driven series toggle, a common interactive pattern. |
| 4 | **Medium** | Add `<ChartSeriesStack>` child support | Enables stacked bar/column/area charts. |
| 5 | **Medium** | Add `<ChartSeriesTooltip>` child support | Per-series tooltip configuration. |
| 6 | **Low** | Consider whether `SunfishComponentBase` is the right base class | Since `SunfishChartSeries` renders no markup, inheriting `Class`/`Style`/`AdditionalAttributes` is misleading. Consider using `ComponentBase` directly or a lighter base. |

---

## Open Questions / Ambiguities

1. **Simple data binding**: The spec shows `Data="@(new List<object>() { 10, 2, 5, 6 })"` without `Field` or `CategoryField`. The current reflection-based `GetDataPoints()` will fail on primitive values. Should the component detect primitive data and handle it as index-based category + direct value?

2. **Series registration timing**: `AddSeries` calls `StateHasChanged()` on the parent chart each time a series registers. With many series, this triggers multiple re-renders during initialization. Should registration be batched?

3. **Data type flexibility**: `Data` is typed as `IEnumerable<object>?`. The spec uses `List<object>` in examples. Should a generic `IEnumerable<TItem>` be supported (as a generic component `SunfishChartSeries<TItem>`) to avoid boxing and enable compile-time property checking instead of reflection?

4. **Color per data point (pie/donut)**: For pie/donut charts, the spec implies each data point can have its own color (via `ColorField`). The current implementation uses `DefaultColors[i % ...]`. Should `ColorField` be added to the series?
