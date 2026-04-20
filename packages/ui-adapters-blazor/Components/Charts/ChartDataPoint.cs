namespace Sunfish.UIAdapters.Blazor.Components.Charts;

/// <summary>
/// Represents a single data point extracted from a chart series.
/// </summary>
public class ChartDataPoint
{
    public string Category { get; init; } = "";
    public double Value { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double BubbleSize { get; init; }
    public object? DataItem { get; init; }
    public int Index { get; init; }
}
