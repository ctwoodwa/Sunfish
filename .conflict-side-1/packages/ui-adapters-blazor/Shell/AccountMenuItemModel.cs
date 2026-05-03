namespace Sunfish.UIAdapters.Blazor.Shell;

/// <summary>
/// Runtime model for a single rendered account-menu entry, including identity,
/// presentation, navigation, hierarchy (for submenus), and state flags.
/// </summary>
/// <remarks>
/// This is the rendered model — distinct from <see cref="AccountMenuItemOptions"/>,
/// which is the configuration shape consumers populate. The shell maps options to
/// models when assembling the menu.
/// </remarks>
public class AccountMenuItemModel
{
    /// <summary>Stable identifier used for keyed rendering and test selectors.</summary>
    public string Id { get; set; } = "";

    /// <summary>Primary display label.</summary>
    public string Label { get; set; } = "";

    /// <summary>Icon name (provider-resolved) rendered alongside the label.</summary>
    public string Icon { get; set; } = "";

    /// <summary>Optional subtitle rendered below or after the label.</summary>
    public string? SecondaryText { get; set; }

    /// <summary>Optional shortcut hint (for example, <c>Ctrl+,</c>) rendered to the right of the label.</summary>
    public string? ShortcutText { get; set; }

    /// <summary>Optional secondary value rendered on the right edge (for example, the active language).</summary>
    public string? RightValueText { get; set; }

    /// <summary>Optional badge text (for example, "New" or a count) rendered on the row.</summary>
    public string? Badge { get; set; }

    /// <summary>Optional navigation target. When set, activating the item navigates here.</summary>
    public string? Href { get; set; }

    /// <summary>Optional click handler. Invoked on activation; typically used in addition to or instead of <see cref="Href"/>.</summary>
    public Func<Task>? Action { get; set; }

    /// <summary>Child items for submenu entries. <see langword="null"/> for leaf items.</summary>
    public List<AccountMenuItemModel>? Children { get; set; }

    /// <summary>Whether the item is rendered. Hidden items are skipped during render.</summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>Whether the item is rendered in a disabled (non-interactive) state.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Whether the item is styled as destructive (for example, "Sign out", "Delete account").</summary>
    public bool IsDestructive { get; set; }

    /// <summary>Whether the item is rendered as currently selected (for example, the active language inside a submenu).</summary>
    public bool IsSelected { get; set; }

    /// <summary>Whether this item opens a submenu (true) or is a leaf entry (false).</summary>
    public bool IsSubmenu { get; set; }

    /// <summary>Sort order within its group; lower values render first.</summary>
    public int SortOrder { get; set; }

    /// <summary>Optional <c>data-testid</c> attribute for end-to-end tests.</summary>
    public string? TestId { get; set; }
}
