namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>Controls which filter UI is displayed in SunfishGantt.</summary>
public enum GanttFilterMode
{
    /// <summary>No filter UI is displayed.</summary>
    None,
    /// <summary>A filter input row is rendered below the column headers.</summary>
    FilterRow,
    /// <summary>Popup filter accessed via funnel icon in column headers.</summary>
    FilterMenu
}

/// <summary>Controls how the tree list enters edit mode.</summary>
public enum GanttTreeListEditMode
{
    /// <summary>Double-click a row to enter inline edit mode. This is the current default behavior.</summary>
    Inline,
    /// <summary>Click a cell to edit it. Tab moves to next editable cell. Enter commits.</summary>
    Incell,
    /// <summary>Click a cell to open a popup form containing all editable fields for the row.</summary>
    Popup
}

/// <summary>Where new items are inserted in the tree list.</summary>
public enum GanttNewRowPosition
{
    Top,
    Bottom
}

/// <summary>Controls how the column filter UI is presented.</summary>
public enum GanttColumnFilterType
{
    /// <summary>Text input filter (default).</summary>
    Text,
    /// <summary>Checkbox list of distinct values hosted in a side drawer.</summary>
    CheckboxList
}

/// <summary>Controls how checkbox filter UI is presented.</summary>
public enum GanttFilterPopupMode
{
    /// <summary>Filter appears in a side drawer (default, accessible).</summary>
    Drawer,
    /// <summary>Filter appears in an anchor-positioned popup.</summary>
    Popup
}

/// <summary>Override the automatic editor type for a column.</summary>
public enum GanttEditorType
{
    /// <summary>Single-line text input.</summary>
    TextBox,
    /// <summary>Multi-line text area.</summary>
    TextArea,
    /// <summary>Checkbox toggle.</summary>
    CheckBox,
    /// <summary>Date picker input.</summary>
    DatePicker,
    /// <summary>Numeric input.</summary>
    NumericTextBox
}

/// <summary>
/// Snapshot of SunfishGantt's internal state. Used with OnStateInit/OnStateChanged
/// for state persistence and restoration.
/// </summary>
public class GanttState<TItem> where TItem : class
{
    /// <summary>Current sort configuration. Null means no sort applied.</summary>
    public GanttSortDescriptor? SortDescriptor { get; set; }

    /// <summary>Current filter values keyed by column field name. Null means no filters.</summary>
    public Dictionary<string, string>? FilterValues { get; set; }

    /// <summary>IDs of currently expanded nodes. Null means use default expansion.</summary>
    public IReadOnlyCollection<object>? ExpandedItems { get; set; }

    /// <summary>Currently active timeline view.</summary>
    public GanttView? View { get; set; }

    /// <summary>The item currently being edited. Null if not in edit mode.</summary>
    public TItem? EditItem { get; set; }

    /// <summary>The original item values before editing started. For comparison/revert.</summary>
    public TItem? OriginalEditItem { get; set; }

    /// <summary>The item being inserted (new item). Null if not inserting.</summary>
    public TItem? InsertedItem { get; set; }

    /// <summary>For Incell editing: which field is being edited. Null for full-row edit or no active cell.</summary>
    public string? EditField { get; set; }

    /// <summary>The parent item when inserting a child. Null for root-level insert.</summary>
    public TItem? ParentItem { get; set; }

    /// <summary>Names of visible columns. Null means all columns visible (default).</summary>
    public IEnumerable<string>? VisibleColumns { get; set; }
}

/// <summary>Describes a single-column sort.</summary>
public class GanttSortDescriptor
{
    /// <summary>The field name to sort by.</summary>
    public string Field { get; set; } = "";

    /// <summary>True for ascending order, false for descending.</summary>
    public bool Ascending { get; set; } = true;
}

/// <summary>Event args for OnStateInit and OnStateChanged.</summary>
public class GanttStateEventArgs<TItem> where TItem : class
{
    /// <summary>The current state snapshot.</summary>
    public GanttState<TItem> State { get; set; } = new();

    /// <summary>
    /// Which property changed (for OnStateChanged). Values: "SortDescriptor", "FilterValues", "ExpandedItems", "View".
    /// Null for OnStateInit.
    /// </summary>
    public string? PropertyName { get; set; }
}
