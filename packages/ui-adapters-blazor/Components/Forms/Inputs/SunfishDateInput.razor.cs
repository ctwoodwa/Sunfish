using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Sunfish.UIAdapters.Blazor.Base;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// Segmented, keyboard-first date input. Distinct from <see cref="SunfishDatePicker"/>
/// (no popup calendar). Users edit month/day/year as individually focused segments.
/// </summary>
public partial class SunfishDateInput : SunfishComponentBase
{
    /// <summary>Current value. Supports two-way binding.</summary>
    [Parameter] public DateTime? Value { get; set; }

    /// <summary>Fires when the effective value changes (per-keystroke when valid).</summary>
    [Parameter] public EventCallback<DateTime?> ValueChanged { get; set; }

    /// <summary>Fires on blur or when the user confirms the value.</summary>
    [Parameter] public EventCallback<DateTime?> OnChange { get; set; }

    /// <summary>Fires when the component loses focus.</summary>
    [Parameter] public EventCallback OnBlur { get; set; }

    /// <summary>Display/edit format. Defaults to "MM/dd/yyyy".</summary>
    [Parameter] public string Format { get; set; } = "MM/dd/yyyy";

    /// <summary>Earliest allowed date.</summary>
    [Parameter] public DateTime? Min { get; set; }

    /// <summary>Latest allowed date.</summary>
    [Parameter] public DateTime? Max { get; set; }

    /// <summary>When false, user input is rejected.</summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>Alias: when true, the input is non-interactive.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>When true, the input is focusable but cannot be edited.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>Placeholder text rendered when no value is set and the input is unfocused.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>Explicit width (e.g. "200px").</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Tab order for the segments wrapper.</summary>
    [Parameter] public int TabIndex { get; set; }

    /// <summary>aria-label applied to the segment group.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>Shows a small clear (x) button when a value is present.</summary>
    [Parameter] public bool ShowClearButton { get; set; }

    // --- internal state ---
    private readonly List<Segment> _segments = new();
    private int _focusedIndex = -1;
    private bool _hasFocus;
    private bool _preventDefault;
    private DateTime? _value;
    private bool _digitReplaceNext;
    private string _lastFormat = string.Empty;
    private DateTime? _lastInboundValue;

    /// <summary>Effective disabled state (Disabled OR !Enabled).</summary>
    private bool IsDisabled => Disabled || !Enabled;

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        var formatChanged = _lastFormat != Format;
        if (formatChanged)
        {
            BuildSegments();
            _lastFormat = Format;
        }

