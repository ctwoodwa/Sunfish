namespace Sunfish.Foundation.Models;

/// <summary>
/// Event arguments for a tree view drag-and-drop operation.
/// </summary>
public class TreeDragDropEventArgs
{
    /// <summary>The data item that was dragged.</summary>
    public object DraggedItem { get; set; } = default!;

    /// <summary>The data item that was the drop target.</summary>
    public object TargetItem { get; set; } = default!;

    /// <summary>The ID of the dragged node.</summary>
    public string DraggedId { get; set; } = string.Empty;

    /// <summary>The ID of the target node.</summary>
    public string TargetId { get; set; } = string.Empty;
}
