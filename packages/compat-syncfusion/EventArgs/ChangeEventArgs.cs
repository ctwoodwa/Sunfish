namespace Sunfish.Compat.Syncfusion;

/// <summary>
/// Syncfusion-shaped change event arguments. Mirrors
/// <c>Syncfusion.Blazor.Buttons.ChangeEventArgs&lt;TChecked&gt;</c> and
/// <c>Syncfusion.Blazor.Inputs.ChangedEventArgs</c>.
///
/// <para><b>Status:</b> Type shipped so consumer handler signatures compile; functional
/// wiring from <c>SfCheckBox</c> / <c>SfTextBox</c> to this type is not hooked in the initial
/// surface — wrappers currently forward through plain <c>ValueChanged</c> and delegate
/// directly to Sunfish. See docs/compat-syncfusion-mapping.md.</para>
/// </summary>
public class ChangeEventArgs<TChecked>
{
    /// <summary>The new value.</summary>
    public TChecked? Checked { get; init; }

    /// <summary>Optional browser event reference.</summary>
    public object? Event { get; init; }
}

/// <summary>
/// Non-generic change event arguments (string-valued inputs). Mirrors
/// <c>Syncfusion.Blazor.Inputs.ChangedEventArgs</c>.
/// </summary>
public class ChangedEventArgs
{
    /// <summary>The new string value.</summary>
    public string? Value { get; init; }

    /// <summary>Optional browser event reference.</summary>
    public object? Event { get; init; }
}

/// <summary>
/// Generic value-commit change event arguments. Mirrors
/// <c>Syncfusion.Blazor.Calendars.ChangedEventArgs&lt;TValue&gt;</c>.
/// </summary>
public class ChangedEventArgs<TValue>
{
    /// <summary>The new value.</summary>
    public TValue? Value { get; init; }

    /// <summary>Optional browser event reference.</summary>
    public object? Event { get; init; }
}
