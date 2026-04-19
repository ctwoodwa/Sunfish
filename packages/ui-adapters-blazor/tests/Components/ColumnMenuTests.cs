using Bunit;
using Bunit.TestDoubles;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Sunfish.Components.Blazor.Components.DataDisplay;
using Sunfish.Components.Blazor.Internal.Interop;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.Components.Blazor.Tests.Components;

/// <summary>
/// bUnit tests for G37 C3 — SunfishDataGrid column menu.
/// Covers: trigger button visibility, menu open/close, menu item visibility based on
/// Sortable/Filterable/Lockable, sort actions, lock toggle, and single-open invariant.
/// </summary>
public class ColumnMenuTests : BunitContext
{
    // ── Test model ─────────────────────────────────────────────────────────

    private sealed record Employee(int Id, string Name, string Department);

    // ── Constructor / DI setup ─────────────────────────────────────────────

    public ColumnMenuTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<IDownloadService, StubDownloadService>();

        // Loose mode: bUnit auto-satisfies import() / attachGrid calls.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    // ── Test data helpers ──────────────────────────────────────────────────

    private static List<Employee> TwoEmployees() =>
    [
        new(1, "Alice", "Engineering"),
        new(2, "Bob",   "Design"),
    ];

    /// <summary>One column with ShowColumnMenu=false (default).</summary>
    private static RenderFragment OneColumnNoMenu() => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Employee>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.Title), "Name");
        builder.CloseComponent();
    };

    /// <summary>Column A (Name) with ShowColumnMenu=true, Column B (Department) with ShowColumnMenu=false.</summary>
    private static RenderFragment OneMenuOneNoMenu() => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Employee>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.Title), "Name");
        builder.AddAttribute(3, nameof(SunfishGridColumn<Employee>.ShowColumnMenu), true);
        builder.CloseComponent();

        builder.OpenComponent<SunfishGridColumn<Employee>>(4);
        builder.AddAttribute(5, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Department));
        builder.AddAttribute(6, nameof(SunfishGridColumn<Employee>.Title), "Department");
        // ShowColumnMenu defaults to false
        builder.CloseComponent();
    };

    /// <summary>Two columns both with ShowColumnMenu=true.</summary>
    private static RenderFragment TwoMenuColumns() => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Employee>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.Title), "Name");
        builder.AddAttribute(3, nameof(SunfishGridColumn<Employee>.ShowColumnMenu), true);
        builder.CloseComponent();

        builder.OpenComponent<SunfishGridColumn<Employee>>(4);
        builder.AddAttribute(5, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Department));
        builder.AddAttribute(6, nameof(SunfishGridColumn<Employee>.Title), "Department");
        builder.AddAttribute(7, nameof(SunfishGridColumn<Employee>.ShowColumnMenu), true);
        builder.CloseComponent();
    };

    // ═══════════════════════════════════════════════════════════════════════
    //  C3.2 — Trigger button visibility
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// ShowColumnMenu=false (default) — no trigger button should appear in the header.
    /// </summary>
    [Fact]
    public void ShowColumnMenu_False_NoTriggerRendered()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.ChildContent, OneColumnNoMenu()));

        var triggers = cut.FindAll(".mar-datagrid-column-menu-trigger");
        Assert.Empty(triggers);
    }

    /// <summary>
    /// ShowColumnMenu=true — the trigger button should appear in the header for that column.
    /// </summary>
    [Fact]
    public void ShowColumnMenu_True_TriggerRendered()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.ChildContent, OneMenuOneNoMenu()));

        var triggers = cut.FindAll(".mar-datagrid-column-menu-trigger");
        Assert.Single(triggers);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  C3.2 — Menu open/close via trigger click
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Clicking the trigger should open the menu (aria-expanded="true" + menu markup present).
    /// </summary>
    [Fact]
    public void ClickTrigger_OpensMenu()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.ChildContent, OneMenuOneNoMenu()));

        // Menu should be closed initially.
        Assert.Empty(cut.FindAll(".mar-datagrid-column-menu"));

        // Click the trigger.
        cut.Find(".mar-datagrid-column-menu-trigger").Click();

        // Menu markup appears and aria-expanded is true.
        Assert.NotEmpty(cut.FindAll(".mar-datagrid-column-menu"));
        var trigger = cut.Find(".mar-datagrid-column-menu-trigger");
        Assert.Equal("true", trigger.GetAttribute("aria-expanded"));
    }

    /// <summary>
    /// Clicking the trigger a second time should close the menu.
    /// </summary>
    [Fact]
    public void ClickTrigger_Twice_ClosesMenu()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.ChildContent, OneMenuOneNoMenu()));

        var trigger = cut.Find(".mar-datagrid-column-menu-trigger");
        trigger.Click();  // open
        trigger.Click();  // close

        Assert.Empty(cut.FindAll(".mar-datagrid-column-menu"));
        Assert.Equal("false", trigger.GetAttribute("aria-expanded"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  C3.3 — Menu item visibility: Sort
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When the column is Sortable (and grid Sortable), sort items should appear in the menu.
    /// </summary>
    [Fact]
    public void Menu_SortableColumn_ShowsSortItems()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.Sortable, true)
            .Add(x => x.ChildContent, OneMenuOneNoMenu()));

        cut.Find(".mar-datagrid-column-menu-trigger").Click();

        var buttons = cut.FindAll(".mar-datagrid-column-menu-item");
        var labels = buttons.Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains("Sort ascending", labels);
        Assert.Contains("Sort descending", labels);
        Assert.Contains("Clear sort", labels);
    }

    /// <summary>
    /// When the column is not Sortable, sort items should NOT appear in the menu.
    /// </summary>
    [Fact]
    public void Menu_NonSortableColumn_HidesSortItems()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<Employee>>(0);
                builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
                builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.ShowColumnMenu), true);
                builder.AddAttribute(3, nameof(SunfishGridColumn<Employee>.Sortable), false);
                builder.CloseComponent();
            }));

        cut.Find(".mar-datagrid-column-menu-trigger").Click();

        var labels = cut.FindAll(".mar-datagrid-column-menu-item").Select(b => b.TextContent.Trim()).ToList();
        Assert.DoesNotContain("Sort ascending", labels);
        Assert.DoesNotContain("Sort descending", labels);
        Assert.DoesNotContain("Clear sort", labels);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  C3.3 — Menu item visibility: Filter
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When FilterMode is set and the column is Filterable, the Filter… item appears.
    /// </summary>
    [Fact]
    public void Menu_FilterableColumn_ShowsFilterItem()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.FilterMode, GridFilterMode.FilterMenu)
            .Add(x => x.ChildContent, OneMenuOneNoMenu()));

        cut.Find(".mar-datagrid-column-menu-trigger").Click();

        var labels = cut.FindAll(".mar-datagrid-column-menu-item").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains("Filter\u2026", labels);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  C3.3 — Menu item visibility: Lock / Unlock
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// When Lockable=true and Locked=false, the menu shows "Lock column".
    /// </summary>
    [Fact]
    public void Menu_LockableColumn_ShowsLockItem()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<Employee>>(0);
                builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
                builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.ShowColumnMenu), true);
                builder.AddAttribute(3, nameof(SunfishGridColumn<Employee>.Lockable), true);
                builder.AddAttribute(4, nameof(SunfishGridColumn<Employee>.Locked), false);
                builder.CloseComponent();
            }));

        cut.Find(".mar-datagrid-column-menu-trigger").Click();

        var labels = cut.FindAll(".mar-datagrid-column-menu-item").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains("Lock column", labels);
    }

    /// <summary>
    /// When Locked=true, the menu shows "Unlock column" instead of "Lock column".
    /// </summary>
    [Fact]
    public void Menu_LockedColumn_ShowsUnlockItem()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<Employee>>(0);
                builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
                builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.ShowColumnMenu), true);
                builder.AddAttribute(3, nameof(SunfishGridColumn<Employee>.Lockable), true);
                builder.AddAttribute(4, nameof(SunfishGridColumn<Employee>.Locked), true);
                builder.CloseComponent();
            }));

        cut.Find(".mar-datagrid-column-menu-trigger").Click();

        var labels = cut.FindAll(".mar-datagrid-column-menu-item").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains("Unlock column", labels);
    }

    /// <summary>
    /// When Lockable=false, the Lock/Unlock item should NOT appear.
    /// </summary>
    [Fact]
    public void Menu_NotLockableColumn_HidesLockItem()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<Employee>>(0);
                builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
                builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.ShowColumnMenu), true);
                builder.AddAttribute(3, nameof(SunfishGridColumn<Employee>.Lockable), false);
                builder.CloseComponent();
            }));

        cut.Find(".mar-datagrid-column-menu-trigger").Click();

        var labels = cut.FindAll(".mar-datagrid-column-menu-item").Select(b => b.TextContent.Trim()).ToList();
        Assert.DoesNotContain("Lock column", labels);
        Assert.DoesNotContain("Unlock column", labels);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  C3.3 — Sort actions
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Clicking "Sort ascending" should sort the column ascending and close the menu.
    /// </summary>
    [Fact]
    public void ClickSortAscending_SortsColumnAndClosesMenu()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.Sortable, true)
            .Add(x => x.ChildContent, OneMenuOneNoMenu()));

        cut.Find(".mar-datagrid-column-menu-trigger").Click();

        var sortAscBtn = cut.FindAll(".mar-datagrid-column-menu-item")
            .First(b => b.TextContent.Trim() == "Sort ascending");
        sortAscBtn.Click();

        // Menu should be closed after action.
        Assert.Empty(cut.FindAll(".mar-datagrid-column-menu"));

        // The column sort indicator should appear (ascending arrow ▲).
        var sortIndicator = cut.Find(".sf-datagrid-sort-indicator");
        Assert.Contains("\u25B2", sortIndicator.TextContent);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  C3.3 — Lock toggle
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Clicking "Lock column" should toggle the column to locked state and close the menu.
    /// After locking, re-opening the menu should show "Unlock column".
    /// </summary>
    [Fact]
    public void ClickLockColumn_TogglesLockedAndClosesMenu()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<Employee>>(0);
                builder.AddAttribute(1, nameof(SunfishGridColumn<Employee>.Field), nameof(Employee.Name));
                builder.AddAttribute(2, nameof(SunfishGridColumn<Employee>.ShowColumnMenu), true);
                builder.AddAttribute(3, nameof(SunfishGridColumn<Employee>.Lockable), true);
                builder.AddAttribute(4, nameof(SunfishGridColumn<Employee>.Locked), false);
                builder.CloseComponent();
            }));

        // Open and click Lock.
        cut.Find(".mar-datagrid-column-menu-trigger").Click();
        var lockBtn = cut.FindAll(".mar-datagrid-column-menu-item")
            .First(b => b.TextContent.Trim() == "Lock column");
        lockBtn.Click();

        // Menu should be closed.
        Assert.Empty(cut.FindAll(".mar-datagrid-column-menu"));

        // The column should now be frozen — CSS class applied.
        var ths = cut.FindAll("thead th");
        Assert.Contains(ths, th => th.ClassList.Contains("mar-datagrid-col--locked"));

        // Re-open: label should now be "Unlock column".
        cut.Find(".mar-datagrid-column-menu-trigger").Click();
        var labels = cut.FindAll(".mar-datagrid-column-menu-item").Select(b => b.TextContent.Trim()).ToList();
        Assert.Contains("Unlock column", labels);
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  C3 — Single-open invariant (opening B closes A)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Only one column menu should be open at a time.
    /// Opening column B's menu should close column A's menu.
    /// </summary>
    [Fact]
    public void OpenSecondMenu_ClosesFirstMenu()
    {
        var cut = Render<SunfishDataGrid<Employee>>(p => p
            .Add(x => x.Data, TwoEmployees())
            .Add(x => x.ChildContent, TwoMenuColumns()));

        Assert.Equal(2, cut.FindAll(".mar-datagrid-column-menu-trigger").Count);

        // Open column A's menu.
        cut.FindAll(".mar-datagrid-column-menu-trigger")[0].Click();
        Assert.Single(cut.FindAll(".mar-datagrid-column-menu"));
        Assert.Equal("true", cut.FindAll(".mar-datagrid-column-menu-trigger")[0].GetAttribute("aria-expanded"));

        // Open column B's menu — column A's menu should close.
        cut.FindAll(".mar-datagrid-column-menu-trigger")[1].Click();
        Assert.Single(cut.FindAll(".mar-datagrid-column-menu"));
        Assert.Equal("false", cut.FindAll(".mar-datagrid-column-menu-trigger")[0].GetAttribute("aria-expanded"));
        Assert.Equal("true", cut.FindAll(".mar-datagrid-column-menu-trigger")[1].GetAttribute("aria-expanded"));
    }
}
