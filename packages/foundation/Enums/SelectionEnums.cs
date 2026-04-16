namespace Sunfish.Core.Enums;

/// <summary>
/// Specifies the filter mode used by a ComboBox when the user types.
/// </summary>
public enum ComboBoxFilterMode
{
    /// <summary>Items whose text contains the filter value.</summary>
    Contains,

    /// <summary>Items whose text starts with the filter value.</summary>
    StartsWith,

    /// <summary>Items whose text ends with the filter value.</summary>
    EndsWith
}

/// <summary>
/// Controls how selected items are displayed as tags in a MultiSelect.
/// </summary>
public enum MultiSelectTagMode
{
    /// <summary>Each selected item is shown as an individual tag.</summary>
    Multiple,

    /// <summary>A single summary tag shows the count of selected items.</summary>
    Single
}

/// <summary>
/// Specifies the preferred position of a dropdown popup relative to its anchor.
/// </summary>
public enum DropdownPopupPosition
{
    /// <summary>Popup appears below the anchor (default).</summary>
    Bottom,

    /// <summary>Popup appears above the anchor.</summary>
    Top,

    /// <summary>Popup position is determined automatically based on available space.</summary>
    Auto
}
