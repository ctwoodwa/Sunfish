using System.Globalization;
using Sunfish.Foundation.Base;
using Sunfish.Components.Blazor.Base;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace Sunfish.Components.Blazor.Components.DataDisplay;

public partial class SunfishGantt<TItem> : SunfishComponentBase, IGanttViewHost, IAsyncDisposable
    where TItem : class
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    internal GanttFieldAccessor<TItem>? _accessor;
    private string? _accessorKey;
    private IEnumerable<TItem>? _lastData;
    private int _lastDataCount = -1;
    private readonly List<GanttNode<TItem>> _roots = new();
    private List<GanttNode<TItem>> _flatVisible = new();
    private readonly HashSet<object> _expandedIds = new();

    // ── Dependency rendering ──────────────────────────────────────────
    private record DependencyLine(double X1, double Y1, double X2, double Y2);
    private List<DependencyLine> _dependencyLines = new();
    private readonly string _instanceId = Guid.NewGuid().ToString("N")[..8];

    // ── Keyboard navigation ─────────────────────────────────────────
    private int _focusedIndex;

    // ── Filter state ──────────────────────────────────────────────────
    private readonly Dictionary<string, string> _filterValues = new();
    /// <summary>Ids that were auto-expanded by the filter so we can restore state when cleared.</summary>
    private readonly HashSet<object> _filterExpandedIds = new();
    private CancellationTokenSource? _filterDebounce;

    /// <summary>Column field currently showing the filter menu popup. Null = no menu open.</summary>
    private string? _filterMenuField;

    /// <summary>Pending filter value in the filter menu input (not yet applied).</summary>
    private string _filterMenuValue = "";

    // ── Checkbox filter state ─────────────────────────────────────────
    /// <summary>Column field whose checkbox-list drawer is currently open. Null = drawer closed.</summary>
    private string? _checkboxFilterField;

    /// <summary>Currently checked values in the open checkbox-list drawer.</summary>
    private HashSet<string> _checkboxFilterSelected = new();

    /// <summary>Distinct values available for the open checkbox-list column.</summary>
    private List<string> _checkboxFilterOptions = new();

    // ── Edit state ─────────────────────────────────────────────────────
    private int _editingRowIndex = -1;
    private Dictionary<string, object?> _editValues = new();
    /// <summary>In Incell mode, which column field is currently being edited. Null = no cell active.</summary>
    private string? _editingField;
    /// <summary>Deep clone of the item captured when editing began. Used for revert/comparison.</summary>
    private TItem? _originalItem;
    /// <summary>The item currently being inserted (new row). Null when not inserting.</summary>
    private TItem? _insertedItem;
    /// <summary>The parent item under which a child is being inserted. Null for root-level insert.</summary>
    private TItem? _parentItem;

    // ── Popup edit state ───────────────────────────────────────────────
    private bool _popupEditOpen;
    private string? _popupEditAnchorId;

    // ── Sort state ────────────────────────────────────────────────────
    private string? _sortField;
    private bool _sortAscending = true;
    /// <summary>Tri-state cycle counter: 0 = first click (asc), 1 = second (desc), 2 = third (clear).</summary>
    private int _sortCycleStep;

    // ── Screen reader announcements ───────────────────────────────────
    private string _announcement = "";

    // ── State init guard ──────────────────────────────────────────────
    private bool _stateInitFired;

    // ── Column chooser state ──────────────────────────────────────────
    private bool _columnChooserOpen;

    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _jsInstance;
    private DotNetObjectReference<SunfishGantt<TItem>>? _dotNetRef;
    private ElementReference _containerRef;

    [Parameter] public IEnumerable<TItem> Data { get; set; } = Enumerable.Empty<TItem>();

    [Parameter] public string IdField { get; set; } = "Id";
    [Parameter] public string ParentIdField { get; set; } = "ParentId";
    [Parameter] public string TitleField { get; set; } = "Title";
    [Parameter] public string StartField { get; set; } = "Start";
    [Parameter] public string EndField { get; set; } = "End";
    [Parameter] public string PercentCompleteField { get; set; } = "PercentComplete";
    [Parameter] public string DependsOnField { get; set; } = "DependsOn";

    /// <summary>
    /// Property name on TItem that contains child items (e.g., "Children" returning IEnumerable&lt;TItem&gt;).
    /// When set, enables hierarchical data binding mode. ParentIdField is ignored in this mode.
    /// </summary>
    [Parameter] public string? ItemsField { get; set; }

    /// <summary>
    /// Property name on TItem that returns a bool indicating whether the item has children that haven't been loaded yet.
    /// Used to show an expand arrow even when Items is empty (for on-demand/lazy loading).
    /// </summary>
    [Parameter] public string? HasChildrenField { get; set; }

    /// <summary>Whether sorting is enabled. When false, clicking column headers does not sort.</summary>
    [Parameter] public bool Sortable { get; set; } = true;

    /// <summary>Controls which filter UI is displayed. Currently only FilterRow is supported.</summary>
    [Parameter] public GanttFilterMode FilterMode { get; set; } = GanttFilterMode.FilterRow;

    /// <summary>Controls whether checkbox filters use a Drawer or Popup.</summary>
    [Parameter] public GanttFilterPopupMode FilterPopupMode { get; set; } = GanttFilterPopupMode.Drawer;

    /// <summary>Debounce delay in milliseconds for filter row input changes. 0 means immediate.</summary>
    [Parameter] public int FilterRowDebounceDelay { get; set; }

    [Parameter] public string? Width { get; set; }
    [Parameter] public string? Height { get; set; }

    [Parameter] public int TaskListWidth { get; set; } = 250;
    [Parameter] public int DayWidth { get; set; } = 30;
    [Parameter] public int RowHeight { get; set; } = 36;

    /// <summary>Custom template for the inner content of timeline bars. Replaces the default progress fill. Receives the task item as context.</summary>
    [Parameter] public RenderFragment<TItem>? TaskTemplate { get; set; }

    /// <summary>Custom tooltip template shown on bar hover. Receives the task item as context. When null, a default tooltip with title + dates is shown.</summary>
    [Parameter] public RenderFragment<TItem>? TooltipTemplate { get; set; }

    [Parameter] public EventCallback<TItem> OnTaskClick { get; set; }
    [Parameter] public EventCallback<GanttEditEventArgs> OnTaskEdit { get; set; }

    /// <summary>The currently selected task item. Supports two-way binding via @bind-SelectedTask.</summary>
    [Parameter] public TItem? SelectedTask { get; set; }

    /// <summary>Callback fired when SelectedTask changes. Used for two-way binding.</summary>
    [Parameter] public EventCallback<TItem?> SelectedTaskChanged { get; set; }

    /// <summary>Fires when a task is selected or deselected via click.</summary>
    [Parameter] public EventCallback<TItem?> OnTaskSelect { get; set; }

    /// <summary>Controls how the tree list enters edit mode. Currently only Inline is supported.</summary>
    [Parameter] public GanttTreeListEditMode TreeListEditMode { get; set; } = GanttTreeListEditMode.Inline;

    /// <summary>Where new items appear when added. Top = first row, Bottom = last row.</summary>
    [Parameter] public GanttNewRowPosition NewRowPosition { get; set; } = GanttNewRowPosition.Top;

    [Parameter] public EventCallback<GanttCreateEventArgs> OnCreate { get; set; }
    [Parameter] public EventCallback<GanttUpdateEventArgs> OnUpdate { get; set; }
    [Parameter] public EventCallback<GanttDeleteEventArgs> OnDelete { get; set; }
    [Parameter] public EventCallback<GanttExpandEventArgs> OnExpand { get; set; }
    [Parameter] public EventCallback<GanttCollapseEventArgs> OnCollapse { get; set; }

    /// <summary>When true, shows a column chooser button in the toolbar.</summary>
    [Parameter] public bool ShowColumnChooser { get; set; }

    /// <summary>Custom toolbar content rendered inside the toolbar area above the Gantt chart.</summary>
    [Parameter] public RenderFragment? GanttToolBarTemplate { get; set; }

    /// <summary>Child content slot where GanttColumn instances are declared.</summary>
    [Parameter] public RenderFragment? GanttColumns { get; set; }

    /// <summary>Child content slot where GanttView instances (GanttDayView, GanttWeekView, etc.) are declared.</summary>
    [Parameter] public RenderFragment? GanttViews { get; set; }

    /// <summary>Child content slot where SunfishGanttDependencies is declared.</summary>
    [Parameter] public RenderFragment? GanttDependenciesSlot { get; set; }

    /// <summary>The currently active view. Supports two-way binding via @bind-View.</summary>
    [Parameter] public GanttView View { get; set; } = GanttView.Week;

    /// <summary>Callback fired when the active view changes.</summary>
    [Parameter] public EventCallback<GanttView> ViewChanged { get; set; }

    /// <summary>Fires during initialization. Use to load saved state.</summary>
    [Parameter] public EventCallback<GanttStateEventArgs<TItem>> OnStateInit { get; set; }

    /// <summary>Fires when sort, filter, expand, or view state changes.</summary>
    [Parameter] public EventCallback<GanttStateEventArgs<TItem>> OnStateChanged { get; set; }

    // ── Column management ──────────────────────────────────────────────

    private readonly List<GanttColumn<TItem>> _columns = new();
    private List<GanttColumn<TItem>>? _visibleColumnsCache;

    internal void RegisterColumn(GanttColumn<TItem> column)
    {
        if (!_columns.Contains(column))
        {
            _columns.Add(column);
            _visibleColumnsCache = null;
            _ = InvokeAsync(StateHasChanged);
        }
    }

    internal void UnregisterColumn(GanttColumn<TItem> column)
    {
        _ = InvokeAsync(() =>
        {
            if (_columns.Remove(column))
            {
                _visibleColumnsCache = null;
                StateHasChanged();
            }
        });
    }

    internal List<GanttColumn<TItem>> VisibleColumns
        => _visibleColumnsCache ??= _columns.Where(c => c.Visible).ToList();

    private async Task ToggleColumnVisibility(GanttColumn<TItem> column)
    {
        column.SetVisible(!column.Visible);
        _visibleColumnsCache = null;
        await FireStateChanged("VisibleColumns");
        await InvokeAsync(StateHasChanged);
    }

    // ── Command column management ──────────────────────────────────────

    private GanttCommandColumn<TItem>? _commandColumn;

    internal void RegisterCommandColumn(GanttCommandColumn<TItem> col)
    {
        _commandColumn = col;
        _ = InvokeAsync(StateHasChanged);
    }

    internal void UnregisterCommandColumn(GanttCommandColumn<TItem> col)
    {
        if (ReferenceEquals(_commandColumn, col))
        {
            _commandColumn = null;
            _ = InvokeAsync(StateHasChanged);
        }
    }

    internal bool HasCommandColumn => _commandColumn is not null;

    // ── Dependencies management ────────────────────────────────────────────────────

    internal SunfishGanttDependencies<TItem>? _dependencies;

    internal void RegisterDependencies(SunfishGanttDependencies<TItem> deps)
    {
        _dependencies = deps;
        _ = InvokeAsync(StateHasChanged);
    }

    internal void UnregisterDependencies(SunfishGanttDependencies<TItem> deps)
    {
        if (ReferenceEquals(_dependencies, deps))
        {
            _dependencies = null;
            _ = InvokeAsync(StateHasChanged);
        }
    }

    // ── View management (IGanttViewHost) ───────────────────────────────

    private readonly List<GanttViewBase> _views = new();

    void IGanttViewHost.RegisterView(GanttViewBase view)
    {
        if (!_views.Contains(view))
        {
            _views.Add(view);
            ComputeTimeline();
            _ = InvokeAsync(StateHasChanged);
        }
    }

    void IGanttViewHost.UnregisterView(GanttViewBase view)
    {
        _ = InvokeAsync(() =>
        {
            if (_views.Remove(view))
                StateHasChanged();
        });
    }

    /// <summary>The view component matching the current View enum, or the first registered, or null.</summary>
    internal GanttViewBase? ActiveView => _views.FirstOrDefault(v => v.ViewType == View) ?? _views.FirstOrDefault();

    /// <summary>Whether timeline rendering should use the view-driven engine (true) or legacy DayWidth fallback (false).</summary>
    private bool UseViewEngine => _views.Count > 0;

    /// <summary>Whether the toolbar area should render (view selector buttons, custom template, or column chooser present).</summary>
    private bool ShowToolbar => _views.Count > 1 || GanttToolBarTemplate is not null || ShowColumnChooser;

    private async Task SwitchView(GanttView view)
    {
        View = view;
        await ViewChanged.InvokeAsync(view);
        ComputeTimeline();
        await FireStateChanged("View");
        await InvokeAsync(StateHasChanged);
    }

    // ── Timeline engine ────────────────────────────────────────────────

    internal record TimelineSlot(DateTime Start, DateTime End, string Label);
    internal record TimelineHeader(DateTime Start, DateTime End, string Label, int SpanSlots);

    private DateTime _rangeStart;
    private DateTime _rangeEnd;
    private List<TimelineSlot> _slots = new();
    private List<TimelineHeader> _mainHeaders = new();
    private double _totalTimelineWidth;
    private bool _timelineComputed;

    private void ComputeTimeline()
    {
        var accessor = _accessor!;
        var visible = _flatVisible;

        if (UseViewEngine)
        {
            var view = ActiveView!;
            _rangeStart = view.RangeStart ?? ComputeDataMin(accessor, visible);
            _rangeEnd = view.RangeEnd ?? ComputeDataMax(accessor, visible);

            // Align range to slot boundaries
            _rangeStart = AlignToSlotStart(_rangeStart, View);
            _rangeEnd = AlignToSlotEnd(_rangeEnd, View);

            _slots = GenerateSlots(_rangeStart, _rangeEnd, View);
            _mainHeaders = GenerateMainHeaders(_slots, View);
            _totalTimelineWidth = _slots.Count * view.SlotWidth;
        }
        else
        {
            // Legacy DayWidth fallback
            var minDate = visible.Count > 0 ? visible.Min(n => accessor.GetStart(n.Item)) : DateTime.Today;
            var maxDate = visible.Count > 0 ? visible.Max(n => accessor.GetEnd(n.Item)) : DateTime.Today.AddDays(30);
            _rangeStart = minDate;
            _rangeEnd = maxDate;
            var totalDays = (maxDate - minDate).TotalDays;
            if (totalDays <= 0) totalDays = 1;
            _totalTimelineWidth = totalDays * DayWidth;
            _slots = new List<TimelineSlot>();
            _mainHeaders = new List<TimelineHeader>();
        }

        _timelineComputed = true;
        ComputeDependencyLines();
    }

    private static DateTime ComputeDataMin(GanttFieldAccessor<TItem> accessor, List<GanttNode<TItem>> visible)
        => visible.Count > 0 ? visible.Min(n => accessor.GetStart(n.Item)) : DateTime.Today;

    private static DateTime ComputeDataMax(GanttFieldAccessor<TItem> accessor, List<GanttNode<TItem>> visible)
        => visible.Count > 0 ? visible.Max(n => accessor.GetEnd(n.Item)) : DateTime.Today.AddDays(30);

    private static DateTime AlignToSlotStart(DateTime date, GanttView view) => view switch
    {
        GanttView.Day => date.Date,
        GanttView.Week => StartOfWeek(date),
        GanttView.Month => StartOfWeek(new DateTime(date.Year, date.Month, 1)),
        GanttView.Year => new DateTime(date.Year, date.Month, 1),
        _ => date
    };

    private static DateTime AlignToSlotEnd(DateTime date, GanttView view) => view switch
    {
        GanttView.Day => date.Date.AddDays(1),
        GanttView.Week => StartOfWeek(date).AddDays(7),
        GanttView.Month => StartOfWeek(new DateTime(date.Year, date.Month, 1).AddMonths(1)).AddDays(7),
        GanttView.Year => new DateTime(date.Year, date.Month, 1).AddMonths(1),
        _ => date
    };

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.Date.AddDays(-diff);
    }

    internal List<TimelineSlot> GenerateSlots(DateTime rangeStart, DateTime rangeEnd, GanttView view)
    {
        var slots = new List<TimelineSlot>();
        switch (view)
        {
            case GanttView.Day:
                // Each slot = 1 hour
                var hourCursor = rangeStart;
                while (hourCursor < rangeEnd)
                {
                    var next = hourCursor.AddHours(1);
                    slots.Add(new TimelineSlot(hourCursor, next, hourCursor.ToString("HH:00")));
                    hourCursor = next;
                }
                break;

            case GanttView.Week:
                // Each slot = 1 day
                var dayCursor = rangeStart.Date;
                while (dayCursor < rangeEnd.Date)
                {
                    var next = dayCursor.AddDays(1);
                    slots.Add(new TimelineSlot(dayCursor, next, dayCursor.ToString("ddd")));
                    dayCursor = next;
                }
                break;

            case GanttView.Month:
                // Each slot = 1 week (starting Monday)
                var weekCursor = StartOfWeek(rangeStart);
                while (weekCursor < rangeEnd)
                {
                    var next = weekCursor.AddDays(7);
                    var weekNum = ISOWeek.GetWeekOfYear(weekCursor);
                    slots.Add(new TimelineSlot(weekCursor, next, $"W{weekNum}"));
                    weekCursor = next;
                }
                break;

            case GanttView.Year:
                // Each slot = 1 month
                var monthCursor = new DateTime(rangeStart.Year, rangeStart.Month, 1);
                while (monthCursor < rangeEnd)
                {
                    var next = monthCursor.AddMonths(1);
                    slots.Add(new TimelineSlot(monthCursor, next, monthCursor.ToString("MMM")));
                    monthCursor = next;
                }
                break;
        }
        return slots;
    }

    internal List<TimelineHeader> GenerateMainHeaders(List<TimelineSlot> slots, GanttView view)
    {
        var headers = new List<TimelineHeader>();
        if (slots.Count == 0) return headers;

        switch (view)
        {
            case GanttView.Day:
                // Main headers = days, grouping hourly slots
                GroupSlots(slots, s => s.Start.Date, s => s.Start.ToString("ddd, MMM d"), headers);
                break;

            case GanttView.Week:
                // Main headers = weeks (Mon DD - Sun DD)
                GroupSlots(slots, s => StartOfWeek(s.Start),
                    s =>
                    {
                        var ws = StartOfWeek(s.Start);
                        var we = ws.AddDays(6);
                        return $"{ws:MMM d} - {we:MMM d}";
                    }, headers);
                break;

            case GanttView.Month:
                // Main headers = months, grouping weekly slots
                GroupSlots(slots, s => new DateTime(s.Start.Year, s.Start.Month, 1),
                    s => s.Start.ToString("MMMM yyyy"), headers);
                break;

            case GanttView.Year:
                // Main headers = years, grouping monthly slots
                GroupSlots(slots, s => new DateTime(s.Start.Year, 1, 1),
                    s => s.Start.Year.ToString(), headers);
                break;
        }
        return headers;
    }

    private static void GroupSlots(
        List<TimelineSlot> slots,
        Func<TimelineSlot, DateTime> groupKey,
        Func<TimelineSlot, string> labelFn,
        List<TimelineHeader> headers)
    {
        var i = 0;
        while (i < slots.Count)
        {
            var key = groupKey(slots[i]);
            var label = labelFn(slots[i]);
            var start = slots[i].Start;
            var spanCount = 0;
            while (i < slots.Count && groupKey(slots[i]) == key)
            {
                spanCount++;
                i++;
            }
            var end = i < slots.Count ? slots[i].Start : slots[i - 1].End;
            headers.Add(new TimelineHeader(start, end, label, spanCount));
        }
    }

    /// <summary>
    /// Gets the pixel offset from the range start for a given date, based on the active view.
    /// For WeekView (slot = 1 day), a date 7 days after RangeStart returns 7 * SlotWidth.
    /// </summary>
    internal double GetPixelOffset(DateTime date)
    {
        if (!UseViewEngine)
        {
            // Legacy: pixel offset based on DayWidth
            return (date - _rangeStart).TotalDays * DayWidth;
        }

        var view = ActiveView!;
        var slotWidth = view.SlotWidth;

        var raw = View switch
        {
            GanttView.Day => (date - _rangeStart).TotalHours * slotWidth,
            GanttView.Week => (date - _rangeStart).TotalDays * slotWidth,
            GanttView.Month => (date - _rangeStart).TotalDays / 7.0 * slotWidth,
            GanttView.Year => GetMonthFractionalOffset(date, _rangeStart) * slotWidth,
            _ => 0
        };

        return Math.Clamp(raw, 0, _totalTimelineWidth);
    }

    /// <summary>
    /// Computes fractional month offset between two dates for YearView pixel mapping.
    /// </summary>
    private static double GetMonthFractionalOffset(DateTime date, DateTime rangeStart)
    {
        int wholeMonths = (date.Year - rangeStart.Year) * 12 + (date.Month - rangeStart.Month);
        double dayFraction = (date.Day - 1.0) / DateTime.DaysInMonth(date.Year, date.Month);
        return wholeMonths + dayFraction;
    }

    /// <summary>
    /// Gets the bar width in pixels for a task spanning from start to end.
    /// Clamped to minimum 4px.
    /// </summary>
    internal double GetBarWidth(DateTime start, DateTime end)
        => Math.Max(GetPixelOffset(end) - GetPixelOffset(start), 4);

    // ── Lifecycle ──────────────────────────────────────────────────────

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            if (UseViewEngine && !_timelineComputed)
            {
                ComputeTimeline();
                StateHasChanged();
            }

            _dotNetRef = DotNetObjectReference.Create(this);
            _jsModule = await JS.InvokeAsync<IJSObjectReference>(
                "import", "./_content/Sunfish.Components/js/marilo-gantt.js");
            _jsInstance = await _jsModule.InvokeAsync<IJSObjectReference>(
                "initGantt", _dotNetRef, _containerRef,
                new { rowHeight = RowHeight, slotWidth = ActiveView?.SlotWidth ?? DayWidth });
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        var prevKey = _accessorKey;
        _accessor = GetAccessor();
        var keyChanged = prevKey != _accessorKey;
        var refChanged = !ReferenceEquals(_lastData, Data);
        if (refChanged || keyChanged)
        {
            var newCount = (Data ?? Enumerable.Empty<TItem>()).Count();
            var skipEmpty = !keyChanged && newCount == 0 && _lastDataCount == 0;
            if (!skipEmpty)
            {
                _lastData = Data;
                _lastDataCount = newCount;
                BuildTree();
                RebuildFlatVisible();
            }
        }
        _visibleColumnsCache = null;
        _timelineComputed = false;
        ComputeTimeline();

        if (!_stateInitFired && OnStateInit.HasDelegate)
        {
            _stateInitFired = true;
            var initialState = GetState();
            var args = new GanttStateEventArgs<TItem> { State = initialState };
            await OnStateInit.InvokeAsync(args);
            // If the handler set a different state object, apply it
            if (args.State is not null && !ReferenceEquals(args.State, initialState))
                await SetStateAsync(args.State);
        }

        await base.OnParametersSetAsync();
    }

    /// <summary>Refreshes the Gantt's internal tree from the current Data collection. Call this after mutating Data in place.</summary>
    public async Task Rebind()
    {
        _lastData = null;
        _lastDataCount = -1;
        BuildTree();
        RebuildFlatVisible();
        _lastData = Data;
        _lastDataCount = (Data ?? Enumerable.Empty<TItem>()).Count();
        ComputeTimeline();
        await InvokeAsync(StateHasChanged);
    }

    private GanttFieldAccessor<TItem> GetAccessor()
    {
        var key = $"{IdField}|{ParentIdField}|{TitleField}|{StartField}|{EndField}|{PercentCompleteField}|{DependsOnField}|{ItemsField}|{HasChildrenField}";
        if (_accessor is null || _accessorKey != key)
        {
            _accessor = new GanttFieldAccessor<TItem>(
                IdField, ParentIdField, TitleField, StartField, EndField, PercentCompleteField, DependsOnField,
                ItemsField, HasChildrenField);
            _accessorKey = key;
        }
        return _accessor;
    }

    private void BuildTree()
    {
        _roots.Clear();
        var accessor = _accessor!;

        if (!string.IsNullOrEmpty(ItemsField))
        {
            // Hierarchical mode: items contain their children via ItemsField
            BuildTreeHierarchical(accessor);
        }
        else
        {
            // Flat mode: ParentId-based linking
            BuildTreeFlat(accessor);
        }

        ComputeSummaryValues();
    }

    private void BuildTreeHierarchical(GanttFieldAccessor<TItem> accessor)
    {
        var items = (Data ?? Enumerable.Empty<TItem>()).ToList();
        foreach (var item in items)
        {
            var node = CreateNodeRecursive(accessor, item, 0);
            _roots.Add(node);
        }

        // Seed expanded state
        SeedExpandedState(_roots);
    }

    private GanttNode<TItem> CreateNodeRecursive(GanttFieldAccessor<TItem> accessor, TItem item, int depth)
    {
        var node = new GanttNode<TItem>
        {
            Item = item,
            Id = accessor.GetId(item),
            Depth = depth,
        };

        var children = accessor.GetItems(item);
        if (children is not null)
        {
            foreach (var child in children)
            {
                var childNode = CreateNodeRecursive(accessor, child, depth + 1);
                childNode.Parent = node;
                node.Children.Add(childNode);
            }
        }

        return node;
    }

    private void SeedExpandedState(List<GanttNode<TItem>> roots)
    {
        var stack = new Stack<GanttNode<TItem>>(roots);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n.Id is not null && n.Children.Count > 0 && !_expandedIds.Contains(n.Id))
                _expandedIds.Add(n.Id);
            foreach (var c in n.Children) stack.Push(c);
        }
    }

    private void BuildTreeFlat(GanttFieldAccessor<TItem> accessor)
    {
        var items = (Data ?? Enumerable.Empty<TItem>()).ToList();
        var byId = new Dictionary<object, GanttNode<TItem>>();
        var ordered = new List<GanttNode<TItem>>(items.Count);

        var insertionIndex = 0;
        foreach (var item in items)
        {
            var node = new GanttNode<TItem>
            {
                Item = item,
                Id = accessor.GetId(item),
                ParentId = accessor.GetParentId(item),
                OriginalIndex = insertionIndex++,
            };
            ordered.Add(node);
            if (node.Id is not null && !byId.ContainsKey(node.Id))
            {
                byId[node.Id] = node;
            }
        }

        // Prune stale expanded ids from deleted nodes so reappearing ids get default-expanded treatment
        _expandedIds.IntersectWith(byId.Keys);

        foreach (var node in ordered)
        {
            if (node.ParentId is not null
                && byId.TryGetValue(node.ParentId, out var parent)
                && !WouldCreateCycle(node, parent))
            {
                parent.Children.Add(node);
                node.Parent = parent;
            }
            else
            {
                _roots.Add(node);
            }
        }

        // Depth assignment via DFS from roots
        var stack = new Stack<GanttNode<TItem>>();
        foreach (var r in _roots)
        {
            r.Depth = 0;
            stack.Push(r);
        }
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            foreach (var c in n.Children)
            {
                c.Depth = n.Depth + 1;
                stack.Push(c);
            }
        }

        // Seed expanded state for nodes with children, preserving existing entries
        foreach (var node in ordered)
        {
            if (node.Id is not null && node.Children.Count > 0 && !_expandedIds.Contains(node.Id))
            {
                _expandedIds.Add(node.Id);
            }
        }
    }

    /// <summary>
    /// Computes summary values for parent nodes: Start = min of children, End = max of children,
    /// PercentComplete = weighted average by duration.
    /// </summary>
    private void ComputeSummaryValues()
    {
        var accessor = _accessor!;
        var allNodes = new List<GanttNode<TItem>>();
        CollectAllNodes(_roots, allNodes);

        foreach (var node in allNodes.OrderByDescending(n => n.Depth))
        {
            if (node.Children.Count == 0) continue;

            var minStart = node.Children.Min(c => accessor.GetStart(c.Item));
            var maxEnd = node.Children.Max(c => accessor.GetEnd(c.Item));
            var totalDuration = node.Children.Sum(c => (accessor.GetEnd(c.Item) - accessor.GetStart(c.Item)).TotalDays);
            var weightedPct = totalDuration > 0
                ? node.Children.Sum(c => accessor.GetPercentComplete(c.Item) * (accessor.GetEnd(c.Item) - accessor.GetStart(c.Item)).TotalDays) / totalDuration
                : 0;

            node.ComputedStart = minStart;
            node.ComputedEnd = maxEnd;
            node.ComputedPercentComplete = weightedPct;
        }
    }

    private static void CollectAllNodes(List<GanttNode<TItem>> roots, List<GanttNode<TItem>> result)
    {
        foreach (var root in roots)
        {
            result.Add(root);
            CollectAllNodes(root.Children, result);
        }
    }

    private static bool WouldCreateCycle(GanttNode<TItem> candidateChild, GanttNode<TItem> candidateParent)
    {
        var cursor = candidateParent;
        while (cursor is not null)
        {
            if (ReferenceEquals(cursor, candidateChild)) return true;
            cursor = cursor.Parent;
        }
        return false;
    }

    private bool IsExpanded(GanttNode<TItem> node)
        => node.Id is null || _expandedIds.Contains(node.Id);

    /// <summary>
    /// Toggles sort on the given field. Cycle: ascending -> descending -> unsorted.
    /// Called from clickable column headers.
    /// </summary>
    internal async Task SortBy(string field)
    {
        if (!Sortable) return;
        if (string.IsNullOrEmpty(field)) return;

        if (_sortField == field)
        {
            _sortCycleStep++;
            if (_sortCycleStep == 1)
            {
                // Second click: descending
                _sortAscending = false;
            }
            else
            {
                // Third click: clear sort
                _sortField = null;
                _sortAscending = true;
                _sortCycleStep = 0;
            }
        }
        else
        {
            // New field: ascending
            _sortField = field;
            _sortAscending = true;
            _sortCycleStep = 0;
        }

        RebuildFlatVisible();
        await FireStateChanged("SortDescriptor");
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Sorts sibling groups recursively by the current sort field, preserving hierarchy.
    /// Children stay under their parent; only the order among siblings changes.
    /// </summary>
    private void ApplyHierarchicalSort()
    {
        if (_accessor is null) return;

        if (_sortField is null)
        {
            RestoreOriginalOrder(_roots);
            return;
        }

        SortSiblings(_roots, _sortField, _sortAscending, _accessor);
    }

    private static void RestoreOriginalOrder(List<GanttNode<TItem>> siblings)
    {
        siblings.Sort((a, b) => a.OriginalIndex.CompareTo(b.OriginalIndex));
        foreach (var node in siblings)
            if (node.Children.Count > 0)
                RestoreOriginalOrder(node.Children);
    }

    private static void SortSiblings(List<GanttNode<TItem>> siblings, string field, bool ascending, GanttFieldAccessor<TItem> accessor)
    {
        if (siblings.Count <= 1) return;

        var sorted = ascending
            ? siblings.OrderBy(n => accessor.GetFieldValue(n.Item, field), NullSafeObjectComparer.Instance).ToList()
            : siblings.OrderByDescending(n => accessor.GetFieldValue(n.Item, field), NullSafeObjectComparer.Instance).ToList();
        siblings.Clear();
        siblings.AddRange(sorted);

        foreach (var node in siblings)
        {
            if (node.Children.Count > 0)
                SortSiblings(node.Children, field, ascending, accessor);
        }
    }

    private sealed class NullSafeObjectComparer : IComparer<object?>
    {
        public static readonly NullSafeObjectComparer Instance = new();
        public int Compare(object? x, object? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            if (x is IComparable cx)
            {
                try { return cx.CompareTo(y); }
                catch { return string.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal); }
            }
            return string.Compare(x.ToString(), y.ToString(), StringComparison.Ordinal);
        }
    }

    // ── Filter logic ──────────────────────────────────────────────────

    /// <summary>Called from @oninput on filter row inputs.</summary>
    internal async Task OnFilterInput(string field, string value)
    {
        if (FilterRowDebounceDelay > 0)
        {
            _filterDebounce?.Cancel();
            _filterDebounce?.Dispose();
            var cts = new CancellationTokenSource();
            _filterDebounce = cts;
            try
            {
                await Task.Delay(FilterRowDebounceDelay, cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }

        if (string.IsNullOrEmpty(value))
            _filterValues.Remove(field);
        else
            _filterValues[field] = value;

        RebuildFlatVisible();
        await FireStateChanged("FilterValues");
        await InvokeAsync(StateHasChanged);
    }

    // ── Filter menu methods ───────────────────────────────────────────

    private void ToggleFilterMenu(string field)
    {
        if (_filterMenuField == field)
        {
            _filterMenuField = null; // Close
        }
        else
        {
            _filterMenuField = field;
            _filterValues.TryGetValue(field, out var current);
            _filterMenuValue = current ?? "";
        }
    }

    private async Task ApplyFilterMenu()
    {
        if (_filterMenuField is null) return;

        if (string.IsNullOrEmpty(_filterMenuValue))
            _filterValues.Remove(_filterMenuField);
        else
            _filterValues[_filterMenuField] = _filterMenuValue;

        _filterMenuField = null;
        _filterExpandedIds.Clear();
        RebuildFlatVisible();
        await FireStateChanged("FilterValues");
        await InvokeAsync(StateHasChanged);
    }

    private async Task ClearFilterMenu()
    {
        if (_filterMenuField is null) return;

        _filterValues.Remove(_filterMenuField);
        _filterMenuValue = "";
        _filterMenuField = null;
        _filterExpandedIds.Clear();
        RebuildFlatVisible();
        await FireStateChanged("FilterValues");
        await InvokeAsync(StateHasChanged);
    }

    private void HandleGanttClick()
    {
        if (_filterMenuField is not null)
        {
            _filterMenuField = null;
        }
    }

    private async Task HandleTaskSelect(TItem item)
    {
        // Toggle selection: clicking the already-selected task deselects it
        var newSelection = ReferenceEquals(SelectedTask, item) ? null : item;
        SelectedTask = newSelection;
        await SelectedTaskChanged.InvokeAsync(newSelection);
        await OnTaskSelect.InvokeAsync(newSelection);
    }

    // ── Checkbox filter methods ───────────────────────────────────────

    private void OpenCheckboxFilter(string field)
    {
        _checkboxFilterField = field;
        var accessor = _accessor!;
        _checkboxFilterOptions = _flatVisible
            .Select(n => accessor.GetFieldValue(n.Item, field)?.ToString() ?? "")
            .Where(v => !string.IsNullOrEmpty(v))
            .Distinct()
            .OrderBy(v => v)
            .ToList();
        // Pre-check currently filtered values; if no filter active, check all
        _checkboxFilterSelected = _filterValues.TryGetValue(field, out var existing)
            ? existing.Split('|').ToHashSet()
            : _checkboxFilterOptions.ToHashSet();
        StateHasChanged();
    }

    private void ToggleCheckboxOption(string value)
    {
        if (_checkboxFilterSelected.Contains(value))
            _checkboxFilterSelected.Remove(value);
        else
            _checkboxFilterSelected.Add(value);
        StateHasChanged();
    }

    private async Task ApplyCheckboxFilter()
    {
        if (_checkboxFilterField is null) return;
        if (_checkboxFilterSelected.Count == _checkboxFilterOptions.Count)
        {
            // All selected = no filter
            _filterValues.Remove(_checkboxFilterField);
        }
        else
        {
            _filterValues[_checkboxFilterField] = string.Join("|", _checkboxFilterSelected);
        }
        _checkboxFilterField = null;
        RebuildFlatVisible();
        await FireStateChanged("FilterValues");
        await InvokeAsync(StateHasChanged);
    }

    private async Task ClearCheckboxFilter()
    {
        if (_checkboxFilterField is null) return;
        _filterValues.Remove(_checkboxFilterField);
        _checkboxFilterField = null;
        RebuildFlatVisible();
        await FireStateChanged("FilterValues");
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Whether any filter is currently active.</summary>
    private bool HasActiveFilters => _filterValues.Count > 0;

    /// <summary>Returns true if a node's own field values match ALL active filters (AND logic).</summary>
    private bool NodeMatchesAllFilters(GanttNode<TItem> node)
    {
        var accessor = _accessor!;
        foreach (var kvp in _filterValues)
        {
            var fieldValue = accessor.GetFieldValue(node.Item, kvp.Key)?.ToString();
            if (fieldValue is null) return false;

            if (kvp.Value.Contains('|'))
            {
                // Checkbox filter: pipe-delimited set of allowed values
                var allowed = kvp.Value.Split('|').ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (!allowed.Contains(fieldValue)) return false;
            }
            else
            {
                // Text filter: case-insensitive contains
                if (!fieldValue.Contains(kvp.Value, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }
        return true;
    }

    /// <summary>Returns true if the node or any of its descendants match all filters.</summary>
    private bool NodeOrDescendantMatches(GanttNode<TItem> node)
    {
        if (NodeMatchesAllFilters(node)) return true;
        foreach (var child in node.Children)
        {
            if (NodeOrDescendantMatches(child)) return true;
        }
        return false;
    }

    /// <summary>
    /// Marks nodes as filter-visible using a bottom-up check.
    /// A node is visible if it matches all filters OR any descendant does.
    /// Auto-expands parents of matches so results are visible.
    /// Returns the set of visible node references.
    /// </summary>
    private HashSet<GanttNode<TItem>>? ComputeFilterVisibility()
    {
        if (!HasActiveFilters)
        {
            // Restore any filter-expanded ids back to collapsed
            foreach (var id in _filterExpandedIds)
                _expandedIds.Remove(id);
            _filterExpandedIds.Clear();
            return null; // null means "show all"
        }

        // First, undo previous auto-expansions so MarkVisible sees a clean slate
        foreach (var id in _filterExpandedIds)
            _expandedIds.Remove(id);
        _filterExpandedIds.Clear();

        // Now compute visibility fresh
        var visible = new HashSet<GanttNode<TItem>>();
        foreach (var root in _roots)
            MarkVisible(root, visible);
        return visible;
    }

    /// <summary>Recursively marks a node visible if it or any descendant matches. Returns true if visible.</summary>
    private bool MarkVisible(GanttNode<TItem> node, HashSet<GanttNode<TItem>> visible)
    {
        bool selfMatches = NodeMatchesAllFilters(node);
        bool anyChildVisible = false;

        foreach (var child in node.Children)
        {
            if (MarkVisible(child, visible))
                anyChildVisible = true;
        }

        if (selfMatches || anyChildVisible)
        {
            visible.Add(node);

            // Auto-expand parents with matching descendants so matches are visible
            if (anyChildVisible && node.Id is not null && !_expandedIds.Contains(node.Id))
            {
                _expandedIds.Add(node.Id);
                _filterExpandedIds.Add(node.Id);
            }

            return true;
        }

        return false;
    }

    private void RebuildFlatVisible()
    {
        // Compute filter visibility (non-destructive), then sort, then flatten
        var filterVisible = ComputeFilterVisibility();
        ApplyHierarchicalSort();

        var list = new List<GanttNode<TItem>>();
        var visited = new HashSet<GanttNode<TItem>>();
        var stack = new Stack<GanttNode<TItem>>();
        for (int i = _roots.Count - 1; i >= 0; i--)
        {
            if (filterVisible is null || filterVisible.Contains(_roots[i]))
                stack.Push(_roots[i]);
        }
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (!visited.Add(n)) continue;
            list.Add(n);
            if (IsExpanded(n))
            {
                for (int i = n.Children.Count - 1; i >= 0; i--)
                {
                    if (filterVisible is null || filterVisible.Contains(n.Children[i]))
                        stack.Push(n.Children[i]);
                }
            }
        }
        _flatVisible = list;

        // Clamp focused index after visibility changes
        if (_focusedIndex >= _flatVisible.Count)
            _focusedIndex = Math.Max(0, _flatVisible.Count - 1);
    }

    private void ComputeDependencyLines()
    {
        _dependencyLines.Clear();
        var accessor = _accessor;
        if (accessor is null) return;

        for (int i = 0; i < _flatVisible.Count; i++)
        {
            var node = _flatVisible[i];
            var deps = accessor.GetDependsOn(node.Item);
            if (deps is null) continue;

            foreach (var depId in deps)
            {
                // Find the dependency source node in _flatVisible
                var srcIdx = _flatVisible.FindIndex(n => Equals(n.Id, depId));
                if (srcIdx < 0) continue;
                var srcNode = _flatVisible[srcIdx];

                // Line from end of source bar to start of dependent bar
                var srcEnd = accessor.GetEnd(srcNode.Item);
                var nodeStart = accessor.GetStart(node.Item);
                var x1 = GetPixelOffset(srcEnd);
                var y1 = srcIdx * RowHeight + RowHeight / 2.0;
                var x2 = GetPixelOffset(nodeStart);
                var y2 = i * RowHeight + RowHeight / 2.0;

                _dependencyLines.Add(new DependencyLine(x1, y1, x2, y2));
            }
        }
    }

    private async Task ToggleExpanded(GanttNode<TItem> node)
    {
        if (node.Id is null) return;

        bool wasExpanded = _expandedIds.Contains(node.Id);
        if (!_expandedIds.Add(node.Id))
        {
            _expandedIds.Remove(node.Id);
        }
        RebuildFlatVisible();

        bool shouldRender = true;
        var taskName = _accessor!.GetTitle(node.Item);
        if (wasExpanded)
        {
            Announce($"{taskName} collapsed");
            var args = new GanttCollapseEventArgs { Item = node.Item };
            await OnCollapse.InvokeAsync(args);
            shouldRender = args.ShouldRender;
        }
        else
        {
            var childCount = node.Children.Count;
            Announce($"{taskName} expanded, {childCount} child tasks");
            var args = new GanttExpandEventArgs { Item = node.Item };
            await OnExpand.InvokeAsync(args);
            shouldRender = args.ShouldRender;
        }

        await FireStateChanged("ExpandedItems");

        if (shouldRender)
            await InvokeAsync(StateHasChanged);
    }

    // ── Keyboard navigation handler ─────────────────────────────────
    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (_flatVisible.Count == 0) return;

        // When in edit mode, Enter commits and Escape cancels
        if (_editingRowIndex >= 0)
        {
            switch (e.Key)
            {
                case "Enter":
                    await CommitEdit();
                    return;
                case "Escape":
                    await CancelEdit();
                    await InvokeAsync(StateHasChanged);
                    return;
                default:
                    return; // Let inputs handle other keys normally
            }
        }

        var prevFocusedIndex = _focusedIndex;
        switch (e.Key)
        {
            case "ArrowDown":
                _focusedIndex = Math.Min(_focusedIndex + 1, _flatVisible.Count - 1);
                break;
            case "ArrowUp":
                _focusedIndex = Math.Max(_focusedIndex - 1, 0);
                break;
            case "ArrowRight":
                var rightNode = _flatVisible[_focusedIndex];
                if (rightNode.Children.Count > 0 && !IsExpanded(rightNode))
                    await ToggleExpanded(rightNode);
                else if (rightNode.Children.Count > 0 && IsExpanded(rightNode))
                    _focusedIndex = Math.Min(_focusedIndex + 1, _flatVisible.Count - 1);
                break;
            case "ArrowLeft":
                var leftNode = _flatVisible[_focusedIndex];
                if (leftNode.Children.Count > 0 && IsExpanded(leftNode))
                    await ToggleExpanded(leftNode);
                else if (leftNode.Parent is not null)
                    _focusedIndex = _flatVisible.IndexOf(leftNode.Parent);
                break;
            case "Home":
                _focusedIndex = 0;
                break;
            case "End":
                _focusedIndex = _flatVisible.Count - 1;
                break;
            case "Enter":
            case " ":
                if (_focusedIndex >= 0 && _focusedIndex < _flatVisible.Count)
                    await OnTaskClick.InvokeAsync(_flatVisible[_focusedIndex].Item);
                break;
            default:
                return; // Don't re-render for unhandled keys
        }

        if (_focusedIndex != prevFocusedIndex && _focusedIndex >= 0 && _focusedIndex < _flatVisible.Count)
        {
            var name = _accessor!.GetTitle(_flatVisible[_focusedIndex].Item);
            Announce($"Task {name}, row {_focusedIndex + 1} of {_flatVisible.Count}");
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Returns the number of siblings (including this node) at the same level under the same parent.</summary>
    private int GetSiblingCount(GanttNode<TItem> node)
        => node.Parent is not null ? node.Parent.Children.Count : _roots.Count;

    /// <summary>Returns the 1-based position of this node among its siblings.</summary>
    private int GetPositionInSiblings(GanttNode<TItem> node)
    {
        var siblings = node.Parent is not null ? node.Parent.Children : _roots;
        return siblings.IndexOf(node) + 1;
    }

    // ── JS interop callbacks (D1) ─────────────────────────────────────

    /// <summary>Converts a pixel delta to a TimeSpan based on the active view's scale.</summary>
    private TimeSpan PixelsToTimeSpan(double deltaPixels)
    {
        if (UseViewEngine)
        {
            var view = ActiveView!;
            var slotWidth = view.SlotWidth;
            if (slotWidth <= 0) return TimeSpan.Zero;

            return View switch
            {
                GanttView.Day => TimeSpan.FromHours(deltaPixels / slotWidth),
                GanttView.Week => TimeSpan.FromDays(deltaPixels / slotWidth),
                GanttView.Month => TimeSpan.FromDays(deltaPixels / slotWidth * 7),
                GanttView.Year => TimeSpan.FromDays(deltaPixels / slotWidth * 30.44),
                _ => TimeSpan.Zero
            };
        }

        // Legacy DayWidth fallback
        return DayWidth > 0 ? TimeSpan.FromDays(deltaPixels / DayWidth) : TimeSpan.Zero;
    }

    [JSInvokable]
    public async Task OnBarMoved(int barIndex, double deltaPixels)
    {
        if (barIndex < 0 || barIndex >= _flatVisible.Count) return;
        var node = _flatVisible[barIndex];
        var accessor = _accessor!;
        var delta = PixelsToTimeSpan(deltaPixels);

        var oldStart = accessor.GetStart(node.Item);
        var oldEnd = accessor.GetEnd(node.Item);
        accessor.SetStart(node.Item, oldStart + delta);
        accessor.SetEnd(node.Item, oldEnd + delta);

        ComputeTimeline();
        await OnUpdate.InvokeAsync(new GanttUpdateEventArgs { Item = node.Item });
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnBarResized(int barIndex, double leftDelta, double rightDelta)
    {
        if (barIndex < 0 || barIndex >= _flatVisible.Count) return;
        var node = _flatVisible[barIndex];
        var accessor = _accessor!;

        if (leftDelta != 0)
        {
            var oldStart = accessor.GetStart(node.Item);
            accessor.SetStart(node.Item, oldStart + PixelsToTimeSpan(leftDelta));
        }

        if (rightDelta != 0)
        {
            var oldEnd = accessor.GetEnd(node.Item);
            accessor.SetEnd(node.Item, oldEnd + PixelsToTimeSpan(rightDelta));
        }

        ComputeTimeline();
        await OnUpdate.InvokeAsync(new GanttUpdateEventArgs { Item = node.Item });
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public async Task OnBarProgressChanged(int barIndex, double newPercent)
    {
        if (barIndex < 0 || barIndex >= _flatVisible.Count) return;
        var node = _flatVisible[barIndex];
        var clamped = Math.Clamp(newPercent, 0, 100);
        _accessor!.SetPercentComplete(node.Item, Math.Round(clamped, 1));

        await OnUpdate.InvokeAsync(new GanttUpdateEventArgs { Item = node.Item });
        await InvokeAsync(StateHasChanged);
    }

    // ── Inline editing ─────────────────────────────────────────────────

    /// <summary>Enters edit mode for the specified row, populating _editValues from current field values.</summary>
    internal async Task BeginEdit(int rowIndex)
    {
        if (rowIndex < 0 || rowIndex >= _flatVisible.Count) return;
        var node = _flatVisible[rowIndex];
        if (OnTaskEdit.HasDelegate)
        {
            var args = new GanttEditEventArgs { Item = node.Item };
            await OnTaskEdit.InvokeAsync(args);
            if (args.IsCancelled) return;
        }
        _editingRowIndex = rowIndex;
        _originalItem = GanttCloneHelper.DeepClone(node.Item);
        _editValues.Clear();
        var cols = VisibleColumns;
        foreach (var col in cols)
        {
            if (col.Editable && !string.IsNullOrEmpty(col.Field))
            {
                _editValues[col.Field] = _accessor!.GetFieldValue(node.Item, col.Field);
            }
        }
        await FireStateChanged("EditItem");
    }

    /// <summary>Writes edited values back to the item and fires OnUpdate.</summary>
    internal async Task CommitEdit()
    {
        if (_editingRowIndex < 0 || _editingRowIndex >= _flatVisible.Count) return;
        var node = _flatVisible[_editingRowIndex];
        var committedField = _editingField ?? (VisibleColumns.FirstOrDefault(c => c.Editable)?.Field ?? "field");
        foreach (var (field, value) in _editValues)
            _accessor!.SetFieldValue(node.Item, field, value);
        _editingRowIndex = -1;
        _editingField = null;
        _editValues.Clear();
        _originalItem = null;
        ComputeTimeline();
        Announce($"{committedField} updated");
        await OnUpdate.InvokeAsync(new GanttUpdateEventArgs { Item = node.Item });
        await FireStateChanged("EditItem");
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Discards edits and exits edit mode.</summary>
    internal async Task CancelEdit()
    {
        _editingRowIndex = -1;
        _editingField = null;
        _editValues.Clear();
        _originalItem = null;
        Announce("Edit cancelled");
        await FireStateChanged("EditItem");
    }

    /// <summary>Enters incell edit mode for a single cell. Only active when TreeListEditMode == Incell.</summary>
    internal async Task BeginCellEdit(int rowIdx, string field)
    {
        if (TreeListEditMode != GanttTreeListEditMode.Incell) return;
        if (rowIdx < 0 || rowIdx >= _flatVisible.Count) return;

        if (OnTaskEdit.HasDelegate)
        {
            var node = _flatVisible[rowIdx];
            var args = new GanttEditEventArgs { Item = node.Item };
            await OnTaskEdit.InvokeAsync(args);
            if (args.IsCancelled) return;
        }

        // If already editing a different cell, commit the previous one first
        if (_editingRowIndex >= 0 && (_editingRowIndex != rowIdx || _editingField != field))
        {
            await CommitEdit();
        }

        var isNewRow = _editingRowIndex != rowIdx;
        _editingRowIndex = rowIdx;
        _editingField = field;
        var node2 = _flatVisible[rowIdx];
        if (isNewRow)
            _originalItem = GanttCloneHelper.DeepClone(node2.Item);
        _editValues.Clear();
        // Only load the single field value
        var val = _accessor!.GetFieldValue(node2.Item, field);
        _editValues[field] = val;

        var taskName2 = _accessor!.GetTitle(node2.Item);
        Announce($"Editing {field} for {taskName2}");
        await FireStateChanged("EditItem");
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Opens the popup edit form for a row. Only active when TreeListEditMode == Popup.</summary>
    internal async Task BeginPopupEdit(int rowIdx, string field)
    {
        if (TreeListEditMode != GanttTreeListEditMode.Popup) return;
        if (rowIdx < 0 || rowIdx >= _flatVisible.Count) return;

        var node = _flatVisible[rowIdx];

        if (OnTaskEdit.HasDelegate)
        {
            var args = new GanttEditEventArgs { Item = node.Item };
            await OnTaskEdit.InvokeAsync(args);
            if (args.IsCancelled) return;
        }

        _editingRowIndex = rowIdx;
        _editingField = field;
        _originalItem = GanttCloneHelper.DeepClone(node.Item);
        _editValues.Clear();
        foreach (var col in VisibleColumns.Where(c => c.Editable && !string.IsNullOrEmpty(c.Field)))
        {
            _editValues[col.Field!] = _accessor!.GetFieldValue(node.Item, col.Field!);
        }

        _popupEditAnchorId = $"mar-gantt-cell-{rowIdx}-{field}";
        _popupEditOpen = true;

        await FireStateChanged("EditItem");
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Commits popup edit values and closes the popup.</summary>
    internal async Task CommitPopupEdit()
    {
        _popupEditOpen = false;
        await CommitEdit();
    }

    /// <summary>Cancels popup editing and closes the popup.</summary>
    internal async Task CancelPopupEdit()
    {
        _popupEditOpen = false;
        await CancelEdit();
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Tabs to the next editable cell in incell mode. Commits current cell first.</summary>
    internal async Task TabToNextCell()
    {
        if (_editingRowIndex < 0 || _editingField is null) return;

        // Snapshot the current position before CommitEdit clears it
        var currentRowIdx = _editingRowIndex;
        var currentField = _editingField;

        // Commit current cell
        await CommitEdit();

        var cols = VisibleColumns.Where(c => c.Editable && !string.IsNullOrEmpty(c.Field)).ToList();
        var currentIdx = cols.FindIndex(c => c.Field == currentField);

        if (currentIdx >= 0 && currentIdx < cols.Count - 1)
        {
            // Next cell in same row
            await BeginCellEdit(currentRowIdx, cols[currentIdx + 1].Field!);
        }
        else if (currentRowIdx < _flatVisible.Count - 1)
        {
            // First cell in next row
            if (cols.Count > 0)
                await BeginCellEdit(currentRowIdx + 1, cols[0].Field!);
        }
        // else: last cell of last row — already committed, just exit
    }

    /// <summary>Keyboard handler for incell edit inputs (Tab, Enter, Escape).</summary>
    private async Task HandleCellKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "Tab":
                await TabToNextCell();
                break;
            case "Enter":
                await CommitEdit();
                break;
            case "Escape":
                await CancelEdit();
                await InvokeAsync(StateHasChanged);
                break;
        }
    }

    /// <summary>Fires OnDelete with the given item when the command column Delete button is clicked.</summary>
    private async Task HandleCommandDelete(TItem item)
    {
        await OnDelete.InvokeAsync(new GanttDeleteEventArgs { Item = item });
    }

    /// <summary>Fires OnCreate with a new default instance when the command column Add button is clicked.</summary>
    private async Task HandleCommandAdd()
    {
        var newItem = Activator.CreateInstance<TItem>();
        _insertedItem = newItem;
        _parentItem = null;
        try
        {
            await OnCreate.InvokeAsync(new GanttCreateEventArgs { Item = newItem });
        }
        finally
        {
            _insertedItem = null;
            _parentItem = null;
        }
    }

    /// <summary>Returns the HTML input type for a given field based on its property type, with optional column EditorType override.</summary>
    private string GetInputType(string field, GanttColumn<TItem>? col = null)
    {
        if (col?.EditorType is { } editorType)
        {
            return editorType switch
            {
                GanttEditorType.TextBox => "text",
                GanttEditorType.TextArea => "text", // textarea handled in markup
                GanttEditorType.CheckBox => "checkbox",
                GanttEditorType.DatePicker => "date",
                GanttEditorType.NumericTextBox => "number",
                _ => "text"
            };
        }
        var prop = typeof(TItem).GetProperty(field);
        if (prop is null) return "text";
        var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return "date";
        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal)
            || type == typeof(int) || type == typeof(long) || type == typeof(short)
            || type == typeof(byte))
            return "number";
        if (type == typeof(bool))
            return "checkbox";
        return "text";
    }

    /// <summary>Parses a string value from an input element back to the correct property type.</summary>
    private object? ParseValue(string field, string? raw)
    {
        if (raw is null) return null;
        var prop = typeof(TItem).GetProperty(field);
        if (prop is null) return raw;
        var type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        if (type == typeof(DateTime) && DateTime.TryParse(raw, out var dt))
            return dt;
        if (type == typeof(DateTimeOffset) && DateTimeOffset.TryParse(raw, out var dto))
            return dto;
        if (type == typeof(double) && double.TryParse(raw, CultureInfo.InvariantCulture, out var dbl))
            return dbl;
        if (type == typeof(float) && float.TryParse(raw, CultureInfo.InvariantCulture, out var flt))
            return flt;
        if (type == typeof(decimal) && decimal.TryParse(raw, CultureInfo.InvariantCulture, out var dec))
            return dec;
        if (type == typeof(int) && int.TryParse(raw, out var i32))
            return i32;
        if (type == typeof(long) && long.TryParse(raw, out var i64))
            return i64;
        if (type == typeof(short) && short.TryParse(raw, out var i16))
            return i16;
        if (type == typeof(byte) && byte.TryParse(raw, out var b))
            return b;
        if (type == typeof(bool))
            return raw == "true" || raw == "True" || raw == "on";
        return raw;
    }

    /// <summary>Formats a value for display in an input element's value attribute.</summary>
    private string FormatEditValue(string field, object? value)
    {
        if (value is null) return string.Empty;
        var inputType = GetInputType(field);
        if (inputType == "date" && value is DateTime dtVal)
            return dtVal.ToString("yyyy-MM-dd");
        if (inputType == "date" && value is DateTimeOffset dtoVal)
            return dtoVal.ToString("yyyy-MM-dd");
        return value.ToString() ?? string.Empty;
    }

    // ── State API ──────────────────────────────────────────────────────

    /// <summary>Returns a snapshot of the current Gantt state.</summary>
    public GanttState<TItem> GetState()
    {
        return new GanttState<TItem>
        {
            SortDescriptor = _sortField is not null
                ? new GanttSortDescriptor { Field = _sortField, Ascending = _sortAscending }
                : null,
            FilterValues = _filterValues.Count > 0
                ? new Dictionary<string, string>(_filterValues)
                : null,
            ExpandedItems = _expandedIds.Count > 0
                ? _expandedIds.ToList().AsReadOnly()
                : null,
            View = View,
            EditItem = _editingRowIndex >= 0 && _editingRowIndex < _flatVisible.Count
                ? _flatVisible[_editingRowIndex].Item
                : default,
            OriginalEditItem = _originalItem,
            InsertedItem = _insertedItem,
            ParentItem = _parentItem,
            EditField = _editingField,
            VisibleColumns = _columns.Any(c => !c.Visible)
                ? _columns.Where(c => c.Visible).Select(c => c.Field).ToList()
                : null,
        };
    }

    /// <summary>Applies a saved state. Pass null to reset to defaults. Does not fire OnStateChanged (avoids echo loops).</summary>
    public async Task SetStateAsync(GanttState<TItem>? state)
    {
        if (state is null)
        {
            _sortField = null;
            _sortAscending = true;
            _sortCycleStep = 0;
            _filterValues.Clear();
            _filterExpandedIds.Clear();
            if (_editingRowIndex >= 0) await CancelEdit();
            // Don't clear _expandedIds — let BuildTree re-seed defaults
            _lastData = null; // Force rebuild
            BuildTree();
            RebuildFlatVisible();
            ComputeTimeline();
            await InvokeAsync(StateHasChanged);
            return;
        }

        // Apply sort
        if (state.SortDescriptor is { } sort)
        {
            _sortField = sort.Field;
            _sortAscending = sort.Ascending;
            _sortCycleStep = sort.Ascending ? 0 : 1;
        }
        else
        {
            _sortField = null;
            _sortAscending = true;
            _sortCycleStep = 0;
        }

        // Apply filters
        _filterValues.Clear();
        _filterExpandedIds.Clear();
        if (state.FilterValues is { Count: > 0 } filters)
        {
            foreach (var (key, value) in filters)
                _filterValues[key] = value;
        }

        // Apply expanded items (null = keep defaults, empty = collapse all, populated = use provided)
        if (state.ExpandedItems is not null)
        {
            _expandedIds.Clear();
            foreach (var id in state.ExpandedItems)
                _expandedIds.Add(id);
        }

        // Apply view
        if (state.View is { } view && view != View)
        {
            View = view;
            await ViewChanged.InvokeAsync(view);
        }

        // Apply visible columns
        if (state.VisibleColumns is { } visibleCols)
        {
            var visibleSet = new HashSet<string>(visibleCols, StringComparer.OrdinalIgnoreCase);
            foreach (var col in _columns)
                col.SetVisible(visibleSet.Contains(col.Field ?? ""));
            _visibleColumnsCache = null;
        }

        RebuildFlatVisible();
        ComputeTimeline();

        // Apply edit state
        if (state.EditItem is not null)
        {
            var editIdx = _flatVisible.FindIndex(n => ReferenceEquals(n.Item, state.EditItem));
            if (editIdx >= 0)
            {
                await BeginEdit(editIdx);
            }
        }
        else if (_editingRowIndex >= 0)
        {
            await CancelEdit();
        }

        // Apply insert tracking (transient — consumer manages data)
        _insertedItem = state.InsertedItem;
        _parentItem = state.ParentItem;

        await InvokeAsync(StateHasChanged);
    }

    private async Task FireStateChanged(string propertyName)
    {
        if (OnStateChanged.HasDelegate)
        {
            await OnStateChanged.InvokeAsync(new GanttStateEventArgs<TItem>
            {
                State = GetState(),
                PropertyName = propertyName
            });
        }
    }

    /// <summary>Sets the aria-live announcement text. The caller is responsible for triggering StateHasChanged after this call.</summary>
    private void Announce(string message)
    {
        _announcement = message;
    }

    public async ValueTask DisposeAsync()
    {
        if (_jsInstance is not null)
        {
            try
            {
                if (_jsModule is not null)
                    await _jsModule.InvokeVoidAsync("dispose", _jsInstance);
            }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException) { }
        }
        if (_jsModule is not null)
        {
            try { await _jsModule.DisposeAsync(); }
            catch (Exception ex) when (ex is JSDisconnectedException or TaskCanceledException or ObjectDisposedException) { }
        }
        _dotNetRef?.Dispose();
    }
}
