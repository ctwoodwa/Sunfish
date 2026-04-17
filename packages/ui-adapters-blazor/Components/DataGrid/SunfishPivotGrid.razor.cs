using Sunfish.Foundation.Base;
using Sunfish.Foundation.Models;
using Microsoft.AspNetCore.Components;

namespace Sunfish.Components.Blazor.Components.DataGrid;

public partial class SunfishPivotGrid : IPivotGridFieldHost
{
    /// <summary>Data source for the pivot grid.</summary>
    [Parameter] public IEnumerable<object>? Data { get; set; }

    /// <summary>
    /// POCO-based row field definitions. Prefer SunfishPivotGridRowField child components instead.
    /// When both child fields and this parameter are provided, child fields take precedence.
    /// </summary>
    [Obsolete("Use SunfishPivotGridRowField child components instead.")]
    [Parameter] public List<PivotGridField> RowFields { get; set; } = new();

    /// <summary>
    /// POCO-based column field definitions. Prefer SunfishPivotGridColumnField child components instead.
    /// When both child fields and this parameter are provided, child fields take precedence.
    /// </summary>
    [Obsolete("Use SunfishPivotGridColumnField child components instead.")]
    [Parameter] public List<PivotGridField> ColumnFields { get; set; } = new();

    /// <summary>
    /// POCO-based measure field definitions. Prefer SunfishPivotGridMeasureField child components instead.
    /// When both child fields and this parameter are provided, child fields take precedence.
    /// </summary>
    [Obsolete("Use SunfishPivotGridMeasureField child components instead.")]
    [Parameter] public List<PivotGridField> MeasureFields { get; set; } = new();

    /// <summary>Aggregate function applied to measure values (used with legacy parameters only).</summary>
    [Parameter] public PivotGridAggregateFunction AggregateFunction { get; set; } = PivotGridAggregateFunction.Sum;

    /// <summary>CSS height of the pivot grid container.</summary>
    [Parameter] public string? Height { get; set; }

    /// <summary>CSS width of the pivot grid container.</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Whether column headers are sortable.</summary>
    [Parameter] public bool Sortable { get; set; }

    /// <summary>Whether filtering is enabled.</summary>
    [Parameter] public bool Filterable { get; set; }

    /// <summary>Accessible label for the pivot grid container. Defaults to "Pivot Grid".</summary>
    [Parameter] public string? AriaLabel { get; set; }

    /// <summary>Child content that accepts SunfishPivotGridRowField, SunfishPivotGridColumnField, and SunfishPivotGridMeasureField components.</summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Custom template for rendering data cells. When set, overrides default value rendering.
    /// The context provides RowKey, ColumnKey, Value, AggregateFunction, MeasureField, and FormattedValue.
    /// </summary>
    [Parameter] public RenderFragment<PivotGridCellContext>? CellTemplate { get; set; }

    /// <summary>
    /// Custom template for rendering row header cells. The context is the row dimension value string.
    /// </summary>
    [Parameter] public RenderFragment<string>? RowHeaderTemplate { get; set; }

    /// <summary>
    /// Custom template for rendering column header cells. The context is the column dimension value string.
    /// </summary>
    [Parameter] public RenderFragment<string>? ColumnHeaderTemplate { get; set; }

    // ── Child field registration ─────────────────��──────────────────────

    private readonly List<SunfishPivotGridRowField> _registeredRowFields = new();
    private readonly List<SunfishPivotGridColumnField> _registeredColumnFields = new();
    private readonly List<SunfishPivotGridMeasureField> _registeredMeasureFields = new();

    void IPivotGridFieldHost.RegisterRowField(SunfishPivotGridRowField field)
    {
        if (!_registeredRowFields.Contains(field))
        {
            _registeredRowFields.Add(field);
            BuildPivot();
            InvokeAsync(StateHasChanged);
        }
    }

    void IPivotGridFieldHost.UnregisterRowField(SunfishPivotGridRowField field)
    {
        if (_registeredRowFields.Remove(field))
        {
            BuildPivot();
            InvokeAsync(StateHasChanged);
        }
    }

    void IPivotGridFieldHost.RegisterColumnField(SunfishPivotGridColumnField field)
    {
        if (!_registeredColumnFields.Contains(field))
        {
            _registeredColumnFields.Add(field);
            BuildPivot();
            InvokeAsync(StateHasChanged);
        }
    }

    void IPivotGridFieldHost.UnregisterColumnField(SunfishPivotGridColumnField field)
    {
        if (_registeredColumnFields.Remove(field))
        {
            BuildPivot();
            InvokeAsync(StateHasChanged);
        }
    }

    void IPivotGridFieldHost.RegisterMeasureField(SunfishPivotGridMeasureField field)
    {
        if (!_registeredMeasureFields.Contains(field))
        {
            _registeredMeasureFields.Add(field);
            BuildPivot();
            InvokeAsync(StateHasChanged);
        }
    }

    void IPivotGridFieldHost.UnregisterMeasureField(SunfishPivotGridMeasureField field)
    {
        if (_registeredMeasureFields.Remove(field))
        {
            BuildPivot();
            InvokeAsync(StateHasChanged);
        }
    }

    // ── Effective field lists ─────────────────────���─────────────────────

#pragma warning disable CS0618 // Obsolete usage is intentional for backward compat

    /// <summary>
    /// Returns the effective row fields. Child-component fields take precedence
    /// over the old POCO RowFields parameter.
    /// </summary>
    internal List<PivotGridField> GetEffectiveRowFields() =>
        _registeredRowFields.Count > 0
            ? _registeredRowFields.Select(f => new PivotGridField { Name = f.Field, Title = f.Title }).ToList()
            : RowFields;

