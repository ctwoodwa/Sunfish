namespace Sunfish.Foundation.Enums;

/// <summary>
/// Selection behavior for <see cref="ListBoxSelectionMode"/>-aware components
/// (e.g., <c>SunfishListBox&lt;TItem&gt;</c>).
/// </summary>
public enum ListBoxSelectionMode
{
    /// <summary>Only one item may be selected at a time.</summary>
    Single,

    /// <summary>Multiple items may be selected (toggle semantics on click).</summary>
    Multiple,

    /// <summary>Items cannot be selected. The component behaves as a read-only display.</summary>
    None
}
