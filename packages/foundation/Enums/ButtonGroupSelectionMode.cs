namespace Sunfish.Foundation.Enums;

/// <summary>
/// Specifies how <see cref="Sunfish.Foundation.Enums.ButtonGroupSelectionMode"/> toggle-button
/// children of a <c>SunfishButtonGroup</c> coordinate their <c>Selected</c> state.
/// </summary>
/// <remarks>
/// Matches the Sunfish Blazor spec for <c>ButtonGroup</c>
/// (<see href="slug:buttongroup-selection"/>). Only <c>ButtonGroupToggleButton</c> children
/// participate in selection; regular <c>ButtonGroupButton</c> children always ignore it.
/// </remarks>
public enum ButtonGroupSelectionMode
{
    /// <summary>Selection is disabled. Toggle buttons manage their own state independently.</summary>
    None,

    /// <summary>
    /// Radio-like: clicking a toggle button selects it and deselects every other toggle button
    /// in the same group. This is the spec default.
    /// </summary>
    Single,

    /// <summary>
    /// Checkbox-like: clicking a toggle button flips its own <c>Selected</c> state without
    /// affecting peers. Multiple toggle buttons can be selected at once.
    /// </summary>
    Multiple
}
