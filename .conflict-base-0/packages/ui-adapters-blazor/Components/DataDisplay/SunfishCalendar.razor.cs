using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Sunfish.Foundation.Enums;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Standalone inline calendar supporting single, multiple, and range selection across
/// Month / Year / Decade / Century views, with optional header and per-view cell templates.
/// Mirrors the Sunfish calendar spec for parity with the Telerik Blazor surface.
/// </summary>
public partial class SunfishCalendar : SunfishComponentBase
{
    // ── Parameters — selection mode ────────────────────────────────────

    /// <summary>How the user may select dates (Single, Multiple, Range). Default <see cref="CalendarSelectionMode.Single"/>.</summary>
    [Parameter] public CalendarSelectionMode SelectionMode { get; set; } = CalendarSelectionMode.Single;

    // ── Parameters — single selection ──────────────────────────────────

    /// <summary>The currently selected date in <see cref="CalendarSelectionMode.Single"/> mode. Supports two-way binding.</summary>
    [Parameter] public DateTime? Value { get; set; }

    /// <summary>Callback fired when <see cref="Value"/> changes.</summary>
    [Parameter] public EventCallback<DateTime?> ValueChanged { get; set; }

    // ── Parameters — multiple selection ────────────────────────────────

    /// <summary>The collection of selected dates in <see cref="CalendarSelectionMode.Multiple"/> mode.</summary>
    [Parameter] public IList<DateTime> SelectedDates { get; set; } = new List<DateTime>();

    /// <summary>Callback fired when <see cref="SelectedDates"/> changes.</summary>
    [Parameter] public EventCallback<IList<DateTime>> SelectedDatesChanged { get; set; }

    // ── Parameters — range selection ───────────────────────────────────

    /// <summary>The start of the selected range in <see cref="CalendarSelectionMode.Range"/> mode. Supports two-way binding.</summary>
    [Parameter] public DateTime? Start { get; set; }

    /// <summary>Callback fired when <see cref="Start"/> changes.</summary>
    [Parameter] public EventCallback<DateTime?> StartChanged { get; set; }

    /// <summary>The end of the selected range in <see cref="CalendarSelectionMode.Range"/> mode. Supports two-way binding.</summary>
    [Parameter] public DateTime? End { get; set; }

    /// <summary>Callback fired when <see cref="End"/> changes.</summary>
    [Parameter] public EventCallback<DateTime?> EndChanged { get; set; }

    // ── Parameters — bounds & disabling ────────────────────────────────

    /// <summary>The minimum selectable date.</summary>
    [Parameter] public DateTime Min { get; set; } = DateTime.MinValue;

    /// <summary>The maximum selectable date.</summary>
    [Parameter] public DateTime Max { get; set; } = DateTime.MaxValue;

    /// <summary>
    /// Returns <c>true</c> for dates that should not be selectable. Applied in addition
    /// to <see cref="Min"/> and <see cref="Max"/>.
    /// </summary>
    [Parameter] public Func<DateTime, bool>? DisabledDates { get; set; }

    // ── Parameters — views ─────────────────────────────────────────────

    /// <summary>The current calendar view level. Supports two-way binding.</summary>
    [Parameter] public CalendarView View { get; set; } = CalendarView.Month;

    /// <summary>Callback fired when <see cref="View"/> changes.</summary>
    [Parameter] public EventCallback<CalendarView> ViewChanged { get; set; }

    /// <summary>The most aggregated view the user can navigate to. Default <see cref="CalendarView.Century"/>.</summary>
    [Parameter] public CalendarView TopView { get; set; } = CalendarView.Century;

    /// <summary>The most detailed view the user can navigate to. Default <see cref="CalendarView.Month"/>.</summary>
    [Parameter] public CalendarView BottomView { get; set; } = CalendarView.Month;

    // ── Parameters — visual options ────────────────────────────────────

    /// <summary>Whether to show ISO week numbers (spec-aligned name).</summary>
    [Parameter] public bool WeekNumber { get; set; }

    /// <summary>Whether to show ISO week numbers (legacy alias retained for back-compat).</summary>
    [Parameter] public bool ShowWeekNumbers { get; set; }

    private bool DisplayWeekNumbers => WeekNumber || ShowWeekNumbers;

    /// <summary>Whether to show days from adjacent months. Default <c>true</c>.</summary>
    [Parameter] public bool ShowOtherMonthDays { get; set; } = true;

    /// <summary>The first day of the week in the month view. Defaults to the current culture's first day.</summary>
    [Parameter] public DayOfWeek FirstDayOfWeek { get; set; } =
        System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.FirstDayOfWeek;

    /// <summary>The calendar orientation (affects multi-view rendering).</summary>
    [Parameter] public CalendarOrientation Orientation { get; set; } = CalendarOrientation.Horizontal;

