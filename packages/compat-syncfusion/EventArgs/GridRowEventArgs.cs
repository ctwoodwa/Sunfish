namespace Sunfish.Compat.Syncfusion;

/// <summary>
/// Syncfusion-shaped row-selected event arguments (SfGrid via GridEvents). Mirrors
/// <c>Syncfusion.Blazor.Grids.RowSelectEventArgs&lt;TValue&gt;</c>.
///
/// <para><b>Status:</b> Type shipped so consumer handler signatures compile; functional
/// wiring from the grid wrapper to this type is incremental. See docs/compat-syncfusion-mapping.md.</para>
/// </summary>
public class RowSelectEventArgs<TValue>
{
    /// <summary>The selected row's data.</summary>
    public TValue? Data { get; init; }

    /// <summary>The rendered row element reference (Syncfusion publishes a weakly-typed row ref).</summary>
    public object? Row { get; init; }

    /// <summary>True when the event originated from a user interaction.</summary>
    public bool IsInteracted { get; init; }

    /// <summary>The row index (0-based).</summary>
    public int RowIndex { get; init; }
}

/// <summary>
/// Syncfusion-shaped cancellable pre-select event arguments. Mirrors
/// <c>Syncfusion.Blazor.Grids.RowSelectingEventArgs&lt;TValue&gt;</c>.
/// </summary>
public class RowSelectingEventArgs<TValue>
{
    /// <summary>The row data about to be selected.</summary>
    public TValue? Data { get; init; }

    /// <summary>The row index (0-based).</summary>
    public int RowIndex { get; init; }

    /// <summary>Cancel the selection.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// Syncfusion-shaped row-render event arguments. Mirrors
/// <c>Syncfusion.Blazor.Grids.RowDataBoundEventArgs&lt;TValue&gt;</c>.
/// </summary>
public class RowDataBoundEventArgs<TValue>
{
    /// <summary>The row data.</summary>
    public TValue? Data { get; init; }

    /// <summary>The rendered row element reference.</summary>
    public object? Row { get; init; }

    /// <summary>The row index.</summary>
    public int RowIndex { get; init; }
}
