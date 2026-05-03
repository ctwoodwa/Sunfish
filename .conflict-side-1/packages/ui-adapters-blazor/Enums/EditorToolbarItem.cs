namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Canonical MVP toolbar-item identifiers for the SunfishEditor MVP surface
/// (<c>Components/Forms/Inputs/SunfishEditor</c>).
///
/// <para>
/// This enum is intentionally a lean subset of the richer
/// <see cref="Sunfish.Foundation.Enums.EditorTool"/> enum used by the full
/// <c>Sunfish.UIAdapters.Blazor.Components.Editors.SunfishEditor</c> component.
/// The MVP editor in <c>Forms/Inputs</c> uses this smaller set for its
/// <c>document.execCommand</c>-based toolbar.
/// </para>
/// </summary>
public enum EditorToolbarItem
{
    /// <summary>Toggle bold formatting on the selection.</summary>
    Bold,

    /// <summary>Toggle italic formatting on the selection.</summary>
    Italic,

    /// <summary>Toggle underline formatting on the selection.</summary>
    Underline,

    /// <summary>Insert or toggle an unordered (bullet) list.</summary>
    BulletList,

    /// <summary>Insert or toggle an ordered (numbered) list.</summary>
    NumberedList,

    /// <summary>Apply a heading block format (H1-H3).</summary>
    Heading,

    /// <summary>Insert a hyperlink on the current selection.</summary>
    Link,

    /// <summary>Insert an image by URL.</summary>
    Image,

    /// <summary>Undo the last edit.</summary>
    Undo,

    /// <summary>Redo the last undone edit.</summary>
    Redo,

    /// <summary>Apply a font size to the selection.</summary>
    FontSize,

    /// <summary>Apply a foreground text color to the selection.</summary>
    Color,
}
