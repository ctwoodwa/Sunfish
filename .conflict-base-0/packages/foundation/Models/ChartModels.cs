using Sunfish.Foundation.Enums;

namespace Sunfish.Foundation.Models;

/// <summary>
/// Framework-agnostic descriptor for a single chart series. Used by
/// polymorphic chart surfaces that accept a <c>Series</c> collection in
/// place of declarative <c>&lt;SunfishChartSeries&gt;</c> child components.
/// </summary>
/// <typeparam name="TItem">Element type of the data collection.</typeparam>
/// <remarks>
/// The existing Blazor adapter also supports a child-component pattern
/// (<c>SunfishChartSeries</c>). This descriptor is the MVP code-first shape
/// that adapters can consume without reflection overhead when the caller
/// prefers a strongly-typed, code-only configuration.
/// </remarks>
public class ChartSeriesDescriptor<TItem>
{
    /// <summary>Display name for the series (shown in legend and tooltips).</summary>
    public string Name { get; set; } = "Series";

    /// <summary>The chart type for this series (line, column, bar, pie, area, scatter, etc.).</summary>
    public ChartSeriesType Type { get; set; } = ChartSeriesType.Line;

    /// <summary>The data source for this series.</summary>
    public IEnumerable<TItem> Data { get; set; } = Enumerable.Empty<TItem>();

    /// <summary>
    /// Selector that returns the numeric value (Y axis) for a data item.
    /// Mutually exclusive with <see cref="Field"/>: use either a selector
    /// (strongly typed) or a property name (reflection).
    /// </summary>
    public Func<TItem, double>? ValueSelector { get; set; }

    /// <summary>
    /// Selector that returns the category label (X axis) for a data item.
    /// Mutually exclusive with <see cref="CategoryField"/>.
    /// </summary>
    public Func<TItem, string>? CategorySelector { get; set; }

    /// <summary>Property name for the value (Y axis). Used when <see cref="ValueSelector"/> is null.</summary>
    public string? Field { get; set; }

    /// <summary>Property name for the category (X axis). Used when <see cref="CategorySelector"/> is null.</summary>
    public string? CategoryField { get; set; }

    /// <summary>Custom color for the series (CSS color).</summary>
    public string? Color { get; set; }

    /// <summary>Whether this series is visible.</summary>
    public bool Visible { get; set; } = true;
}
