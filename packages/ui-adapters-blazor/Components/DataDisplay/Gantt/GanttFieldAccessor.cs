using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Reflection-based field accessor for SunfishGantt&lt;TItem&gt;. Caches PropertyInfo lookups
/// per (TItem, field-name) so per-render access is cheap.
/// </summary>
internal sealed class GanttFieldAccessor<TItem> where TItem : class
{
    private static readonly ConcurrentDictionary<string, PropertyInfo?> _cache = new();

    public string IdField { get; }
    public string ParentIdField { get; }
    public string TitleField { get; }
    public string StartField { get; }
    public string EndField { get; }
    public string PercentCompleteField { get; }
    public string DependsOnField { get; }

    private readonly string? _itemsField;
    private readonly string? _hasChildrenField;

    public GanttFieldAccessor(
        string idField,
        string parentIdField,
        string titleField,
        string startField,
        string endField,
        string percentCompleteField,
        string dependsOnField,
        string? itemsField = null,
        string? hasChildrenField = null)
    {
        IdField = idField;
        ParentIdField = parentIdField;
        TitleField = titleField;
        StartField = startField;
        EndField = endField;
        PercentCompleteField = percentCompleteField;
        DependsOnField = dependsOnField;
        _itemsField = itemsField;
        _hasChildrenField = hasChildrenField;
    }

    private PropertyInfo? GetProp(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        return _cache.GetOrAdd(name, static n =>
            typeof(TItem).GetProperty(n, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase));
    }

    private object? Read(TItem item, string name)
        => item is null ? null : GetProp(name)?.GetValue(item);

    private void Write(TItem item, string name, object? value)
    {
        if (item is null || string.IsNullOrEmpty(name)) return;
        var prop = GetProp(name);
        if (prop is null || !prop.CanWrite) return;
        prop.SetValue(item, Convert.ChangeType(value, prop.PropertyType));
    }

    public void SetStart(TItem item, DateTime value) => Write(item, StartField, value);
    public void SetEnd(TItem item, DateTime value) => Write(item, EndField, value);
    public void SetPercentComplete(TItem item, double value) => Write(item, PercentCompleteField, value);

    /// <summary>
    /// Sets an arbitrary field value on the item using reflection.
    /// Handles nullable types and type conversion via Convert.ChangeType.
    /// </summary>
    public void SetFieldValue(TItem item, string field, object? value)
    {
        var prop = GetProp(field);
        if (prop?.CanWrite != true) return;
        var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
        var converted = value is null ? null : Convert.ChangeType(value, targetType);
        prop.SetValue(item, converted);
    }

    /// <summary>Public cached field lookup for use by GanttColumn.</summary>
    public object? GetFieldValue(TItem item, string fieldName)
    {
        if (item is null || string.IsNullOrEmpty(fieldName)) return null;
        var prop = GetProp(fieldName);
        return prop?.GetValue(item);
    }

    public object? GetId(TItem item) => Read(item, IdField);
    public object? GetParentId(TItem item) => Read(item, ParentIdField);

    public string GetTitle(TItem item) => Read(item, TitleField)?.ToString() ?? string.Empty;

    public DateTime GetStart(TItem item) => ToDateTime(Read(item, StartField));
    public DateTime GetEnd(TItem item) => ToDateTime(Read(item, EndField));

    public double GetPercentComplete(TItem item)
    {
        var v = Read(item, PercentCompleteField);
        if (v is null) return 0;
        try { return Convert.ToDouble(v); }
        catch { return 0; }
    }

    public IEnumerable<object>? GetDependsOn(TItem item)
    {
        var v = Read(item, DependsOnField);
        if (v is null) return null;
        if (v is IEnumerable e && v is not string)
        {
            var list = new List<object>();
            foreach (var o in e) if (o is not null) list.Add(o);
            return list;
        }
        return null;
    }

    public IEnumerable<TItem>? GetItems(TItem item)
    {
        if (_itemsField is null) return null;
        var val = GetFieldValue(item, _itemsField);
        return val as IEnumerable<TItem>;
    }

    public bool GetHasChildren(TItem item)
    {
        if (_hasChildrenField is null) return false;
        var val = GetFieldValue(item, _hasChildrenField);
        return val is true;
    }

    private static DateTime ToDateTime(object? v)
    {
        if (v is null) return default;
        if (v is DateTime dt) return dt;
        if (v is DateTimeOffset dto) return dto.DateTime;
        if (DateTime.TryParse(v.ToString(), out var parsed)) return parsed;
        return default;
    }
}
