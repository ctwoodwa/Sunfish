namespace Sunfish.Compat.Syncfusion;

/// <summary>
/// Syncfusion-shaped per-keystroke input event arguments (SfTextBox.Input). Mirrors
/// <c>Syncfusion.Blazor.Inputs.InputEventArgs</c>.
/// </summary>
public class InputEventArgs
{
    /// <summary>The current string value.</summary>
    public string? Value { get; init; }

    /// <summary>The previous value.</summary>
    public string? PreviousValue { get; init; }
}

/// <summary>
/// Syncfusion-shaped focus-in event arguments (SfTextBox.Focus). Mirrors
/// <c>Syncfusion.Blazor.Inputs.FocusInEventArgs</c>.
/// </summary>
public class FocusInEventArgs
{
    /// <summary>Optional browser event reference.</summary>
    public object? Event { get; init; }
}

/// <summary>
/// Syncfusion-shaped focus-out event arguments (SfTextBox.Blur). Mirrors
/// <c>Syncfusion.Blazor.Inputs.FocusOutEventArgs</c>.
/// </summary>
public class FocusOutEventArgs
{
    /// <summary>The string value at the moment focus was lost.</summary>
    public string? Value { get; init; }

    /// <summary>Optional browser event reference.</summary>
    public object? Event { get; init; }
}

/// <summary>
/// Syncfusion-shaped date-picker focus event arguments (SfDatePicker.Focus / .Blur). Mirrors
/// <c>Syncfusion.Blazor.Calendars.FocusEventArgs</c>.
/// </summary>
public class FocusEventArgs
{
    /// <summary>Optional browser event reference.</summary>
    public object? Event { get; init; }
}
