namespace Sunfish.Core.Enums;

/// <summary>
/// Timeline zoom granularity for the AllocationScheduler.
/// Controls both the display grain and the authoritative editing level.
/// </summary>
public enum TimeGranularity
{
    Day,
    Week,
    Month,
    Quarter,
    Year
}

/// <summary>Whether allocation cells display hours or currency values.</summary>
public enum AllocationValueMode
{
    Hours,
    Currency
}

/// <summary>
/// Policy for distributing a higher-level period value into sub-bucket records.
/// Used by the AllocationScheduler context menu distribution commands.
/// </summary>
public enum DistributionMode
{
    /// <summary>Divide total evenly across sub-buckets.</summary>
    EvenSpread,

    /// <summary>Preserve relative weighting of existing values.</summary>
    ProportionalToExisting,

    /// <summary>Weight toward start of period.</summary>
    FrontLoaded,

    /// <summary>Weight toward end of period.</summary>
    BackLoaded,

    /// <summary>Weight by working days in each sub-bucket.</summary>
    WorkingDaysWeighted,

    /// <summary>Consumer-supplied via OnDistributeRequested.</summary>
    Custom
}

/// <summary>Cell selection behavior for the AllocationScheduler.</summary>
public enum AllocationSelectionMode
{
    /// <summary>No cell selection.</summary>
    None,

    /// <summary>Single cell selection.</summary>
    Cell,

    /// <summary>Range (rectangle) selection via click-drag or Shift+click.</summary>
    Range
}

/// <summary>How variance between actuals and targets is displayed.</summary>
public enum DeltaDisplayMode
{
    /// <summary>Show delta as an absolute value (e.g., +8h, -$2,400).</summary>
    Value,

    /// <summary>Show delta as a percentage (e.g., +20%).</summary>
    Percentage,

    /// <summary>Show a status icon (over, under, on-target).</summary>
    StatusIcon
}

/// <summary>The unit of an allocation value -- hours or currency.</summary>
public enum AllocationUnit
{
    Hours,
    Currency
}
