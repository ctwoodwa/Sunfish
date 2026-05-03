namespace Sunfish.Compat.Syncfusion;

/// <summary>
/// Syncfusion-shaped grid-filter event arguments. Mirrors
/// <c>Syncfusion.Blazor.Grids.FilterEventArgs</c>.
/// </summary>
public class FilterEventArgs
{
    /// <summary>The column field being filtered.</summary>
    public string? ColumnName { get; init; }

    /// <summary>The filter operator (e.g. <c>"equal"</c>, <c>"contains"</c>).</summary>
    public string? Operator { get; init; }

    /// <summary>The filter value.</summary>
    public object? Value { get; init; }

    /// <summary>Cancel the filter change.</summary>
    public bool Cancel { get; set; }
}

/// <summary>
/// Syncfusion-shaped grid-sort event arguments. Mirrors
/// <c>Syncfusion.Blazor.Grids.SortEventArgs</c>.
/// </summary>
public class SortEventArgs
{
    /// <summary>The column field being sorted.</summary>
    public string? ColumnName { get; init; }

    /// <summary>The sort direction (as a string; Syncfusion uses the enum-name literal).</summary>
    public string? Direction { get; init; }

    /// <summary>Cancel the sort change.</summary>
    public bool Cancel { get; set; }
}
