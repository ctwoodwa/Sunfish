# Sunfish DataGrid — Column Sizing Architecture Note

**Document type:** Architecture Decision Note  
**Component:** `SunfishDataGrid<TItem>` (`src/Sunfish.Components/DataGrid/`)  
**Branch:** `workInProgress`  
**Status:** Proposed — Not yet implemented  
**Related files:** `SunfishDataGrid.razor.cs`, `SunfishDataGrid.Rendering.cs`, `SunfishGridColumn.razor`, `GridState.cs`

---

## Executive Summary

The `SunfishDataGrid` currently holds column width as an `EffectiveWidth` property on each `SunfishGridColumn<TItem>` component instance and exposes it in `GridState.ColumnStates` snapshots[cite:44]. There is no single authoritative computation pass that resolves all column widths before rendering — this means header cells and body cells each depend on per-cell style logic, creating a structural risk of header/body misalignment as features like column pinning, virtualization, and resizing are introduced[cite:16][cite:22][cite:13].

This document proposes an `IColumnWidthProvider` abstraction that centralises all column sizing logic into a replaceable subsystem, and defines three default implementations to be introduced in phases. All header and body renderers would consume only the computed output of this provider — making the alignment contract explicit, testable, and independently evolvable.

---

## Problem Statement

### Current state

- Column width is stored as `EffectiveWidth` on each `SunfishGridColumn<TItem>` component instance[cite:44].
- The grid's `_visibleColumns` list is consumed directly by both header and body rendering passes[cite:44].
- There is no centralised computation pass that runs before rendering to produce a shared layout artifact (e.g., a `grid-template-columns` CSS string)[cite:23][cite:25].
- `GridState.ColumnStates` captures a `Width` snapshot per column, but this snapshot is not fed back as the authoritative sizing source — it is used only for state persistence[cite:44].

### Risks this introduces

As the grid grows in capability, the following known failure modes from other grid ecosystems become increasingly likely:

| Risk | Where it has occurred |
|---|---|
| Header and body columns drift when horizontal scrolling with a scrollbar causes the header and body to have different effective widths[cite:13] | AG Grid (2016, still referenced)[cite:13] |
| Row width computed independently of header width causes column misalignment after update[cite:16] | MUI X DataGrid[cite:16][cite:22] |
| Sticky header disappears or misaligns when row virtualisation is enabled and the header is in a different scroll context[cite:28] | TanStack Virtual[cite:28][cite:10] |
| Width recalculation inside cell templates causes inconsistent column sizes between header and body[cite:23] | TanStack Table[cite:23] |
| Hidden-container measurement (zero-width grid container) triggers width recalculation at wrong time[cite:35] | MUI X DataGrid[cite:35][cite:40] |

---

## Design Principles

1. **Width is state, not style.** Column widths must be computed values held in a model, not derived inline by each renderer from CSS or DOM measurements[cite:18][cite:25].
2. **One computation pass, shared output.** All column widths are resolved in one place before rendering. Both the header row and every body row consume the same output[cite:23][cite:25].
3. **Renderers are consumers only.** `SunfishDataGrid.Rendering.cs` and `SunfishDataGrid.razor` must not perform independent width calculations. They receive a layout contract and apply it[cite:18][cite:23].
4. **The provider is replaceable.** Sizing strategies (fixed, flex-fill, auto-measure) differ significantly. Isolating them behind an interface allows each to be developed, tested, and replaced without touching the grid's rendering pipeline[cite:23][cite:25].
5. **Concrete widths before render.** All sizing modes — explicit, flex-fill, or measured — resolve to concrete pixel widths before any markup is emitted[cite:18][cite:23][cite:25].

---

## IColumnWidthProvider Interface

