using System.Reflection;
using Microsoft.AspNetCore.Components;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay.Pivot;

/// <summary>
/// Cross-tab (pivot) grid that groups a flat data source by one or more row and
/// column fields, computes measure aggregates, and renders the result as a 2-D table.
/// </summary>
/// <remarks>
/// This MVP surface (ADR 0022 canonical example) uses server-side reflection to
/// read field values, executes <c>GroupBy → Aggregate</c> in memory, and renders
/// a static <c>&lt;table&gt;</c>. Virtualization, cell merging, named ranges,
/// and export are deferred.
/// </remarks>
/// <typeparam name="TItem">Element type of the <see cref="Data"/> source.</typeparam>
public partial class SunfishPivotGrid<TItem> : SunfishComponentBase
{
    /// <summary>The flat data source that will be cross-tabulated.</summary>
    [Parameter] public IEnumerable<TItem>? Data { get; set; }

    /// <summary>Field names (on <typeparamref name="TItem"/>) that become row group headers.</summary>
    [Parameter] public List<string> RowFields { get; set; } = new();

    /// <summary>Field names (on <typeparamref name="TItem"/>) that become column group headers.</summary>
    [Parameter] public List<string> ColumnFields { get; set; } = new();

    /// <summary>Measures (value cells) to render. Each measure becomes one row/column stripe.</summary>
    [Parameter] public List<PivotMeasure> Measures { get; set; } = new();

    /// <summary>When <c>true</c>, adds grand total row and column. Defaults to <c>true</c>.</summary>
    [Parameter] public bool ShowGrandTotals { get; set; } = true;

    /// <summary>When <c>true</c>, adds subtotal rows/columns for nested groups. Defaults to <c>true</c>.</summary>
    [Parameter] public bool ShowSubTotals { get; set; } = true;

    /// <summary>Optional CSS height of the pivot grid container.</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>Optional CSS width of the pivot grid container.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Optional ARIA label for the pivot grid. Defaults to <c>"Pivot grid"</c>.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    // ── Computed state ─────────────────────────────────────────────────

    /// <summary>Row group key tuples (one key per row field). Ordered.</summary>
    protected List<string[]> RowKeys { get; private set; } = new();

    /// <summary>Column group key tuples (one key per column field). Ordered.</summary>
    protected List<string[]> ColumnKeys { get; private set; } = new();

    /// <summary>Computed aggregate table keyed on (rowKeyTuple, colKeyTuple, measureField).</summary>
    protected Dictionary<(string row, string col, string measure), PivotCellValue> Cells { get; private set; }
        = new();

    /// <summary>Gets the effective measures — an implicit <c>Count</c> measure is used if none were supplied.</summary>
    protected List<PivotMeasure> EffectiveMeasures
        => Measures.Count > 0
            ? Measures
            : new List<PivotMeasure> { new() { Field = "*", Aggregation = PivotAggregation.Count, DisplayName = "Count" } };

    /// <inheritdoc />
    protected override void OnParametersSet()
    {
        BuildPivot();
    }

    /// <summary>
    /// Forces the pivot grid to re-read its data source and recompute aggregates.
    /// </summary>
    public async Task Rebind()
    {
        BuildPivot();
        await InvokeAsync(StateHasChanged);
    }

    /// <summary>Builds the sorted row/column keys and the aggregate cache.</summary>
    private void BuildPivot()
    {
        RowKeys = new List<string[]>();
        ColumnKeys = new List<string[]>();
        Cells = new Dictionary<(string, string, string), PivotCellValue>();

        if (Data is null) return;

        var rowFields = RowFields ?? new List<string>();
        var colFields = ColumnFields ?? new List<string>();
        var measures = EffectiveMeasures;

        var data = Data.ToList();
        if (data.Count == 0) return;

        // Build row/column key sets and a GroupBy bucket of contributing items.
        var rowKeySet = new HashSet<string>();
        var colKeySet = new HashSet<string>();
        var rowKeyList = new List<string[]>();
        var colKeyList = new List<string[]>();
        var buckets = new Dictionary<(string rowKey, string colKey), List<TItem>>();

        foreach (var item in data)
        {
            var rowTuple = rowFields.Select(f => ReadField(item, f) ?? "(empty)").ToArray();
            var colTuple = colFields.Select(f => ReadField(item, f) ?? "(empty)").ToArray();

            var rowKey = string.Join("", rowTuple);
            var colKey = string.Join("", colTuple);

            if (rowKeySet.Add(rowKey)) rowKeyList.Add(rowTuple);
            if (colKeySet.Add(colKey)) colKeyList.Add(colTuple);

            var bucketKey = (rowKey, colKey);
            if (!buckets.TryGetValue(bucketKey, out var bucket))
            {
                bucket = new List<TItem>();
                buckets[bucketKey] = bucket;
            }
            bucket.Add(item);
        }

        rowKeyList.Sort(CompareKeyTuples);
        colKeyList.Sort(CompareKeyTuples);
        RowKeys = rowKeyList;
        ColumnKeys = colKeyList;

        foreach (var measure in measures)
        {
            foreach (var rowTuple in rowKeyList)
            {
                var rowKey = string.Join("", rowTuple);

                foreach (var colTuple in colKeyList)
                {
                    var colKey = string.Join("", colTuple);
                    if (!buckets.TryGetValue((rowKey, colKey), out var bucket))
                    {
                        continue;
                    }
                    var cell = ComputeCell(bucket, measure, isTotal: false);
                    Cells[(rowKey, colKey, measure.Field)] = cell;
                }

                if (ShowGrandTotals)
                {
                    var rowBucket = buckets
                        .Where(kv => kv.Key.rowKey == rowKey)
                        .SelectMany(kv => kv.Value)
                        .ToList();
                    Cells[(rowKey, GrandTotalKey, measure.Field)] = ComputeCell(rowBucket, measure, isTotal: true);
                }
            }

            if (ShowGrandTotals)
            {
                foreach (var colTuple in colKeyList)
                {
                    var colKey = string.Join("", colTuple);
                    var colBucket = buckets
                        .Where(kv => kv.Key.colKey == colKey)
                        .SelectMany(kv => kv.Value)
                        .ToList();
                    Cells[(GrandTotalKey, colKey, measure.Field)] = ComputeCell(colBucket, measure, isTotal: true);
                }

                var all = buckets.SelectMany(kv => kv.Value).ToList();
                Cells[(GrandTotalKey, GrandTotalKey, measure.Field)] = ComputeCell(all, measure, isTotal: true);
            }
        }
    }

