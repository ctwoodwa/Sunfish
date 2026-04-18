namespace Sunfish.Foundation.Enums;

/// <summary>
/// Specifies the layout orientation of a form.
/// </summary>
public enum FormOrientation
{
    /// <summary>Labels appear above their fields (stacked vertically).</summary>
    Vertical,

    /// <summary>Labels appear beside their fields (side by side).</summary>
    Horizontal
}

/// <summary>
/// Specifies how validation messages are displayed in a form.
/// </summary>
public enum FormValidationMessageType
{
    /// <summary>Validation messages appear inline below the field.</summary>
    Inline,

    /// <summary>Validation messages appear as tooltips on the field.</summary>
    Tooltip,

    /// <summary>No per-field validation messages are displayed.</summary>
    None
}

/// <summary>
/// Specifies the alignment of form buttons.
/// </summary>
public enum FormButtonsLayout
{
    /// <summary>Buttons aligned to the start.</summary>
    Start,

    /// <summary>Buttons centered.</summary>
    Center,

    /// <summary>Buttons aligned to the end.</summary>
    End,

    /// <summary>Buttons stretched to fill available space.</summary>
    Stretch
}
