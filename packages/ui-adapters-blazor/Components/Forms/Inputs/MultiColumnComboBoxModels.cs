using Microsoft.AspNetCore.Components;

namespace Sunfish.UIAdapters.Blazor.Components.Forms.Inputs;

/// <summary>
/// Column configuration for <c>SunfishMultiColumnComboBox</c>'s popup table.
/// </summary>
public class ComboBoxColumn
{
    /// <summary>Property path on the row model to bind (e.g. <c>"Department"</c>).</summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>Header caption. When null/empty, <see cref="Field"/> is used.</summary>
    public string? Title { get; set; }

    /// <summary>CSS width for the column (e.g. <c>"120px"</c> or <c>"20%"</c>). Optional.</summary>
    public string? Width { get; set; }

    /// <summary>Optional template rendered in the cell. When null the raw field value is shown.</summary>
    public RenderFragment<object>? Template { get; set; }
}

/// <summary>
/// Event args for the <c>SunfishMultiColumnComboBox.OnRead</c> callback. The handler
/// is expected to populate <see cref="Data"/> (and optionally <see cref="Total"/>) based
/// on <see cref="Filter"/>, <see cref="Skip"/>, and <see cref="Take"/>.
/// </summary>
/// <typeparam name="TItem">The row type.</typeparam>
public class MultiColumnComboBoxReadEventArgs<TItem>
{
    /// <summary>Current input filter text.</summary>
    public string? Filter { get; set; }

    /// <summary>Zero-based offset for paging.</summary>
    public int Skip { get; set; }

    /// <summary>Page size. 0 indicates no paging.</summary>
    public int Take { get; set; }

    /// <summary>Handler output: data for the current page.</summary>
    public IEnumerable<TItem> Data { get; set; } = Array.Empty<TItem>();

    /// <summary>Handler output: total count across all pages (for virtualization).</summary>
    public int Total { get; set; }
}
