using System.Globalization;
using Sunfish.UIAdapters.Blazor.Components.DataDisplay;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Resolves explicit widths (or defaults) into a shared immutable contract.
/// </summary>
public sealed class FixedWidthProvider : IColumnWidthProvider
{
    // Unspecified columns should remain auto-sized so the fixed-layout table can fill the container.
    public string DefaultWidth { get; init; } = "auto";

    public GridLayoutContract Resolve(IReadOnlyList<ColumnSizingEntry> columns)
    {
        var widthById = new Dictionary<string, string>(columns.Count);
        var orderedIds = new List<string>(columns.Count);

        foreach (var column in columns)
        {
            var raw = string.IsNullOrWhiteSpace(column.ExplicitWidth) ? DefaultWidth : column.ExplicitWidth!;
            var normalized = NormalizeAndClamp(raw, column.MinWidth, column.MaxWidth);

            widthById[column.Id] = normalized;
            orderedIds.Add(column.Id);
        }

        var template = string.Join(" ", orderedIds.Select(id => widthById[id]));

        // Compute frozen column offsets
        var frozenOffsets = new Dictionary<string, double>();
        var frozenColumnIds = new HashSet<string>();
        var frozenPositions = new Dictionary<string, GridColumnFrozenPosition>();

        var frozenColumns = columns.Where(c => c.Locked).ToList();
        if (frozenColumns.Count > 0)
        {
            // Start-frozen columns: cumulative left offset
            var startFrozen = frozenColumns
                .Where(c => c.FrozenPosition == GridColumnFrozenPosition.Start)
                .ToList();
            double startOffset = 0;
            foreach (var col in startFrozen)
            {
                frozenColumnIds.Add(col.Id);
                frozenPositions[col.Id] = GridColumnFrozenPosition.Start;
                frozenOffsets[col.Id] = startOffset;
                startOffset += GetPixelWidth(widthById[col.Id]);
            }

            // End-frozen columns: cumulative right offset (from rightmost inward).
            // Sort by column position descending so the rightmost column gets offset 0
            // regardless of the order in which FrozenPosition == End was declared.
            var columnIndex = columns
                .Select((c, i) => (c.Id, i))
                .ToDictionary(x => x.Id, x => x.i);
            var endFrozen = frozenColumns
                .Where(c => c.FrozenPosition == GridColumnFrozenPosition.End)
                .OrderByDescending(c => columnIndex.GetValueOrDefault(c.Id, 0))  // rightmost first
                .ToList();
            double endOffset = 0;
            foreach (var col in endFrozen)
            {
                frozenColumnIds.Add(col.Id);
                frozenPositions[col.Id] = GridColumnFrozenPosition.End;
                frozenOffsets[col.Id] = endOffset;
                endOffset += GetPixelWidth(widthById[col.Id]);
            }
        }

        return new GridLayoutContract
        {
            WidthById = widthById,
            OrderedColumnIds = orderedIds,
            CenterGridTemplate = template,
            FrozenOffsets = frozenOffsets,
            FrozenColumnIds = frozenColumnIds,
            FrozenPositions = frozenPositions
        };
    }

    private static string NormalizeAndClamp(string width, double min, double? max)
    {
        if (TryParsePixelWidth(width, out var px))
        {
            var clamped = max.HasValue ? Math.Clamp(px, min, max.Value) : Math.Max(px, min);
            return $"{clamped.ToString("0.###", CultureInfo.InvariantCulture)}px";
        }

        if (double.TryParse(width, NumberStyles.Float, CultureInfo.InvariantCulture, out var numberOnly))
        {
            var clamped = max.HasValue ? Math.Clamp(numberOnly, min, max.Value) : Math.Max(numberOnly, min);
            return $"{clamped.ToString("0.###", CultureInfo.InvariantCulture)}px";
        }

        return width;
    }

    /// <summary>
    /// Extracts a pixel value from a resolved width string. Falls back to 150px for non-pixel widths
    /// (frozen columns require a concrete pixel offset).
    /// </summary>
    private static double GetPixelWidth(string width)
    {
        return TryParsePixelWidth(width, out var px) ? px : 150;
    }

    private static bool TryParsePixelWidth(string width, out double value)
    {
        value = 0;
        var trimmed = width.Trim();
        if (!trimmed.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var numeric = trimmed[..^2].Trim();
        return double.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
