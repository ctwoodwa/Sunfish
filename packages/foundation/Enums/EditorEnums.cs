namespace Sunfish.Core.Enums;

/// <summary>
/// Specifies the editing mode of the editor component.
/// </summary>
public enum EditorEditMode
{
    /// <summary>Rich text editing mode (WYSIWYG contenteditable).</summary>
    Edit,

    /// <summary>Preview mode showing rendered HTML content.</summary>
    Preview,

    /// <summary>Raw HTML source editing mode.</summary>
    Source
}

/// <summary>
/// Specifies available toolbar tools in a WYSIWYG editor.
/// </summary>
public enum EditorTool
{
    Bold,
    Italic,
    Underline,
    Strikethrough,
    OrderedList,
    UnorderedList,
    Indent,
    Outdent,
    AlignLeft,
    AlignCenter,
    AlignRight,
    AlignJustify,
    Link,
    Unlink,
    Image,
    HorizontalRule,
    ClearFormatting,
    Undo,
    Redo,
    Table,
    Format,
    FontSize,
    FontFamily,
    FontColor,
    BackgroundColor,
    Subscript,
    Superscript,
    ViewSource,
    // Table manipulation tools
    AddColumnBefore,
    AddColumnAfter,
    AddRowBefore,
    AddRowAfter,
    DeleteColumn,
    DeleteRow,
    DeleteTable,
    MergeCells,
    SplitCell
}
