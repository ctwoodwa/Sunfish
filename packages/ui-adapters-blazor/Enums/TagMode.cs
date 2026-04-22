namespace Sunfish.UIAdapters.Blazor.Enums;

/// <summary>
/// Controls how selected items are rendered as chips in the trigger of a
/// <see cref="Components.Forms.Inputs.SunfishMultiSelect{TItem, TValue}"/>.
/// Mirrors the Telerik MultiSelect <c>TagMode</c> surface.
/// </summary>
public enum TagMode
{
    /// <summary>
    /// Collapse all selected items into a single summary chip (e.g. "3 items"). Optionally
    /// replaced by <see cref="Components.Forms.Inputs.SunfishMultiSelect{TItem, TValue}.SummaryTagTemplate"/>
    /// or <see cref="Components.Forms.Inputs.SunfishMultiSelect{TItem, TValue}.ChipTemplate"/>.
    /// </summary>
    Single,

    /// <summary>
    /// Render one chip per selected value. This is the default.
    /// </summary>
    Multiple
}
