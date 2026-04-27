using System.Globalization;
using Microsoft.AspNetCore.Components;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Scheduling;

/// <summary>
/// Canonical MVP scheduler for the Sunfish Scheduling component family.
/// Renders a calendar with pluggable <c>Day</c>, <c>Week</c>, <c>WorkWeek</c>, <c>Month</c>,
/// and <c>Agenda</c> views and places user-supplied events as absolutely-positioned blocks
/// inside their date/time cells.
/// </summary>
/// <typeparam name="TEvent">
/// The event item type. Values are read through string-accessor fields
/// (<see cref="IdField"/>, <see cref="TitleField"/>, <see cref="StartField"/>, etc.)
/// so consumers bring their own domain shapes without adapters.
/// </typeparam>
/// <remarks>
/// <para>
/// This is the <strong>MVP surface</strong> mapped under
/// <c>apps/docs/component-specs/component-mapping.json → scheduler</c> and lives in the
/// <c>Sunfish.UIAdapters.Blazor.Components.Scheduling</c> namespace. A non-generic
/// <c>Sunfish.UIAdapters.Blazor.Components.DataDisplay.SunfishScheduler</c> predates this
/// file and remains in place for the DataDisplay demo set; the two components share a name
/// but live in different namespaces by design.
/// </para>
/// <para>
/// Drag-to-move, drag-to-resize, recurring-event expansion, and edit popups are
/// deliberately <em>deferred</em> from this MVP (hence the <c>partial</c> mapping status).
/// Click, create, update, delete, and view-change callbacks are wired and cancellable.
/// </para>
/// </remarks>
public partial class SunfishScheduler<TEvent> : SunfishComponentBase
    where TEvent : class
{
    // ── Data & field accessors ────────────────────────────────────────

    /// <summary>Collection of events to render.</summary>
    [Parameter, EditorRequired] public IEnumerable<TEvent> Data { get; set; } = [];

    /// <summary>Name of the property that yields the event id.</summary>
    [Parameter, EditorRequired] public string IdField { get; set; } = "Id";

    /// <summary>Name of the property that yields the event title.</summary>
    [Parameter, EditorRequired] public string TitleField { get; set; } = "Title";

    /// <summary>Name of the property that yields the event start <see cref="DateTime"/>.</summary>
    [Parameter, EditorRequired] public string StartField { get; set; } = "Start";

    /// <summary>Name of the property that yields the event end <see cref="DateTime"/>.</summary>
    [Parameter, EditorRequired] public string EndField { get; set; } = "End";

    /// <summary>
    /// Name of the property that yields a boolean indicating the event spans a whole day.
    /// Optional — when the accessor returns <c>null</c> or a non-boolean the event is
    /// treated as timed.
    /// </summary>
    [Parameter] public string AllDayField { get; set; } = "AllDay";

    /// <summary>
    /// Name of the property that yields the resource id the event belongs to (optional).
    /// When <see cref="Resources"/> is set, Day view columns are grouped by matching id.
    /// </summary>
    [Parameter] public string ResourceField { get; set; } = "ResourceId";

    /// <summary>
    /// Name of the property that yields a CSS color string for the event (optional).
    /// Falls back to the matching resource's <see cref="SchedulerResource.Color"/> and
    /// finally to the theme default.
    /// </summary>
    [Parameter] public string ColorField { get; set; } = "Color";

    // ── View / navigation ─────────────────────────────────────────────

    /// <summary>The active calendar view.</summary>
    [Parameter] public SchedulerView View { get; set; } = SchedulerView.Week;

    /// <summary>Two-way bound callback for <see cref="View"/> changes.</summary>
    [Parameter] public EventCallback<SchedulerView> ViewChanged { get; set; }

    /// <summary>The date the active view is focused on.</summary>
    [Parameter] public DateTime CurrentDate { get; set; } = DateTime.Today;

    /// <summary>Two-way bound callback for <see cref="CurrentDate"/> changes.</summary>
    [Parameter] public EventCallback<DateTime> CurrentDateChanged { get; set; }

    /// <summary>Optional list of resource columns (Day view).</summary>
    [Parameter] public List<SchedulerResource>? Resources { get; set; }

    /// <summary>First hour shown on time-grid views (Day / Week / WorkWeek). Default <c>8</c>.</summary>
    [Parameter] public int StartHour { get; set; } = 8;

    /// <summary>End hour shown on time-grid views (exclusive). Default <c>18</c>.</summary>
    [Parameter] public int EndHour { get; set; } = 18;

    /// <summary>Row height in pixels for each hour slot. Default <c>48</c>.</summary>
    [Parameter] public int HourSlotHeight { get; set; } = 48;

    /// <summary>Pixel width of the left-hand time-of-day gutter column. Default <c>64</c>.</summary>
    [Parameter] public int TimeGutterWidth { get; set; } = 64;

    /// <summary>Optional explicit inline <c>width:</c>.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Optional explicit inline <c>height:</c>.</summary>
    [Parameter] public string? Height { get; set; }

    // ── Event callbacks ───────────────────────────────────────────────

    /// <summary>Fires when the user clicks an event block.</summary>
    [Parameter] public EventCallback<SchedulerEventArgs<TEvent>> OnEventClick { get; set; }

    /// <summary>Fires before a new event is inserted. Set <c>IsCancelled</c> to veto.</summary>
    [Parameter] public EventCallback<SchedulerEventArgs<TEvent>> OnEventCreate { get; set; }

    /// <summary>Fires before an existing event is updated. Set <c>IsCancelled</c> to veto.</summary>
    [Parameter] public EventCallback<SchedulerEventArgs<TEvent>> OnEventUpdate { get; set; }

    /// <summary>Fires before an event is deleted. Set <c>IsCancelled</c> to veto.</summary>
    [Parameter] public EventCallback<SchedulerEventArgs<TEvent>> OnEventDelete { get; set; }

    /// <summary>Fires before the active view changes. Set <c>IsCancelled</c> to veto.</summary>
    [Parameter] public EventCallback<SchedulerViewChangeEventArgs> OnViewChange { get; set; }

    // ── Internal helpers ──────────────────────────────────────────────

    private readonly Dictionary<Type, Dictionary<string, Func<object, object?>>> _accessorCache = new();

    private Func<object, object?>? GetAccessor(string fieldName)
    {
        if (string.IsNullOrEmpty(fieldName)) return null;
        var type = typeof(TEvent);
        if (!_accessorCache.TryGetValue(type, out var cache))
        {
            cache = new Dictionary<string, Func<object, object?>>();
            _accessorCache[type] = cache;
        }
        if (cache.TryGetValue(fieldName, out var cached)) return cached;

        var prop = type.GetProperty(fieldName);
        Func<object, object?> getter = prop is null ? _ => null : inst => prop.GetValue(inst);
        cache[fieldName] = getter;
        return getter;
    }

    private object? GetField(TEvent item, string field) => GetAccessor(field)?.Invoke(item);

    private string GetTitle(TEvent item) => GetField(item, TitleField)?.ToString() ?? string.Empty;

    private object? GetId(TEvent item) => GetField(item, IdField);

    private DateTime GetStart(TEvent item) => ParseDate(GetField(item, StartField)) ?? DateTime.Today;

    private DateTime GetEnd(TEvent item) =>
        ParseDate(GetField(item, EndField)) ?? GetStart(item).AddHours(1);

    private bool GetAllDay(TEvent item) =>
        GetField(item, AllDayField) is bool b && b;

    private object? GetResourceId(TEvent item) => GetField(item, ResourceField);

    private string? GetColor(TEvent item)
    {
        var raw = GetField(item, ColorField)?.ToString();
        if (!string.IsNullOrWhiteSpace(raw)) return raw;

        var resId = GetResourceId(item);
        if (resId is null || Resources is null) return null;
        var match = Resources.FirstOrDefault(r => Equals(r.Id, resId) || string.Equals(r.Id?.ToString(), resId?.ToString(), StringComparison.Ordinal));
        return match?.Color;
    }

    private static DateTime? ParseDate(object? raw) => raw switch
    {
        null              => null,
        DateTime dt       => dt,
        DateTimeOffset o  => o.DateTime,
        string s when DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var p) => p,
        _                 => null,
    };

    // ── View math ─────────────────────────────────────────────────────

    private DateTime AnchorForView()
    {
        return View switch
        {
            SchedulerView.Day      => CurrentDate.Date,
            SchedulerView.Week     => StartOfWeek(CurrentDate.Date, DayOfWeek.Sunday),
            SchedulerView.WorkWeek => StartOfWeek(CurrentDate.Date, DayOfWeek.Monday),
            SchedulerView.Month    => new DateTime(CurrentDate.Year, CurrentDate.Month, 1),
            SchedulerView.Agenda   => CurrentDate.Date,
            _                      => CurrentDate.Date,
        };
    }

    private IReadOnlyList<DateTime> GetVisibleDays()
    {
        return View switch
        {
            SchedulerView.Day      => [CurrentDate.Date],
            SchedulerView.Week     => Enumerable.Range(0, 7).Select(i => StartOfWeek(CurrentDate.Date, DayOfWeek.Sunday).AddDays(i)).ToList(),
            SchedulerView.WorkWeek => Enumerable.Range(0, 5).Select(i => StartOfWeek(CurrentDate.Date, DayOfWeek.Monday).AddDays(i)).ToList(),
            SchedulerView.Month    => BuildMonthGrid(),
            SchedulerView.Agenda   => Enumerable.Range(0, 14).Select(i => CurrentDate.Date.AddDays(i)).ToList(),
            _                      => [CurrentDate.Date],
        };
    }

    private List<DateTime> BuildMonthGrid()
    {
        var first  = new DateTime(CurrentDate.Year, CurrentDate.Month, 1);
        var start  = StartOfWeek(first, DayOfWeek.Sunday);
        var result = new List<DateTime>(42);
        for (int i = 0; i < 42; i++) result.Add(start.AddDays(i));
        return result;
    }

    private static DateTime StartOfWeek(DateTime d, DayOfWeek first)
    {
        int diff = ((int)d.DayOfWeek - (int)first + 7) % 7;
        return d.Date.AddDays(-diff);
    }

    private IReadOnlyList<TEvent> GetEventsOnDay(DateTime day)
    {
        var matches = new List<TEvent>();
        foreach (var evt in Data)
        {
            var s = GetStart(evt).Date;
            var e = GetEnd(evt).Date;
            if (day.Date >= s && day.Date <= e) matches.Add(evt);
        }
        return matches;
    }

    private IReadOnlyList<TEvent> GetAgendaEvents()
    {
        var list = new List<TEvent>();
        foreach (var evt in Data)
        {
            var e = GetEnd(evt);
            if (e.Date >= CurrentDate.Date) list.Add(evt);
        }
        list.Sort((a, b) => GetStart(a).CompareTo(GetStart(b)));
        return list;
    }

    private (double top, double height) ComputeTimedGeometry(TEvent evt)
    {
        var start = GetStart(evt);
        var end   = GetEnd(evt);

        var startFractionalHour = Math.Max(StartHour, Math.Min(EndHour, start.Hour + start.Minute / 60.0));
        var endFractionalHour   = Math.Max(StartHour, Math.Min(EndHour, end.Hour   + end.Minute   / 60.0));

        if (endFractionalHour < startFractionalHour) endFractionalHour = startFractionalHour + 0.5;

        var top    = (startFractionalHour - StartHour) * HourSlotHeight;
        var height = Math.Max(18, (endFractionalHour - startFractionalHour) * HourSlotHeight);
        return (top, height);
    }

    // ── Navigation ────────────────────────────────────────────────────

    private Task NavigatePrevious() => ShiftCurrentDate(-1);
    private Task NavigateNext()     => ShiftCurrentDate(+1);

    private async Task ShiftCurrentDate(int direction)
    {
        var next = View switch
        {
            SchedulerView.Day      => CurrentDate.AddDays(direction),
            SchedulerView.Week     => CurrentDate.AddDays(direction * 7),
            SchedulerView.WorkWeek => CurrentDate.AddDays(direction * 7),
            SchedulerView.Month    => CurrentDate.AddMonths(direction),
            SchedulerView.Agenda   => CurrentDate.AddDays(direction * 14),
            _                      => CurrentDate.AddDays(direction),
        };
        CurrentDate = next;
        await CurrentDateChanged.InvokeAsync(next);
        Announce($"Showing {GetHeaderTitle()}.");
    }

    private async Task SwitchView(SchedulerView next)
    {
        if (next == View) return;
        if (OnViewChange.HasDelegate)
        {
            var args = new SchedulerViewChangeEventArgs { FromView = View, ToView = next };
            await OnViewChange.InvokeAsync(args);
            if (args.IsCancelled) return;
        }
        View = next;
        await ViewChanged.InvokeAsync(next);
        Announce($"View changed to {next}.");
    }

    private string GetHeaderTitle()
    {
        return View switch
        {
            SchedulerView.Day      => CurrentDate.ToString("dddd, MMM d, yyyy", CultureInfo.CurrentCulture),
            SchedulerView.Week     => $"Week of {StartOfWeek(CurrentDate.Date, DayOfWeek.Sunday):MMM d, yyyy}",
            SchedulerView.WorkWeek => $"Work Week of {StartOfWeek(CurrentDate.Date, DayOfWeek.Monday):MMM d, yyyy}",
            SchedulerView.Month    => CurrentDate.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            SchedulerView.Agenda   => $"Upcoming from {CurrentDate:MMM d, yyyy}",
            _                      => CurrentDate.ToString("d", CultureInfo.CurrentCulture),
        };
    }

    // ── Callback helpers ──────────────────────────────────────────────

    private async Task InvokeEventClick(TEvent item)
    {
        if (!OnEventClick.HasDelegate) return;
        await OnEventClick.InvokeAsync(new SchedulerEventArgs<TEvent> { Item = item });
    }

    /// <summary>
    /// Public helper: invokes <see cref="OnEventCreate"/>. Returns <c>true</c> when the handler did not cancel.
    /// </summary>
    public async Task<bool> RequestCreate(TEvent item)
    {
        if (!OnEventCreate.HasDelegate) return true;
        var args = new SchedulerEventArgs<TEvent> { Item = item, IsNew = true };
        await OnEventCreate.InvokeAsync(args);
        return !args.IsCancelled;
    }

    /// <summary>
    /// Public helper: invokes <see cref="OnEventUpdate"/>. Returns <c>true</c> when the handler did not cancel.
    /// </summary>
    public async Task<bool> RequestUpdate(TEvent item)
    {
        if (!OnEventUpdate.HasDelegate) return true;
        var args = new SchedulerEventArgs<TEvent> { Item = item };
        await OnEventUpdate.InvokeAsync(args);
        return !args.IsCancelled;
    }

    /// <summary>
    /// Public helper: invokes <see cref="OnEventDelete"/>. Returns <c>true</c> when the handler did not cancel.
    /// </summary>
    public async Task<bool> RequestDelete(TEvent item)
    {
        if (!OnEventDelete.HasDelegate) return true;
        var args = new SchedulerEventArgs<TEvent> { Item = item };
        await OnEventDelete.InvokeAsync(args);
        return !args.IsCancelled;
    }
}