    // ── Parameters — templates ─────────────────────────────────────────

    /// <summary>Optional custom template rendered inside the calendar header for all views.</summary>
    [Parameter] public RenderFragment? HeaderTemplate { get; set; }

    /// <summary>Optional custom template for an individual day / month / year / decade cell.</summary>
    [Parameter] public RenderFragment<DateTime>? CellTemplate { get; set; }

    /// <summary>Optional custom template for each week-number cell in the month view.</summary>
    [Parameter] public RenderFragment<int>? WeekCellTemplate { get; set; }

    // ── Parameters — events ────────────────────────────────────────────

    /// <summary>Fires after any selection change (Single/Multiple/Range).</summary>
    [Parameter] public EventCallback OnChange { get; set; }

    /// <summary>Fires when the user switches between views (Month/Year/Decade/Century).</summary>
    [Parameter] public EventCallback<CalendarView> OnViewChange { get; set; }

    /// <summary>Fires when the user navigates forward / backward within a view (page, year, decade).</summary>
    [Parameter] public EventCallback<DateTime> OnNavigate { get; set; }

    /// <summary>Fires when a day cell is clicked (legacy; prefer <see cref="OnChange"/>).</summary>
    [Parameter] public EventCallback<DateTime> OnDateClick { get; set; }

    // ── Fields ──────────────────────────────────────────────────────────

    private DateTime _displayDate = DateTime.Today;
    private DateTime _focusedDate = DateTime.Today;

    // ── Lifecycle ───────────────────────────────────────────────────────

    protected override void OnParametersSet()
    {
        if (Value.HasValue
            && _displayDate.Year == DateTime.Today.Year
            && _displayDate.Month == DateTime.Today.Month)
        {
            _displayDate = new DateTime(Value.Value.Year, Value.Value.Month, 1);
            _focusedDate = Value.Value.Date;
        }
    }

    private string OrientationClass => Orientation == CalendarOrientation.Vertical
        ? "sf-calendar--vertical"
        : "sf-calendar--horizontal";

    // ── Helpers ─────────────────────────────────────────────────────────

    private bool IsDateDisabled(DateTime date)
    {
        if (date.Date < Min.Date) return true;
        if (date.Date > Max.Date) return true;
        return DisabledDates?.Invoke(date.Date) == true;
    }

    private bool IsSelected(DateTime date) => SelectionMode switch
    {
        CalendarSelectionMode.Single   => Value.HasValue && Value.Value.Date == date.Date,
        CalendarSelectionMode.Multiple => SelectedDates.Any(d => d.Date == date.Date),
        CalendarSelectionMode.Range    => Start.HasValue && End.HasValue && date.Date >= Start.Value.Date && date.Date <= End.Value.Date,
        _                              => false
    };

    private bool IsRangeEdge(DateTime date) => SelectionMode == CalendarSelectionMode.Range
        && ((Start.HasValue && Start.Value.Date == date.Date) || (End.HasValue && End.Value.Date == date.Date));

    private string[] GetDayHeaders()
    {
        var dfi = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat;
        var names = dfi.AbbreviatedDayNames;
        var result = new string[7];
        for (int i = 0; i < 7; i++)
        {
            result[i] = names[((int)FirstDayOfWeek + i) % 7];
        }
        return result;
    }

    private List<DateTime[]> GetWeeks()
    {
        var weeks = new List<DateTime[]>();
        var firstOfMonth = new DateTime(_displayDate.Year, _displayDate.Month, 1);
        var diff = ((int)firstOfMonth.DayOfWeek - (int)FirstDayOfWeek + 7) % 7;
        var startDate = firstOfMonth.AddDays(-diff);

        for (int w = 0; w < 6; w++)
        {
            var week = new DateTime[7];
            for (int d = 0; d < 7; d++)
            {
                week[d] = startDate.AddDays(w * 7 + d);
            }
            if (w == 5 && week[0].Month != _displayDate.Month) break;
            weeks.Add(week);
        }
        return weeks;
    }

