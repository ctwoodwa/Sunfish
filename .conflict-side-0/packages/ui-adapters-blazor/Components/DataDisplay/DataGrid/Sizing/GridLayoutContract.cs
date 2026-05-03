using Sunfish.UIAdapters.Blazor.Components.DataDisplay;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Shared width output consumed by header/body/footer renderers.
/// </summary>
public sealed class GridLayoutContract
{
    public static GridLayoutContract Empty { get; } = new();

    public IReadOnlyDictionary<string, string> WidthById { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<string> OrderedColumnIds { get; init; } = [];

    public string CenterGridTemplate { get; init; } = string.Empty;

    /// <summary>Cumulative pixel offset for each frozen column (left or right depending on FrozenPosition).</summary>
    public IReadOnlyDictionary<string, double> FrozenOffsets { get; init; } = new Dictionary<string, double>();

    /// <summary>Set of column IDs that are frozen.</summary>
    public IReadOnlySet<string> FrozenColumnIds { get; init; } = new HashSet<string>();

    /// <summary>Frozen positions by column ID.</summary>
    public IReadOnlyDictionary<string, GridColumnFrozenPosition> FrozenPositions { get; init; } = new Dictionary<string, GridColumnFrozenPosition>();
}