    private static int CompareKeyTuples(string[] a, string[] b)
    {
        var len = Math.Min(a.Length, b.Length);
        for (var i = 0; i < len; i++)
        {
            var cmp = string.Compare(a[i], b[i], StringComparison.OrdinalIgnoreCase);
            if (cmp != 0) return cmp;
        }
        return a.Length.CompareTo(b.Length);
    }

    private PivotCellValue ComputeCell(List<TItem> bucket, PivotMeasure measure, bool isTotal)
    {
        if (bucket.Count == 0)
        {
            return new PivotCellValue { Value = null, Formatted = "-", IsGrandTotal = isTotal };
        }

        double? aggregated = measure.Aggregation switch
        {
            PivotAggregation.Count => bucket.Count,
            PivotAggregation.CountDistinct => bucket
                .Select(x => ReadField(x, measure.Field) ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .Count(),
            _ => AggregateNumeric(bucket, measure),
        };

        return new PivotCellValue
        {
            Value = aggregated,
            Formatted = FormatValue(aggregated, measure),
            IsGrandTotal = isTotal,
        };
    }

    private static double? AggregateNumeric(List<TItem> bucket, PivotMeasure measure)
    {
        var values = bucket
            .Select(item => TryReadNumeric(item, measure.Field))
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToList();

        if (values.Count == 0) return null;

        return measure.Aggregation switch
        {
            PivotAggregation.Sum => values.Sum(),
            PivotAggregation.Average => values.Average(),
            PivotAggregation.Min => values.Min(),
            PivotAggregation.Max => values.Max(),
            _ => values.Sum(),
        };
    }

    private static string FormatValue(double? value, PivotMeasure measure)
    {
        if (!value.HasValue) return "-";

        if (!string.IsNullOrEmpty(measure.Format))
        {
            return string.Format($"{{0:{measure.Format}}}", value.Value);
        }

        return value.Value == Math.Floor(value.Value)
            ? value.Value.ToString("N0")
            : value.Value.ToString("N2");
    }

    private static string? ReadField(TItem? item, string fieldName)
    {
        if (item is null || string.IsNullOrEmpty(fieldName) || fieldName == "*") return null;
        var type = item.GetType();
        var prop = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is not null)
        {
            return prop.GetValue(item)?.ToString();
        }
        var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        return field?.GetValue(item)?.ToString();
    }

    private static double? TryReadNumeric(TItem? item, string fieldName)
    {
        if (item is null) return null;
        var type = item.GetType();
        object? raw = null;
        var prop = type.GetProperty(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (prop is not null)
        {
            raw = prop.GetValue(item);
        }
        else
        {
            var field = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            raw = field?.GetValue(item);
        }

        return raw switch
        {
            null => null,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            short s => s,
            decimal dec => (double)dec,
            _ => double.TryParse(raw.ToString(), out var parsed) ? parsed : null,
        };
    }

    /// <summary>Constant sentinel key used for the grand-total row/column bucket.</summary>
    protected const string GrandTotalKey = "__sf_grand_total__";

    /// <summary>Produces the string label displayed for a row or column total key.</summary>
    protected static string TotalLabel => "Total";

    /// <summary>Gets the composed size style for the outer container.</summary>
    protected string SizeStyle()
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(Width)) parts.Add($"width:{Width}");
        if (!string.IsNullOrEmpty(Height)) parts.Add($"height:{Height};overflow:auto");
        return string.Join(";", parts);
    }

    /// <summary>Joins a row or column tuple into the storage key used for lookups.</summary>
    protected static string ComposeKey(string[] tuple) => string.Join("", tuple);

    /// <summary>Gets the cell value for a given row tuple, column tuple, and measure field.</summary>
    protected PivotCellValue? GetCell(string[] rowTuple, string[] colTuple, PivotMeasure measure)
    {
        var key = (ComposeKey(rowTuple), ComposeKey(colTuple), measure.Field);
        return Cells.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>Gets the grand-total row cell for a given column tuple + measure.</summary>
    protected PivotCellValue? GetColumnTotal(string[] colTuple, PivotMeasure measure)
    {
        var key = (GrandTotalKey, ComposeKey(colTuple), measure.Field);
        return Cells.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>Gets the grand-total column cell for a given row tuple + measure.</summary>
    protected PivotCellValue? GetRowTotal(string[] rowTuple, PivotMeasure measure)
    {
        var key = (ComposeKey(rowTuple), GrandTotalKey, measure.Field);
        return Cells.TryGetValue(key, out var value) ? value : null;
    }

    /// <summary>Gets the grand-total intersection cell for a measure.</summary>
    protected PivotCellValue? GetGrandTotal(PivotMeasure measure)
    {
        var key = (GrandTotalKey, GrandTotalKey, measure.Field);
        return Cells.TryGetValue(key, out var value) ? value : null;
    }
}
