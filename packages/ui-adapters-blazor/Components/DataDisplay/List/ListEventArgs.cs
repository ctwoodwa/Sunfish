namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Event arguments for list item reorder events.
/// </summary>
public class ListReorderEventArgs
{
    /// <summary>The item that was moved.</summary>
    public object Item { get; set; } = default!;

    /// <summary>The original index of the item.</summary>
    public int OldIndex { get; set; }

    /// <summary>The new index of the item.</summary>
    public int NewIndex { get; set; }
}