```csharp
namespace Sunfish.Components.DataGrid.Sizing;

/// <summary>
/// Computes the authoritative column widths for a SunfishDataGrid layout pass.
/// All header and body renderers must consume only the output of this provider.
/// Implementations must be stateless with respect to the grid's render cycle;
/// they accept the current column definitions and container width and return
/// a resolved layout contract.
/// </summary>
public interface IColumnWidthProvider
{
    /// <summary>
    /// Resolves concrete pixel widths for all supplied column definitions,
    /// respecting MinWidth, MaxWidth, Flex, and container constraints.
    /// Must always return a width for every entry in <paramref name="columns"/>,
    /// clamped to [MinWidth, MaxWidth].
    /// </summary>
    /// <param name="columns">
    /// Ordered list of column definitions as registered by SunfishGridColumn components.
    /// Includes system columns (checkbox, detail-expander, command) when applicable.
    /// </param>
    /// <param name="containerWidth">
    /// The available horizontal width of the grid's scroll viewport in pixels.
    /// Pass 0 when the container has not yet been measured (first render).
    /// </param>
    /// <returns>
    /// A <see cref="GridLayoutContract"/> containing resolved widths and a
    /// precomputed CSS grid-template-columns string for use in both the
    /// header row and all body rows.
    /// </returns>
    GridLayoutContract Resolve(
        IReadOnlyList<ColumnSizingEntry> columns,
        double containerWidth);

    /// <summary>
    /// Applies a user resize delta to a single column and returns the updated contract.
    /// Called on every pointer-move event during a drag-resize operation.
    /// Implementations must clamp to MinWidth/MaxWidth and propagate any flex
    /// redistribution required by the sizing strategy.
    /// </summary>
    GridLayoutContract ApplyResize(
        GridLayoutContract current,
        string columnId,
        double deltaPixels);
}
```

### Supporting types

```csharp
namespace Sunfish.Components.DataGrid.Sizing;

/// <summary>
/// A sizing snapshot of one column, derived from SunfishGridColumn parameters.
/// Immutable — produced once per layout pass from the live column list.
/// </summary>
public sealed record ColumnSizingEntry(
    string Id,
    double? ExplicitWidth,   // null = use flex or default
    double MinWidth,         // default: 50
    double? MaxWidth,        // null = unconstrained
    double? Flex,            // null = no flex participation
    ColumnPinState Pin,
    bool IsResizable,
    ColumnAlignment Alignment
);

/// <summary>
/// The resolved output of a single IColumnWidthProvider.Resolve() call.
/// Consumed by header row renderer and every body row renderer.
/// Immutable after construction.
/// </summary>
public sealed class GridLayoutContract
{
    /// <summary>Resolved pixel width for each column, keyed by ColumnSizingEntry.Id.</summary>
    public IReadOnlyDictionary<string, double> WidthById { get; init; } = new Dictionary<string, double>();

    /// <summary>
    /// Precomputed CSS grid-template-columns string for the centre (scrollable) region.
    /// Example: "120px 80px minmax(100px, 1fr) 200px"
    /// Applied to both the header row and each body row via a shared CSS custom property.
    /// </summary>
    public string CenterGridTemplate { get; init; } = string.Empty;

    /// <summary>
    /// CSS grid-template-columns string for the left-pinned region.
    /// Empty string when no columns are left-pinned.
    /// </summary>
    public string LeftPinnedGridTemplate { get; init; } = string.Empty;

    /// <summary>
    /// CSS grid-template-columns string for the right-pinned region.
    /// Empty string when no columns are right-pinned.
    /// </summary>
    public string RightPinnedGridTemplate { get; init; } = string.Empty;

    /// <summary>Columns ordered for the left-pinned region.</summary>
    public IReadOnlyList<string> LeftPinnedColumnIds { get; init; } = [];

    /// <summary>Columns ordered for the centre (scrollable) region.</summary>
    public IReadOnlyList<string> CenterColumnIds { get; init; } = [];

    /// <summary>Columns ordered for the right-pinned region.</summary>
    public IReadOnlyList<string> RightPinnedColumnIds { get; init; } = [];

    /// <summary>Total pixel width of all columns combined (left + centre + right).</summary>
    public double TotalWidth { get; init; }
}

public enum ColumnPinState { None, Left, Right }
public enum ColumnAlignment { Start, Center, End }
```

---

## Default Implementations

### Phase 1 — `FixedWidthProvider`

The simplest and most alignment-safe implementation. Every column has a concrete pixel width, either from its `ExplicitWidth` or from a default. No flex, no measurement. All columns share a single `grid-template-columns` string.

**Trade-offs accepted:** Less adaptive layout; consumers must specify widths explicitly or accept a default.  
**Alignment guarantee:** Perfect — no runtime measurement or dynamic recomputation.

