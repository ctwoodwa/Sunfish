namespace Sunfish.Components.Blazor.Components.DataGrid;

/// <summary>
/// Defines a command that can be placed on grid rows, cells, or toolbar.
/// Engine-agnostic: no JS/DOM dependencies in the public API.
/// </summary>
public class GridCommandDefinition
{
    /// <summary>Unique identifier for the command.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Display text for the command button.</summary>
    public string? Text { get; set; }

    /// <summary>Icon name for the command button.</summary>
    public string? Icon { get; set; }

    /// <summary>Where the command is rendered (Cell, Row, Toolbar).</summary>
    public GridCommandPlacement Placement { get; set; } = GridCommandPlacement.Row;

    /// <summary>Whether the command is visible. Can be set dynamically.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Whether the command is enabled. Can be set dynamically.</summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Whether to show a confirmation dialog before executing.</summary>
    public bool RequiresConfirmation { get; set; }

    /// <summary>Confirmation message text (when RequiresConfirmation is true).</summary>
    public string? ConfirmationText { get; set; }
}

/// <summary>
/// Specifies where a grid command is rendered.
/// </summary>
public enum GridCommandPlacement
{
    /// <summary>Rendered as a button in the row's command cell.</summary>
    Row,

    /// <summary>Rendered in an overflow/context menu on the row.</summary>
    Menu,

    /// <summary>Rendered in the grid toolbar.</summary>
    Toolbar
}

/// <summary>
/// Event args for grid CUD (Create, Update, Delete) operations.
/// </summary>
public class GridEditEventArgs<TItem>
{
    /// <summary>The item being edited, created, or deleted.</summary>
    public TItem Item { get; set; } = default!;

    /// <summary>Whether the operation was cancelled by the handler.</summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Event args for the OnModelInit event, providing a new empty item for create operations.
/// </summary>
public class GridModelInitEventArgs<TItem>
{
    /// <summary>Set this to a new instance of TItem to use as the create model.</summary>
    public TItem Item { get; set; } = default!;
}

/// <summary>
/// Event args for grid command execution.
/// </summary>
public class GridCommandEventArgs<TItem>
{
    /// <summary>The command that was executed.</summary>
    public string CommandId { get; set; } = string.Empty;

    /// <summary>The item the command was executed on (null for toolbar commands).</summary>
    public TItem? Item { get; set; }

    /// <summary>Whether the command execution was cancelled.</summary>
    public bool IsCancelled { get; set; }
}
