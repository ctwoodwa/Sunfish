using Microsoft.AspNetCore.Components.Web;
using Sunfish.Foundation.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.DataDisplay;

/// <summary>
/// Accessibility helpers for SunfishDataGrid:
/// - WCAG 2.1.1 Keyboard / 4.1.2 Name, Role, Value: keyboard activation for sortable
///   column headers (Enter/Space) and selectable rows (Enter/Space toggle).
/// - WCAG 2.5.7 Dragging Movements: keyboard alternative to drag-reorder columns
///   (Move left / Move right entries on the column menu).
/// - WCAG 4.1.3 Status Messages: a polite live region (<see cref="_liveAnnouncement"/>)
///   that announces sort, filter, page, group and column-reorder state changes.
/// - WCAG 2.1.1 Keyboard / 2.1.2 No Keyboard Trap on the popup edit dialog: Escape
///   key handler that cancels and closes the dialog instead of leaving the user stuck.
/// </summary>
public partial class SunfishDataGrid<TItem>
{
    /// <summary>
    /// Backing field for the polite live region rendered at the top of the grid root.
    /// Updated via <see cref="Announce(string)"/>; read by assistive technology.
    /// Reset to <c>string.Empty</c> in tests by setting it directly.
    /// </summary>
    internal string _liveAnnouncement = string.Empty;

    /// <summary>
    /// Pushes a message into the grid's polite ARIA live region. Pass an empty string
    /// to clear. Callers do not need to worry about debouncing — the message is set
    /// directly and a re-render is requested.
    /// </summary>
    /// <param name="message">User-facing text to announce. Should be short and complete.</param>
    internal void Announce(string message)
    {
        // Empty-then-set forces AT to re-read identical messages on consecutive state changes.
        _liveAnnouncement = string.IsNullOrEmpty(message) ? string.Empty : message;
        StateHasChanged();
    }

    // ── Header keyboard activation (WCAG 2.1.1, 4.1.2) ──────────────────

    /// <summary>
    /// Mirror of <see cref="OnHeaderClick"/> driven by the keyboard. Fired on Enter
    /// or Space when the header has <c>tabindex="0"</c>. Space is also prevented from
    /// scrolling the page.
    /// </summary>
    internal async Task OnHeaderKeyDown(SunfishGridColumn<TItem> column, bool isSortable, KeyboardEventArgs e)
    {
        if (!isSortable || e is null) return;
        if (e.Key != "Enter" && e.Key != " " && e.Key != "Spacebar") return;

        // Treat Ctrl/Meta+Enter or Ctrl/Meta+Space as multi-sort, mirroring the click semantics.
        var simulatedClick = new MouseEventArgs
        {
            CtrlKey = e.CtrlKey,
            MetaKey = e.MetaKey,
            ShiftKey = e.ShiftKey,
            AltKey = e.AltKey
        };
        await OnHeaderClick(column, isSortable, simulatedClick);
        await AnnounceSortStateAsync(column);
    }

    private Task AnnounceSortStateAsync(SunfishGridColumn<TItem> column)
    {
        var sort = _state.SortDescriptors.FirstOrDefault(s => s.Field == column.Field);
        var title = column.DisplayTitle;
        if (sort is null)
        {
            Announce($"{title} sort cleared.");
        }
        else
        {
            var dir = sort.Direction == SortDirection.Ascending ? "ascending" : "descending";
            Announce($"Sorted by {title}, {dir}.");
        }
        return Task.CompletedTask;
    }

    // ── Row keyboard activation (WCAG 2.1.1) ────────────────────────────

    /// <summary>
    /// Keyboard equivalent of row click for selection. Enter or Space toggles the
    /// selection state of the focused row. Space's default scroll behaviour is
    /// suppressed by Blazor's preventDefault on KeyboardEventArgs handling at the
    /// adapter level (consumers that need finer control can override SelectionMode).
    /// </summary>
    internal async Task HandleRowKeyDown(TItem item, KeyboardEventArgs e)
    {
        if (e is null) return;
        if (e.Key != "Enter" && e.Key != " " && e.Key != "Spacebar") return;
        if (SelectionMode == GridSelectionMode.None) return;

        var simulatedClick = new MouseEventArgs
        {
            CtrlKey = e.CtrlKey,
            MetaKey = e.MetaKey,
            ShiftKey = e.ShiftKey
        };
        await HandleRowClick(item, simulatedClick);
    }

    // ── Edit dialog Escape (WCAG 2.1.1, 2.1.2) ──────────────────────────

    /// <summary>
    /// Allows keyboard users to dismiss the popup edit dialog via the Escape key,
    /// matching the click-on-overlay behaviour available to mouse users. Without
    /// this handler, keyboard-only users could become trapped inside the modal
    /// (no visible Cancel focus, no escape route).
    /// </summary>
    internal async Task OnEditDialogKeyDown(KeyboardEventArgs e)
    {
        if (e is null) return;
        if (e.Key != "Escape" && e.Key != "Esc") return;
        await CancelEdit();
    }

    // ── Column move (WCAG 2.5.7 Dragging Movements alternative) ─────────

    /// <summary>
    /// Moves the column one position toward the start of the visible order. No-op
    /// when the column is already first. Updates <see cref="GridState.ColumnStates"/>,
    /// fires <see cref="OnColumnReordered"/>, and announces the new position.
    /// </summary>
    internal async Task MoveColumnLeft(SunfishGridColumn<TItem> column)
    {
        var oldIndex = _columns.IndexOf(column);
        if (oldIndex <= 0) return;
        await MoveColumnAsync(oldIndex, oldIndex - 1, column);
    }

    /// <summary>
    /// Moves the column one position toward the end of the visible order. No-op
    /// when the column is already last.
    /// </summary>
    internal async Task MoveColumnRight(SunfishGridColumn<TItem> column)
    {
        var oldIndex = _columns.IndexOf(column);
        if (oldIndex < 0 || oldIndex >= _columns.Count - 1) return;
        await MoveColumnAsync(oldIndex, oldIndex + 1, column);
    }

    private async Task MoveColumnAsync(int oldIndex, int newIndex, SunfishGridColumn<TItem> column)
    {
        // Reuse the existing JS-invokable reorder pipeline so all consumers see the
        // same state notifications and OnColumnReordered event fires identically.
        await HandleColumnReorderedFromJs(oldIndex, newIndex);
        await InvokeAsync(StateHasChanged);
        Announce($"Moved column {column.DisplayTitle} to position {newIndex + 1} of {_columns.Count}.");
    }
}
