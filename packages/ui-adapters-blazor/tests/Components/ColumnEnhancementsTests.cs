using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.UIAdapters.Blazor.Components.DataDisplay;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Enums;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

/// <summary>
/// bUnit tests for SunfishGridColumn enhancements (G37 A5):
/// Editable (A5.1+A5.2), HeaderClass (A5.3), Id/EffectiveId (A5.4),
/// ShowColumnMenu (A5.5), VisibleInColumnChooser (A5.6).
/// </summary>
public class ColumnEnhancementsTests : BunitContext
{
    public ColumnEnhancementsTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<Sunfish.UIAdapters.Blazor.Internal.Interop.IDownloadService, StubDownloadService>();
    }

    // ── Test model ─────────────────────────────────────────────────────────

    private sealed class Person
    {
        public string Name { get; set; } = "";
        public string City { get; set; } = "";
    }

    private static List<Person> OnePerson() =>
    [
        new Person { Name = "Alice", City = "Seattle" }
    ];

    // ── A5.1 + A5.2 — Editable ────────────────────────────────────────────

    /// <summary>
    /// A5.1 default — Editable defaults to true.
    /// </summary>
    [Fact]
    public void Editable_DefaultsToTrue()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.ChildContent, BuildColumns(editable: null)));

        var grid = cut.Instance;
        var col = grid._columns.First(c => c.Field == nameof(Person.Name));
        Assert.True(col.Editable);
    }

    /// <summary>
    /// A5.2 — When Editable=true (default) and the row is in Inline edit mode,
    /// the EditorTemplate is rendered in the cell.
    /// </summary>
    [Fact]
    public async Task Editable_True_RendersEditorTemplateInInlineEditMode()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.EditMode, GridEditMode.Inline)
            .Add(x => x.ChildContent, BuildColumnsWithEditor(editable: true)));

        var grid = cut.Instance;
        var item = OnePerson()[0];
        // Bind data first so _displayedItems is populated
        await cut.InvokeAsync(() => grid.Rebind());
        var displayedItem = grid._displayedItems.First();

        await cut.InvokeAsync(() => grid.BeginEdit(displayedItem));
        cut.Render();

        // The editor sentinel text should appear
        Assert.Contains("editor-sentinel", cut.Markup);
    }

    /// <summary>
    /// A5.2 — When Editable=false, the EditorTemplate is NOT rendered even when the row is
    /// in Inline edit mode; the display value is shown instead.
    /// </summary>
    [Fact]
    public async Task Editable_False_SkipsEditorTemplateInInlineEditMode()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.EditMode, GridEditMode.Inline)
            .Add(x => x.ChildContent, BuildColumnsWithEditor(editable: false)));

        var grid = cut.Instance;
        await cut.InvokeAsync(() => grid.Rebind());
        var displayedItem = grid._displayedItems.First();

        await cut.InvokeAsync(() => grid.BeginEdit(displayedItem));
        cut.Render();

        // Editor sentinel text must NOT appear; Alice should appear (display value)
        Assert.DoesNotContain("editor-sentinel", cut.Markup);
        Assert.Contains("Alice", cut.Markup);
    }

    // ── A5.3 — HeaderClass ────────────────────────────────────────────────

    /// <summary>
    /// A5.3 — When HeaderClass is set, it is appended to the &lt;th&gt; class attribute.
    /// </summary>
    [Fact]
    public void HeaderClass_AppendsToThClassAttribute()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.ChildContent, BuildColumnsWithHeaderClass("my-custom-header")));

        // Find the th for the Name column and assert the custom class is present
        var headerCells = cut.FindAll("thead th");
        var nameHeader = headerCells.First(th => th.TextContent.Contains("Name"));
        Assert.Contains("my-custom-header", nameHeader.GetAttribute("class") ?? "");
    }

    /// <summary>
    /// A5.3 — HeaderClass is composed with any pre-existing class on the header cell.
    /// Verify by using a Locked column (which adds the lock class) together with HeaderClass:
    /// both classes must appear on the th.
    /// </summary>
    [Fact]
    public void HeaderClass_PreservesExistingClasses()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.ChildContent, BuildColumnsWithLockedAndHeaderClass("extra-class")));

        var headerCells = cut.FindAll("thead th");
        var nameHeader = headerCells.First(th => th.TextContent.Contains("Name"));
        var cls = nameHeader.GetAttribute("class") ?? "";

        // Both the lock class AND the custom HeaderClass must be present
        Assert.Contains("mar-datagrid-col--locked", cls);
        Assert.Contains("extra-class", cls);
    }

    /// <summary>
    /// A5.3 — When HeaderClass is null (default), the th class attribute has no extra junk.
    /// </summary>
    [Fact]
    public void HeaderClass_Null_NoExtraWhitespaceOrTrailingSpace()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.ChildContent, BuildColumns(editable: null)));

        var headerCells = cut.FindAll("thead th");
        var nameHeader = headerCells.First(th => th.TextContent.Contains("Name"));
        var cls = nameHeader.GetAttribute("class") ?? "";

        // Class must not end with a trailing space
        Assert.Equal(cls.Trim(), cls);
        // Class must not contain consecutive spaces
        Assert.DoesNotContain("  ", cls);
    }

    // ── A5.4 — Id + EffectiveId ───────────────────────────────────────────

    /// <summary>
    /// A5.4 — When Id is set, EffectiveId returns the explicit Id value.
    /// </summary>
    [Fact]
    public void EffectiveId_ReturnsId_WhenIdIsSet()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.ChildContent, BuildColumnsWithId(id: "name-col", field: nameof(Person.Name))));

        var grid = cut.Instance;
        var col = grid._columns.First(c => c.Field == nameof(Person.Name));
        Assert.Equal("name-col", col.EffectiveId);
    }

    /// <summary>
    /// A5.4 — When Id is null and Field is set, EffectiveId falls back to Field.
    /// </summary>
    [Fact]
    public void EffectiveId_FallsBackToField_WhenIdIsNull()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.ChildContent, BuildColumnsWithId(id: null, field: nameof(Person.Name))));

        var grid = cut.Instance;
        var col = grid._columns.First(c => c.Field == nameof(Person.Name));
        Assert.Equal(nameof(Person.Name), col.EffectiveId);
    }

    /// <summary>
    /// A5.4 — When both Id and Field are null/empty, EffectiveId returns an index-based key.
    /// </summary>
    [Fact]
    public void EffectiveId_FallsBackToIndex_WhenIdAndFieldAreBothNull()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.ChildContent, BuildColumnsWithId(id: null, field: "")));

        var grid = cut.Instance;
        var col = grid._columns.First();
        // EffectiveId must be "column-{index}" pattern
        Assert.Matches(@"^column-\d+$", col.EffectiveId);
    }

    // ── A5.5 — ShowColumnMenu ─────────────────────────────────────────────

    /// <summary>
    /// A5.5 — ShowColumnMenu defaults to false and can be set to true.
    /// The parameter is readable from the column after registration.
    /// </summary>
    [Fact]
    public void ShowColumnMenu_DefaultsFalse_CanBeSetTrue()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.ChildContent, BuildColumnsWithShowColumnMenu(showMenu: true)));

        var grid = cut.Instance;
        var col = grid._columns.First(c => c.Field == nameof(Person.Name));
        Assert.True(col.ShowColumnMenu);
    }

    /// <summary>
    /// A5.5 — ShowColumnMenu=false is the default for a plain column.
    /// </summary>
    [Fact]
    public void ShowColumnMenu_DefaultsToFalse()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.ChildContent, BuildColumns(editable: null)));

        var grid = cut.Instance;
        var col = grid._columns.First(c => c.Field == nameof(Person.Name));
        Assert.False(col.ShowColumnMenu);
    }

    // ── A5.6 — VisibleInColumnChooser ─────────────────────────────────────

    /// <summary>
    /// A5.6 — VisibleInColumnChooser defaults to true.
    /// </summary>
    [Fact]
    public void VisibleInColumnChooser_DefaultsToTrue()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.ChildContent, BuildColumns(editable: null)));

        var grid = cut.Instance;
        var col = grid._columns.First(c => c.Field == nameof(Person.Name));
        Assert.True(col.VisibleInColumnChooser);
    }

    /// <summary>
    /// A5.6 — VisibleInColumnChooser=false is respected and stored on the column.
    /// </summary>
    [Fact]
    public void VisibleInColumnChooser_CanBeSetToFalse()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.ChildContent, BuildColumnsWithVisibleInChooser(false)));

        var grid = cut.Instance;
        var col = grid._columns.First(c => c.Field == nameof(Person.Name));
        Assert.False(col.VisibleInColumnChooser);
    }

    // ── A5.5 + A5.6 — GridState plumbing ──────────────────────────────────

    /// <summary>
    /// A5.5 + A5.6 — ShowColumnMenu and VisibleInColumnChooser are captured in GetState().ColumnStates.
    /// </summary>
    [Fact]
    public void GetState_ColumnStates_IncludesShowColumnMenuAndVisibleInColumnChooser()
    {
        var cut = Render<SunfishDataGrid<Person>>(p => p
            .Add(x => x.Data, OnePerson())
            .Add(x => x.ChildContent, BuildColumnsWithMenuAndChooser(showMenu: true, visibleInChooser: false)));

        var grid = cut.Instance;
        var state = grid.GetState();
        var colState = state.ColumnStates.First(cs => cs.Field == nameof(Person.Name));

        Assert.True(colState.ShowColumnMenu);
        Assert.False(colState.VisibleInColumnChooser);
    }

    // ── RenderFragment helpers ─────────────────────────────────────────────

    private static RenderFragment BuildColumns(bool? editable) => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Person>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Person>.Field), nameof(Person.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Person>.Title), "Name");
        if (editable.HasValue)
            builder.AddAttribute(3, nameof(SunfishGridColumn<Person>.Editable), editable.Value);
        builder.CloseComponent();
    };

    private static RenderFragment BuildColumnsWithEditor(bool editable) => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Person>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Person>.Field), nameof(Person.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Person>.Title), "Name");
        builder.AddAttribute(3, nameof(SunfishGridColumn<Person>.Editable), editable);
        builder.AddAttribute(4, nameof(SunfishGridColumn<Person>.EditorTemplate),
            (RenderFragment<Person>)(item => b =>
            {
                b.AddMarkupContent(0, $"<span>editor-sentinel</span>");
            }));
        builder.CloseComponent();
    };

    private static RenderFragment BuildColumnsWithLockedAndHeaderClass(string headerClass) => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Person>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Person>.Field), nameof(Person.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Person>.Title), "Name");
        builder.AddAttribute(3, nameof(SunfishGridColumn<Person>.Locked), true);
        builder.AddAttribute(4, nameof(SunfishGridColumn<Person>.HeaderClass), headerClass);
        builder.CloseComponent();
    };

    private static RenderFragment BuildColumnsWithHeaderClass(string headerClass) => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Person>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Person>.Field), nameof(Person.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Person>.Title), "Name");
        builder.AddAttribute(3, nameof(SunfishGridColumn<Person>.HeaderClass), headerClass);
        builder.CloseComponent();
    };

    private static RenderFragment BuildColumnsWithId(string? id, string field) => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Person>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Person>.Field), field);
        builder.AddAttribute(2, nameof(SunfishGridColumn<Person>.Title), "Name");
        if (id != null)
            builder.AddAttribute(3, nameof(SunfishGridColumn<Person>.Id), id);
        builder.CloseComponent();
    };

    private static RenderFragment BuildColumnsWithShowColumnMenu(bool showMenu) => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Person>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Person>.Field), nameof(Person.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Person>.Title), "Name");
        builder.AddAttribute(3, nameof(SunfishGridColumn<Person>.ShowColumnMenu), showMenu);
        builder.CloseComponent();
    };

    private static RenderFragment BuildColumnsWithVisibleInChooser(bool visible) => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Person>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Person>.Field), nameof(Person.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Person>.Title), "Name");
        builder.AddAttribute(3, nameof(SunfishGridColumn<Person>.VisibleInColumnChooser), visible);
        builder.CloseComponent();
    };

    private static RenderFragment BuildColumnsWithMenuAndChooser(bool showMenu, bool visibleInChooser) => builder =>
    {
        builder.OpenComponent<SunfishGridColumn<Person>>(0);
        builder.AddAttribute(1, nameof(SunfishGridColumn<Person>.Field), nameof(Person.Name));
        builder.AddAttribute(2, nameof(SunfishGridColumn<Person>.Title), "Name");
        builder.AddAttribute(3, nameof(SunfishGridColumn<Person>.ShowColumnMenu), showMenu);
        builder.AddAttribute(4, nameof(SunfishGridColumn<Person>.VisibleInColumnChooser), visibleInChooser);
        builder.CloseComponent();
    };
}
