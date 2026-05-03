namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Identifies a built-in <see cref="Components.Forms.Inputs.SunfishListBox{TItem}"/> toolbar tool.
/// Consumers supply <c>Tools</c> as an <c>ICollection&lt;ListBoxToolName&gt;</c> to select which
/// buttons appear and in what order.
/// </summary>
public enum ListBoxToolName
{
    /// <summary>Moves the selected items one position up in the list.</summary>
    MoveUp,

    /// <summary>Moves the selected items one position down in the list.</summary>
    MoveDown,

    /// <summary>Removes the selected items from the list.</summary>
    Remove,

    /// <summary>Transfers the selected items from this ListBox to the connected (destination) ListBox.</summary>
    TransferTo,

    /// <summary>Transfers the selected items from the connected (source) ListBox to this one.</summary>
    TransferFrom,

    /// <summary>Transfers all items from this ListBox to the connected (destination) ListBox.</summary>
    TransferAllTo,

    /// <summary>Transfers all items from the connected (source) ListBox to this one.</summary>
    TransferAllFrom
}
