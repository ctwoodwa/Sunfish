namespace Sunfish.Foundation.Enums;

/// <summary>
/// Represents the current state of a cell in SunfishDataSheet.
/// </summary>
public enum CellState
{
    /// <summary>Cell value has not been modified.</summary>
    Pristine,

    /// <summary>Cell value has been changed but not yet saved.</summary>
    Dirty,

    /// <summary>Cell has a validation error.</summary>
    Invalid,

    /// <summary>Cell is part of a save operation in progress.</summary>
    Saving,

    /// <summary>Cell was recently saved successfully.</summary>
    Saved
}
