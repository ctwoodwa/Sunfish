namespace Sunfish.Foundation.Enums;

/// <summary>
/// Specifies how the data grid enters edit mode.
/// </summary>
public enum GridEditMode
{
    /// <summary>Editing is disabled.</summary>
    None,

    /// <summary>A row enters edit mode inline within the grid.</summary>
    Inline,

    /// <summary>A popup dialog opens for editing the row.</summary>
    Popup,

    /// <summary>Individual cells can be edited directly.</summary>
    InCell
}

/// <summary>
/// Specifies the row selection mode of a data grid.
/// </summary>
public enum GridSelectionMode
{
    /// <summary>No row selection.</summary>
    None,

    /// <summary>Only one row can be selected at a time.</summary>
    Single,

    /// <summary>Multiple rows can be selected simultaneously.</summary>
    Multiple
}

/// <summary>
/// Specifies the filter display mode for a data grid.
/// </summary>
public enum GridFilterMode
{
    /// <summary>No filter UI is displayed.</summary>
    None,

    /// <summary>Filter inputs are displayed in each column header row.</summary>
    FilterRow,

    /// <summary>A filter menu is available via each column header.</summary>
    FilterMenu,

    /// <summary>A checkbox list of distinct values is available via each column header.</summary>
    CheckBoxList
}

/// <summary>
/// Specifies whether the grid allows single or multiple column sorting.
/// </summary>
public enum GridSortMode
{
    /// <summary>Only one column can be sorted at a time.</summary>
    Single,

    /// <summary>Multiple columns can be sorted simultaneously (Ctrl+Click).</summary>
    Multiple
}

/// <summary>
/// Specifies the unit of selection in a data grid.
/// </summary>
public enum GridSelectionUnit
{
    /// <summary>Selection operates on whole rows (default).</summary>
    Row,

    /// <summary>Selection operates on individual cells.</summary>
    Cell
}

/// <summary>
/// Controls the visual density of a data grid — affects font size, cell padding, and row height.
/// CSS class <c>mar-datagrid--size-{small|medium|large}</c> is applied to the grid's root element;
/// each tier defines <c>--mar-datagrid-font-size</c>, <c>--mar-datagrid-cell-padding</c>, and
/// <c>--mar-datagrid-row-height</c> CSS custom properties.
/// </summary>
public enum DataGridSize
{
    /// <summary>Compact layout: smaller font, tighter padding, shorter rows.</summary>
    Small,

    /// <summary>Standard layout (default).</summary>
    Medium,

    /// <summary>Spacious layout: larger font, more padding, taller rows.</summary>
    Large
}
