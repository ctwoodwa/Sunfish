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
