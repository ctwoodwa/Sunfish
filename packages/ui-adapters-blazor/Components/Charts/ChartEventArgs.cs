namespace Sunfish.Components.Blazor.Components.Charts;

/// <summary>
/// Event args for chart data point click events.
/// </summary>
public class ChartSeriesClickEventArgs
{
    /// <summary>The data item of the clicked point.</summary>
    public object? DataItem { get; init; }

    /// <summary>The category (label) of the clicked data point.</summary>
    public string? Category { get; init; }

    /// <summary>The value of the clicked data point.</summary>
    public double? Value { get; init; }

    /// <summary>For pie/donut charts, the percentage of the total.</summary>
    public double? Percentage { get; init; }

    /// <summary>The index of the clicked series.</summary>
    public int SeriesIndex { get; init; }

    /// <summary>The name of the clicked series.</summary>
    public string? SeriesName { get; init; }

    /// <summary>The color of the clicked series.</summary>
    public string? SeriesColor { get; init; }

    /// <summary>The index of the clicked data point within the series.</summary>
    public int CategoryIndex { get; init; }
}

/// <summary>
/// Event args for chart legend item click events.
/// </summary>
public class ChartLegendItemClickEventArgs
{
    /// <summary>The index of the clicked series.</summary>
    public int SeriesIndex { get; init; }

    /// <summary>The legend text.</summary>
    public string? Text { get; init; }

    /// <summary>For pie/donut, the data point index.</summary>
    public int PointIndex { get; init; }
}

/// <summary>
/// Legacy alias for backward compatibility.
/// </summary>
public class ChartClickEventArgs : ChartSeriesClickEventArgs { }

/// <summary>
/// Event args for chart render lifecycle events.
/// </summary>
public class ChartRenderEventArgs
{
    /// <summary>Chart width (CSS value).</summary>
    public string Width { get; init; } = "";

    /// <summary>Chart height (CSS value).</summary>
    public string Height { get; init; } = "";

    /// <summary>Number of visible series.</summary>
    public int SeriesCount { get; init; }

    /// <summary>Total data points across all visible series.</summary>
    public int TotalDataPoints { get; init; }
}

/// <summary>
/// Context provided to tooltip templates.
/// </summary>
public class ChartTooltipContext
{
    /// <summary>Name of the series.</summary>
    public string SeriesName { get; init; } = "";

    /// <summary>Category label of the data point.</summary>
    public string Category { get; init; } = "";

    /// <summary>Numeric value of the data point.</summary>
    public double Value { get; init; }

    /// <summary>Formatted value string.</summary>
    public string FormattedValue { get; init; } = "";

    /// <summary>Color of the series.</summary>
    public string Color { get; init; } = "";

    /// <summary>Original data item.</summary>
    public object? DataItem { get; init; }

    /// <summary>For pie/donut charts, the percentage of total.</summary>
    public double? Percentage { get; init; }
}
