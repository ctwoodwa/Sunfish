namespace Sunfish.Components.Blazor.Components.Editors;

/// <summary>
/// Base class for editor command arguments.
/// </summary>
public abstract class EditorCommandArgs
{
    /// <summary>The command name.</summary>
    public string Command { get; init; } = "";
}

/// <summary>
/// Arguments for simple tool commands (bold, italic, etc.).
/// </summary>
public class ToolCommandArgs : EditorCommandArgs { }

/// <summary>
/// Arguments for HTML insertion commands.
/// </summary>
public class HtmlCommandArgs : EditorCommandArgs
{
    /// <summary>The HTML content to insert at the cursor position.</summary>
    public string Html { get; init; } = "";
}

/// <summary>
/// Arguments for format commands (heading, paragraph, etc.).
/// </summary>
public class FormatCommandArgs : EditorCommandArgs
{
    /// <summary>The block format tag (e.g., "h1", "h2", "p", "blockquote").</summary>
    public string Tag { get; init; } = "p";
}

/// <summary>
/// Arguments for table insertion commands.
/// </summary>
public class TableCommandArgs : EditorCommandArgs
{
    /// <summary>Number of rows to insert.</summary>
    public int Rows { get; init; } = 2;

    /// <summary>Number of columns to insert.</summary>
    public int Columns { get; init; } = 2;
}

/// <summary>
/// Arguments for link creation commands.
/// </summary>
public class LinkCommandArgs : EditorCommandArgs
{
    /// <summary>The URL of the link.</summary>
    public string Href { get; init; } = "";

    /// <summary>The display text. If null, uses the selected text.</summary>
    public string? Text { get; init; }

    /// <summary>The link target (e.g., "_blank").</summary>
    public string? Target { get; init; }

    /// <summary>The link title attribute.</summary>
    public string? Title { get; init; }
}

/// <summary>
/// Arguments for image insertion commands.
/// </summary>
public class ImageCommandArgs : EditorCommandArgs
{
    /// <summary>The image source URL.</summary>
    public string Src { get; init; } = "";

    /// <summary>The alt text for the image.</summary>
    public string Alt { get; init; } = "";

    /// <summary>Optional width.</summary>
    public string? Width { get; init; }

    /// <summary>Optional height.</summary>
    public string? Height { get; init; }
}

/// <summary>
/// Arguments for color commands (foreColor, backColor).
/// </summary>
public class ColorCommandArgs : EditorCommandArgs
{
    /// <summary>The color value (hex, rgb, or named).</summary>
    public string Color { get; init; } = "";
}

/// <summary>
/// Arguments for font size commands.
/// </summary>
public class FontSizeCommandArgs : EditorCommandArgs
{
    /// <summary>The font size value (1-7 for execCommand, or CSS value).</summary>
    public string Size { get; init; } = "3";
}

/// <summary>
/// Arguments for font family commands.
/// </summary>
public class FontFamilyCommandArgs : EditorCommandArgs
{
    /// <summary>The font family name.</summary>
    public string Family { get; init; } = "";
}
