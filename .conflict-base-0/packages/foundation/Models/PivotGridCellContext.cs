namespace Sunfish.Foundation.Models;

/// <summary>
/// Context object passed to the PivotGrid CellTemplate for rendering custom data cell content.
/// </summary>
public class PivotGridCellContext
{
    /// <summary>The row dimension key value for this cell.</summary>
    public string RowKey { get; init; } = "";

    /// <summary>The column dimension key value for this cell.</summary>
    public string ColumnKey { get; init; } = "";

    /// <summary>The aggregated numeric value, or null if no data exists for this intersection.</summary>
    public object? Value { get; init; }

    /// <summary>The aggregate function used to compute the value.</summary>
    public PivotGridAggregateFunction AggregateFunction { get; init; }

    /// <summary>The name of the measure field.</summary>
    public string MeasureField { get; init; } = "";

    /// <summary>The pre-formatted string representation of the value.</summary>
    public string FormattedValue { get; init; } = "";
}
