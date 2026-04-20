namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// Event arguments for the <see cref="SunfishMultiSelect{TItem, TValue}.OnItemRender"/>
/// callback. Allows the consumer to modify per-item rendering attributes.
/// </summary>
/// <typeparam name="TItem">The item type displayed in the dropdown.</typeparam>
public class MultiSelectItemRenderEventArgs<TItem>
{
    /// <summary>The item being rendered.</summary>
    public TItem Item { get; init; } = default!;

    /// <summary>
    /// Set additional CSS class(es) to apply to this item's option element.
    /// Combined with the standard option classes from the CSS provider.
    /// </summary>
    public string? CssClass { get; set; }

    /// <summary>
    /// Set true to render this item as disabled. Disabled items are not selectable
    /// and emit aria-disabled="true".
    /// </summary>
    public bool IsDisabled { get; set; }
}

/// <summary>
/// Event arguments for the <see cref="SunfishMultiSelect{TItem, TValue}.OnRead"/>
/// server-side data callback. The consumer must set <see cref="Data"/> and
/// <see cref="Total"/> in their handler.
/// </summary>
/// <typeparam name="TItem">The item type displayed in the dropdown.</typeparam>
public class MultiSelectReadEventArgs<TItem>
{
    /// <summary>
    /// The current filter text entered by the user. Empty when the dropdown
    /// has just been opened and no filter has been applied yet.
    /// </summary>
    public string Filter { get; init; } = string.Empty;

    /// <summary>
    /// Cancellation token that is cancelled when a newer read request starts
    /// before this one completes (e.g., the user keeps typing).
    /// </summary>
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Set this to the items to display in the dropdown for the current filter.
    /// </summary>
    public IEnumerable<TItem> Data { get; set; } = Array.Empty<TItem>();

    /// <summary>
    /// Set this to the total number of items available on the server (before
    /// filtering or paging is applied).
    /// </summary>
    public int Total { get; set; }
}
