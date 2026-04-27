namespace Sunfish.UIAdapters.Blazor.Components.Charts;

/// <summary>
/// Represents a single data point extracted from a chart series.
/// </summary>
public class ChartDataPoint
{
    /// <summary>The categorical label for this point (for example, an axis tick label or series name).</summary>
    public string Category { get; init; } = "";

    /// <summary>The primary numeric value (for example, bar height, line Y for categorical X).</summary>
    public double Value { get; init; }

    /// <summary>The X coordinate for scatter / bubble / XY line series.</summary>
    public double X { get; init; }

    /// <summary>The Y coordinate for scatter / bubble / XY line series.</summary>
    public double Y { get; init; }

    /// <summary>The bubble radius for bubble-chart series. Zero when not applicable.</summary>
    public double BubbleSize { get; init; }

    /// <summary>The original data item this point was projected from. Useful for callbacks needing the source row.</summary>
    public object? DataItem { get; init; }

    /// <summary>The zero-based index of this point within its series.</summary>
    public int Index { get; init; }
}
