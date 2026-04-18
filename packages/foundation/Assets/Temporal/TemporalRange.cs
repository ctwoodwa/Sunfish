namespace Sunfish.Foundation.Assets.Temporal;

/// <summary>
/// Half-open temporal validity range: <c>[ValidFrom, ValidTo)</c>. A <c>null</c> upper bound
/// means the range is open to the future ("currently valid").
/// </summary>
public readonly record struct TemporalRange(DateTimeOffset ValidFrom, DateTimeOffset? ValidTo)
{
    /// <summary>
    /// Returns <c>true</c> when <paramref name="at"/> falls in <c>[ValidFrom, ValidTo)</c>.
    /// </summary>
    public bool IsValidAt(DateTimeOffset at)
        => ValidFrom <= at && (ValidTo is null || at < ValidTo);

    /// <summary>
    /// Returns <c>true</c> when this range overlaps with <paramref name="other"/>.
    /// </summary>
    public bool OverlapsWith(TemporalRange other)
    {
        var aEnd = ValidTo ?? DateTimeOffset.MaxValue;
        var bEnd = other.ValidTo ?? DateTimeOffset.MaxValue;
        return ValidFrom < bEnd && other.ValidFrom < aEnd;
    }

    /// <summary>A range that is always valid.</summary>
    public static TemporalRange Forever => new(DateTimeOffset.MinValue, null);
}
