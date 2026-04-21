using Sunfish.Foundation.Data;

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

/// <summary>
/// Event arguments for ListView edit-flow events
/// (<c>OnCreate</c>, <c>OnUpdate</c>, <c>OnDelete</c>, <c>OnEdit</c>, <c>OnCancel</c>).
/// Handlers may set <see cref="IsCancelled"/> to prevent the operation.
/// </summary>
/// <typeparam name="TItem">The list item type.</typeparam>
public class ListViewCommandEventArgs<TItem>
{
    /// <summary>The data item that the command applies to.</summary>
    public TItem Item { get; set; } = default!;

    /// <summary>Whether the current edit item represents a newly inserted (not yet saved) item.</summary>
    public bool IsNew { get; set; }

    /// <summary>
    /// Set to <c>true</c> to cancel the operation (e.g., block entering edit mode,
    /// prevent save, or discard changes on cancel).
    /// </summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Non-generic convenience alias over <see cref="ListViewCommandEventArgs{TItem}"/>.
/// Prefer the generic version for type safety.
/// </summary>
public class ListViewCommandEventArgs : ListViewCommandEventArgs<object>
{
}

/// <summary>
/// Event arguments for the ListView <c>OnRead</c> server-side data callback.
/// Handlers must set <see cref="Data"/> (and optionally <see cref="Total"/>)
/// in response to the current paging / grouping state.
/// </summary>
/// <typeparam name="TItem">The list item type.</typeparam>
public class ListViewReadEventArgs<TItem>
{
    /// <summary>The data request containing paging and grouping descriptors.</summary>
    public DataRequest Request { get; init; } = new();

    /// <summary>Set this to the items for the current page/view.</summary>
    public IEnumerable<TItem> Data { get; set; } = [];

    /// <summary>Set this to the total number of items (before paging) so the pager can calculate page count.</summary>
    public int Total { get; set; }
}
