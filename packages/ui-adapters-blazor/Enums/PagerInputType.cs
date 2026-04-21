namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Determines how <c>SunfishPagination</c> renders the page selector.
/// Mirrors the <c>PagerInputType</c> surface in the Sunfish pager spec.
/// </summary>
public enum PagerInputType
{
    /// <summary>
    /// Render numeric page buttons inline (for example <c>1 2 3 ... 10</c>) with
    /// an ellipsis when the page count exceeds <c>ButtonCount</c>. This is the default.
    /// </summary>
    Buttons,

    /// <summary>
    /// Render a single native <c>&lt;select&gt;</c> dropdown that lists every page,
    /// preceded by "Page" and followed by "of {totalPages}".
    /// </summary>
    Dropdown,

    /// <summary>
    /// Render a numeric <c>&lt;input&gt;</c> that accepts a page index, preceded by
    /// "Page" and followed by "of {totalPages}". The page changes on
    /// <c>change</c> / <kbd>Enter</kbd> to avoid firing on every keystroke.
    /// </summary>
    Input
}
