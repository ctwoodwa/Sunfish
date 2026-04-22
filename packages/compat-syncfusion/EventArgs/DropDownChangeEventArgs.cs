namespace Sunfish.Compat.Syncfusion;

/// <summary>
/// Syncfusion-shaped change event arguments for DropDownList / ComboBox. Mirrors
/// <c>Syncfusion.Blazor.DropDowns.ChangeEventArgs&lt;TValue, TItem&gt;</c>.
/// </summary>
public class DropDownChangeEventArgs<TValue, TItem>
{
    /// <summary>The selected value.</summary>
    public TValue? Value { get; init; }

    /// <summary>The previously selected value.</summary>
    public TValue? PreviousValue { get; init; }

    /// <summary>The full item record (may be null if <c>Value</c> has no matching record).</summary>
    public TItem? ItemData { get; init; }

    /// <summary>True when the event originated from a user interaction (vs. programmatic).</summary>
    public bool IsInteracted { get; init; }
}

/// <summary>
/// Syncfusion-shaped filter-text change event arguments for DropDownList / ComboBox. Mirrors
/// <c>Syncfusion.Blazor.DropDowns.FilteringEventArgs</c>.
/// </summary>
public class FilteringEventArgs
{
    /// <summary>The current filter text.</summary>
    public string? Text { get; init; }

    /// <summary>Cancel the filter event.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// Syncfusion-shaped item-select event arguments for DropDownList / ComboBox. Mirrors
/// <c>Syncfusion.Blazor.DropDowns.SelectEventArgs&lt;TItem&gt;</c>.
/// </summary>
public class SelectEventArgs<TItem>
{
    /// <summary>The selected item.</summary>
    public TItem? ItemData { get; init; }

    /// <summary>True when the event originated from a user interaction.</summary>
    public bool IsInteracted { get; init; }

    /// <summary>Cancel the selection.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// Syncfusion-shaped custom-value event arguments for ComboBox. Mirrors
/// <c>Syncfusion.Blazor.DropDowns.CustomValueSpecifierEventArgs&lt;TValue&gt;</c>.
/// </summary>
public class CustomValueSpecifierEventArgs<TValue>
{
    /// <summary>The custom text the user typed.</summary>
    public string? Text { get; init; }

    /// <summary>The value to adopt (consumer sets this on the args).</summary>
    public TValue? Value { get; set; }
}