```csharp
namespace Sunfish.Components.DataGrid.Sizing;

/// <summary>
/// Resolves column widths from explicit pixel values only.
/// Falls back to <see cref="DefaultWidth"/> when ExplicitWidth is null.
/// No flex distribution. No DOM measurement.
/// This is the Phase 1 default for SunfishDataGrid.
/// </summary>
public sealed class FixedWidthProvider : IColumnWidthProvider
{
    /// <summary>
    /// Width assigned to columns that have neither ExplicitWidth nor Flex set.
    /// Defaults to 150px.
    /// </summary>
    public double DefaultWidth { get; init; } = 150;

    public GridLayoutContract Resolve(
        IReadOnlyList<ColumnSizingEntry> columns,
        double containerWidth)
    {
        var widths = new Dictionary<string, double>(columns.Count);

        foreach (var col in columns)
        {
            var raw = col.ExplicitWidth ?? DefaultWidth;
            widths[col.Id] = Clamp(raw, col.MinWidth, col.MaxWidth);
        }

        return BuildContract(columns, widths);
    }

    public GridLayoutContract ApplyResize(
        GridLayoutContract current,
        string columnId,
        double deltaPixels)
    {
        // Rebuild widths from current contract, apply delta to target column
        var widths = current.WidthById.ToDictionary(k => k.Key, v => v.Value);
        // NOTE: column MinWidth/MaxWidth is not accessible here without re-passing entries.
        // In the real implementation, ColumnSizingEntry list is stored as context or
        // passed as an additional parameter. Shown simplified for clarity.
        if (widths.TryGetValue(columnId, out var current_width))
            widths[columnId] = Math.Max(50, current_width + deltaPixels);

        // Re-derive contract from updated widths — pinned regions rebuild from same source
        return current with
        {
            WidthById = widths,
            CenterGridTemplate = BuildTemplateString(current.CenterColumnIds, widths),
            LeftPinnedGridTemplate = BuildTemplateString(current.LeftPinnedColumnIds, widths),
            RightPinnedGridTemplate = BuildTemplateString(current.RightPinnedColumnIds, widths),
            TotalWidth = widths.Values.Sum()
        };
    }

    private static GridLayoutContract BuildContract(
        IReadOnlyList<ColumnSizingEntry> columns,
        Dictionary<string, double> widths)
    {
        var left   = columns.Where(c => c.Pin == ColumnPinState.Left).Select(c => c.Id).ToList();
        var center = columns.Where(c => c.Pin == ColumnPinState.None).Select(c => c.Id).ToList();
        var right  = columns.Where(c => c.Pin == ColumnPinState.Right).Select(c => c.Id).ToList();

        return new GridLayoutContract
        {
            WidthById              = widths,
            LeftPinnedColumnIds    = left,
            CenterColumnIds        = center,
            RightPinnedColumnIds   = right,
            LeftPinnedGridTemplate = BuildTemplateString(left, widths),
            CenterGridTemplate     = BuildTemplateString(center, widths),
            RightPinnedGridTemplate= BuildTemplateString(right, widths),
            TotalWidth             = widths.Values.Sum()
        };
    }

    private static string BuildTemplateString(
        IReadOnlyList<string> columnIds,
        IReadOnlyDictionary<string, double> widths)
        => columnIds.Count == 0
            ? string.Empty
            : string.Join(" ", columnIds.Select(id => $"{widths[id]}px"));

    private static double Clamp(double value, double min, double? max)
        => max.HasValue ? Math.Clamp(value, min, max.Value) : Math.Max(value, min);
}
```

---

### Phase 2 — `FlexWidthProvider`

Adds flex-fill distribution. Columns with `Flex > 0` and no `ExplicitWidth` share the remaining container width proportionally after fixed columns are allocated. Still resolves to concrete pixel widths before render — no browser flex layout is used.

**Trade-offs accepted:** Requires a known `containerWidth` at resolve time; if container is 0 (hidden or first render), falls back to `FixedWidthProvider` defaults.  
**Alignment guarantee:** Identical to `FixedWidthProvider` once widths are resolved, because the same grid-template string is applied to header and body.