        // Sync inbound Value -> internal state only when it actually changes,
        // so that user typing isn't clobbered by the parent re-rendering.
        if (!Equals(_lastInboundValue, Value))
        {
            _value = Value;
            _lastInboundValue = Value;
            LoadValueIntoSegments(_value);
        }
    }

    private void BuildSegments()
    {
        _segments.Clear();
        _focusedIndex = -1;

        // Tokenize the format string into contiguous date parts (M/MM/d/dd/y/yy/yyyy/H/HH/m/mm/s/ss)
        // and literals. This parser is intentionally narrow — enough for the parity surface.
        var i = 0;
        while (i < Format.Length)
        {
            var c = Format[i];
            if (IsFormatChar(c))
            {
                var start = i;
                while (i < Format.Length && Format[i] == c) i++;
                var token = Format.Substring(start, i - start);
                _segments.Add(CreateSegmentForToken(token));
            }
            else
            {
                _segments.Add(Segment.Literal(c.ToString()));
                i++;
            }
        }

        // Focus the first editable segment by default
        for (var j = 0; j < _segments.Count; j++)
        {
            if (!_segments[j].IsLiteral) { _focusedIndex = j; break; }
        }
    }

    private static bool IsFormatChar(char c) =>
        c is 'M' or 'd' or 'y' or 'H' or 'h' or 'm' or 's';

    private Segment CreateSegmentForToken(string token)
    {
        var c = token[0];
        var width = token.Length;
        return c switch
        {
            'M' => new Segment(SegmentKind.Month, token, width, 1, 12, "Month"),
            'd' => new Segment(SegmentKind.Day, token, width, 1, 31, "Day"),
            'y' => new Segment(SegmentKind.Year, token, width, width <= 2 ? 0 : 1, width <= 2 ? 99 : 9999, "Year"),
            'H' => new Segment(SegmentKind.Hour24, token, width, 0, 23, "Hour"),
            'h' => new Segment(SegmentKind.Hour12, token, width, 1, 12, "Hour"),
            'm' => new Segment(SegmentKind.Minute, token, width, 0, 59, "Minute"),
            's' => new Segment(SegmentKind.Second, token, width, 0, 59, "Second"),
            _ => Segment.Literal(token),
        };
    }

    private void LoadValueIntoSegments(DateTime? dt)
    {
        foreach (var seg in _segments)
        {
            if (seg.IsLiteral) continue;
            if (dt is null)
            {
                seg.Value = null;
                continue;
            }
            seg.Value = seg.Kind switch
            {
                SegmentKind.Month => dt.Value.Month,
                SegmentKind.Day => dt.Value.Day,
                SegmentKind.Year => seg.Width <= 2 ? dt.Value.Year % 100 : dt.Value.Year,
                SegmentKind.Hour24 => dt.Value.Hour,
                SegmentKind.Hour12 => ((dt.Value.Hour + 11) % 12) + 1,
                SegmentKind.Minute => dt.Value.Minute,
                SegmentKind.Second => dt.Value.Second,
                _ => null,
            };
        }
    }

    private void FocusSegment(int index)
    {
        if (IsDisabled) return;
        if (index < 0 || index >= _segments.Count) return;
        if (_segments[index].IsLiteral) return;
        _focusedIndex = index;
        _digitReplaceNext = true;
    }

    private void HandleFocusIn(FocusEventArgs _)
    {
        _hasFocus = true;
        if (_focusedIndex < 0)
        {
            for (var i = 0; i < _segments.Count; i++)
            {
                if (!_segments[i].IsLiteral) { _focusedIndex = i; break; }
            }
        }
        _digitReplaceNext = true;
    }

    private async Task HandleFocusOut(FocusEventArgs _)
    {
        _hasFocus = false;
        // Commit any staged value on blur (and fire OnChange/OnBlur per spec)
        CommitFromSegments();
        await OnChange.InvokeAsync(_value);
        await OnBlur.InvokeAsync();
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (IsDisabled) { _preventDefault = false; return; }
        if (ReadOnly)
        {
            // Allow arrow navigation even when read-only, but block edits
            _preventDefault = e.Key is "ArrowLeft" or "ArrowRight";
            if (_preventDefault)
            {
                if (e.Key == "ArrowLeft") MoveFocus(-1);
                else MoveFocus(+1);
            }
            return;
        }

        switch (e.Key)
        {
            case "ArrowLeft":
                _preventDefault = true;
                MoveFocus(-1);
                break;
            case "ArrowRight":
                _preventDefault = true;
                MoveFocus(+1);
                break;
            case "ArrowUp":
                _preventDefault = true;
                await IncrementFocused(+1);
                break;
            case "ArrowDown":
                _preventDefault = true;
                await IncrementFocused(-1);
                break;
            case "Backspace":
            case "Delete":
                _preventDefault = true;
                ClearFocusedSegment();
                await NotifyValueChanged();
                break;
            case "Enter":
                _preventDefault = true;
                CommitFromSegments();
                await OnChange.InvokeAsync(_value);
                break;
            case "Home":
                _preventDefault = true;
                MoveFocusToEdge(first: true);
                break;
            case "End":
                _preventDefault = true;
                MoveFocusToEdge(first: false);
                break;
            case "Tab":
                _preventDefault = false;
                break;
            default:
                if (e.Key.Length == 1 && char.IsDigit(e.Key[0]))
                {
                    _preventDefault = true;
                    await TypeDigit(e.Key[0]);
                }
                else if (e.Key == "/" || e.Key == "-" || e.Key == "." || e.Key == " ")
                {
                    _preventDefault = true;
                    MoveFocus(+1);
                }
                else
                {
                    _preventDefault = false;
                }
                break;
        }
    }

    private void MoveFocus(int delta)
    {
        if (_segments.Count == 0) return;
        var idx = _focusedIndex;
        for (var step = 0; step < _segments.Count; step++)
        {
            idx += delta;
            if (idx < 0 || idx >= _segments.Count) return;
            if (!_segments[idx].IsLiteral)
            {
                _focusedIndex = idx;
                _digitReplaceNext = true;
                return;
            }
        }
    }

    private void MoveFocusToEdge(bool first)
    {
        if (first)
        {
            for (var i = 0; i < _segments.Count; i++)
                if (!_segments[i].IsLiteral) { _focusedIndex = i; _digitReplaceNext = true; return; }
        }
        else
        {
            for (var i = _segments.Count - 1; i >= 0; i--)
                if (!_segments[i].IsLiteral) { _focusedIndex = i; _digitReplaceNext = true; return; }
        }
    }

    private async Task IncrementFocused(int delta)
    {
        if (_focusedIndex < 0) return;
        var seg = _segments[_focusedIndex];
        if (seg.IsLiteral) return;

        var current = seg.Value ?? SegmentDefault(seg);
        var next = current + delta;
        next = Clamp(next, seg.Min, seg.Max);
        seg.Value = next;
        _digitReplaceNext = true;
        await NotifyValueChanged();
    }

    private void ClearFocusedSegment()
    {
        if (_focusedIndex < 0) return;
        var seg = _segments[_focusedIndex];
        if (seg.IsLiteral) return;
        seg.Value = null;
        _digitReplaceNext = true;
    }

    private async Task TypeDigit(char d)
    {
        if (_focusedIndex < 0) return;
        var seg = _segments[_focusedIndex];
        if (seg.IsLiteral) return;

        var digit = d - '0';
        int candidate;

        if (_digitReplaceNext || seg.Value is null)
        {
            candidate = digit;
            _digitReplaceNext = false;
        }
        else
        {
            candidate = (seg.Value!.Value * 10) + digit;
            // If appending overflows the segment max, start fresh with this digit.
            if (candidate > seg.Max) candidate = digit;
        }

        // For month/day: if the leading digit can't start any valid 2-digit value
        // (e.g. "4" for month, "4" for day can still be a single digit), keep it.
        // Clamp only if it exceeds max.
        if (candidate > seg.Max) candidate = seg.Max;
        if (candidate < seg.Min && candidate != 0) candidate = seg.Min;

        seg.Value = candidate;

        // Auto-advance: once the segment can no longer accept more digits
        // (i.e. any further digit would overflow), move to the next segment.
        var willOverflow = (candidate * 10) > seg.Max;
        var reachedWidth = candidate.ToString().Length >= seg.Width && seg.Width <= 4;
        if (willOverflow || reachedWidth)
        {
            MoveFocus(+1);
        }

        await NotifyValueChanged();
    }

    private static int SegmentDefault(Segment seg) => seg.Kind switch
    {
        SegmentKind.Year => DateTime.Now.Year,
        SegmentKind.Month => DateTime.Now.Month,
        SegmentKind.Day => DateTime.Now.Day,
        _ => seg.Min,
    };

    private static int Clamp(int n, int min, int max) =>
        n < min ? max : n > max ? min : n; // wrap on arrow keys

    private async Task ClearValue()
    {
        foreach (var seg in _segments)
        {
            if (!seg.IsLiteral) seg.Value = null;
        }
        _value = null;
        await ValueChanged.InvokeAsync(null);
        await OnChange.InvokeAsync(null);
    }

    private async Task NotifyValueChanged()
    {
        if (!TryBuildDate(out var dt))
        {
            // Value not yet complete — don't emit a ValueChanged (would be null-thrash).
            return;
        }
        if (_value == dt) return;
        _value = dt;
        _lastInboundValue = dt;
        await ValueChanged.InvokeAsync(dt);
    }

    private void CommitFromSegments()
    {
        if (TryBuildDate(out var dt))
        {
            _value = dt;
            _lastInboundValue = dt;
        }
    }

    private bool TryBuildDate(out DateTime? result)
    {
        result = null;
        int year = -1, month = -1, day = -1, hour = 0, minute = 0, second = 0;
        var anySet = false;

        foreach (var seg in _segments)
        {
            if (seg.IsLiteral) continue;
            if (seg.Value is null)
            {
                // Incomplete — for date components we need all three
                if (seg.Kind is SegmentKind.Year or SegmentKind.Month or SegmentKind.Day)
                    return false;
                continue;
            }
            anySet = true;
            switch (seg.Kind)
            {
                case SegmentKind.Year:
                    year = seg.Width <= 2 ? 2000 + seg.Value.Value : seg.Value.Value;
                    break;
                case SegmentKind.Month: month = seg.Value.Value; break;
                case SegmentKind.Day: day = seg.Value.Value; break;
                case SegmentKind.Hour24: hour = seg.Value.Value; break;
                case SegmentKind.Hour12: hour = seg.Value.Value % 12; break;
                case SegmentKind.Minute: minute = seg.Value.Value; break;
                case SegmentKind.Second: second = seg.Value.Value; break;
            }
        }

        if (!anySet) return false;
        if (year < 0 || month < 0 || day < 0) return false;

        try
        {
            var maxDay = DateTime.DaysInMonth(year, month);
            if (day > maxDay) day = maxDay;
            var dt = new DateTime(year, month, day, hour, minute, second);
            if (Min.HasValue && dt < Min.Value) dt = Min.Value;
            if (Max.HasValue && dt > Max.Value) dt = Max.Value;
            result = dt;
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private string? WidthStyle() =>
        !string.IsNullOrEmpty(Width) ? $"width:{Width}" : null;

    // --- segment model ---

    private enum SegmentKind { Month, Day, Year, Hour24, Hour12, Minute, Second, Literal }

    private sealed class Segment
    {
        public SegmentKind Kind { get; }
        public string Token { get; }
        public int Width { get; }
        public int Min { get; }
        public int Max { get; }
        public string AriaLabel { get; }
        public int? Value { get; set; }

        public Segment(SegmentKind kind, string token, int width, int min, int max, string ariaLabel)
        {
            Kind = kind;
            Token = token;
            Width = width;
            Min = min;
            Max = max;
            AriaLabel = ariaLabel;
        }

        public static Segment Literal(string text) =>
            new(SegmentKind.Literal, text, text.Length, 0, 0, string.Empty) { Value = null };

        public bool IsLiteral => Kind == SegmentKind.Literal;
        public bool HasValue => Value is not null;

        public string Display
        {
            get
            {
                if (IsLiteral) return Token;
                if (Value is null)
                {
                    // Render placeholder glyph matching the token width (e.g. MM -> "mm")
                    return Token.ToLowerInvariant();
                }

                return Kind switch
                {
                    SegmentKind.Year when Width <= 2 =>
                        Value.Value.ToString("D2", CultureInfo.InvariantCulture),
                    SegmentKind.Year =>
                        Value.Value.ToString("D4", CultureInfo.InvariantCulture),
                    _ =>
                        Width >= 2
                            ? Value.Value.ToString("D2", CultureInfo.InvariantCulture)
                            : Value.Value.ToString(CultureInfo.InvariantCulture),
                };
            }
        }
    }
}
