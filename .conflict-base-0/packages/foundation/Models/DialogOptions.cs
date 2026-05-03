namespace Sunfish.Foundation.Models;

/// <summary>
/// Configuration options for programmatically opened dialogs via the dialog service.
/// </summary>
public class DialogOptions
{
    /// <summary>
    /// The dialog title displayed in the header.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The message or content body of the dialog.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The width of the dialog (CSS value, e.g., "400px", "50%"). Null uses default sizing.
    /// </summary>
    public string? Width { get; set; }

    /// <summary>
    /// The height of the dialog (CSS value, e.g., "300px"). Null uses auto-height.
    /// </summary>
    public string? Height { get; set; }

    /// <summary>
    /// Whether the dialog displays a modal overlay. Default is true.
    /// </summary>
    public bool Modal { get; set; } = true;

    /// <summary>
    /// Whether the dialog can be dragged by its title bar. Default is false.
    /// </summary>
    public bool Draggable { get; set; }

    /// <summary>
    /// Predefined button set for the dialog footer.
    /// </summary>
    public DialogButtons Buttons { get; set; } = DialogButtons.Ok;
}

/// <summary>
/// Predefined button combinations for dialog footers.
/// </summary>
public enum DialogButtons
{
    /// <summary>A single OK button.</summary>
    Ok,

    /// <summary>OK and Cancel buttons.</summary>
    OkCancel,

    /// <summary>Yes and No buttons.</summary>
    YesNo,

    /// <summary>Yes, No, and Cancel buttons.</summary>
    YesNoCancel,

    /// <summary>Retry and Cancel buttons.</summary>
    RetryCancel,

    /// <summary>No predefined buttons; use custom content.</summary>
    None
}