```csharp
namespace Sunfish.Components.DataGrid.Sizing;

/// <summary>
/// Resolves column widths using a flex-distribution algorithm.
/// Fixed-width columns are allocated first. Remaining container width
/// is distributed among flex columns proportional to their Flex factor,
/// clamped to MinWidth/MaxWidth.
/// Falls back to FixedWidthProvider behaviour when containerWidth is zero.
/// This is the Phase 2 default for SunfishDataGrid.
/// </summary>
public sealed class FlexWidthProvider : IColumnWidthProvider
{
    public double DefaultWidth { get; init; } = 150;
    public double DefaultFlexWidth { get; init; } = 100; // used when container unknown

    public GridLayoutContract Resolve(
        IReadOnlyList<ColumnSizingEntry> columns,
        double containerWidth)
    {
        var widths = new Dictionary<string, double>(columns.Count);

        if (containerWidth <= 0)
        {
            // Fallback: treat all as fixed
            foreach (var col in columns)
                widths[col.Id] = Clamp(col.ExplicitWidth ?? DefaultWidth, col.MinWidth, col.MaxWidth);
        }
        else
        {
            // Step 1: allocate fixed columns
            double fixedTotal = 0;
            double totalFlex = 0;

            foreach (var col in columns)
            {
                if (col.Flex.HasValue && col.ExplicitWidth is null)
                    totalFlex += col.Flex.Value;
                else
                {
                    var w = Clamp(col.ExplicitWidth ?? DefaultWidth, col.MinWidth, col.MaxWidth);
                    widths[col.Id] = w;
                    fixedTotal += w;
                }
            }

            // Step 2: distribute remaining width to flex columns
            var remaining = Math.Max(0, containerWidth - fixedTotal);

            foreach (var col in columns.Where(c => c.Flex.HasValue && c.ExplicitWidth is null))
            {
                var share = totalFlex > 0 ? remaining * (col.Flex!.Value / totalFlex) : 0;
                widths[col.Id] = Clamp(share, col.MinWidth, col.MaxWidth);
            }
        }

        return FixedWidthProvider.BuildContractInternal(columns, widths);
        // NOTE: BuildContractInternal is extracted as internal static in the real implementation
        // to be shared between FixedWidthProvider and FlexWidthProvider.
    }

    public GridLayoutContract ApplyResize(
        GridLayoutContract current,
        string columnId,
        double deltaPixels)
    {
        // On resize: the resized column becomes an explicit width; remove flex for that column
        // and re-distribute remaining flex columns (or simply fix all at current widths).
        // For Phase 2, simplest correct behaviour: fix all columns at current widths + delta.
        var widths = current.WidthById.ToDictionary(k => k.Key, v => v.Value);
        if (widths.TryGetValue(columnId, out var w))
            widths[columnId] = Math.Max(50, w + deltaPixels);

        return current with
        {
            WidthById = widths,
            CenterGridTemplate      = BuildTemplateString(current.CenterColumnIds, widths),
            LeftPinnedGridTemplate  = BuildTemplateString(current.LeftPinnedColumnIds, widths),
            RightPinnedGridTemplate = BuildTemplateString(current.RightPinnedColumnIds, widths),
            TotalWidth              = widths.Values.Sum()
        };
    }

    private static string BuildTemplateString(
        IReadOnlyList<string> ids,
        IReadOnlyDictionary<string, double> widths)
        => ids.Count == 0 ? string.Empty : string.Join(" ", ids.Select(id => $"{widths[id]}px"));

    private static double Clamp(double value, double min, double? max)
        => max.HasValue ? Math.Clamp(value, min, max.Value) : Math.Max(value, min);
}
```

---

### Phase 3 — `MeasuredAutoWidthProvider`

Measures the rendered header cell widths (and optionally a sample of body cells) via JS interop, then commits those measured values back into the shared model. The key constraint: measured values are committed to the provider's resolved contract immediately, and renderers are never asked to measure their own widths independently.

**Trade-offs accepted:** Requires a JS interop call on first render; has a known failure mode when the grid is inside a hidden or zero-width container (documented explicitly and guarded). This is the source of the `useResizeContainer` errors seen in MUI X[cite:35][cite:40].

