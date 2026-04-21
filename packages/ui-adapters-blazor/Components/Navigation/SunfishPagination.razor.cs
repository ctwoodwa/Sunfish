using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Sunfish.UIAdapters.Blazor.Base;
using Sunfish.UIAdapters.Blazor.Enums;

namespace Sunfish.UIAdapters.Blazor.Components.Navigation;

/// <summary>
/// Framework-agnostic page navigator. Exposes numeric page buttons with ellipsis
/// collapsing, optional first/previous/next/last arrows, page-size selector, page
/// info text, refresh button, and three page-selector rendering modes
/// (<see cref="PagerInputType.Buttons"/>, <see cref="PagerInputType.Dropdown"/>,
/// <see cref="PagerInputType.Input"/>).
///
/// <para>
/// The DataGrid / ListView composition layer consumes this component; the
/// historical binding surface (<c>Page</c>, <c>PageChanged</c>, <c>Total</c>,
/// <c>PageSize</c>, <c>PageSizeChanged</c>, <c>PageSizes</c>, <c>ButtonCount</c>,
/// <c>ShowInfo</c>, <c>AriaLabel</c>) is preserved verbatim for backward-compat.
/// </para>
/// </summary>
public partial class SunfishPagination : SunfishComponentBase
{
    // ─────────────────────────── Paging state ───────────────────────────

    /// <summary>
    /// The current 1-based page number. Supports two-way binding via
    /// <see cref="PageChanged"/>.
    /// </summary>
    [Parameter] public int Page { get; set; } = 1;

    /// <summary>Fires when the current page changes.</summary>
    [Parameter] public EventCallback<int> PageChanged { get; set; }

    /// <summary>The total number of items across all pages.</summary>
    [Parameter] public int Total { get; set; }

    /// <summary>
    /// The number of items per page. Default is <c>10</c>. Supports two-way
    /// binding via <see cref="PageSizeChanged"/>.
    /// </summary>
    [Parameter] public int PageSize { get; set; } = 10;

    /// <summary>Fires when the page-size selector changes.</summary>
    [Parameter] public EventCallback<int> PageSizeChanged { get; set; }

    /// <summary>
    /// Controls the page-size selector. Accepts either a concrete
    /// <see cref="IList{T}"/> of <see cref="int"/> (shows those sizes) or a
    /// <see cref="bool"/> flag (<c>true</c> shows the default size list). When
    /// <c>null</c> / <c>false</c> / empty the selector is hidden.
    /// </summary>
    [Parameter] public object? PageSizes { get; set; }

    // ────────────────────────── Button-mode config ───────────────────────

    /// <summary>
    /// Maximum number of numeric page buttons rendered before the page window
    /// collapses with an ellipsis. Default is <c>10</c>.
    /// </summary>
    [Parameter] public int ButtonCount { get; set; } = 10;

    // ────────────────────────── Feature toggles ──────────────────────────

    /// <summary>
    /// Shows the "start-end of total items" info string when <c>true</c>.
    /// </summary>
    [Parameter] public bool Info { get; set; }

    /// <summary>
    /// Legacy alias for <see cref="Info"/> retained for backward compatibility
    /// with the previous 1.x surface. If either is <c>true</c> the info string
    /// is shown.
    /// </summary>
    [Parameter] public bool ShowInfo { get; set; }

    /// <summary>
    /// Selects how the page is rendered — numeric button bar, native dropdown,
    /// or numeric input. Default is <see cref="PagerInputType.Buttons"/>.
    /// </summary>
    [Parameter] public PagerInputType InputType { get; set; } = PagerInputType.Buttons;

    /// <summary>Shows previous / next arrow buttons when <c>true</c> (default).</summary>
    [Parameter] public bool PreviousNext { get; set; } = true;

    /// <summary>Shows first / last arrow buttons when <c>true</c> (default).</summary>
    [Parameter] public bool FirstLast { get; set; } = true;

    /// <summary>
    /// When <c>true</c> the nav element is focusable and responds to
    /// <kbd>ArrowLeft</kbd> / <kbd>ArrowRight</kbd> / <kbd>Home</kbd> /
    /// <kbd>End</kbd> to change pages.
    /// </summary>
    [Parameter] public bool Navigatable { get; set; }

    /// <summary>
    /// Shows a refresh button that re-fires <see cref="PageChanged"/> and
    /// <see cref="OnRefresh"/> without changing the current page.
    /// </summary>
    [Parameter] public bool RefreshButton { get; set; }

    /// <summary>Fires when the refresh button is clicked.</summary>
    [Parameter] public EventCallback OnRefresh { get; set; }

    /// <summary>Accessible label for the pagination <c>&lt;nav&gt;</c>.</summary>
    [Parameter] public string AriaLabel { get; set; } = "pagination";

