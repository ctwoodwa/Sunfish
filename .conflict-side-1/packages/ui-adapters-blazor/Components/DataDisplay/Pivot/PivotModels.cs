using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay.Pivot;

/// <summary>
/// Describes a single measure on a <c>SunfishPivotGrid</c>: the source field name,
/// the aggregation to apply, an optional display name for the column header, and
/// an optional .NET format string for the rendered value.
/// </summary>
public sealed class PivotMeasure
{
    /// <summary>Name of the source field on the data item (resolved via reflection).</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Aggregation function applied to values in the measure cross-tab.</summary>
    public PivotAggregation Aggregation { get; set; } = PivotAggregation.Sum;

    /// <summary>Friendly header shown for this measure. Falls back to <see cref="Field"/>.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// .NET format string applied to aggregated values
    /// (for example <c>N2</c>, <c>C0</c>, <c>0.00%</c>). Falls back to an automatic format.
    /// </summary>
    public string? Format { get; set; }

    /// <summary>Gets the header text shown for this measure.</summary>
    public string EffectiveHeader => string.IsNullOrEmpty(DisplayName) ? Field : DisplayName;
}

/// <summary>
/// Computed cross-tab cell value returned by <c>SunfishPivotGrid</c>'s internal builder.
/// </summary>
public sealed class PivotCellValue
{
    /// <summary>The aggregated numeric value, or <c>null</c> when no rows contributed.</summary>
    public double? Value { get; set; }

    /// <summary>Pre-formatted string used by the default cell template.</summary>
    public string Formatted { get; set; } = "-";

    /// <summary>True when the cell represents a grand total row/column.</summary>
    public bool IsGrandTotal { get; set; }

    /// <summary>True when the cell represents a subtotal row/column.</summary>
    public bool IsSubTotal { get; set; }
}
