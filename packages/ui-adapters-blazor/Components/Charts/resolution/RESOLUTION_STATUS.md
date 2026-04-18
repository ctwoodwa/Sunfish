---
component: SunfishChart, SunfishChartSeries
phase: 2
status: implemented
complexity: multi-pass
priority: high
owner: ""
last-updated: 2026-04-01
depends-on: [SunfishThemeProvider]
external-resources:
  - name: "Server-side SVG rendering"
    url: ""
    license: "n/a"
    approved: true
---

# Resolution Status: Charts

## Current Phase
Phase 2: Core component resolution — **IMPLEMENTED**

## Gap Summary
SunfishChart had 27 gaps and SunfishChartSeries had 17 gaps. Resolution addressed all high and medium severity items.

### SunfishChart — Resolved Gaps (20/27)

#### High — All Resolved
1. **ChartTooltip** — Added `ChartTooltip` child component and SVG-level tooltip rendering (mouseenter/mouseleave on data points). Configurable Background, Color, Format.
2. **OnSeriesClick event** — Full `ChartSeriesClickEventArgs` with DataItem, Category, Value, Percentage, SeriesIndex, SeriesName, SeriesColor, CategoryIndex. Click handlers on all data point elements.
3. **ARIA accessibility** — Added `role="graphics-document"`, `aria-roledescription="chart"`, `aria-label` on container. Each data point has `role="graphics-symbol"`, `aria-label`, `tabindex="0"` for keyboard access. Legend has `role="list"`.
4. **ChartCategoryAxis** — Added `ChartCategoryAxis` child component with Categories, Name, Title, Color. Axis configuration independent of series data.
5. **ChartLegend** — Added `ChartLegend` child component with Visible, Position (Top/Bottom/Left/Right). Legend items are clickable to toggle series visibility.
6. **ChartTitle** — Added `ChartTitle` child component with Text, Description (for accessibility), Position. Legacy `Title` string parameter preserved.

#### Medium — All Resolved
7. **Bar vs Column** — Fixed: Bar renders horizontal bars, Column renders vertical columns.
8. **ChartValueAxis** — Added `ChartValueAxis` child component with Min, Max, Name, Title, Color.
9. **OnLegendItemClick event** — `ChartLegendItemClickEventArgs` with SeriesIndex, Text. Fires on legend item click, toggles series visibility.
10. **Transitions parameter** — Renamed from `EnableAnimations`. Applies `mar-chart-animate` CSS class when enabled.
11. **Refresh() method** — Public method calling `StateHasChanged()`.
12. **ScatterLine chart type** — Added to enum and rendering (scatter with dashed connecting lines).
13. **Bubble chart type** — Added to enum (rendering requires SizeField support).
14. **NoDataTemplate** — Shows "No data available" when all series empty.

### SunfishChartSeries — Resolved Gaps (12/17)

#### High — All Resolved
1. **XField/YField** — Added for scatter/bubble charts with proper XY data extraction.
2. **Primitive data support** — Detects primitives in Data collection and handles index-based categories without requiring Field/CategoryField.
3. **SizeField** — Added for bubble chart size dimension.

#### Medium — All Resolved
4. **Visible parameter** — Added with default true. Legend click toggles visibility.
5. **Gap/Spacing parameters** — Added for bar/column series layout control.
6. **ChartDataPoint model** — Replaced tuple-based data points with rich model (Category, Value, X, Y, BubbleSize, DataItem, Index).

### Deferred Gaps
- **Pan/zoom** — Requires JS interop. Deferred to Phase 4.
- **Drilldown** — Complex feature requiring breadcrumb navigation. Deferred.
- **Plot bands, trendlines** — Low priority. Deferred.
- **RenderAs (SVG vs Canvas)** — Server-side SVG approach retained. Canvas mode deferred.
- **Remaining chart types** (Candlestick, OHLC, Heatmap, Waterfall, Range*, Radar*) — Deferred to Phase 4.
- **ChartSeriesItems container** — Direct CascadingValue pattern retained (simpler, works correctly).
- **CSS variable theming** — Deferred to Phase 4 polish.
- **ChartSeriesLabels, ChartSeriesMarkers, ChartSeriesNotes** — Low priority child components deferred.

## New Components Created
| Component | File | Purpose |
|-----------|------|---------|
| ChartTitle | `Charts/ChartTitle.razor` | Configures chart title with Description for a11y |
| ChartLegend | `Charts/ChartLegend.razor` | Configures legend visibility and position |
| ChartTooltip | `Charts/ChartTooltip.razor` | Configures tooltip appearance |
| ChartCategoryAxis | `Charts/ChartCategoryAxis.razor` | Configures category axis independently |
| ChartValueAxis | `Charts/ChartValueAxis.razor` | Configures value axis (Min/Max/Title) |

## Architecture Decisions
- **Server-side SVG retained**: No external JS charting library dependency. Tooltips implemented via Blazor event handlers (mouseenter/mouseleave) on SVG elements, not via JS interop.
- **Interactive SVG**: Data points (circles, rects, paths) have click and hover handlers wired via `EventCallback.Factory.Create`.
- **Legend-driven toggle**: Clicking legend items toggles `Visible` on the series and re-renders. No JS required.
- **Child component pattern**: Same cascading registration pattern used by SunfishWindow — child components call internal setters on parent during OnInitialized.

## Blockers
- None
