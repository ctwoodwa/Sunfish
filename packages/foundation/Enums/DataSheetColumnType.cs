namespace Sunfish.Core.Enums;

/// <summary>
/// Defines the editor type for a column in SunfishDataSheet.
/// </summary>
public enum DataSheetColumnType
{
    /// <summary>Free-text input.</summary>
    Text,

    /// <summary>Numeric input with step support.</summary>
    Number,

    /// <summary>Date picker input.</summary>
    Date,

    /// <summary>Dropdown select from a list of options.</summary>
    Select,

    /// <summary>Boolean checkbox toggle.</summary>
    Checkbox,

    /// <summary>Display-only computed value; never enters edit mode.</summary>
    Computed
}
