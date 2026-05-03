using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// Event arguments fired by <see cref="SunfishListBox{TItem}.OnActionClick"/> when
/// any toolbar tool is invoked. This is a single entry point that consumers can
/// listen to in place of the individual per-tool events.
/// </summary>
/// <typeparam name="TItem">The ListBox item model type.</typeparam>
public class ListBoxActionClickEventArgs<TItem>
{
    /// <summary>The tool that was clicked.</summary>
    public ListBoxToolName Tool { get; init; }

    /// <summary>The items affected by the action (currently selected items at the time of the click).</summary>
    public List<TItem> Items { get; init; } = new();

    /// <summary>
    /// When <c>true</c>, the ListBox will suppress the built-in event that normally follows
    /// (for example, <c>OnReorder</c> for <see cref="ListBoxToolName.MoveUp"/>). Default <c>false</c>.
    /// </summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Event arguments fired by <see cref="SunfishListBox{TItem}.OnReorder"/> when the user
/// clicks <see cref="ListBoxToolName.MoveUp"/> or <see cref="ListBoxToolName.MoveDown"/>.
/// </summary>
/// <typeparam name="TItem">The ListBox item model type.</typeparam>
public class ListBoxReorderEventArgs<TItem>
{
    /// <summary>The index of the first item in <see cref="Items"/> before reordering.</summary>
    public int FromIndex { get; init; }

    /// <summary>The target index after reordering.</summary>
    public int ToIndex { get; init; }

    /// <summary>The selected item(s) being reordered.</summary>
    public List<TItem> Items { get; init; } = new();
}

/// <summary>
/// Event arguments fired by <see cref="SunfishListBox{TItem}.OnRemove"/> when the user
/// clicks the <see cref="ListBoxToolName.Remove"/> toolbar button.
/// </summary>
/// <typeparam name="TItem">The ListBox item model type.</typeparam>
public class ListBoxRemoveEventArgs<TItem>
{
    /// <summary>The selected item(s) to be removed.</summary>
    public List<TItem> Items { get; init; } = new();
}

/// <summary>
/// Event arguments fired by <see cref="SunfishListBox{TItem}.OnTransfer"/> when the user
/// clicks any of the transfer toolbar buttons.
/// </summary>
/// <typeparam name="TItem">The ListBox item model type.</typeparam>
public class ListBoxTransferEventArgs<TItem>
{
    /// <summary>The tool that initiated the transfer.</summary>
    public ListBoxToolName Tool { get; init; }

    /// <summary>The <c>Id</c> of the destination ListBox, if the connection was made by id.</summary>
    public string? DestinationListBoxId { get; init; }

    /// <summary>The selected item(s) being moved.</summary>
    public List<TItem> Items { get; init; } = new();
}

/// <summary>
/// Event arguments fired by <see cref="SunfishListBox{TItem}.OnDrop"/> when the user
/// releases a dragged item over a drop target.
/// </summary>
/// <typeparam name="TItem">The ListBox item model type.</typeparam>
public class ListBoxDropEventArgs<TItem>
{
    /// <summary>The index of the item in the destination ListBox that received the drop, or <c>null</c> for the empty area below the last item.</summary>
    public int? DestinationIndex { get; init; }

    /// <summary>The <c>Id</c> of the destination ListBox instance.</summary>
    public string? DestinationListBoxId { get; init; }

    /// <summary>The dropped item(s).</summary>
    public List<TItem> Items { get; init; } = new();
}
