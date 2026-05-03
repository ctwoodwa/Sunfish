using System.Text;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Sunfish.UIAdapters.Blazor.Base;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// A masked-input control that enforces a format string while the user types.
/// Supports the standard mask tokens (<c>0</c>, <c>9</c>, <c>#</c>, <c>L</c>,
/// <c>?</c>, <c>&amp;</c>, <c>C</c>, <c>A</c>, <c>a</c>), case-shift directives
/// (<c>&gt;</c>, <c>&lt;</c>), literal escaping with <c>\</c>, literal pass-through,
/// caret skipping over literal positions, and optional visibility of the mask on
/// focus only.
/// </summary>
public partial class SunfishMaskedInput : SunfishComponentBase
{
    // ── Parameters: Value + events ─────────────────────────────────────────

    /// <summary>The component value. Supports two-way binding.</summary>
    [Parameter] public string Value { get; set; } = string.Empty;

    /// <summary>Fires on every keystroke with the new value.</summary>
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    /// <summary>Fires when the user confirms the value (Enter or blur).</summary>
    [Parameter] public EventCallback<object?> OnChange { get; set; }

    /// <summary>Fires when the component loses focus.</summary>
    [Parameter] public EventCallback OnBlur { get; set; }

    // ── Parameters: Mask configuration ─────────────────────────────────────

    /// <summary>
    /// The mask pattern the user must follow. Recognized rule tokens:
    /// <c>0</c> required digit, <c>9</c> optional digit/space,
    /// <c>#</c> digit/space/plus/minus, <c>L</c> required letter,
    /// <c>?</c> optional letter/space, <c>&amp;</c> required character (not space),
    /// <c>C</c> optional character, <c>A</c> required alphanumeric,
    /// <c>a</c> optional alphanumeric, <c>&gt;</c> and <c>&lt;</c> case-shift
    /// directives, <c>\</c> escape the next character as a literal.
    /// </summary>
    [Parameter] public string? Mask { get; set; }

    /// <summary>When true, the mask is only rendered while the input is focused.</summary>
    [Parameter] public bool MaskOnFocus { get; set; }

    /// <summary>Character shown at unfilled mask positions. Defaults to <c>_</c>.</summary>
    [Parameter] public char Prompt { get; set; } = '_';

    /// <summary>
    /// Character substituted in the raw <see cref="Value"/> for unfilled positions.
    /// Defaults to a single space. Set to <c>null</c> to omit.
    /// </summary>
    [Parameter] public char? PromptPlaceholder { get; set; } = ' ';

    /// <summary>When true, mask literals are included in the bound <see cref="Value"/>.</summary>
    [Parameter] public bool IncludeLiterals { get; set; }

    // ── Parameters: Standard input-shaping ─────────────────────────────────

    /// <summary>The <c>placeholder</c> attribute. When MaskOnFocus is true and the
    /// input is unfocused with no value, this is shown instead of the mask.</summary>
    [Parameter] public string? Placeholder { get; set; }

    /// <summary>When false, renders the input as disabled.</summary>
    [Parameter] public bool Enabled { get; set; } = true;

    /// <summary>When true, the input is readonly.</summary>
    [Parameter] public bool ReadOnly { get; set; }

    /// <summary>When true, the input renders the Sunfish invalid styling.</summary>
    [Parameter] public bool IsInvalid { get; set; }

    /// <summary>Pass-through <c>id</c> attribute.</summary>
    [Parameter] public string? Id { get; set; }

    /// <summary>Pass-through <c>name</c> attribute.</summary>
    [Parameter] public string? Name { get; set; }

    /// <summary>Pass-through <c>tabindex</c> attribute.</summary>
    [Parameter] public int? TabIndex { get; set; }

    /// <summary>Pass-through <c>title</c> attribute.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>Pass-through <c>inputmode</c> attribute.</summary>
    [Parameter] public string? InputMode { get; set; }

    /// <summary>Pass-through <c>autocapitalize</c> attribute.</summary>
    [Parameter] public string? AutoCapitalize { get; set; }

