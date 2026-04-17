namespace Sunfish.Foundation.Models;

/// <summary>
/// Defines a field used in the pivot grid for row, column, or measure grouping.
/// </summary>
public class PivotGridField
{
    /// <summary>The property name on the data item to read from.</summary>
    public string Name { get; set; } = "";

    /// <summary>Display title for the field header. Falls back to Name when null.</summary>
    public string? Title { get; set; }
}

/// <summary>
/// Aggregate functions supported by <see cref="PivotGridField"/> measures.
/// </summary>
public enum PivotGridAggregateFunction
{
    Sum,
    Count,
    Average,
    Min,
    Max
}
