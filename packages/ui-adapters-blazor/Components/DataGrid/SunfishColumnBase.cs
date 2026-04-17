using Sunfish.Foundation.Base;
using Sunfish.Components.Blazor.Base;
using Sunfish.Foundation.Models;
using Microsoft.AspNetCore.Components;

namespace Sunfish.Components.Blazor.Components.DataGrid;

/// <summary>
/// Minimal column descriptor interface used internally by components that need
/// to consume columns from both child-component and legacy POCO sources.
/// </summary>
internal interface IColumnDescriptor
{
    string Field { get; }
    string DisplayTitle { get; }
    string? Width { get; }
    bool Visible { get; }
    string GetDisplayValue(object? item);
}

/// <summary>
/// Abstract base class for declarative column components that register with a parent
/// host via CascadingParameter. Encapsulates the common column metadata shared across
/// SunfishGridColumn, SunfishTreeListColumn, and future column types.
/// </summary>
public abstract class SunfishColumnBase : SunfishComponentBase, IColumnDescriptor
{
    /// <summary>The property name on TItem to bind to this column.</summary>
    [Parameter] public string Field { get; set; } = "";

    /// <summary>Header text. Defaults to Field if not set.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>CSS width for the column (e.g. "200px", "25%").</summary>
    [Parameter] public string? Width { get; set; }

    /// <summary>Whether this column is visible. Defaults to true.</summary>
    [Parameter] public bool Visible { get; set; } = true;

    /// <summary>Additional CSS class for the column cells.</summary>
    [Parameter] public string? ColumnClass { get; set; }

    /// <summary>Additional CSS class for the header cell.</summary>
    [Parameter] public string? HeaderClass { get; set; }

    /// <summary>Whether this column is sortable. Null means inherit from the parent component.</summary>
    [Parameter] public bool? Sortable { get; set; }

    /// <summary>Whether this column is filterable. Null means inherit from the parent component.</summary>
    [Parameter] public bool? Filterable { get; set; }

    /// <summary>Gets the display title for the column header.</summary>
    public string DisplayTitle => Title ?? Field;

    /// <summary>
    /// Gets the value of this column's field from the given item via reflection.
    /// </summary>
    public object? GetFieldValue(object? item)
    {
        if (item is null || string.IsNullOrEmpty(Field)) return null;
        return item.GetType().GetProperty(Field)?.GetValue(item);
    }

    /// <summary>
    /// Gets the formatted display string for the given item (base implementation uses ToString).
    /// Override in derived classes for richer formatting.
    /// </summary>
    public virtual string GetDisplayValue(object? item)
    {
        var value = GetFieldValue(item);
        return value?.ToString() ?? string.Empty;
    }
}

/// <summary>
/// Lightweight column descriptor used internally when mapping legacy POCO
/// <see cref="TreeListColumn"/> instances to the unified column model.
/// Not a Blazor component â€” just carries metadata.
/// </summary>
#pragma warning disable CS0618 // Obsolete usage is intentional for backward compat
internal sealed class LegacyColumnAdapter : IColumnDescriptor
{
    public string Field { get; }
    public string? Title { get; }
    public string? Width { get; }
    public bool Visible => true;
    public string DisplayTitle => Title ?? Field;

    public LegacyColumnAdapter(TreeListColumn poco)
    {
        Field = poco.Field;
        Title = poco.Title;
        Width = poco.Width;
    }

    public string GetDisplayValue(object? item)
    {
        if (item is null || string.IsNullOrEmpty(Field)) return string.Empty;
        return item.GetType().GetProperty(Field)?.GetValue(item)?.ToString() ?? string.Empty;
    }
}
#pragma warning restore CS0618
