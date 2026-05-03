namespace Sunfish.UIAdapters.Blazor.Components.Layout;

/// <summary>
/// Base event args for accordion item interactions in data-driven mode.
/// Mirrors the Sunfish PanelBar spec where handlers receive the clicked/expanded/collapsed item.
/// </summary>
public class AccordionItemEventArgs
{
    /// <summary>The data item associated with the interaction. Cast to your model type.</summary>
    public object? Item { get; init; }
}

/// <summary>
/// Strongly-typed event args used by <see cref="AccordionItemEventArgs"/> consumers that want
/// to avoid a cast. The untyped base is kept to match spec samples that do <c>args.Item as Model</c>.
/// </summary>
/// <typeparam name="TItem">The data item type.</typeparam>
public class AccordionItemEventArgs<TItem> : AccordionItemEventArgs
{
    /// <summary>The typed data item associated with the interaction.</summary>
    public new TItem? Item
    {
        get => (TItem?)base.Item;
        init => base.Item = value;
    }
}

/// <summary>
/// Event args raised before an accordion item expands. Setting <see cref="IsCancelled"/> to
/// <c>true</c> aborts the expansion while leaving focus/selection on the item (matches PanelBar spec).
/// </summary>
public class AccordionExpandEventArgs : AccordionItemEventArgs
{
    /// <summary>Cancels the expansion when set to <c>true</c>.</summary>
    public bool IsCancelled { get; set; }
}

/// <summary>
/// Event args raised before an accordion item collapses. Setting <see cref="IsCancelled"/> to
/// <c>true</c> aborts the collapse.
/// </summary>
public class AccordionCollapseEventArgs : AccordionItemEventArgs
{
    /// <summary>Cancels the collapse when set to <c>true</c>.</summary>
    public bool IsCancelled { get; set; }
}