    private static int GetWeekNumber(DateTime date) =>
        System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
            date,
            System.Globalization.CalendarWeekRule.FirstFourDayWeek,
            DayOfWeek.Monday);

    // ── Event handlers ──────────────────────────────────────────────────

    private async Task OnCellClick(DateTime date, bool disabled)
    {
        if (disabled) return;
        await SelectDate(date);
    }

    private async Task SelectDate(DateTime date)
    {
        _focusedDate = date.Date;

        switch (SelectionMode)
        {
            case CalendarSelectionMode.Single:
                Value = date;
                await ValueChanged.InvokeAsync(date);
                break;

            case CalendarSelectionMode.Multiple:
                var next = new List<DateTime>(SelectedDates);
                var existing = next.FirstOrDefault(d => d.Date == date.Date);
                if (existing != default) next.Remove(existing);
                else next.Add(date.Date);
                SelectedDates = next;
                await SelectedDatesChanged.InvokeAsync(next);
                break;

            case CalendarSelectionMode.Range:
                if (!Start.HasValue || (Start.HasValue && End.HasValue))
                {
                    Start = date.Date;
                    End = null;
                    await StartChanged.InvokeAsync(Start);
                    await EndChanged.InvokeAsync(End);
                }
                else if (date.Date < Start.Value.Date)
                {
                    End = Start;
                    Start = date.Date;
                    await StartChanged.InvokeAsync(Start);
                    await EndChanged.InvokeAsync(End);
                }
                else
                {
                    End = date.Date;
                    await EndChanged.InvokeAsync(End);
                }
                break;
        }

        await OnDateClick.InvokeAsync(date);
        await OnChange.InvokeAsync();
    }

    private async Task NavigateToDisplayDate(DateTime date)
    {
        _displayDate = date;
        await OnNavigate.InvokeAsync(date);
    }

    private async Task SwitchView(CalendarView next)
    {
        if (next == View) return;
        if (next < BottomView) next = BottomView;
        if (next > TopView) next = TopView;
        View = next;
        await ViewChanged.InvokeAsync(next);
        await OnViewChange.InvokeAsync(next);
    }

    private Task PreviousMonth()   => NavigateToDisplayDate(_displayDate.AddMonths(-1));
    private Task NextMonth()       => NavigateToDisplayDate(_displayDate.AddMonths(1));
    private Task PreviousYear()    => NavigateToDisplayDate(_displayDate.AddYears(-1));
    private Task NextYear()        => NavigateToDisplayDate(_displayDate.AddYears(1));
    private Task PreviousDecade()  => NavigateToDisplayDate(_displayDate.AddYears(-10));
    private Task NextDecade()      => NavigateToDisplayDate(_displayDate.AddYears(10));
    private Task PreviousCentury() => NavigateToDisplayDate(_displayDate.AddYears(-100));
    private Task NextCentury()     => NavigateToDisplayDate(_displayDate.AddYears(100));

    private Task SwitchToYearView()    => SwitchView(CalendarView.Year);
    private Task SwitchToDecadeView()  => SwitchView(CalendarView.Decade);
    private Task SwitchToCenturyView() => SwitchView(CalendarView.Century);

    private async Task SelectMonth(int month)
    {
        _displayDate = new DateTime(_displayDate.Year, month, 1);
        if (View != BottomView) await SwitchView(View - 1);
    }

    private async Task SelectYear(int year)
    {
        _displayDate = new DateTime(year, _displayDate.Month, 1);
        if (View != BottomView) await SwitchView(View - 1);
    }

    private async Task SelectDecade(int year)
    {
        _displayDate = new DateTime(year, 1, 1);
        if (View != BottomView) await SwitchView(View - 1);
    }

    // ── Keyboard navigation ────────────────────────────────────────────

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (View != CalendarView.Month) return;

        var focused = _focusedDate;
        var ctrl    = e.CtrlKey;
        DateTime? target = null;

        switch (e.Key)
        {
            case "ArrowLeft":  target = focused.AddDays(-1); break;
            case "ArrowRight": target = focused.AddDays(1); break;
            case "ArrowUp":    target = focused.AddDays(-7); break;
            case "ArrowDown":  target = focused.AddDays(7); break;

            case "PageUp":
                target = ctrl ? focused.AddYears(-1) : focused.AddMonths(-1);
                break;
            case "PageDown":
                target = ctrl ? focused.AddYears(1) : focused.AddMonths(1);
                break;

            case "Home":
                target = new DateTime(focused.Year, focused.Month, 1);
                break;
            case "End":
                target = new DateTime(focused.Year, focused.Month,
                    DateTime.DaysInMonth(focused.Year, focused.Month));
                break;

            case "Enter":
            case " ":
                if (!IsDateDisabled(focused)) await SelectDate(focused);
                return;
        }

        if (target.HasValue)
        {
            _focusedDate = target.Value.Date;
            if (_focusedDate.Year != _displayDate.Year || _focusedDate.Month != _displayDate.Month)
            {
                _displayDate = new DateTime(_focusedDate.Year, _focusedDate.Month, 1);
                await OnNavigate.InvokeAsync(_displayDate);
            }
            StateHasChanged();
        }
    }

    // ── Public API ──────────────────────────────────────────────────────

    /// <summary>Navigates to the specified date and (optionally) switches the view.</summary>
    public Task NavigateTo(DateTime date, CalendarView view)
    {
        _displayDate = new DateTime(date.Year, date.Month, 1);
        _focusedDate = date.Date;
        return SwitchView(view);
    }

    /// <summary>Forces a re-render.</summary>
    public void Refresh() => StateHasChanged();
}
