using System.Globalization;
using Microsoft.AspNetCore.Components;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Scheduling;

/// <summary>
/// Canonical MVP Gantt chart for the Sunfish Scheduling component family.
/// Displays a flat or hierarchical collection of task items as a two-pane timeline:
/// a left-hand task list and a right-hand horizontal timeline with a bar per task
/// positioned proportionally to its start and end dates.
/// </summary>
/// <typeparam name="TItem">
/// The task item type. Values are read through string-accessor fields
/// (<see cref="IdField"/>, <see cref="StartField"/>, <see cref="EndField"/>, etc.)
/// so consumers are free to use their own domain shapes without adapters.
/// </typeparam>
/// <remarks>
/// <para>
/// This is the <strong>MVP surface</strong> mapped under
/// <c>apps/docs/component-specs/component-mapping.json → gantt</c> and lives in the
/// <c>Sunfish.UIAdapters.Blazor.Components.Scheduling</c> namespace. A separate, richer
/// <c>Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishGantt&lt;TItem&gt;</c>
/// predates this file and remains in place for the DataDisplay demo set; the two
/// components share a name but live in different namespaces by design.
/// </para>
/// <para>
/// Drag-to-resize, drag-to-reorder, task virtualisation, and full dependency routing
/// are deliberately <em>deferred</em> from this MVP (hence the <c>partial</c> mapping
/// status). Click, update, create, and delete callbacks are wired and cancellable.
/// </para>
/// </remarks>
public partial class SunfishGantt<TItem> : SunfishComponentBase
    where TItem : class
{
    // ── Data & field accessors ────────────────────────────────────────

    /// <summary>Flat collection of task items to render. A parent/child tree is inferred from <see cref="ParentIdField"/>.</summary>
    [Parameter, EditorRequired] public IEnumerable<TItem> Data { get; set; } = [];

    /// <summary>Name of the property on <typeparamref name="TItem"/> that yields the task's unique id.</summary>
    [Parameter, EditorRequired] public string IdField { get; set; } = "Id";

    /// <summary>
    /// Name of the property that yields the parent id (or <c>null</c> for roots).
    /// Leave default to treat every row as a root.
    /// </summary>
    [Parameter] public string ParentIdField { get; set; } = "ParentId";

    /// <summary>Name of the property that yields the task start <see cref="DateTime"/>.</summary>
    [Parameter, EditorRequired] public string StartField { get; set; } = "Start";

    /// <summary>Name of the property that yields the task end <see cref="DateTime"/>.</summary>
    [Parameter, EditorRequired] public string EndField { get; set; } = "End";

    /// <summary>
    /// Name of the property that yields the percent-complete value (0..100). Optional —
    /// when the accessor returns no numeric, the bar's fill overlay is omitted.
    /// </summary>
    [Parameter] public string PercentCompleteField { get; set; } = "PercentComplete";

    /// <summary>Name of the property that yields the display title shown in the left column and the bar tooltip.</summary>
    [Parameter, EditorRequired] public string TitleField { get; set; } = "Title";

    /// <summary>
    /// Name of the property that yields an <see cref="IEnumerable{T}"/> of predecessor task ids.
    /// Rendered as thin connector arrows between bars. Optional.
    /// </summary>
    [Parameter] public string DependenciesField { get; set; } = "Dependencies";

    // ── Timeline configuration ────────────────────────────────────────

    /// <summary>Timeline granularity. Drives column width units and header labels.</summary>
    [Parameter] public GanttView View { get; set; } = GanttView.Week;

    /// <summary>
    /// Left edge of the timeline. Defaults to the minimum task start across <see cref="Data"/>
    /// (or <see cref="DateTime.Today"/> when the collection is empty).
    /// </summary>
    [Parameter] public DateTime? TimelineStart { get; set; }

    /// <summary>
    /// Right edge of the timeline. Defaults to the maximum task end across <see cref="Data"/>
    /// (or <see cref="TimelineStart"/> + 30 days when the collection is empty).
    /// </summary>
    [Parameter] public DateTime? TimelineEnd { get; set; }

    /// <summary>Pixel width of a single day column in the timeline. Default <c>32</c>.</summary>
    [Parameter] public int ColumnWidth { get; set; } = 32;

    /// <summary>Row height in pixels for both the task list and the timeline. Default <c>32</c>.</summary>
    [Parameter] public int RowHeight { get; set; } = 32;

    /// <summary>Pixel width of the left-hand task-title column. Default <c>260</c>.</summary>
    [Parameter] public int TitleColumnWidth { get; set; } = 260;

    /// <summary>Optional explicit width applied as inline <c>width:</c>.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Optional explicit height applied as inline <c>height:</c>.</summary>
    [Parameter] public string? Height { get; set; }

    // ── Event callbacks ───────────────────────────────────────────────

    /// <summary>Fires when the user clicks a task row (either in the list or the bar).</summary>
    [Parameter] public EventCallback<GanttTaskEventArgs<TItem>> OnTaskClick { get; set; }

    /// <summary>
    /// Fires when a consumer-triggered update is about to be applied to a task.
    /// Set <c>IsCancelled</c> to veto the change.
    /// </summary>
    [Parameter] public EventCallback<GanttTaskEventArgs<TItem>> OnTaskUpdate { get; set; }

    /// <summary>
    /// Fires when a new task is about to be added. Set <c>IsCancelled</c> to veto the insert.
    /// </summary>
    [Parameter] public EventCallback<GanttTaskEventArgs<TItem>> OnTaskCreate { get; set; }

    /// <summary>Fires when a task is about to be removed. Set <c>IsCancelled</c> to keep it.</summary>
    [Parameter] public EventCallback<GanttTaskEventArgs<TItem>> OnTaskDelete { get; set; }

    // ── Internal state ────────────────────────────────────────────────

    private readonly Dictionary<Type, PropertyAccessorCache> _accessorCache = new();

    private DateTime EffectiveStart => TimelineStart ?? ComputeDefaultTimelineStart();
    private DateTime EffectiveEnd   => TimelineEnd   ?? ComputeDefaultTimelineEnd();

    private int TotalDays => Math.Max(1, (int)Math.Ceiling((EffectiveEnd.Date - EffectiveStart.Date).TotalDays) + 1);
    private int TotalTimelineWidth => TotalDays * ColumnWidth;

    private IReadOnlyList<TItem> _flattened = [];

    protected override void OnParametersSet()
    {
        _flattened = FlattenHierarchy(Data);
    }

    // ── Accessors (string → PropertyInfo cache per TItem) ─────────────

    private sealed class PropertyAccessorCache
    {
        public readonly Dictionary<string, Func<object, object?>> Getters = new();
    }

    private Func<object, object?>? GetAccessor(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return null;
        var type = typeof(TItem);
        if (!_accessorCache.TryGetValue(type, out var cache))
        {
            cache = new PropertyAccessorCache();
            _accessorCache[type] = cache;
        }
        if (cache.Getters.TryGetValue(fieldName, out var cached)) return cached;

        var prop = type.GetProperty(fieldName);
        if (prop == null)
        {
            cache.Getters[fieldName] = _ => null;
            return cache.Getters[fieldName];
        }
        Func<object, object?> getter = inst => prop.GetValue(inst);
        cache.Getters[fieldName] = getter;
        return getter;
    }

    private object? GetField(TItem item, string field) => GetAccessor(field)?.Invoke(item);

    private string GetTitle(TItem item) =>
        GetField(item, TitleField)?.ToString() ?? string.Empty;

    private object? GetId(TItem item) => GetField(item, IdField);

    private object? GetParentId(TItem item) => GetField(item, ParentIdField);

    private DateTime GetStart(TItem item)
    {
        var raw = GetField(item, StartField);
        return raw switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var p) => p,
            _ => DateTime.Today,
        };
    }

    private DateTime GetEnd(TItem item)
    {
        var raw = GetField(item, EndField);
        return raw switch
        {
            DateTime dt => dt,
            DateTimeOffset dto => dto.DateTime,
            string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var p) => p,
            _ => GetStart(item).AddDays(1),
        };
    }

    private double GetPercentComplete(TItem item)
    {
        var raw = GetField(item, PercentCompleteField);
        return raw switch
        {
            double d => Math.Clamp(d, 0d, 100d),
            float f  => Math.Clamp((double)f, 0d, 100d),
            decimal m => Math.Clamp((double)m, 0d, 100d),
            int i    => Math.Clamp((double)i, 0d, 100d),
            long l   => Math.Clamp((double)l, 0d, 100d),
            string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) => Math.Clamp(p, 0d, 100d),
            _        => 0d,
        };
    }

    private IEnumerable<object> GetDependencies(TItem item)
    {
        if (GetField(item, DependenciesField) is not System.Collections.IEnumerable list) yield break;
        foreach (var entry in list)
        {
            if (entry is null) continue;
            yield return entry;
        }
    }

    // ── Timeline math ─────────────────────────────────────────────────

    private IReadOnlyList<TItem> FlattenHierarchy(IEnumerable<TItem> source)
    {
        if (source is null) return [];
        var all = source as IReadOnlyList<TItem> ?? source.ToList();
        if (all.Count == 0) return Array.Empty<TItem>();

        // Group by parent id (null / missing → root bucket).
        var byParent = new Dictionary<string, List<TItem>>();
        foreach (var item in all)
        {
            var parentKey = GetParentId(item)?.ToString() ?? "";
            if (!byParent.TryGetValue(parentKey, out var bucket))
            {
                bucket = new List<TItem>();
                byParent[parentKey] = bucket;
            }
            bucket.Add(item);
        }

        var result = new List<TItem>(all.Count);
        void Walk(string parent)
        {
            if (!byParent.TryGetValue(parent, out var children)) return;
            foreach (var child in children)
            {
                result.Add(child);
                var idStr = GetId(child)?.ToString() ?? "";
                Walk(idStr);
            }
        }
        Walk("");

        // Fall back to source order if parent chain was degenerate.
        return result.Count == all.Count ? result : all;
    }

    private DateTime ComputeDefaultTimelineStart()
    {
        DateTime? min = null;
        foreach (var item in _flattened.Count == 0 ? Data : _flattened)
        {
            var s = GetStart(item).Date;
            if (min == null || s < min) min = s;
        }
        return (min ?? DateTime.Today).Date;
    }

    private DateTime ComputeDefaultTimelineEnd()
    {
        DateTime? max = null;
        foreach (var item in _flattened.Count == 0 ? Data : _flattened)
        {
            var e = GetEnd(item).Date;
            if (max == null || e > max) max = e;
        }
        return (max ?? ComputeDefaultTimelineStart().AddDays(30)).Date;
    }

    private (double left, double width) ComputeBarGeometry(TItem item)
    {
        var start = GetStart(item).Date;
        var end   = GetEnd(item).Date;
        var origin = EffectiveStart.Date;

        var leftDays  = (start - origin).TotalDays;
        var spanDays  = Math.Max(1, (end - start).TotalDays + 1);

        return (leftDays * ColumnWidth, spanDays * ColumnWidth);
    }

    // ── Header cells ──────────────────────────────────────────────────

    private IReadOnlyList<(DateTime Day, string Label, bool IsMonthBoundary)> BuildHeaderCells()
    {
        var cells = new List<(DateTime, string, bool)>(TotalDays);
        for (int i = 0; i < TotalDays; i++)
        {
            var d = EffectiveStart.Date.AddDays(i);
            var isBoundary = d.Day == 1 || i == 0;
            var label = View switch
            {
                GanttView.Day   => d.ToString("MMM d", CultureInfo.CurrentCulture),
                GanttView.Week  => d.ToString("ddd d", CultureInfo.CurrentCulture),
                GanttView.Month => d.Day.ToString(CultureInfo.CurrentCulture),
                GanttView.Year  => d.Day == 1 ? d.ToString("MMM yyyy", CultureInfo.CurrentCulture) : string.Empty,
                _ => d.ToString("d", CultureInfo.CurrentCulture),
            };
            cells.Add((d, label, isBoundary));
        }
        return cells;
    }

    // ── Event helpers ─────────────────────────────────────────────────

    private async Task InvokeTaskClick(TItem item)
    {
        if (!OnTaskClick.HasDelegate) return;
        await OnTaskClick.InvokeAsync(new GanttTaskEventArgs<TItem> { Item = item });
    }

    /// <summary>
    /// Public helper intended for consumer-built forms: invokes <see cref="OnTaskUpdate"/>
    /// and returns <c>true</c> when the handler did not cancel. MVP surface only — the
    /// component itself does not mutate <see cref="Data"/>.
    /// </summary>
    public async Task<bool> RequestUpdate(TItem item)
    {
        if (!OnTaskUpdate.HasDelegate) return true;
        var args = new GanttTaskEventArgs<TItem> { Item = item };
        await OnTaskUpdate.InvokeAsync(args);
        return !args.IsCancelled;
    }

    /// <summary>
    /// Public helper: invokes <see cref="OnTaskCreate"/> for a freshly-built <typeparamref name="TItem"/>.
    /// Returns <c>true</c> when the handler did not cancel.
    /// </summary>
    public async Task<bool> RequestCreate(TItem item)
    {
        if (!OnTaskCreate.HasDelegate) return true;
        var args = new GanttTaskEventArgs<TItem> { Item = item, IsNew = true };
        await OnTaskCreate.InvokeAsync(args);
        return !args.IsCancelled;
    }

    /// <summary>
    /// Public helper: invokes <see cref="OnTaskDelete"/>. Returns <c>true</c> when the
    /// handler did not cancel.
    /// </summary>
    public async Task<bool> RequestDelete(TItem item)
    {
        if (!OnTaskDelete.HasDelegate) return true;
        var args = new GanttTaskEventArgs<TItem> { Item = item };
        await OnTaskDelete.InvokeAsync(args);
        return !args.IsCancelled;
    }
}
