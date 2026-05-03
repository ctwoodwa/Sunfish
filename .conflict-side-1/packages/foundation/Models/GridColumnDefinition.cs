namespace Sunfish.Foundation.Models;

/// <summary>
/// Defines the configuration for a data grid column, used when columns
/// are specified programmatically rather than declaratively.
/// </summary>
public class GridColumnDefinition
{
    /// <summary>
    /// The property name on the data item that this column displays.
    /// </summary>
    public string Field { get; set; } = string.Empty;

    /// <summary>
    /// The display title shown in the column header. Defaults to <see cref="Field"/> if not set.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// The width of the column (e.g., "200px", "20%"). Null uses auto-sizing.
    /// </summary>
    public string? Width { get; set; }

    /// <summary>
    /// Whether the column can be sorted by clicking the header.
    /// </summary>
    public bool Sortable { get; set; } = true;

    /// <summary>
    /// Whether the column can be filtered.
    /// </summary>
    public bool Filterable { get; set; } = true;

    /// <summary>
    /// Whether the column is visible. Hidden columns are not rendered.
    /// </summary>
    public bool Visible { get; set; } = true;

    /// <summary>
    /// Whether the column can be resized by dragging the header border.
    /// </summary>
    public bool Resizable { get; set; }

    /// <summary>
    /// A format string applied to the cell value (e.g., "{0:C2}" for currency).
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// The CSS text-align value for the column content (e.g., "left", "center", "right").
    /// </summary>
    public string? TextAlign { get; set; }
}
