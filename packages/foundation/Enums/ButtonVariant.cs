namespace Sunfish.Core.Enums;

/// <summary>
/// Specifies the HTML type attribute for a button element.
/// </summary>
public enum ButtonType
{
    /// <summary>A standard button that does not submit a form.</summary>
    Button,

    /// <summary>Submits the associated form.</summary>
    Submit,

    /// <summary>Resets the associated form.</summary>
    Reset
}

/// <summary>
/// Specifies the visual style variant of a button.
/// </summary>
public enum ButtonVariant
{
    /// <summary>The primary call-to-action style.</summary>
    Primary,

    /// <summary>A secondary, less prominent style.</summary>
    Secondary,

    /// <summary>A destructive or dangerous action style.</summary>
    Danger,

    /// <summary>A cautionary action style.</summary>
    Warning,

    /// <summary>An informational action style.</summary>
    Info,

    /// <summary>A positive or confirmation action style.</summary>
    Success
}