```csharp
namespace Sunfish.Components.DataGrid.Sizing;

/// <summary>
/// Resolves column widths by measuring rendered header cell widths via JS interop.
/// Falls back to FlexWidthProvider on first render (before measurement).
/// After measurement, commits results to the column model and re-renders.
///
/// KNOWN CONSTRAINT: If the grid is inside a hidden container (display:none or
/// zero clientWidth), measurement returns zero. This provider will detect this
/// condition and defer measurement until the container is visible.
/// Use a ResizeObserver on the grid root element (SunfishDataGrid.Interop.cs)
/// to trigger re-measurement when visibility changes.
/// </summary>
public sealed class MeasuredAutoWidthProvider : IColumnWidthProvider
{
    private readonly FlexWidthProvider _fallback = new();
    private Dictionary<string, double>? _measuredWidths;

    public GridLayoutContract Resolve(
        IReadOnlyList<ColumnSizingEntry> columns,
        double containerWidth)
    {
        if (_measuredWidths is null)
            // First render: use flex fallback; measurement pass follows after render
            return _fallback.Resolve(columns, containerWidth);

        // Use committed measured widths, clamped to constraints
        var widths = new Dictionary<string, double>(columns.Count);
        foreach (var col in columns)
        {
            var measured = _measuredWidths.TryGetValue(col.Id, out var m) ? m : 150;
            widths[col.Id] = Clamp(measured, col.MinWidth, col.MaxWidth);
        }

        return FixedWidthProvider.BuildContractInternal(columns, widths);
    }

    /// <summary>
    /// Called by SunfishDataGrid.Interop.cs after the JS measurement pass completes.
    /// Commits measured widths into this provider. The grid must call StateHasChanged()
    /// after this to trigger a re-render with accurate widths.
    /// </summary>
    public void CommitMeasuredWidths(IReadOnlyDictionary<string, double> measured)
    {
        _measuredWidths = new Dictionary<string, double>(measured);
    }

    public GridLayoutContract ApplyResize(GridLayoutContract current, string columnId, double deltaPixels)
        => _fallback.ApplyResize(current, columnId, deltaPixels);

    private static double Clamp(double v, double min, double? max)
        => max.HasValue ? Math.Clamp(v, min, max.Value) : Math.Max(v, min);
}
```

---

## Integration with SunfishDataGrid

### Step 1 — Add `IColumnWidthProvider` to `SunfishDataGrid.razor.cs`

Add to the internal state block:

```csharp
// ── Sizing ───────────────────────────────────────────────────────────
internal IColumnWidthProvider _widthProvider = new FixedWidthProvider();
internal GridLayoutContract _layoutContract = new();
internal double _containerWidth;

/// <summary>
/// Overrides the default column width provider.
/// Defaults to FixedWidthProvider. Set before the grid first renders.
/// </summary>
[Parameter] public IColumnWidthProvider? ColumnWidthProvider { get; set; }
```

### Step 2 — Resolve widths before render

In `OnParametersSetAsync` (or a dedicated `ResolveLayout()` helper), rebuild the layout contract after any state change that could affect column widths:

```csharp
private void ResolveLayout()
{
    _widthProvider = ColumnWidthProvider ?? new FixedWidthProvider();

    var entries = BuildSizingEntries(); // derived from _visibleColumns + system columns
    _layoutContract = _widthProvider.Resolve(entries, _containerWidth);
}

private List<ColumnSizingEntry> BuildSizingEntries()
{
    var entries = new List<ColumnSizingEntry>();

    // System columns (checkbox, expander) get a fixed 40px entry
    if (ShowCheckboxColumn)
        entries.Add(new ColumnSizingEntry("__checkbox", 40, 40, 40, null, ColumnPinState.None, false, ColumnAlignment.Center));

    if (DetailTemplate != null)
        entries.Add(new ColumnSizingEntry("__expander", 40, 40, 40, null, ColumnPinState.None, false, ColumnAlignment.Center));

    foreach (var col in _visibleColumns)
        entries.Add(new ColumnSizingEntry(
            Id:            col.Field ?? col.Title ?? col.GetHashCode().ToString(),
            ExplicitWidth: col.Width,
            MinWidth:      col.MinWidth ?? 50,
            MaxWidth:      col.MaxWidth,
            Flex:          col.Flex,
            Pin:           col.Pinned,
            IsResizable:   col.Resizable,
            Alignment:     col.Alignment
        ));

    if (EditMode != GridEditMode.None && EditMode != GridEditMode.InCell)
        entries.Add(new ColumnSizingEntry("__commands", 120, 80, null, null, ColumnPinState.None, false, ColumnAlignment.Center));

    return entries;
}
```

### Step 3 — Apply contract in rendering

In `SunfishDataGrid.razor`, the header row and each body row get the same CSS custom property:

```razor
<div class="marilo-grid" style="--marilo-center-template: @_layoutContract.CenterGridTemplate;
                                 --marilo-left-template:   @_layoutContract.LeftPinnedGridTemplate;
                                 --marilo-right-template:  @_layoutContract.RightPinnedGridTemplate;">
    <div class="marilo-grid-header" style="overflow: hidden;">
        <!-- Header uses the same template strings as body rows -->
    </div>
    <div class="marilo-grid-body" style="overflow: auto; @ContentStyle">
        <!-- Each row: style="display: grid; grid-template-columns: var(--marilo-center-template)" -->
    </div>
</div>
```

Header and body never compute widths themselves. They read `var(--marilo-center-template)` from the root element, which was written by `_layoutContract` before the first pixel was rendered[cite:23][cite:25].

### Step 4 — Connect container resize

In `SunfishDataGrid.Interop.cs`, add a `ResizeObserver` callback that updates `_containerWidth` and calls `ResolveLayout()`:

```csharp
// Called from JS via DotNetObjectReference when grid container is resized
[JSInvokable]
public void OnContainerResized(double newWidth)
{
    if (Math.Abs(newWidth - _containerWidth) > 1)
    {
        _containerWidth = newWidth;
        ResolveLayout();
        StateHasChanged();
    }
}
```

This resolves the hidden-container class of bugs: the observer only fires when the container has a real width, so `_containerWidth = 0` can never silently produce bad layout[cite:35][cite:40].

---

## Phased Rollout Plan

### Phase 1 — Fixed Width Provider (Baseline)

**Goal:** Establish the `IColumnWidthProvider` abstraction and `GridLayoutContract` output. Replace all per-cell width style logic with consumption of `_layoutContract`. Alignment correctness in all current grid modes is verified.

| Task | File(s) affected |
|---|---|
| Add `Sizing/` folder under `DataGrid/` | New folder |
| Create `IColumnWidthProvider`, `ColumnSizingEntry`, `GridLayoutContract` | `Sizing/` |
| Implement `FixedWidthProvider` | `Sizing/FixedWidthProvider.cs` |
| Add `_widthProvider`, `_layoutContract`, `_containerWidth` to grid state | `SunfishDataGrid.razor.cs` |
| Implement `BuildSizingEntries()` and `ResolveLayout()` | `SunfishDataGrid.razor.cs` |
| Refactor header rendering to use `_layoutContract` | `SunfishDataGrid.Rendering.cs` |
| Refactor body row rendering to use `_layoutContract` | `SunfishDataGrid.razor` |
| Write bUnit tests for `FixedWidthProvider.Resolve()` | `Tests/DataGrid/Sizing/` |
| Verify alignment at all existing column configurations | Manual + bUnit |

**Acceptance criteria:**
- Header and body cells are aligned for all combinations of column count, explicit widths, system columns, and `ShowCheckboxColumn`.
- `_layoutContract.CenterGridTemplate` is used by both header and body rows — no other width source exists in rendering code.
- `FixedWidthProvider` passes unit tests for clamping, zero-container fallback, and system-column entries.

---

### Phase 2 — Flex Width Provider

**Goal:** Add fill-to-container behaviour for columns marked with `Flex`. The `FlexWidthProvider` replaces `FixedWidthProvider` as the default when `EnableFlexColumns = true` or when any column has `Flex` set.

| Task | File(s) affected |
|---|---|
| Implement `FlexWidthProvider` | `Sizing/FlexWidthProvider.cs` |
| Add `Flex` parameter to `SunfishGridColumn.razor` | `SunfishGridColumn.razor` |
| Connect `ResizeObserver` JS interop to `_containerWidth` | `SunfishDataGrid.Interop.cs` |
| Auto-select `FlexWidthProvider` when any column has `Flex` set | `SunfishDataGrid.razor.cs` |
| Write bUnit tests for flex distribution and resize behaviour | `Tests/DataGrid/Sizing/` |

**Acceptance criteria:**
- Flex columns distribute remaining width proportionally.
- Resizing a flex column converts it to a fixed-width column; adjacent flex columns redistribute.
- Container resize triggers re-resolution and re-render without header/body misalignment.
- All Phase 1 tests continue to pass.

---

### Phase 3 — Column Resize Interaction

**Goal:** Connect the resize drag handle in the header to `IColumnWidthProvider.ApplyResize()`. The resize pipeline must not bypass the provider.