    /// <summary>
    /// Returns the effective column fields. Child-component fields take precedence.
    /// </summary>
    internal List<PivotGridField> GetEffectiveColumnFields() =>
        _registeredColumnFields.Count > 0
            ? _registeredColumnFields.Select(f => new PivotGridField { Name = f.Field, Title = f.Title }).ToList()
            : ColumnFields;

    /// <summary>
    /// Returns the effective measure fields. Child-component fields take precedence.
    /// </summary>
    internal List<PivotGridField> GetEffectiveMeasureFields() =>
        _registeredMeasureFields.Count > 0
            ? _registeredMeasureFields.Select(f => new PivotGridField { Name = f.Field, Title = f.Title }).ToList()
            : MeasureFields;

    /// <summary>
    /// Returns the effective aggregate function. When child measure fields are registered,
    /// uses the first measure field's AggregateFunction; otherwise falls back to the parameter.
    /// </summary>
    internal PivotGridAggregateFunction GetEffectiveAggregateFunction() =>
        _registeredMeasureFields.Count > 0
            ? _registeredMeasureFields.First().AggregateFunction
            : AggregateFunction;

#pragma warning restore CS0618

    // ── Public methods ────────────────────────────────────────────────

    /// <summary>
    /// Forces the pivot grid to re-read its data source and recompute aggregates.
    /// Call this method after programmatically changing the <see cref="Data"/> collection
    /// without replacing its object reference.
    /// </summary>
    public async Task Rebind()
    {
        BuildPivot();
        await InvokeAsync(StateHasChanged);
    }

    //── Pivot computation ─────────────────���─────────────────────────────

    internal List<string> _effectiveRowKeys = new();
    internal List<string> _effectiveColumnKeys = new();
    internal Dictionary<(string row, string col), double> _aggregates = new();

    internal string SizeStyle()
    {
        var parts = new List<string>();
        if (Width != null) parts.Add($"width:{Width}");
        if (Height != null) parts.Add($"height:{Height}");
        return string.Join(";", parts);
    }

    protected override void OnParametersSet()
    {
        BuildPivot();
    }

    private void BuildPivot()
    {
        _effectiveRowKeys.Clear();
        _effectiveColumnKeys.Clear();
        _aggregates.Clear();

        var rowFields = GetEffectiveRowFields();
        var colFields = GetEffectiveColumnFields();
        var measureFields = GetEffectiveMeasureFields();

        if (Data == null || !rowFields.Any() || !colFields.Any() || !measureFields.Any())
            return;

        var dataList = Data.ToList();
        if (dataList.Count == 0) return;

        var rowField = rowFields.First();
        var colField = colFields.First();
        var measureField = measureFields.First();

        var grouped = new Dictionary<(string row, string col), List<double>>();

        foreach (var item in dataList)
        {
            string rowVal = GetPropertyValue(item, rowField.Name) ?? "(empty)";
            string colVal = GetPropertyValue(item, colField.Name) ?? "(empty)";
            var measureVal = GetNumericValue(item, measureField.Name);

            if (!_effectiveRowKeys.Contains(rowVal)) _effectiveRowKeys.Add(rowVal);
            if (!_effectiveColumnKeys.Contains(colVal)) _effectiveColumnKeys.Add(colVal);

            var key = (rowVal, colVal);
            if (!grouped.ContainsKey(key)) grouped[key] = new List<double>();
            grouped[key].Add(measureVal);
        }

        if (Sortable)
        {
            _effectiveRowKeys.Sort();
            _effectiveColumnKeys.Sort();
        }

        var aggFunc = GetEffectiveAggregateFunction();
        foreach (var kvp in grouped)
        {
            _aggregates[kvp.Key] = Aggregate(kvp.Value, aggFunc);
        }
    }

    private static double Aggregate(List<double> values, PivotGridAggregateFunction func) => func switch
    {
        PivotGridAggregateFunction.Sum => values.Sum(),
        PivotGridAggregateFunction.Count => values.Count,
        PivotGridAggregateFunction.Average => values.Average(),
        PivotGridAggregateFunction.Min => values.Min(),
        PivotGridAggregateFunction.Max => values.Max(),
        _ => values.Sum()
    };

    internal string FormatValue(double value)
    {
        // Use the first registered measure field's Format if available
        var format = _registeredMeasureFields.Count > 0
            ? _registeredMeasureFields.First().Format
            : null;

        if (!string.IsNullOrEmpty(format))
            return string.Format($"{{0:{format}}}", value);

        return value == Math.Floor(value) ? value.ToString("N0") : value.ToString("N2");
    }

    internal PivotGridCellContext BuildCellContext(string rowKey, string colKey, double? value)
    {
        var measureFields = GetEffectiveMeasureFields();
        var measureName = measureFields.Count > 0 ? measureFields.First().Name : "";
        var aggFunc = GetEffectiveAggregateFunction();
        var formatted = value.HasValue ? FormatValue(value.Value) : "-";

        return new PivotGridCellContext
        {
            RowKey = rowKey,
            ColumnKey = colKey,
            Value = value.HasValue ? (object)value.Value : null,
            AggregateFunction = aggFunc,
            MeasureField = measureName,
            FormattedValue = formatted
        };
    }

    private static string? GetPropertyValue(object item, string propertyName)
    {
        var prop = item.GetType().GetProperty(propertyName);
        return prop?.GetValue(item)?.ToString();
    }

    private static double GetNumericValue(object item, string propertyName)
    {
        var prop = item.GetType().GetProperty(propertyName);
        var val = prop?.GetValue(item);
        if (val is double d) return d;
        if (val is int i) return i;
        if (val is decimal dec) return (double)dec;
        if (val is float f) return f;
        if (val is long l) return l;
        if (double.TryParse(val?.ToString(), out var parsed)) return parsed;
        return 0;
    }
}
