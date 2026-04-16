namespace Sunfish.Core.Enums;

/// <summary>
/// Specifies the type of chart series visualization.
/// </summary>
public enum ChartSeriesType
{
    /// <summary>A line chart connecting data points.</summary>
    Line,

    /// <summary>A horizontal bar chart.</summary>
    Bar,

    /// <summary>A vertical column chart.</summary>
    Column,

    /// <summary>An area chart with filled region below the line.</summary>
    Area,

    /// <summary>A circular pie chart.</summary>
    Pie,

    /// <summary>A donut chart with a hollow center.</summary>
    Donut,

    /// <summary>A scatter plot of individual data points.</summary>
    Scatter,

    /// <summary>A scatter plot with connected lines.</summary>
    ScatterLine,

    /// <summary>A bubble chart (scatter with sized markers).</summary>
    Bubble
}

/// <summary>
/// Specifies the position of a chart legend or title.
/// </summary>
public enum ChartPosition
{
    Top,
    Bottom,
    Left,
    Right
}
