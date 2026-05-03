namespace Sunfish.Foundation.Models.DataSheet;

/// <summary>
/// A value/label pair used by Select-type columns in SunfishDataSheet.
/// </summary>
public class DataSheetSelectOption
{
    /// <summary>The stored value.</summary>
    public string Value { get; init; } = "";

    /// <summary>The display label shown in the dropdown.</summary>
    public string Label { get; init; } = "";
}