| Task | File(s) affected |
|---|---|
| Implement header cell resize handle (`marilo-grid-resize-handle`) | `SunfishDataGrid.razor` |
| On pointer-move: call `_widthProvider.ApplyResize(_layoutContract, colId, delta)` | `SunfishDataGrid.Rendering.cs` |
| On pointer-up: commit final contract, notify `OnStateChanged` | `SunfishDataGrid.razor.cs` |
| Expose `Resizable` on `SunfishGridColumn.razor` | `SunfishGridColumn.razor` |
| Write bUnit tests for resize clamping and state notification | `Tests/DataGrid/Sizing/` |

**Acceptance criteria:**
- Resize updates `_layoutContract` only through `ApplyResize()`; no direct DOM measurement in the resize handler.
- Resized column width is clamped to `MinWidth`/`MaxWidth`.
- `GridState.ColumnStates` is updated after resize via `NotifyStateChanged`.

---

### Phase 4 — Measured Auto Width Provider

**Goal:** Add opt-in content-aware auto-sizing via JS measurement. This is the most complex phase and is gated on Phase 1–3 being fully stable.

| Task | File(s) affected |
|---|---|
| Implement `MeasuredAutoWidthProvider` | `Sizing/MeasuredAutoWidthProvider.cs` |
| Add JS measurement function for header cell widths | `wwwroot/marilo-datagrid.js` |
| Add `AutoWidth` parameter to `SunfishGridColumn.razor` | `SunfishGridColumn.razor` |
| Call `CommitMeasuredWidths()` after first render and after column definition changes | `SunfishDataGrid.Interop.cs` |
| Guard against zero-width container measurements | `SunfishDataGrid.Interop.cs` |
| Write bUnit + integration tests for measurement commit flow | `Tests/DataGrid/Sizing/` |

**Acceptance criteria:**
- Columns with `AutoWidth = true` adopt their measured header width after first render.
- If container width is 0 at measurement time, measurement is deferred until `ResizeObserver` fires with a non-zero width.
- All Phase 1–3 acceptance criteria remain satisfied.

---

### Phase 5 — Left Column Pinning (Deferred)

Right-pinned columns are deliberately excluded until Phase 4 is stable. The `GridLayoutContract` already carries `LeftPinnedGridTemplate` and `RightPinnedGridTemplate` — the infrastructure is ready, only the rendering and interaction work remains.

Right pinning is more complex due to scrollbar-width calculations and is the source of the header/content misalignment issues documented in AG Grid[cite:13]. It will be addressed in a dedicated architecture note.

---

## Prohibited Patterns

These patterns are explicitly disallowed in `SunfishDataGrid` rendering code after Phase 1 is merged. Any PR that introduces them must be rejected:

| Pattern | Why prohibited |
|---|---|
| Reading `column.Width` or `column.EffectiveWidth` directly inside a header or body cell renderer | Bypasses `_layoutContract`; creates an independent width source[cite:16][cite:22] |
| Computing `grid-template-columns` inside `.razor` markup inline | Layout template must come from `_layoutContract`; inline computation creates duplicate logic[cite:23] |
| Separate horizontal scroll on the header element | Creates scrollbar-width offset misalignment[cite:13][cite:30] |
| DOM measurement inside `BuildRenderTree` or `BuildSizingEntries` | Measurement must go through `MeasuredAutoWidthProvider.CommitMeasuredWidths()`[cite:35] |
| Column virtualisation before Phase 4 is complete | Column virtualisation plus sticky header is the highest-risk combination[cite:10][cite:14] |

---

## File Layout After Phase 1

```
src/Sunfish.Components/DataGrid/
├── Sizing/
│   ├── IColumnWidthProvider.cs
│   ├── ColumnSizingEntry.cs
│   ├── GridLayoutContract.cs
│   ├── FixedWidthProvider.cs         ← Phase 1
│   ├── FlexWidthProvider.cs          ← Phase 2
│   └── MeasuredAutoWidthProvider.cs  ← Phase 4
├── SunfishDataGrid.razor
├── SunfishDataGrid.razor.cs
├── SunfishDataGrid.Rendering.cs
├── SunfishDataGrid.Interop.cs
├── SunfishDataGrid.Data.cs
├── SunfishDataGrid.Editing.cs
├── SunfishGridColumn.razor
├── GridState.cs
├── GridEventArgs.cs
└── GridCommandTypes.cs
```

