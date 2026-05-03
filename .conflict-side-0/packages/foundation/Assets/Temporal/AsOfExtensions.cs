namespace Sunfish.Foundation.Assets.Temporal;

/// <summary>Helpers for as-of queries over <see cref="TemporalRange"/> sequences.</summary>
public static class AsOfExtensions
{
    /// <summary>
    /// Filters the sequence to items whose <see cref="TemporalRange"/> contains <paramref name="asOf"/>.
    /// </summary>
    public static IEnumerable<T> WhereValidAt<T>(
        this IEnumerable<T> source,
        DateTimeOffset asOf,
        Func<T, TemporalRange> rangeSelector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(rangeSelector);
        foreach (var item in source)
        {
            if (rangeSelector(item).IsValidAt(asOf)) yield return item;
        }
    }
}