    /// <summary>Pass-through <c>spellcheck</c> attribute.</summary>
    [Parameter] public string? SpellCheck { get; set; }

    /// <summary>Pass-through <c>autocomplete</c> attribute.</summary>
    [Parameter] public string? AutoComplete { get; set; }

    /// <summary>Pass-through <c>aria-label</c> attribute.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>Pass-through <c>aria-labelledby</c> attribute.</summary>
    [Parameter] public string? AriaLabelledBy { get; set; }

    /// <summary>Pass-through <c>aria-describedby</c> attribute.</summary>
    [Parameter] public string? AriaDescribedBy { get; set; }

    /// <summary>When true, renders an &quot;x&quot; clear button inside the input.</summary>
    [Parameter] public bool ShowClearButton { get; set; }

    // ── Internal state ────────────────────────────────────────────────────

    private ElementReference _inputElement;
    private bool _isFocused;

    /// <summary>
    /// The raw user input held as a per-mask-position character array.
    /// Index i corresponds to mask position i. Empty entries use '\0'.
    /// </summary>
    private char[] _slots = Array.Empty<char>();

    /// <summary>Parsed mask tokens. Re-computed whenever <see cref="Mask"/> changes.</summary>
    private List<MaskToken> _tokens = new();

    private string? _lastMask;
    private string? _lastSyncedValue;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        if (!string.Equals(Mask, _lastMask, StringComparison.Ordinal))
        {
            _tokens = ParseMask(Mask);
            _slots = new char[_tokens.Count];
            _lastMask = Mask;
            _lastSyncedValue = null; // force re-sync of the incoming Value
        }