    // ────────────────────────── Computed state ───────────────────────────

    /// <summary>Total number of pages given <see cref="Total"/> / <see cref="PageSize"/>.</summary>
    public int ComputedTotalPages =>
        PageSize > 0 ? Math.Max(1, (int)Math.Ceiling((double)Total / PageSize)) : 1;

    private bool ResolvedShowInfo => Info || ShowInfo;

    private static readonly int[] DefaultPageSizes = { 5, 10, 25, 50, 100 };

    /// <summary>
    /// Resolves the <see cref="PageSizes"/> parameter (which accepts either a
    /// list or a <see cref="bool"/>) to a concrete list of integers, or
    /// <c>null</c> if the selector should be hidden.
    /// </summary>
    private IList<int>? ResolvedPageSizes
    {
        get
        {
            switch (PageSizes)
            {
                case null:
                    return null;
                case bool enabled:
                    return enabled ? DefaultPageSizes : null;
                case IList<int> list:
                    return list;
                case IEnumerable<int> seq:
                    return seq.ToList();
                default:
                    return null;
            }
        }
    }

    // ────────────────────────── Rendering helpers ────────────────────────

    /// <summary>
    /// Represents either a numeric page button or an ellipsis placeholder in
    /// the button-mode render path.
    /// </summary>
    internal readonly struct ButtonSlot
    {
        public ButtonSlot(int page, bool isEllipsis)
        {
            Page = page;
            IsEllipsis = isEllipsis;
        }

        public int Page { get; }
        public bool IsEllipsis { get; }

        public static ButtonSlot ForPage(int page) => new(page, false);
        public static ButtonSlot Ellipsis() => new(0, true);
    }

    /// <summary>
    /// Produces the slot sequence for button mode. When the total page count
    /// exceeds <see cref="ButtonCount"/> the window collapses around the
    /// current page with ellipsis placeholders on one or both sides, always
    /// pinning page 1 and the last page.
    /// </summary>
    internal IEnumerable<ButtonSlot> GetButtonSlots()
    {
        var total = ComputedTotalPages;
        var max = Math.Max(1, ButtonCount);

        if (total <= max)
        {
            for (var i = 1; i <= total; i++)
                yield return ButtonSlot.ForPage(i);
            yield break;
        }

        // Reserve two slots for the pinned first/last pages.
        var innerSlots = Math.Max(1, max - 2);
        var half = innerSlots / 2;

        var start = Math.Max(2, Page - half);
        var end = Math.Min(total - 1, start + innerSlots - 1);
        start = Math.Max(2, end - innerSlots + 1);

        yield return ButtonSlot.ForPage(1);

        if (start > 2)
            yield return ButtonSlot.Ellipsis();

        for (var i = start; i <= end; i++)
            yield return ButtonSlot.ForPage(i);

        if (end < total - 1)
            yield return ButtonSlot.Ellipsis();

        yield return ButtonSlot.ForPage(total);
    }

    private string GetInfoText()
    {
        if (Total <= 0) return "0 items";
        var start = (Page - 1) * PageSize + 1;
        var end = Math.Min(Page * PageSize, Total);
        return $"{start}-{end} of {Total} items";
    }

    // ────────────────────────── Event handlers ───────────────────────────

    internal async Task GoToPage(int page)
    {
        page = Math.Clamp(page, 1, ComputedTotalPages);
        if (page != Page)
        {
            Page = page;
            await PageChanged.InvokeAsync(page);
        }
    }

    private Task GoToFirst() => GoToPage(1);
    private Task GoToLast() => GoToPage(ComputedTotalPages);
    private Task PreviousPage() => GoToPage(Page - 1);
    private Task NextPage() => GoToPage(Page + 1);

    private async Task HandlePageSizeChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var newSize) && newSize > 0)
        {
            PageSize = newSize;
            await PageSizeChanged.InvokeAsync(newSize);
            // Reset to page 1 when page size changes — keeps the spec contract that
            // "PageSizeChanged fires, then PageChanged fires with 1" for listeners
            // that rebuild the current slice.
            await GoToPage(1);
        }
    }

    private async Task HandlePageSelectChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var newPage))
            await GoToPage(newPage);
    }

    private async Task HandlePageInputChange(ChangeEventArgs e)
    {
        if (int.TryParse(e.Value?.ToString(), out var newPage))
            await GoToPage(newPage);
    }

    private async Task HandleRefresh()
    {
        await OnRefresh.InvokeAsync();
        await PageChanged.InvokeAsync(Page);
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (!Navigatable) return;

        switch (e.Key)
        {
            case "ArrowLeft":
                await PreviousPage();
                break;
            case "ArrowRight":
                await NextPage();
                break;
            case "Home":
                await GoToFirst();
                break;
            case "End":
                await GoToLast();
                break;
        }
    }
}
