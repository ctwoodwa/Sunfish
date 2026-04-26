// using Sunfish.UIAdapters.Blazor.Components.DataGrid.Sizing; — removed: Sizing types now flat in DataDisplay namespace
using Sunfish.Foundation.Base;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.Foundation.BusinessLogic.Enums;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Resource-vs-time allocation grid: rows are resources of type <typeparamref name="TResource"/>,
/// columns are time buckets at the configured <see cref="TimeGranularity"/>, and each cell shows
/// the allocation (hours, percentage, etc.) for that resource within that bucket.
/// </summary>
/// <remarks>
/// Supports keyboard-first cell navigation, range selection, drag-to-fill, splitter-resizable
/// resource pane, and column-derived layout. The component is built with JS interop
/// (<c>AllocationSchedulerInterop</c>) for high-performance pointer/keyboard handling, so the
/// component is <see cref="IAsyncDisposable"/> and consumers must dispose it via the standard
/// Blazor lifecycle.
/// </remarks>
/// <typeparam name="TResource">The resource model type (people, equipment, rooms, etc.).</typeparam>
public partial class SunfishAllocationScheduler<TResource> : SunfishComponentBase, IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // ── Data Binding ────────────────────────────────────────────────────

    [Parameter] public IEnumerable<TResource>? Resources { get; set; }
    [Parameter] public IEnumerable<AllocationRecord>? Allocations { get; set; }
    [Parameter] public IEnumerable<AllocationTarget>? Targets { get; set; }

    // ── Timeline Configuration ──────────────────────────────────────────

    [Parameter] public TimeGranularity AuthoritativeLevel { get; set; }
    [Parameter] public TimeGranularity ViewGrain { get; set; }
    [Parameter] public EventCallback<TimeGranularity> ViewGrainChanged { get; set; }
    [Parameter] public DateTime VisibleStart { get; set; } = DateTime.Today;
    [Parameter] public EventCallback<DateTime> VisibleStartChanged { get; set; }
    [Parameter] public DateTime? VisibleEnd { get; set; }

    /// <summary>
    /// Minimum number of time-range columns that must remain visible regardless of
    /// pane width. The actual column count is computed dynamically from the measured
    /// pane width divided by a per-granularity minimum column width; this parameter
    /// acts as a floor. Defaults to 3.
    /// </summary>
    [Parameter] public int MinVisibleColumns { get; set; } = 3;

    /// <summary>Obsolete: use <see cref="MinVisibleColumns"/> instead.</summary>
    [Obsolete("Use MinVisibleColumns instead. DefaultRangeLength now acts as a minimum floor, not an exact count.")]
    [Parameter] public int DefaultRangeLength
    {
        get => MinVisibleColumns;
        set => MinVisibleColumns = value;
    }

    [Parameter] public TimeGranularity DefaultRangeUnit { get; set; } = TimeGranularity.Month;

    // ── Sizing ───────────────────────────────────────────────────────────

    [Parameter] public IColumnWidthProvider? ColumnWidthProvider { get; set; }

    /// <summary>
    /// Overrides the number of time columns that fill the panel width in non-Day view modes.
    /// When null, Month uses all visible buckets, Week uses all visible buckets, and Day uses fixed 80px columns.
    /// Set to a positive integer to force exactly that many equally-spaced columns regardless of view mode.
    /// </summary>
    [Parameter] public int? VisibleColumnOverride { get; set; }

    /// <summary>
    /// Fixed width in pixels for each time-range column in the timeline pane.
    /// Defaults to 80px. Must be >= 40px.
    /// All time columns render at exactly this width regardless of container size;
    /// the timeline panel scrolls horizontally when columns overflow.
    /// </summary>
    [Parameter] public int TimeColumnWidth { get; set; } = 80;

    /// <summary>
    /// When true, the user can resize individual time columns by dragging the
    /// column header border (analogous to Excel column resize).
    /// Defaults to false.
    /// </summary>
    [Parameter] public bool AllowTimeColumnResize { get; set; }

    /// <summary>
    /// When true, double-clicking a time column header border triggers AutoFit:
    /// the column snaps to the width of its widest rendered cell content
    /// (analogous to Excel AutoFit Column Width).
    /// Only applies when AllowTimeColumnResize is true.
    /// </summary>
    [Parameter] public bool AutoFitOnDoubleClick { get; set; } = true;

    /// <summary>
    /// Minimum column width in pixels enforced during time column resize drag.
    /// Defaults to 48px.
    /// </summary>
    [Parameter] public int MinTimeColumnWidth { get; set; } = 48;

    /// <summary>
    /// Maximum column width in pixels enforced during time column resize drag.
    /// 0 = no maximum. Defaults to 0.
    /// </summary>
    [Parameter] public int MaxTimeColumnWidth { get; set; }

    /// <summary>
    /// EventCallback fired after the user resizes one or more time columns.
    /// Payload is a Dictionary mapping column index to new width in pixels.
    /// </summary>
    [Parameter] public EventCallback<Dictionary<int, int>> OnTimeColumnResized { get; set; }

    // ── Splitter ────────────────────────────────────────────────────────

    [Parameter] public double? SplitterPosition { get; set; }
    [Parameter] public EventCallback<double> SplitterPositionChanged { get; set; }
    [Parameter] public double? DefaultSplitterPosition { get; set; }
    [Parameter] public double MinRightPaneWidth { get; set; } = 300;
    [Parameter] public bool AllowSplitterCollapse { get; set; }
    [Parameter] public string? SplitterCssClass { get; set; }
    [Parameter] public EventCallback<SplitterSide> OnSplitterCollapsed { get; set; }
    [Parameter] public EventCallback<double> OnSplitterRestored { get; set; }

    // ── Display ─────────────────────────────────────────────────────────

    [Parameter] public AllocationValueMode ValueMode { get; set; } = AllocationValueMode.Hours;
    [Parameter] public bool StripedRows { get; set; } = true;

    /// <summary>
    /// When true (default), renders a date picker and Jump button in the toolbar
    /// so the user can navigate directly to any date without stepping forward/back.
    /// </summary>
    [Parameter] public bool ShowJumpToDate { get; set; } = true;
    [Parameter] public bool ShowTargets { get; set; }
    [Parameter] public bool ShowDeltas { get; set; }
    [Parameter] public DeltaDisplayMode DeltaDisplayMode { get; set; } = DeltaDisplayMode.Value;
    [Parameter] public string? Height { get; set; }
    [Parameter] public string? Width { get; set; }
    [Parameter] public bool EnableLoaderContainer { get; set; } = true;

    // ── Interaction ─────────────────────────────────────────────────────

    [Parameter] public bool AllowDragFill { get; set; } = true;
    [Parameter] public bool AllowKeyboardEdit { get; set; } = true;
    [Parameter] public bool AllowBulkEdit { get; set; } = true;
    [Parameter] public bool EnableContextMenu { get; set; } = true;
    [Parameter] public IEnumerable<AllocationMenuDescriptor>? ContextMenuItems { get; set; }
    [Parameter] public AllocationSelectionMode SelectionMode { get; set; } = AllocationSelectionMode.Range;
    [Parameter] public DistributionMode DefaultDistributionMode { get; set; } = DistributionMode.EvenSpread;
    [Parameter] public bool AllowZoomEdit { get; set; }

    // ── Scenario Planning ───────────────────────────────────────────────

    [Parameter] public IEnumerable<AllocationSet>? AllocationSets { get; set; }
    [Parameter] public IEnumerable<ScenarioOverride>? ScenarioOverrides { get; set; }
    [Parameter] public Guid ActiveSetId { get; set; }
    [Parameter] public EventCallback<Guid> ActiveSetIdChanged { get; set; }
    [Parameter] public Guid? CompareSetId { get; set; }
    [Parameter] public bool ShowBaselineDiff { get; set; }
    [Parameter] public string? BaselineDateFormat { get; set; }
    [Parameter] public bool ShowComparisonPanel { get; set; }
    [Parameter] public bool ShowCriticalPath { get; set; }

    // ── Templates ───────────────────────────────────────────────────────

    [Parameter] public RenderFragment? AllocationResourceColumns { get; set; }
    [Parameter] public RenderFragment? ToolbarTemplate { get; set; }
    [Parameter] public RenderFragment? EmptyTemplate { get; set; }
    [Parameter] public RenderFragment<AllocationCellContext>? CellTemplate { get; set; }
    [Parameter] public RenderFragment<TResource>? ResourceRowTemplate { get; set; }

    // ── Events ──────────────────────────────────────────────────────────

    [Parameter] public EventCallback<CellEditedArgs> OnCellEdited { get; set; }
    [Parameter] public EventCallback<RangeEditedArgs> OnRangeEdited { get; set; }
    [Parameter] public EventCallback<ContextMenuActionArgs> OnContextMenuAction { get; set; }
    [Parameter] public EventCallback<DistributeArgs> OnDistributeRequested { get; set; }
    [Parameter] public EventCallback<ShiftValuesArgs> OnShiftValues { get; set; }
    [Parameter] public EventCallback<MoveValuesArgs> OnMoveValues { get; set; }
    [Parameter] public EventCallback<TargetChangedArgs> OnTargetChanged { get; set; }
    [Parameter] public EventCallback<VisibleRangeChangedArgs> OnVisibleRangeChanged { get; set; }
    [Parameter] public EventCallback<SelectionChangedArgs> OnSelectionChanged { get; set; }
    [Parameter] public EventCallback<ScenarioChangedArgs> OnScenarioChanged { get; set; }
    [Parameter] public EventCallback<ScenarioCreatedArgs> OnScenarioCreated { get; set; }
    [Parameter] public EventCallback<AllocationOverriddenArgs> OnAllocationOverridden { get; set; }
    [Parameter] public EventCallback<ScenarioStatusChangedArgs> OnScenarioStatusChanged { get; set; }
    [Parameter] public EventCallback<ScenarioPromotedArgs> OnScenarioPromoted { get; set; }
    [Parameter] public EventCallback<CanExecuteActionArgs> CanExecuteAction { get; set; }

    // ── Per-Granularity Minimum Column Widths ─────────────────────────
    // Used by the ResizeObserver to compute how many columns fit in the pane.

    private static readonly Dictionary<TimeGranularity, int> MinColumnWidthsByGrain = new()
    {
        { TimeGranularity.Day,     60  },
        { TimeGranularity.Week,    80  },
        { TimeGranularity.Month,   90  },
        { TimeGranularity.Quarter, 100 },
        { TimeGranularity.Year,    120 },
    };

    // ── Internal State ──────────────────────────────────────────────────

    private readonly List<AllocationResourceColumn<TResource>> _columns = new();
    private IEnumerable<AllocationSet>? _allocationSets;
    private IEnumerable<AllocationRecord>? _effectiveAllocations;
    private List<DateRange> _visibleBuckets = new();
    private List<TimelineHeaderGroup> _headerGroups = new();
    private HashSet<(object ResourceKey, DateTime BucketStart)> _selectedCells = new();
    private TimeGranularity _currentViewGrain;
    private bool _isLoading;
    private GridLayoutContract _layoutContract = GridLayoutContract.Empty;
    private IColumnWidthProvider _widthProvider = new FixedWidthProvider();
    private readonly Dictionary<object, string> _columnSizingIds = new();
    private ElementReference _gridRef;
    private IJSObjectReference? _jsModule;
    private DotNetObjectReference<SunfishAllocationScheduler<TResource>>? _dotNetRef;
    private bool _contextMenuVisible;
    private double _contextMenuX;
    private double _contextMenuY;
    private object? _contextMenuResourceKey;
    private DateRange? _contextMenuBucket;
    private bool _editMode;
    private string _editValue = string.Empty;
    private object? _editResourceKey;
    private DateRange? _editBucket;

    // Active cell — the last focused/clicked cell; determines fill handle placement.
    private (object ResourceKey, DateTime BucketStart)? _activeCell;

    // Jump-to-date picker state
    private DateTime _jumpToDate = DateTime.Today;

    // Per-column runtime widths for time columns (bucket index → px). Set during resize drag.
    private readonly Dictionary<int, int> _timeColumnRuntimeWidths = new();

    // Unique DOM id for the right (timeline) pane so ResizeObserver can target it.
    private readonly string _rightPaneId = $"sched-right-{Guid.NewGuid():N}";

    // Computed column count — updated dynamically by the JS ResizeObserver callback.
    // Before JS fires, defaults to MinVisibleColumns (set in OnParametersSet).
    private int _visibleColumnCount;
    private bool _paneObserverActive;

    // Splitter state
    private double _lastNonCollapsedPosition;
    private bool _isDraggingSplitter;
    private bool _isSplitterFocused;
    private SplitterSide? _collapsedSide;
    private bool _splitterInitialized;

    // ── Lifecycle ────────────────────────────────────────────────────────

    protected override void OnInitialized()
    {
        _currentViewGrain = ViewGrain == default ? AuthoritativeLevel : ViewGrain;
        _allocationSets = AllocationSets;
    }

    protected override void OnParametersSet()
    {
        var previousGrain = _currentViewGrain;
        _currentViewGrain = ViewGrain == default ? AuthoritativeLevel : ViewGrain;
        _allocationSets = AllocationSets;

        // Before the JS ResizeObserver fires, use MinVisibleColumns as the column count.
        if (!_paneObserverActive)
            _visibleColumnCount = Math.Max(1, MinVisibleColumns);

        _effectiveAllocations = ComputeEffectiveAllocations();
        _visibleBuckets = ComputeVisibleBuckets();
        _headerGroups = ComputeTimelineHeaderGroups();
        ResolveLayoutContract();

        // Track whether grain changed so we can re-register the observer
        if (previousGrain != _currentViewGrain)
            _grainChangedSinceLastRender = true;

        // On first render, apply DefaultSplitterPosition restore if provided
        if (!_splitterInitialized && _columns.Count > 0)
        {
            _splitterInitialized = true;
            var target = SplitterPosition ?? DefaultSplitterPosition;
            if (target.HasValue)
            {
                DistributeWidthToColumns(target.Value);
            }
            _lastNonCollapsedPosition = ComputeColumnWidthSum();
        }
    }

    private bool _grainChangedSinceLastRender;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            try
            {
                _jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/Sunfish.Components/js/allocation-scheduler.js");

                await _jsModule.InvokeVoidAsync("AllocationSchedulerInterop.initScrollSync", _gridRef);
                await _jsModule.InvokeVoidAsync("AllocationSchedulerInterop.initSplitter", _gridRef, _dotNetRef);
                await _jsModule.InvokeVoidAsync("AllocationSchedulerInterop.initColumnResize", _gridRef, _dotNetRef);
                if (AllowTimeColumnResize)
                    await _jsModule.InvokeVoidAsync("AllocationSchedulerInterop.initTimeColumnResize", _gridRef, _dotNetRef, MinTimeColumnWidth, MaxTimeColumnWidth, AutoFitOnDoubleClick);
                if (AllowDragFill)
                    await _jsModule.InvokeVoidAsync("AllocationSchedulerInterop.initDragFill", _gridRef, _dotNetRef);
                if (AllowKeyboardEdit)
                    await _jsModule.InvokeVoidAsync("AllocationSchedulerInterop.initKeyboardNav", _gridRef, _dotNetRef);
                await _jsModule.InvokeVoidAsync("AllocationSchedulerInterop.initClipboard", _gridRef, _dotNetRef);

                // Start observing the timeline pane width for dynamic column count
                await RegisterPaneObserverAsync();
            }
            catch (JSException)
            {
                // JS interop not available (e.g., prerendering or test context)
            }
        }
        else if (_grainChangedSinceLastRender && _jsModule is not null)
        {
            _grainChangedSinceLastRender = false;
            // Re-register with updated minColWidth for the new grain
            try
            {
                await RegisterPaneObserverAsync();
            }
            catch (JSException) { }
        }
    }

    private async Task RegisterPaneObserverAsync()
    {
        if (_jsModule is null || _dotNetRef is null) return;
        var minColWidth = MinColumnWidthsByGrain.GetValueOrDefault(_currentViewGrain, 80);
        await _jsModule.InvokeVoidAsync(
            "AllocationSchedulerInterop.observePane",
            _dotNetRef, _rightPaneId, minColWidth, MinVisibleColumns);
    }

    // ── JS Interop Callbacks ────────────────────────────────────────────

    /// <summary>
    /// Called by the JS ResizeObserver when the timeline pane width changes
    /// (browser resize, splitter drag, etc.). Recomputes the visible column
    /// count and rebuilds buckets if the count changed.
    /// </summary>
    [JSInvokable]
    public void OnPaneWidthChanged(double paneWidth, int computedCount)
    {
        _paneObserverActive = true;
        var clamped = Math.Max(MinVisibleColumns, computedCount);
        if (_visibleColumnCount == clamped) return;

        _visibleColumnCount = clamped;
        _visibleBuckets = ComputeVisibleBuckets();
        _headerGroups = ComputeTimelineHeaderGroups();
        ResolveLayoutContract();
        InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// JS interop entry point: invoked when the user completes a drag-fill gesture from the
    /// fill handle on a selected cell. Applies the source cell's value to all targets and
    /// raises both <c>OnRangeEdited</c> (batched) and <c>OnCellEdited</c> (per cell).
    /// </summary>
    /// <param name="payloadJson">Serialized <c>DragFillPayloadDto</c> describing the source cell and target cells.</param>
    [JSInvokable]
    public async Task OnDragFillCompleted(string payloadJson)
    {
        var payload = System.Text.Json.JsonSerializer.Deserialize<DragFillPayloadDto>(payloadJson);
        if (payload?.source is null || payload.targets is null || payload.targets.Count == 0) return;

        // Resolve source value
        var srcStart = DateTime.TryParse(payload.source.bucketStart, out var sd) ? sd : default;
        var srcBucket = new DateRange { Start = srcStart, End = AdvanceDate(srcStart, _currentViewGrain, 1) };
        var sourceRecord = GetRecord(payload.source.resourceKey ?? string.Empty, srcBucket);
        var fillValue = sourceRecord?.Value ?? 0;

        // Resolve target cells, skipping disabled/read-only
        var targetRefs = payload.targets
            .Select(c =>
            {
                var start = DateTime.TryParse(c.bucketStart, out var dt) ? dt : default;
                var bucket = new DateRange { Start = start, End = AdvanceDate(start, _currentViewGrain, 1) };
                return new { ResourceKey = (object)(c.resourceKey ?? string.Empty), Bucket = bucket };
            })
            .Where(r => IsCellEditable(r.Bucket) && !IsCellDisabled(r.Bucket))
            .ToList();

        if (targetRefs.Count == 0) return;

        var affectedRecords = targetRefs
            .Select(r => GetRecord(r.ResourceKey, r.Bucket))
            .Where(r => r is not null)
            .Cast<AllocationRecord>()
            .ToList();

        // Fire batch event so consumers can update their data in one shot
        await OnRangeEdited.InvokeAsync(new RangeEditedArgs
        {
            AffectedRecords = affectedRecords,
            Value = fillValue
        });

        // Also fire individual OnCellEdited for each target so single-cell consumers are notified
        foreach (var r in targetRefs)
        {
            var record = GetRecord(r.ResourceKey, r.Bucket);
            await OnCellEdited.InvokeAsync(new CellEditedArgs
            {
                ResourceKey = r.ResourceKey,
                BucketStart = r.Bucket.Start,
                BucketEnd = r.Bucket.End,
                OldValue = record?.Value ?? 0,
                NewValue = fillValue
            });
        }
    }

    /// <summary>
    /// JS interop entry point: invoked when keyboard navigation moves focus to a cell.
    /// Updates the active cell and (if selection is enabled) replaces the current selection
    /// with the focused cell, then raises <c>OnSelectionChanged</c>.
    /// </summary>
    /// <param name="cellKeyJson">Serialized <c>CellKeyDto</c> identifying the focused cell.</param>
    [JSInvokable]
    public async Task OnCellFocused(string cellKeyJson)
    {
        var cell = System.Text.Json.JsonSerializer.Deserialize<CellKeyDto>(cellKeyJson);
        if (cell?.resourceKey is null) return;

        var start = DateTime.TryParse(cell.bucketStart, out var dt) ? dt : default;
        var bucket = new DateRange { Start = start, End = AdvanceDate(start, _currentViewGrain, 1) };

        // Keyboard navigation moves the active cell; move selection without toggling
        _activeCell = (cell.resourceKey, bucket.Start);
        if (SelectionMode != AllocationSelectionMode.None)
        {
            _selectedCells.Clear();
            _selectedCells.Add((cell.resourceKey, bucket.Start));
            await OnSelectionChanged.InvokeAsync(new SelectionChangedArgs
            {
                SelectedCells = GetSelectedCells(),
                SelectionMode = SelectionMode
            });
        }
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// JS interop entry point: invoked when the user presses Escape. Exits edit mode and
    /// closes any open context menu.
    /// </summary>
    [JSInvokable]
    public Task OnEscapePressed()
    {
        _editMode = false;
        _contextMenuVisible = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>
    /// JS interop entry point: invoked when the user presses Delete on a cell. Clears the
    /// cell's value (subject to editable / disabled checks) and raises the standard edit events.
    /// </summary>
    /// <param name="cellKeyJson">Serialized <c>CellKeyDto</c> identifying the cell to clear.</param>
    [JSInvokable]
    public async Task OnDeletePressed(string cellKeyJson)
    {
        var cell = System.Text.Json.JsonSerializer.Deserialize<CellKeyDto>(cellKeyJson);
        if (cell?.resourceKey is null) return;

        var start = DateTime.TryParse(cell.bucketStart, out var dt) ? dt : default;
        var bucket = new DateRange { Start = start, End = AdvanceDate(start, _currentViewGrain, 1) };

        if (!IsCellEditable(bucket) || IsCellDisabled(bucket)) return;

        var record = GetRecord(cell.resourceKey, bucket);
        await OnCellEdited.InvokeAsync(new CellEditedArgs
        {
            ResourceKey = cell.resourceKey,
            BucketStart = bucket.Start,
            BucketEnd = bucket.End,
            OldValue = record?.Value ?? 0,
            NewValue = 0
        });
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// JS interop: user pressed Enter or F2 on a focused cell — enter edit mode.
    /// </summary>
    [JSInvokable]
    public async Task OnEnterEditMode(string cellKeyJson)
    {
        var cell = System.Text.Json.JsonSerializer.Deserialize<CellKeyDto>(cellKeyJson);
        if (cell?.resourceKey is null) return;

        var start = DateTime.TryParse(cell.bucketStart, out var dt) ? dt : default;
        var bucket = new DateRange { Start = start, End = AdvanceDate(start, _currentViewGrain, 1) };

        if (!IsCellEditable(bucket) || IsCellDisabled(bucket)) return;

        var record = GetRecord(cell.resourceKey, bucket);
        _editMode = true;
        _editResourceKey = cell.resourceKey;
        _editBucket = bucket;
        _editValue = record?.Value.ToString("0.#") ?? "0";
        _activeCell = (cell.resourceKey, bucket.Start);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// JS interop: user started typing while a cell was focused — enter edit mode
    /// with the typed character as the initial value (replaces existing content).
    /// </summary>
    [JSInvokable]
    public async Task OnStartTyping(string cellKeyJson, string initialChar)
    {
        var cell = System.Text.Json.JsonSerializer.Deserialize<CellKeyDto>(cellKeyJson);
        if (cell?.resourceKey is null) return;

        var start = DateTime.TryParse(cell.bucketStart, out var dt) ? dt : default;
        var bucket = new DateRange { Start = start, End = AdvanceDate(start, _currentViewGrain, 1) };

        if (!IsCellEditable(bucket) || IsCellDisabled(bucket)) return;

        _editMode = true;
        _editResourceKey = cell.resourceKey;
        _editBucket = bucket;
        _editValue = initialChar;   // Replace existing value — matches Excel direct-type behaviour
        _activeCell = (cell.resourceKey, bucket.Start);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// JS interop: user pasted tab/newline-delimited text.
    /// Parses the TSV and fires OnCellEdited for each editable target cell.
    /// Disabled/read-only cells in the paste range are silently skipped.
    /// </summary>
    [JSInvokable]
    public async Task OnPasteData(string tsv)
    {
        if (_activeCell is null) return;

        var resourceList = Resources?.ToList();
        if (resourceList is null) return;

        // Find active resource index
        var activeResourceKey = _activeCell.Value.ResourceKey;
        var activeBucketStart = _activeCell.Value.BucketStart;

        int startResourceIdx = -1;
        for (int i = 0; i < resourceList.Count; i++)
        {
            if (Equals(GetResourceKey(resourceList[i]), activeResourceKey))
            {
                startResourceIdx = i;
                break;
            }
        }
        if (startResourceIdx < 0) return;

        int startBucketIdx = _visibleBuckets.FindIndex(b => b.Start == activeBucketStart);
        if (startBucketIdx < 0) return;

        var rows = tsv.Split('\n');

        for (int ri = 0; ri < rows.Length; ri++)
        {
            int resourceIdx = startResourceIdx + ri;
            if (resourceIdx >= resourceList.Count) break;

            var resourceKey = GetResourceKey(resourceList[resourceIdx]);
            var cols = rows[ri].Split('\t');

            for (int ci = 0; ci < cols.Length; ci++)
            {
                int bucketIdx = startBucketIdx + ci;
                if (bucketIdx >= _visibleBuckets.Count) break;

                var bucket = _visibleBuckets[bucketIdx];
                if (!IsCellEditable(bucket) || IsCellDisabled(bucket)) continue;

                // Strip common unit suffixes before parsing
                var raw = cols[ci].Trim().TrimEnd('h', 'H').Replace("$", "").Trim();
                if (!decimal.TryParse(raw, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var newValue))
                    continue;

                var record = GetRecord(resourceKey, bucket);
                await OnCellEdited.InvokeAsync(new CellEditedArgs
                {
                    ResourceKey = resourceKey,
                    BucketStart = bucket.Start,
                    BucketEnd = bucket.End,
                    OldValue = record?.Value ?? 0,
                    NewValue = newValue
                });
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    private record CellKeyDto(string? resourceKey, string? bucketStart);

    // Payload sent by the JS fill-handle drag handler.
    private record DragFillPayloadDto(CellKeyDto? source, List<CellKeyDto>? targets);

    // ── Public Methods (via @ref) ───────────────────────────────────────

    /// <summary>
    /// Rebuilds the visible buckets and header groups from the current parameters, showing
    /// the loading spinner while the recomputation runs. Call after mutating the data
    /// source out-of-band.
    /// </summary>
    public async Task Rebind()
    {
        _isLoading = true;
        await InvokeAsync(StateHasChanged);

        _effectiveAllocations = ComputeEffectiveAllocations();
        _visibleBuckets = ComputeVisibleBuckets();
        _headerGroups = ComputeTimelineHeaderGroups();

        _isLoading = false;
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Forces a re-render without recomputing visible buckets. Cheaper than <see cref="Rebind"/>.
    /// </summary>
    public async Task Refresh()
    {
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Navigates the timeline so the given date becomes the first visible bucket. Raises
    /// <c>VisibleStartChanged</c> and <c>OnVisibleRangeChanged</c>.
    /// </summary>
    /// <param name="date">The date that should become the new <see cref="VisibleStart"/>.</param>
    public async Task NavigateTo(DateTime date)
    {
        VisibleStart = date;
        _visibleBuckets = ComputeVisibleBuckets();
        _headerGroups = ComputeTimelineHeaderGroups();
        await VisibleStartChanged.InvokeAsync(VisibleStart);
        await OnVisibleRangeChanged.InvokeAsync(new VisibleRangeChangedArgs
        {
            NewStart = VisibleStart,
            NewEnd = GetVisibleEnd(),
            ViewGrain = _currentViewGrain
        });
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Advances the timeline by one bucket at the current view grain.</summary>
    public Task NavigateForward() => NavigateTo(AdvanceDate(VisibleStart, _currentViewGrain, 1));

    /// <summary>Steps the timeline back by one bucket at the current view grain.</summary>
    public Task NavigateBack() => NavigateTo(AdvanceDate(VisibleStart, _currentViewGrain, -1));

    private Task HandleJumpToDate() => NavigateTo(_jumpToDate);

    /// <summary>
    /// Navigate so the current-period column (today/this week/this month/etc.)
    /// appears as the second column in the viewport, giving one column of context.
    /// </summary>
    public Task NavigateToToday()
    {
        var periodStart = GetPeriodStart(DateTime.Today, _currentViewGrain);
        var target = AdvanceDate(periodStart, _currentViewGrain, -1);
        return NavigateTo(target);
    }

    /// <summary>Returns a snapshot of the currently selected cells as <see cref="AllocationCellRef"/> values.</summary>
    public IReadOnlyList<AllocationCellRef> GetSelectedCells() =>
        _selectedCells.Select(c => new AllocationCellRef
        {
            ResourceKey = c.ResourceKey,
            BucketStart = c.BucketStart
        }).ToList();

    /// <summary>Clears the cell selection and raises <c>OnSelectionChanged</c> with an empty selection.</summary>
    public async Task ClearSelection()
    {
        _selectedCells.Clear();
        await OnSelectionChanged.InvokeAsync(new SelectionChangedArgs
        {
            SelectedCells = Array.Empty<AllocationCellRef>(),
            SelectionMode = SelectionMode
        });
        await InvokeAsync(StateHasChanged);
    }

    // ── Splitter — Column-Derived Width ───────────────────────────────────

    /// <summary>
    /// Computes the total left-pane width as the sum of all visible column widths.
    /// This is the single source of truth for splitter position — no independent pane width exists.
    /// </summary>
    private double ComputeColumnWidthSum()
    {
        return _columns.Where(c => c.Visible).Sum(c => ParseColumnWidth(c.EffectiveWidth));
    }

    /// <summary>
    /// Derived MinLeftPaneWidth: sum of MinWidth for all visible columns.
    /// </summary>
    public double MinLeftPaneWidth => _columns.Where(c => c.Visible).Sum(c => c.MinWidth);

    /// <summary>
    /// Whether any visible column is resizable. If none are, the splitter is locked.
    /// </summary>
    private bool HasResizableColumn => _columns.Any(c => c.Visible && c.AllowResize);

    /// <summary>
    /// Finds the rightmost visible resizable column.
    /// </summary>
    private AllocationResourceColumn<TResource>? GetLastResizableColumn()
    {
        return _columns.Where(c => c.Visible && c.AllowResize).LastOrDefault();
    }

    private static double ParseColumnWidth(string width)
    {
        if (string.IsNullOrWhiteSpace(width) || width == "auto")
            return 100; // sensible default for auto columns

        // Strip "px" suffix and parse
        var numeric = width.Replace("px", "").Trim();
        return double.TryParse(numeric, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var val) ? val : 100;
    }

    /// <summary>
    /// Distributes a target total width across resizable columns proportionally.
    /// Non-resizable columns keep their current width.
    /// </summary>
    private void DistributeWidthToColumns(double targetTotal)
    {
        var visibleCols = _columns.Where(c => c.Visible).ToList();
        var fixedSum = visibleCols.Where(c => !c.AllowResize).Sum(c => ParseColumnWidth(c.EffectiveWidth));
        var resizable = visibleCols.Where(c => c.AllowResize).ToList();

        if (resizable.Count == 0) return; // all locked, nothing to distribute

        var available = targetTotal - fixedSum;
        if (available < resizable.Sum(c => c.MinWidth))
            available = resizable.Sum(c => c.MinWidth);

        var currentResizableSum = resizable.Sum(c => ParseColumnWidth(c.EffectiveWidth));
        if (currentResizableSum <= 0) currentResizableSum = resizable.Count * 100;

        foreach (var col in resizable)
        {
            var proportion = ParseColumnWidth(col.EffectiveWidth) / currentResizableSum;
            var newWidth = available * proportion;
            newWidth = Math.Max(col.MinWidth, newWidth);
            if (col.MaxWidth.HasValue) newWidth = Math.Min(col.MaxWidth.Value, newWidth);
            col.RuntimeWidth = $"{newWidth:F0}px";
        }
    }

    /// <summary>
    /// Applies a pixel delta to the last resizable column's width.
    /// Returns the actual delta applied (may be less due to min/max constraints).
    /// </summary>
    private double ApplyDeltaToLastResizableColumn(double deltaPx)
    {
        var col = GetLastResizableColumn();
        if (col is null) return 0;

        var currentWidth = ParseColumnWidth(col.EffectiveWidth);
        var newWidth = currentWidth + deltaPx;
        newWidth = Math.Max(col.MinWidth, newWidth);
        if (col.MaxWidth.HasValue) newWidth = Math.Min(col.MaxWidth.Value, newWidth);

        var actualDelta = newWidth - currentWidth;
        col.RuntimeWidth = $"{newWidth:F0}px";
        return actualDelta;
    }

    // ── Splitter Public Methods ──────────────────────────────────────────

    /// <summary>
    /// Sets the splitter position by distributing the requested left-pane width across the
    /// resizable resource columns. Honors per-column min/max constraints. Raises
    /// <c>SplitterPositionChanged</c>.
    /// </summary>
    /// <param name="widthPx">Desired left-pane width in pixels (clamped to the sum of column min widths).</param>
    public async Task SetSplitterPosition(double widthPx)
    {
        var minLeft = MinLeftPaneWidth;
        var clamped = Math.Max(widthPx, minLeft);
        DistributeWidthToColumns(clamped);
        ResolveLayoutContract();

        var newPosition = ComputeColumnWidthSum();
        _lastNonCollapsedPosition = newPosition;
        _collapsedSide = null;
        await SplitterPositionChanged.InvokeAsync(newPosition);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Collapses the splitter to one side, hiding the resource pane (Left) or the timeline pane (Right).
    /// Remembers the prior position so <see cref="RestoreSplitter"/> can put it back.
    /// </summary>
    /// <param name="side">Which side to collapse.</param>
    /// <exception cref="InvalidOperationException">Thrown when <c>AllowSplitterCollapse</c> is false.</exception>
    public async Task CollapseSplitter(SplitterSide side)
    {
        if (!AllowSplitterCollapse)
            throw new InvalidOperationException("AllowSplitterCollapse must be true to collapse the splitter.");

        _lastNonCollapsedPosition = _collapsedSide is null ? ComputeColumnWidthSum() : _lastNonCollapsedPosition;
        _collapsedSide = side;
        await OnSplitterCollapsed.InvokeAsync(side);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Restores the splitter to its last non-collapsed position. No-op if the splitter is
    /// not currently collapsed.
    /// </summary>
    public async Task RestoreSplitter()
    {
        if (_collapsedSide is null) return;

        var restoreWidth = _lastNonCollapsedPosition > 0 ? _lastNonCollapsedPosition : ComputeColumnWidthSum();
        DistributeWidthToColumns(restoreWidth);
        ResolveLayoutContract();

        var newPosition = ComputeColumnWidthSum();
        _collapsedSide = null;
        await OnSplitterRestored.InvokeAsync(newPosition);
        await SplitterPositionChanged.InvokeAsync(newPosition);
        await InvokeAsync(StateHasChanged);
    }

    // ── Splitter JS Interop Callbacks ──────────────────────────────────

    /// <summary>
    /// JS interop entry point: invoked when the splitter drag ends. If collapse is allowed
    /// and the user dragged below half the minimum, the splitter snaps closed; otherwise
    /// the delta is applied to the rightmost resizable column.
    /// </summary>
    /// <param name="newLeftWidth">Final left-pane width in pixels reported by JS.</param>
    [JSInvokable]
    public async Task OnSplitterDragEnd(double newLeftWidth)
    {
        _isDraggingSplitter = false;
        var currentSum = ComputeColumnWidthSum();
        var deltaPx = newLeftWidth - currentSum;

        if (AllowSplitterCollapse)
        {
            var minLeft = MinLeftPaneWidth;
            if (newLeftWidth < minLeft * 0.5)
            {
                _lastNonCollapsedPosition = currentSum;
                _collapsedSide = SplitterSide.Left;
                await OnSplitterCollapsed.InvokeAsync(SplitterSide.Left);
                await InvokeAsync(StateHasChanged);
                return;
            }
        }

        ApplyDeltaToLastResizableColumn(deltaPx);
        ResolveLayoutContract();

        var finalPosition = ComputeColumnWidthSum();
        _lastNonCollapsedPosition = finalPosition;
        _collapsedSide = null;
        await SplitterPositionChanged.InvokeAsync(finalPosition);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// JS interop entry point: invoked when the user requests collapse of the right pane
    /// (typically via a chevron affordance). Remembers the prior position and raises
    /// <c>OnSplitterCollapsed</c> with <see cref="SplitterSide.Right"/>.
    /// </summary>
    [JSInvokable]
    public async Task OnSplitterCollapseRight()
    {
        if (!AllowSplitterCollapse) return;
        _lastNonCollapsedPosition = ComputeColumnWidthSum();
        _collapsedSide = SplitterSide.Right;
        await OnSplitterCollapsed.InvokeAsync(SplitterSide.Right);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// JS interop entry point: invoked continuously during a splitter drag. Applies the
    /// delta to the rightmost resizable column for live preview without firing the
    /// commit-time events.
    /// </summary>
    /// <param name="newLeftWidth">Current left-pane width in pixels reported by JS.</param>
    [JSInvokable]
    public Task OnSplitterDragMove(double newLeftWidth)
    {
        _isDraggingSplitter = true;
        // During drag, apply delta to last column for live preview
        var currentSum = ComputeColumnWidthSum();
        var deltaPx = newLeftWidth - currentSum;
        ApplyDeltaToLastResizableColumn(deltaPx);
        ResolveLayoutContract();
        StateHasChanged();
        return Task.CompletedTask;
    }

    internal bool IsSplitterCollapsed => _collapsedSide.HasValue;
    internal SplitterSide? CollapsedSide => _collapsedSide;
    internal double CurrentSplitterPosition => ComputeColumnWidthSum();
    internal bool IsDraggingSplitter => _isDraggingSplitter;

    private string GetLeftPaneStyle()
    {
        if (_collapsedSide == SplitterSide.Left) return "width:0;overflow:hidden;";
        if (_collapsedSide == SplitterSide.Right) return "flex:1 1 auto;";
        var sum = ComputeColumnWidthSum();
        return $"width:{sum}px;";
    }

    private string GetRightPaneStyle()
    {
        if (_collapsedSide == SplitterSide.Right) return "width:0;overflow:hidden;";
        return string.Empty; // flex:1 1 0 from CSS fills remaining space
    }

    private async Task HandleRestoreClick()
    {
        await RestoreSplitter();
    }

    private void HandleSplitterFocus() => _isSplitterFocused = true;
    private void HandleSplitterBlur() => _isSplitterFocused = false;

    private async Task HandleSplitterKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        const double smallStep = 16;
        const double largeStep = 64;

        switch (e.Key)
        {
            case "ArrowLeft":
                var leftDelta = e.ShiftKey ? largeStep : smallStep;
                await SetSplitterPosition(ComputeColumnWidthSum() - leftDelta);
                break;
            case "ArrowRight":
                var rightDelta = e.ShiftKey ? largeStep : smallStep;
                await SetSplitterPosition(ComputeColumnWidthSum() + rightDelta);
                break;
            case "Home":
                if (AllowSplitterCollapse)
                    await CollapseSplitter(SplitterSide.Left);
                else
                    await SetSplitterPosition(MinLeftPaneWidth);
                break;
            case "End":
                if (AllowSplitterCollapse)
                    await CollapseSplitter(SplitterSide.Right);
                else
                    await SetSplitterPosition(double.MaxValue); // JS will clamp to container
                break;
            case "Enter":
                if (AllowSplitterCollapse)
                {
                    if (_collapsedSide.HasValue)
                        await RestoreSplitter();
                    else
                        await CollapseSplitter(SplitterSide.Right);
                }
                break;
        }
    }

    // ── Column Resize JS Interop Callbacks ─────────────────────────────

    /// <summary>Called on every mousemove while a column header resize is in progress. Updates
    /// the column RuntimeWidth and re-renders for live feedback.</summary>
    [JSInvokable]
    public Task OnColumnResizeDrag(string columnId, double newWidth)
    {
        var col = FindColumnBySizingId(columnId);
        if (col is null) return Task.CompletedTask;

        var clamped = Math.Max(col.MinWidth, col.MaxWidth.HasValue ? Math.Min(col.MaxWidth.Value, newWidth) : newWidth);
        col.RuntimeWidth = $"{clamped:F0}px";
        ResolveLayoutContract();
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>Called once on mouseup to finalize a column header resize.</summary>
    [JSInvokable]
    public async Task OnColumnResizeEnd(string columnId, double newWidth)
    {
        var col = FindColumnBySizingId(columnId);
        if (col is null) return;

        var clamped = Math.Max(col.MinWidth, col.MaxWidth.HasValue ? Math.Min(col.MaxWidth.Value, newWidth) : newWidth);
        col.RuntimeWidth = $"{clamped:F0}px";
        ResolveLayoutContract();

        var newPosition = ComputeColumnWidthSum();
        await SplitterPositionChanged.InvokeAsync(newPosition);
        await InvokeAsync(StateHasChanged);
    }

    private AllocationResourceColumn<TResource>? FindColumnBySizingId(string id)
        => _columnSizingIds
            .Where(kvp => kvp.Value == id && kvp.Key is AllocationResourceColumn<TResource>)
            .Select(kvp => (AllocationResourceColumn<TResource>)kvp.Key)
            .FirstOrDefault();

    // ── Time Column Resize JS Interop Callbacks ──────────────────────────

    /// <summary>Called on every mousemove while a time column header resize is in progress.
    /// Updates the per-column runtime width and re-renders for live feedback.</summary>
    [JSInvokable]
    public Task OnTimeColumnResizeDrag(int colIndex, int newWidth)
    {
        var min = Math.Max(40, MinTimeColumnWidth);
        var clamped = Math.Max(min, newWidth);
        if (MaxTimeColumnWidth > 0) clamped = Math.Min(MaxTimeColumnWidth, clamped);

        _timeColumnRuntimeWidths[colIndex] = clamped;
        StateHasChanged();
        return Task.CompletedTask;
    }

    /// <summary>Called once on mouseup to finalize a time column resize.</summary>
    [JSInvokable]
    public async Task OnTimeColumnResizeEnd(int colIndex, int newWidth)
    {
        var min = Math.Max(40, MinTimeColumnWidth);
        var clamped = Math.Max(min, newWidth);
        if (MaxTimeColumnWidth > 0) clamped = Math.Min(MaxTimeColumnWidth, clamped);

        _timeColumnRuntimeWidths[colIndex] = clamped;

        var dict = new Dictionary<int, int> { [colIndex] = clamped };
        await OnTimeColumnResized.InvokeAsync(dict);
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Called by JS after measuring the widest cell content for auto-fit.</summary>
    [JSInvokable]
    public async Task OnTimeColumnAutoFit(int colIndex, int measuredWidth)
    {
        var min = Math.Max(40, MinTimeColumnWidth);
        var clamped = Math.Max(min, measuredWidth);
        if (MaxTimeColumnWidth > 0) clamped = Math.Min(MaxTimeColumnWidth, clamped);

        _timeColumnRuntimeWidths[colIndex] = clamped;

        var dict = new Dictionary<int, int> { [colIndex] = clamped };
        await OnTimeColumnResized.InvokeAsync(dict);
        await InvokeAsync(StateHasChanged);
    }

    // ── Column Registration ─────────────────────────────────────────────

    internal void AddColumn(AllocationResourceColumn<TResource> column)
    {
        if (!_columns.Contains(column))
        {
            _columns.Add(column);
            ResolveLayoutContract();
            // SplitterPosition is always derived from column widths — no separate update needed
        }
    }

    internal void RemoveColumn(AllocationResourceColumn<TResource> column)
    {
        _columns.Remove(column);
        ResolveLayoutContract();
    }

    // ── Width Resolution (shared layout contract) ───────────────────────

    private void ResolveLayoutContract()
    {
        _widthProvider = ColumnWidthProvider ?? new FixedWidthProvider();

        var visibleCols = _columns.Where(c => c.Visible).ToList();
        var entries = new List<ColumnSizingEntry>(visibleCols.Count + _visibleBuckets.Count);

        // Resource columns
        for (var i = 0; i < visibleCols.Count; i++)
        {
            var col = visibleCols[i];
            var id = $"res-{col.Field}-{i}";
            _columnSizingIds[col] = id;
            entries.Add(new ColumnSizingEntry(id, col.EffectiveWidth, 50, null));
        }

        // Time bucket columns — use grain-specific default widths
        var bucketWidth = GetBucketDefaultWidth(_currentViewGrain);
        for (var i = 0; i < _visibleBuckets.Count; i++)
        {
            var id = $"bucket-{i}";
            entries.Add(new ColumnSizingEntry(id, bucketWidth, 50, null));
        }

        _layoutContract = _widthProvider.Resolve(entries);
    }

    private string GetBucketDefaultWidth(TimeGranularity grain)
    {
        var px = Math.Max(40, TimeColumnWidth);
        return $"{px}px";
    }

    /// <summary>
    /// Returns the number of columns that should fill the panel width via CSS calc.
    /// When VisibleEnd is null (the common dynamic case), returns _visibleColumnCount
    /// so columns stretch to fill the pane edge-to-edge.
    /// When VisibleEnd is explicitly set, returns 0 (fixed pixel columns with scrollbar).
    /// </summary>
    private int GetFillColumnCount()
    {
        if (VisibleColumnOverride.HasValue && VisibleColumnOverride.Value > 0)
            return VisibleColumnOverride.Value;

        // Dynamic mode: columns fill the pane when no explicit VisibleEnd is set
        if (!VisibleEnd.HasValue)
            return _visibleColumnCount;

        return 0; // explicit range → fixed-pixel columns with scrollbar
    }

    /// <summary>
    /// Returns the inline style string for the timeline table element.
    /// Uses table-layout:fixed without width:100% so the table auto-sizes to the
    /// sum of fixed-pixel column widths, allowing horizontal scrolling.
    /// </summary>
    private string GetTimelineTableStyle()
    {
        var fillCount = GetFillColumnCount();
        return fillCount > 0
            ? "table-layout:fixed;width:100%;"
            : "table-layout:fixed;";
    }

    internal string? GetResolvedColumnWidth(AllocationResourceColumn<TResource> column)
    {
        if (!_columnSizingIds.TryGetValue(column, out var id)) return null;
        return _layoutContract.WidthById.TryGetValue(id, out var width) ? width : null;
    }

    internal string? GetColumnWidthStyle(AllocationResourceColumn<TResource> column)
    {
        var width = GetResolvedColumnWidth(column);
        return width is null ? null : $"width:{width};";
    }

    internal string? GetBucketWidthStyle(int bucketIndex)
    {
        var fillCount = GetFillColumnCount();
        if (fillCount > 0)
        {
            // Distribute panel width evenly via CSS calc — no JS measurement needed.
            // Works because the timeline table has width:100% in fill modes.
            return $"width:calc(100% / {fillCount});";
        }

        // Check per-column runtime override (set during resize drag)
        if (_timeColumnRuntimeWidths.TryGetValue(bucketIndex, out var runtimePx))
            return $"width:{runtimePx}px;";

        // Fixed pixel width from layout contract; overflow → scrollbar.
        var id = $"bucket-{bucketIndex}";
        return _layoutContract.WidthById.TryGetValue(id, out var width) ? $"width:{width};" : null;
    }

    /// <summary>
    /// Returns the effective pixel width for a time column, accounting for per-column overrides.
    /// </summary>
    internal int GetEffectiveTimeColumnWidth(int bucketIndex)
    {
        if (_timeColumnRuntimeWidths.TryGetValue(bucketIndex, out var runtimePx))
            return runtimePx;
        return Math.Max(40, TimeColumnWidth);
    }

    // ── Event Handlers ──────────────────────────────────────────────────

    private async Task HandleViewGrainChange(ChangeEventArgs e)
    {
        if (Enum.TryParse<TimeGranularity>(e.Value?.ToString(), out var grain))
        {
            _currentViewGrain = grain;
            _visibleBuckets = ComputeVisibleBuckets();
            _headerGroups = ComputeTimelineHeaderGroups();
            ResolveLayoutContract();
            await ViewGrainChanged.InvokeAsync(grain);
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandleNavigateBack() => await NavigateBack();
    private async Task HandleNavigateForward() => await NavigateForward();
    private async Task HandleNavigateToToday() => await NavigateToToday();

    private async Task HandleScenarioSwitch(Guid setId)
    {
        if (setId == ActiveSetId) return;
        var previous = ActiveSetId;
        ActiveSetId = setId;
        _effectiveAllocations = ComputeEffectiveAllocations();
        await ActiveSetIdChanged.InvokeAsync(setId);
        await OnScenarioChanged.InvokeAsync(new ScenarioChangedArgs
        {
            PreviousSetId = previous,
            NewSetId = setId
        });
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleCellClick(object resourceKey, DateRange bucket)
    {
        _activeCell = (resourceKey, bucket.Start);

        if (SelectionMode == AllocationSelectionMode.None) return;

        var key = (resourceKey, bucket.Start);
        if (SelectionMode == AllocationSelectionMode.Cell)
        {
            _selectedCells.Clear();
            _selectedCells.Add(key);
        }
        else
        {
            if (_selectedCells.Contains(key))
                _selectedCells.Remove(key);
            else
                _selectedCells.Add(key);
        }

        await OnSelectionChanged.InvokeAsync(new SelectionChangedArgs
        {
            SelectedCells = GetSelectedCells(),
            SelectionMode = SelectionMode
        });
    }

    private async Task HandleCellDoubleClick(object resourceKey, DateRange bucket)
    {
        if (!IsCellEditable(bucket)) return;

        _editMode = true;
        _editResourceKey = resourceKey;
        _editBucket = bucket;
        var record = GetRecord(resourceKey, bucket);
        _editValue = record?.Value.ToString("0.#") ?? "0";
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleEditCommit(ChangeEventArgs e)
    {
        if (_editResourceKey is null || _editBucket is null) return;

        var newValueStr = e.Value?.ToString() ?? "0";
        if (!decimal.TryParse(newValueStr, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var newValue))
            newValue = 0;

        var record = GetRecord(_editResourceKey, _editBucket);
        var oldValue = record?.Value ?? 0;

        _editMode = false;

        await OnCellEdited.InvokeAsync(new CellEditedArgs
        {
            ResourceKey = _editResourceKey,
            BucketStart = _editBucket.Start,
            BucketEnd = _editBucket.End,
            OldValue = oldValue,
            NewValue = newValue
        });
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleEditBlur()
    {
        if (_editMode)
        {
            _editMode = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandleEditKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            _editMode = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task HandleCellContextMenu(Microsoft.AspNetCore.Components.Web.MouseEventArgs e, object resourceKey, DateRange bucket)
    {
        if (!EnableContextMenu) return;

        // Check CanExecuteAction for each command
        _contextMenuResourceKey = resourceKey;
        _contextMenuBucket = bucket;
        _contextMenuX = e.ClientX;
        _contextMenuY = e.ClientY;
        _contextMenuVisible = true;
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleContextMenuCommand(string commandName)
    {
        _contextMenuVisible = false;

        var targetCells = _selectedCells.Any()
            ? GetSelectedCells()
            : _contextMenuResourceKey is not null && _contextMenuBucket is not null
                ? new List<AllocationCellRef> { new() { ResourceKey = _contextMenuResourceKey, BucketStart = _contextMenuBucket.Start } }
                : Array.Empty<AllocationCellRef>() as IReadOnlyList<AllocationCellRef>;

        var canExecuteArgs = new CanExecuteActionArgs
        {
            CommandName = commandName,
            TargetCells = targetCells
        };
        await CanExecuteAction.InvokeAsync(canExecuteArgs);
        if (!canExecuteArgs.IsEnabled) return;

        var actionArgs = new ContextMenuActionArgs
        {
            CommandName = commandName,
            TargetCells = targetCells
        };
        await OnContextMenuAction.InvokeAsync(actionArgs);
        if (actionArgs.IsCancelled) return;

        // Handle built-in commands
        switch (commandName)
        {
            case "shift-forward":
                await HandleShiftCommand(1);
                break;
            case "shift-backward":
                await HandleShiftCommand(-1);
                break;
            case "distribute":
                await HandleDistributeCommand();
                break;
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleShiftCommand(int direction)
    {
        if (_contextMenuResourceKey is null || _contextMenuBucket is null) return;

        var records = (_effectiveAllocations ?? Enumerable.Empty<AllocationRecord>())
            .Where(a => Equals(a.ResourceId, _contextMenuResourceKey))
            .ToList();

        await OnShiftValues.InvokeAsync(new ShiftValuesArgs
        {
            ResourceKey = _contextMenuResourceKey,
            TaskId = records.FirstOrDefault()?.TaskId ?? 0,
            Direction = direction,
            Periods = 1,
            AffectedRecords = records
        });
    }

    private async Task HandleDistributeCommand()
    {
        if (_contextMenuResourceKey is null || _contextMenuBucket is null) return;

        var record = GetRecord(_contextMenuResourceKey, _contextMenuBucket);
        if (record is null) return;

        await OnDistributeRequested.InvokeAsync(new DistributeArgs
        {
            SourcePeriod = _contextMenuBucket,
            TargetValue = record.Value,
            TargetGranularity = AuthoritativeLevel,
            Mode = DefaultDistributionMode,
            ProposedDistribution = new[] { record }
        });
    }

    // ── Computed Properties ─────────────────────────────────────────────

    private string? SizeStyle
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Height)) parts.Add($"height:{Height}");
            if (!string.IsNullOrEmpty(Width)) parts.Add($"width:{Width}");
            return parts.Count > 0 ? string.Join(";", parts) : null;
        }
    }

    // ── Helper Methods ──────────────────────────────────────────────────

    private IEnumerable<AllocationRecord> ComputeEffectiveAllocations()
    {
        var baseAllocations = Allocations ?? Enumerable.Empty<AllocationRecord>();

        if (AllocationSets is null || ScenarioOverrides is null)
            return baseAllocations;

        var activeSet = AllocationSets.FirstOrDefault(s => s.SetId == ActiveSetId);
        if (activeSet is null || activeSet.Type == AllocationSetType.Baseline)
            return baseAllocations;

        var overrides = ScenarioOverrides.Where(o => o.SetId == ActiveSetId).ToList();
        var overriddenIds = overrides
            .Where(o => o.OriginalAllocationId.HasValue)
            .Select(o => o.OriginalAllocationId!.Value)
            .ToHashSet();
        var deletedIds = overrides
            .Where(o => o.IsDeleted && o.OriginalAllocationId.HasValue)
            .Select(o => o.OriginalAllocationId!.Value)
            .ToHashSet();

        var result = new List<AllocationRecord>();

        foreach (var alloc in baseAllocations)
        {
            if (deletedIds.Contains(alloc.AllocationId)) continue;
            if (overriddenIds.Contains(alloc.AllocationId))
            {
                var ovr = overrides.First(o => o.OriginalAllocationId == alloc.AllocationId && !o.IsDeleted);
                result.Add(ovr.Override);
            }
            else
            {
                result.Add(alloc);
            }
        }

        // Add new allocations (OriginalAllocationId is null, not deleted)
        result.AddRange(overrides
            .Where(o => o.OriginalAllocationId is null && !o.IsDeleted)
            .Select(o => o.Override));

        return result;
    }

    private List<DateRange> ComputeVisibleBuckets()
    {
        var buckets = new List<DateRange>();
        var end = GetVisibleEnd();
        var current = VisibleStart;

        while (current < end)
        {
            var next = AdvanceDate(current, _currentViewGrain, 1);
            buckets.Add(new DateRange { Start = current, End = next });
            current = next;
        }

        return buckets;
    }

    private DateTime GetVisibleEnd()
    {
        if (VisibleEnd.HasValue) return VisibleEnd.Value;
        // Dynamic: generate enough buckets to fill _visibleColumnCount at the current grain
        return AdvanceDate(VisibleStart, _currentViewGrain, _visibleColumnCount);
    }

    private static DateTime AdvanceDate(DateTime date, TimeGranularity grain, int count)
    {
        return grain switch
        {
            TimeGranularity.Day => date.AddDays(count),
            TimeGranularity.Week => date.AddDays(7 * count),
            TimeGranularity.Month => date.AddMonths(count),
            TimeGranularity.Quarter => date.AddMonths(3 * count),
            TimeGranularity.Year => date.AddYears(count),
            _ => date.AddDays(count)
        };
    }

    // ── Current-Period Detection ──────────────────────────────────────

    /// <summary>
    /// Returns true when the given bucket's period contains "now", used to
    /// highlight the current day/week/month/quarter/year column.
    /// </summary>
    private bool IsCurrentPeriod(DateRange bucket)
    {
        var now = DateTime.Now;
        return _currentViewGrain switch
        {
            TimeGranularity.Day     => bucket.Start.Date == DateTime.Today,
            TimeGranularity.Week    => GetIsoWeekStart(now) == bucket.Start.Date,
            TimeGranularity.Month   => bucket.Start.Year == now.Year && bucket.Start.Month == now.Month,
            TimeGranularity.Quarter => GetQuarterStart(now) == bucket.Start.Date,
            TimeGranularity.Year    => bucket.Start.Year == now.Year,
            _                       => false,
        };
    }

    /// <summary>Returns the start of the period that contains the given date.</summary>
    private static DateTime GetPeriodStart(DateTime date, TimeGranularity grain) => grain switch
    {
        TimeGranularity.Day     => date.Date,
        TimeGranularity.Week    => GetIsoWeekStart(date),
        TimeGranularity.Month   => new DateTime(date.Year, date.Month, 1),
        TimeGranularity.Quarter => GetQuarterStart(date),
        TimeGranularity.Year    => new DateTime(date.Year, 1, 1),
        _                       => date.Date,
    };

    private static DateTime GetIsoWeekStart(DateTime date)
    {
        var dow = (int)date.DayOfWeek;
        var diff = dow == 0 ? 6 : dow - 1; // Monday = 0
        return date.AddDays(-diff).Date;
    }

    private static DateTime GetQuarterStart(DateTime date)
    {
        var quarterMonth = ((date.Month - 1) / 3) * 3 + 1;
        return new DateTime(date.Year, quarterMonth, 1);
    }

    private object GetResourceKey(TResource resource)
    {
        // Use reflection to find an Id or Key property
        var type = typeof(TResource);
        var idProp = type.GetProperty("Id") ?? type.GetProperty("Key");
        return idProp?.GetValue(resource) ?? resource!;
    }

    private string GetResourceLabel(TResource resource)
    {
        var type = typeof(TResource);
        var nameProp = type.GetProperty("Name") ?? type.GetProperty("Title");
        return nameProp?.GetValue(resource)?.ToString() ?? resource?.ToString() ?? string.Empty;
    }

    private object? GetFieldValue(TResource resource, string field)
    {
        var prop = typeof(TResource).GetProperty(field);
        return prop?.GetValue(resource);
    }

    private AllocationRecord? GetRecord(object resourceKey, DateRange bucket)
    {
        return _effectiveAllocations?.FirstOrDefault(a =>
            Equals(a.ResourceId, resourceKey) &&
            a.BucketStart >= bucket.Start &&
            a.BucketStart < bucket.End);
    }

    private Func<object, DateRange, AllocationRecord?> _baselineRecord =>
        (resourceKey, bucket) =>
        {
            return Allocations?.FirstOrDefault(a =>
                Equals(a.ResourceId, resourceKey) &&
                a.BucketStart >= bucket.Start &&
                a.BucketStart < bucket.End);
        };

    private bool IsCellEditable(DateRange bucket)
    {
        if (_currentViewGrain != AuthoritativeLevel && !AllowZoomEdit)
            return false;
        return true;
    }

    private bool IsCellSelected(object resourceKey, DateRange bucket) =>
        _selectedCells.Contains((resourceKey, bucket.Start));

    private bool IsCellActive(object resourceKey, DateRange bucket) =>
        _activeCell.HasValue &&
        Equals(_activeCell.Value.ResourceKey, resourceKey) &&
        _activeCell.Value.BucketStart == bucket.Start;

    private bool IsCellConflict(object resourceKey, DateRange bucket)
    {
        // Check for overlapping allocations on same resource in same bucket
        if (_effectiveAllocations is null) return false;
        var count = _effectiveAllocations.Count(a =>
            Equals(a.ResourceId, resourceKey) &&
            a.BucketStart < bucket.End &&
            a.BucketEnd > bucket.Start);
        return count > 1;
    }

    private bool IsCellDisabled(DateRange bucket) => false;

    private bool IsResourceOverAllocated(object resourceKey)
    {
        if (Targets is null || _effectiveAllocations is null) return false;
        var totalActual = _effectiveAllocations.Where(a => Equals(a.ResourceId, resourceKey)).Sum(a => a.Value);
        var totalTarget = Targets.Where(t => Equals(t.ResourceId, resourceKey)).Sum(t => t.TargetValue);
        return totalTarget > 0 && totalActual > totalTarget;
    }

    private bool IsResourceRowSelected(object resourceKey) =>
        _selectedCells.Any(c => Equals(c.ResourceKey, resourceKey));

    private decimal? GetDelta(object resourceKey, DateRange bucket)
    {
        if (Targets is null || _effectiveAllocations is null) return null;
        var actual = _effectiveAllocations
            .Where(a => Equals(a.ResourceId, resourceKey) && a.BucketStart >= bucket.Start && a.BucketStart < bucket.End)
            .Sum(a => a.Value);
        var target = Targets
            .Where(t => Equals(t.ResourceId, resourceKey) && t.PeriodStart <= bucket.Start && t.PeriodEnd >= bucket.End)
            .Sum(t => t.TargetValue);
        if (target == 0) return null;
        return actual - target;
    }

    // ── Timeline Header Groups ────────────────────────────────────────────

    /// <summary>Represents a grouped header cell that spans multiple time columns.</summary>
    internal record TimelineHeaderGroup(string Text, int StartIndex, int ColumnSpan);

    /// <summary>Whether grouped header rows should be rendered above the leaf header row.</summary>
    internal bool HasTimelineHeaderGroups => _headerGroups.Count > 0;

    /// <summary>
    /// Computes grouped header rows from the visible time columns.
    /// Groups are determined by the current view grain:
    /// Day/Week → group by month-year of bucket start date.
    /// Month/Quarter → group by year.
    /// Year → no grouping (single leaf row is sufficient).
    /// </summary>
    private List<TimelineHeaderGroup> ComputeTimelineHeaderGroups()
    {
        if (_visibleBuckets.Count == 0) return new();

        return _currentViewGrain switch
        {
            TimeGranularity.Day => GroupBucketsBy(b => b.Start.ToString("MMM yyyy")),
            TimeGranularity.Week => GroupBucketsBy(b => b.Start.ToString("MMM yyyy")),
            TimeGranularity.Month => GroupBucketsBy(b => b.Start.Year.ToString()),
            TimeGranularity.Quarter => GroupBucketsBy(b => b.Start.Year.ToString()),
            // Year view: no meaningful grouping above year-level columns
            TimeGranularity.Year => new(),
            _ => new()
        };
    }

    private List<TimelineHeaderGroup> GroupBucketsBy(Func<DateRange, string> keySelector)
    {
        var groups = new List<TimelineHeaderGroup>();
        string? currentKey = null;
        int startIdx = 0;
        int count = 0;

        for (int i = 0; i < _visibleBuckets.Count; i++)
        {
            var key = keySelector(_visibleBuckets[i]);
            if (key != currentKey)
            {
                if (currentKey is not null)
                    groups.Add(new(currentKey, startIdx, count));
                currentKey = key;
                startIdx = i;
                count = 1;
            }
            else
            {
                count++;
            }
        }

        if (currentKey is not null)
            groups.Add(new(currentKey, startIdx, count));

        return groups;
    }

    /// <summary>
    /// Formats the leaf-row (bottom) header label for a time column.
    /// When grouped headers are active, the leaf label is shorter because
    /// month/year context is shown in the group row above.
    /// </summary>
    private string FormatBucketHeader(DateRange bucket)
    {
        return _currentViewGrain switch
        {
            // Day: short day label — month/year shown in group row
            TimeGranularity.Day => bucket.Start.ToString("ddd d"),
            // Week: week number + short date — month/year shown in group row
            TimeGranularity.Week => $"W{GetIsoWeekNumber(bucket.Start)} · {bucket.Start:MMM d}",
            // Month: short month name — year shown in group row
            TimeGranularity.Month => bucket.Start.ToString("MMM"),
            // Quarter: quarter label — year shown in group row
            TimeGranularity.Quarter => $"Q{(bucket.Start.Month - 1) / 3 + 1}",
            // Year: full year (no group row)
            TimeGranularity.Year => bucket.Start.ToString("yyyy"),
            _ => bucket.Start.ToShortDateString()
        };
    }

    private string FormatValue(decimal value)
    {
        return ValueMode switch
        {
            AllocationValueMode.Hours => $"{value:N1}h",
            AllocationValueMode.Currency => $"${value:N0}",
            _ => value.ToString("N1")
        };
    }

    private string FormatDelta(decimal delta)
    {
        var sign = delta > 0 ? "+" : "";
        return DeltaDisplayMode switch
        {
            DeltaDisplayMode.Value when ValueMode == AllocationValueMode.Hours => $"{sign}{delta:N1}h",
            DeltaDisplayMode.Value => $"{sign}${delta:N0}",
            DeltaDisplayMode.Percentage => $"{sign}{delta:P0}",
            _ => delta > 0 ? "Over" : "Under"
        };
    }

    private string GetCellAriaLabel(TResource resource, DateRange bucket, AllocationRecord? record)
    {
        var resourceName = GetResourceLabel(resource);
        var bucketLabel = FormatBucketHeader(bucket);
        if (record is null) return $"{resourceName}, {bucketLabel}, empty";
        return $"{resourceName}, {bucketLabel}, {FormatValue(record.Value)}";
    }

    private string GetScenarioLabel(AllocationSet set)
    {
        if (!string.IsNullOrEmpty(set.DisplayLabel)) return set.DisplayLabel;
        if (set.Type == AllocationSetType.Baseline && set.IsLocked && set.FinalizedDate.HasValue)
        {
            var fmt = BaselineDateFormat ?? "MMM d, yyyy";
            return $"Baseline As of {set.FinalizedDate.Value.ToString(fmt)}";
        }
        return set.Name;
    }

    private static int GetIsoWeekNumber(DateTime date)
    {
        var day = System.Globalization.CultureInfo.InvariantCulture.Calendar.GetDayOfWeek(date);
        if (day >= DayOfWeek.Monday && day <= DayOfWeek.Wednesday)
            date = date.AddDays(3);
        return System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            date, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
    }

    private void HandleGridClick()
    {
        if (_contextMenuVisible)
        {
            _contextMenuVisible = false;
            StateHasChanged();
        }
    }

    // ── IAsyncDisposable ────────────────────────────────────────────────

    /// <summary>
    /// Releases the JS interop module and tears down pane observers / event listeners.
    /// Safe to call repeatedly.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_jsModule is not null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("AllocationSchedulerInterop.unobservePane", _rightPaneId);
                await _jsModule.InvokeVoidAsync("AllocationSchedulerInterop.dispose", _gridRef);
                await _jsModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // Circuit already disconnected
            }
        }
        _dotNetRef?.Dispose();
    }
}