        if (!string.Equals(Value, _lastSyncedValue, StringComparison.Ordinal))
        {
            SyncSlotsFromValue(Value ?? string.Empty);
            _lastSyncedValue = Value;
        }
    }

    // ── Public imperative API ─────────────────────────────────────────────

    /// <summary>Focuses the underlying input element.</summary>
    public ValueTask FocusAsync() => _inputElement.FocusAsync();

    // ── Render helpers ────────────────────────────────────────────────────

    private string RenderedDisplayValue
    {
        get
        {
            // Mask-on-focus + not focused + empty → show nothing (Placeholder handles hint).
            if (MaskOnFocus && !_isFocused && IsSlotsEmpty())
                return string.Empty;

            if (_tokens.Count == 0)
                return Value ?? string.Empty;

            var sb = new StringBuilder(_tokens.Count);
            for (int i = 0; i < _tokens.Count; i++)
            {
                var t = _tokens[i];
                if (t.IsRule)
                {
                    sb.Append(_slots[i] != '\0' ? _slots[i] : Prompt);
                }
                else
                {
                    sb.Append(t.Literal);
                }
            }
            return sb.ToString();
        }
    }

    private string? EffectivePlaceholder
    {
        get
        {
            // When MaskOnFocus is true and we're not focused, the placeholder should
            // win over the mask; otherwise the rendered value carries the mask and
            // no placeholder is needed.
            if (MaskOnFocus && !_isFocused && IsSlotsEmpty())
                return Placeholder;
            return Placeholder;
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────

    private async Task OnInput(ChangeEventArgs e)
    {
        var incoming = e.Value?.ToString() ?? string.Empty;

        if (_tokens.Count == 0)
        {
            Value = incoming;
            _lastSyncedValue = Value;
            await ValueChanged.InvokeAsync(Value);
            return;
        }

        ApplyIncomingToSlots(incoming);

        var bound = ComposeBoundValue();
        Value = bound;
        _lastSyncedValue = bound;
        await ValueChanged.InvokeAsync(bound);
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
        {
            await OnChange.InvokeAsync(Value);
        }
    }

    private void OnFocus(FocusEventArgs _)
    {
        _isFocused = true;
    }

    private async Task OnBlurHandler(FocusEventArgs _)
    {
        _isFocused = false;
        await OnChange.InvokeAsync(Value);
        await OnBlur.InvokeAsync();
    }

    private async Task ClearValue()
    {
        Array.Clear(_slots, 0, _slots.Length);
        var bound = ComposeBoundValue();
        Value = bound;
        _lastSyncedValue = bound;
        await ValueChanged.InvokeAsync(bound);
    }

    // ── Mask engine ───────────────────────────────────────────────────────

    /// <summary>
    /// Parses the mask string into an ordered token list. Rule tokens carry the
    /// predicate used to validate a keystroke; literal tokens carry the character
    /// emitted verbatim.
    /// </summary>
    internal static List<MaskToken> ParseMask(string? mask)
    {
        var tokens = new List<MaskToken>();
        if (string.IsNullOrEmpty(mask)) return tokens;

        CaseShift shift = CaseShift.None;

        for (int i = 0; i < mask.Length; i++)
        {
            char c = mask[i];

            // Escape next char as literal.
            if (c == '\\' && i + 1 < mask.Length)
            {
                tokens.Add(MaskToken.Lit(mask[i + 1]));
                i++;
                continue;
            }

            // Case-shift directives don't emit a position; they affect later rules.
            if (c == '>')
            {
                shift = CaseShift.Upper;
                continue;
            }
            if (c == '<')
            {
                shift = CaseShift.Lower;
                continue;
            }

            var predicate = TryGetRulePredicate(c, out var required);
            if (predicate != null)
            {
                tokens.Add(MaskToken.Rule(predicate, required, shift));
            }
            else
            {
                tokens.Add(MaskToken.Lit(c));
            }
        }
        return tokens;
    }

    private static Func<char, bool>? TryGetRulePredicate(char token, out bool required)
    {
        switch (token)
        {
            case '0': required = true;  return c => char.IsDigit(c);
            case '9': required = false; return c => char.IsDigit(c) || c == ' ';
            case '#': required = false; return c => char.IsDigit(c) || c == ' ' || c == '+' || c == '-';
            case 'L': required = true;  return c => char.IsLetter(c);
            case '?': required = false; return c => char.IsLetter(c) || c == ' ';
            case '&': required = true;  return c => !char.IsWhiteSpace(c);
            case 'C': required = false; return _ => true;
            case 'A': required = true;  return c => char.IsLetterOrDigit(c);
            case 'a': required = false; return c => char.IsLetterOrDigit(c) || c == ' ';
            default:  required = false; return null;
        }
    }

    /// <summary>
    /// Merges the browser's reported input string back into the per-position slot
    /// buffer. We diff strictly by mask position and treat literal positions as
    /// immovable — so typing advances past them, and deletions skip back over them.
    /// </summary>
    private void ApplyIncomingToSlots(string incoming)
    {
        // Step 1: strip the mask's literal-at-position characters from the incoming
        // string so we're left with only the user's rule-position input. This is the
        // robust way to handle both typing (which adds a character before the next
        // literal) and backspace (which removes either a rule slot or, if the caret
        // lands on a literal, the preceding rule slot).
        var inputChars = new List<char>(incoming.Length);

        // Build a map: for each index in `incoming`, determine if that position
        // aligns with a mask literal (by greedy alignment) — if so, skip; otherwise
        // collect the char as a user keystroke.
        // We do a tolerant alignment: walk both cursors, and whenever the incoming
        // char equals the mask literal at the current token, consume both; otherwise
        // the incoming char is a user keystroke.
        int tokenIdx = 0;
        for (int i = 0; i < incoming.Length; i++)
        {
            // Skip prompt characters (the user didn't really type these).
            if (incoming[i] == Prompt)
            {
                if (tokenIdx < _tokens.Count && _tokens[tokenIdx].IsRule)
                    tokenIdx++;
                continue;
            }

            // Advance past any literal tokens whose char matches the incoming char.
            while (tokenIdx < _tokens.Count
                   && !_tokens[tokenIdx].IsRule
                   && _tokens[tokenIdx].Literal == incoming[i])
            {
                tokenIdx++;
                goto NextIncoming;
            }

            // Also advance past literal tokens that appear before the next rule
            // (they're rendered regardless of user input).
            while (tokenIdx < _tokens.Count && !_tokens[tokenIdx].IsRule)
            {
                tokenIdx++;
            }

            if (tokenIdx < _tokens.Count)
            {
                inputChars.Add(incoming[i]);
                tokenIdx++;
            }
            else
            {
                // Incoming exceeds mask length; ignore excess characters.
            }

        NextIncoming:;
        }

        // Step 2: repopulate the slot buffer from the collected user input,
        // validating each keystroke against the rule predicate.
        Array.Clear(_slots, 0, _slots.Length);
        int inputIdx = 0;
        for (int t = 0; t < _tokens.Count && inputIdx < inputChars.Count; t++)
        {
            if (!_tokens[t].IsRule) continue;

            char candidate = inputChars[inputIdx];
            if (_tokens[t].Predicate!(candidate))
            {
                _slots[t] = ApplyShift(candidate, _tokens[t].Shift);
                inputIdx++;
            }
            else
            {
                // Rejected character: skip the incoming char and try again against
                // the same rule slot (tolerant of the browser reporting the mask's
                // own literal where we expected a user char).
                inputIdx++;
                t--;
            }
        }
    }

    private static char ApplyShift(char c, CaseShift shift) => shift switch
    {
        CaseShift.Upper => char.ToUpperInvariant(c),
        CaseShift.Lower => char.ToLowerInvariant(c),
        _ => c,
    };

    /// <summary>
    /// Builds the outward-facing <see cref="Value"/> from the slot buffer. Honors
    /// <see cref="IncludeLiterals"/> and <see cref="PromptPlaceholder"/>.
    /// </summary>
    private string ComposeBoundValue()
    {
        if (_tokens.Count == 0) return Value ?? string.Empty;

        var sb = new StringBuilder(_tokens.Count);
        for (int i = 0; i < _tokens.Count; i++)
        {
            var t = _tokens[i];
            if (t.IsRule)
            {
                if (_slots[i] != '\0')
                {
                    sb.Append(_slots[i]);
                }
                else if (PromptPlaceholder is char ph)
                {
                    sb.Append(ph);
                }
            }
            else if (IncludeLiterals)
            {
                sb.Append(t.Literal);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Populates the slot buffer from an incoming bound value, using the same
    /// tolerant alignment rules as user typing.
    /// </summary>
    private void SyncSlotsFromValue(string value)
    {
        if (_slots.Length == 0) return;
        Array.Clear(_slots, 0, _slots.Length);
        if (string.IsNullOrEmpty(value)) return;

        int v = 0;
        for (int t = 0; t < _tokens.Count && v < value.Length; t++)
        {
            var tok = _tokens[t];
            if (tok.IsRule)
            {
                char c = value[v];
                // Treat the prompt-placeholder as "empty" when rebuilding.
                if (PromptPlaceholder is char ph && c == ph)
                {
                    v++;
                    continue;
                }
                if (tok.Predicate!(c))
                {
                    _slots[t] = ApplyShift(c, tok.Shift);
                    v++;
                }
                else
                {
                    // Not a match — skip this input char and try against same slot.
                    v++;
                    t--;
                }
            }
            else if (IncludeLiterals && v < value.Length && value[v] == tok.Literal)
            {
                v++;
            }
        }
    }

    private bool IsSlotsEmpty()
    {
        for (int i = 0; i < _slots.Length; i++)
            if (_slots[i] != '\0') return false;
        return true;
    }

    // ── Types ─────────────────────────────────────────────────────────────

    internal enum CaseShift { None, Upper, Lower }

    internal readonly struct MaskToken
    {
        public bool IsRule { get; }
        public char Literal { get; }
        public Func<char, bool>? Predicate { get; }
        public bool Required { get; }
        public CaseShift Shift { get; }

        private MaskToken(bool isRule, char literal, Func<char, bool>? predicate, bool required, CaseShift shift)
        {
            IsRule = isRule; Literal = literal; Predicate = predicate;
            Required = required; Shift = shift;
        }

        public static MaskToken Lit(char c) => new(false, c, null, false, CaseShift.None);
        public static MaskToken Rule(Func<char, bool> predicate, bool required, CaseShift shift)
            => new(true, '\0', predicate, required, shift);
    }
}
