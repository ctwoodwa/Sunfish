namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Aggregation function applied to a <c>SunfishPivotGrid</c> measure when
/// cross-tabulating values across row and column dimensions.
/// </summary>
public enum PivotAggregation
{
    /// <summary>Sum of numeric measure values (default).</summary>
    Sum,

    /// <summary>Arithmetic mean of numeric measure values.</summary>
    Average,

    /// <summary>Count of rows contributing to the cell (non-null values).</summary>
    Count,

    /// <summary>Minimum of numeric measure values.</summary>
    Min,

    /// <summary>Maximum of numeric measure values.</summary>
    Max,

    /// <summary>Count of distinct measure values contributing to the cell.</summary>
    CountDistinct,
}
