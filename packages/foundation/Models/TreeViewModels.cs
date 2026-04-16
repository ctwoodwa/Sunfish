using Microsoft.AspNetCore.Components.Web;

namespace Sunfish.Foundation.Models;

/// <summary>
/// Event arguments for a tree view context menu (right-click) event.
/// </summary>
public class TreeItemContextMenuEventArgs
{
    /// <summary>The data item that was right-clicked.</summary>
    public object Item { get; set; } = default!;

    /// <summary>The ID of the right-clicked node.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>The mouse event args from the context menu event.</summary>
    public MouseEventArgs MouseEventArgs { get; set; } = default!;
}

/// <summary>
/// Event arguments for a tree view inline edit completion.
/// </summary>
public class TreeItemEditEventArgs
{
    /// <summary>The ID of the edited node.</summary>
    public string ItemId { get; set; } = string.Empty;

    /// <summary>The new text value after editing.</summary>
    public string NewText { get; set; } = string.Empty;
}

/// <summary>
/// Context passed to a custom checkbox template in a tree view.
/// </summary>
public class CheckboxContext
{
    /// <summary>Whether the checkbox is checked.</summary>
    public bool Checked { get; set; }

    /// <summary>Whether the checkbox is in an indeterminate (mixed) state.</summary>
    public bool Indeterminate { get; set; }

    /// <summary>Whether the checkbox is disabled.</summary>
    public bool Disabled { get; set; }

    /// <summary>Callback to invoke when the checkbox value changes.</summary>
    public Action<bool> OnChange { get; set; } = _ => { };
}
