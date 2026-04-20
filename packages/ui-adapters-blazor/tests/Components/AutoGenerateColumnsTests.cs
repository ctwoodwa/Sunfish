using System.ComponentModel.DataAnnotations;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.UIAdapters.Blazor.Components.DataDisplay;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components;

/// <summary>Tests for <see cref="SunfishDataGrid{TItem}.AutoGenerateColumns"/> (G37 A2).</summary>
public class AutoGenerateColumnsTests : BunitContext
{
    public AutoGenerateColumnsTests()
    {
        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<Sunfish.UIAdapters.Blazor.Internal.Interop.IDownloadService, StubDownloadService>();
    }

    // ── Test models ────────────────────────────────────────────────────────

    private record SimpleItem(int Id, string Name, DateTime CreatedAt);

    private record DisplayNameItem(
        [property: Display(Name = "Pretty Name")] string PropWithDisplayName,
        string PropWithoutDisplayName);

    private record AutoGenerateFieldFalseItem(
        [property: Display(AutoGenerateField = false)] string Skipped,
        string Included);

    private record DisplayOrderItem(
        [property: Display(Order = 2)] string Second,
        [property: Display(Order = 1)] string First);

    private record EditableAttributeItem(
        [property: Editable(false)] string ReadOnlyProp,
        string EditableProp);

    private record ComplexItem(
        int Id,
        List<string> Tags,           // complex — skipped
        SimpleItem? Child,            // complex — skipped
        int? NullableInt,             // nullable value type — included
        DateTime? NullableDate);      // nullable DateTime — included

    private enum Status { Active, Inactive }
    private record EnumItem(int Id, Status Status);

    private record GuidAndDateOnlyItem(Guid Id, DateOnly Date);

    private record ExplicitOverrideItem(int Id, string Name, DateTime CreatedAt);

    // ── Tests ──────────────────────────────────────────────────────────────

    /// <summary>A2.1 — When AutoGenerateColumns is false (default), no columns are produced without explicit children.</summary>
    [Fact]
    public void AutoGenerateColumns_False_ProducesNoColumns_WhenNoChildrenProvided()
    {
        var cut = Render<SunfishDataGrid<SimpleItem>>(p => p
            .Add(x => x.AutoGenerateColumns, false));

        var grid = cut.Instance;
        Assert.Empty(grid._columns);
    }

    /// <summary>A2.2 — AutoGenerateColumns=true reflects TItem's public primitive properties into columns.</summary>
    [Fact]
    public void AutoGenerateColumns_True_GeneratesColumnsFromSimpleRecord()
    {
        var cut = Render<SunfishDataGrid<SimpleItem>>(p => p
            .Add(x => x.AutoGenerateColumns, true));

        var grid = cut.Instance;
        var fields = grid._columns.Select(c => c.Field).ToList();

        Assert.Equal(3, fields.Count);
        Assert.Contains("Id", fields);
        Assert.Contains("Name", fields);
        Assert.Contains("CreatedAt", fields);
    }

    /// <summary>A2.3 — [Display(Name = "Pretty Name")] is used as the column title.</summary>
    [Fact]
    public void AutoGenerateColumns_HonoursDisplayName_ForTitle()
    {
        var cut = Render<SunfishDataGrid<DisplayNameItem>>(p => p
            .Add(x => x.AutoGenerateColumns, true));

        var grid = cut.Instance;
        var col = grid._columns.First(c => c.Field == "PropWithDisplayName");
        Assert.Equal("Pretty Name", col.Title);
    }

    /// <summary>A2.3 — [Display(AutoGenerateField = false)] causes the property to be skipped.</summary>
    [Fact]
    public void AutoGenerateColumns_SkipsProperty_WhenAutoGenerateFieldIsFalse()
    {
        var cut = Render<SunfishDataGrid<AutoGenerateFieldFalseItem>>(p => p
            .Add(x => x.AutoGenerateColumns, true));

        var grid = cut.Instance;
        var fields = grid._columns.Select(c => c.Field).ToList();

        Assert.DoesNotContain("Skipped", fields);
        Assert.Contains("Included", fields);
    }

    /// <summary>A2.3 — [Display(Order = N)] controls auto-generated column ordering.</summary>
    [Fact]
    public void AutoGenerateColumns_RespectsDisplayOrder()
    {
        var cut = Render<SunfishDataGrid<DisplayOrderItem>>(p => p
            .Add(x => x.AutoGenerateColumns, true));

        var grid = cut.Instance;
        var fields = grid._columns.Select(c => c.Field).ToList();

        // "First" has Order=1, "Second" has Order=2, so First should come before Second.
        var firstIdx = fields.IndexOf("First");
        var secondIdx = fields.IndexOf("Second");
        Assert.True(firstIdx < secondIdx, $"Expected 'First' (Order=1) before 'Second' (Order=2), but got indices {firstIdx} and {secondIdx}.");
    }

    /// <summary>A2.3 — [Editable(false)] is reflected on the column's Editable property.</summary>
    [Fact]
    public void AutoGenerateColumns_HonoursEditableAttribute()
    {
        var cut = Render<SunfishDataGrid<EditableAttributeItem>>(p => p
            .Add(x => x.AutoGenerateColumns, true));

        var grid = cut.Instance;
        var readOnlyCol = grid._columns.First(c => c.Field == "ReadOnlyProp");
        var editableCol = grid._columns.First(c => c.Field == "EditableProp");

        Assert.False(readOnlyCol.Editable);
        Assert.True(editableCol.Editable);
    }

    /// <summary>A2.5 — Complex types (List, POCO) are skipped; nullable primitives and DateTime? are included.</summary>
    [Fact]
    public void AutoGenerateColumns_SkipsComplexTypes_IncludesNullableValueTypes()
    {
        var cut = Render<SunfishDataGrid<ComplexItem>>(p => p
            .Add(x => x.AutoGenerateColumns, true));

        var grid = cut.Instance;
        var fields = grid._columns.Select(c => c.Field).ToList();

        // Should be included
        Assert.Contains("Id", fields);
        Assert.Contains("NullableInt", fields);
        Assert.Contains("NullableDate", fields);

        // Should be skipped — List<string> and complex POCO
        Assert.DoesNotContain("Tags", fields);
        Assert.DoesNotContain("Child", fields);
    }

    /// <summary>A2.4 — Explicit child columns take priority; auto-gen fills fields not covered by children.</summary>
    [Fact]
    public void AutoGenerateColumns_ChildrenWin_ReflectionFillsGaps()
    {
        var cut = Render<SunfishDataGrid<ExplicitOverrideItem>>(p => p
            .Add(x => x.AutoGenerateColumns, true)
            .Add(x => x.ChildContent, builder =>
            {
                builder.OpenComponent<SunfishGridColumn<ExplicitOverrideItem>>(0);
                builder.AddAttribute(1, "Field", "Id");
                builder.AddAttribute(2, "Title", "Custom Id");
                builder.CloseComponent();
            }));

        var grid = cut.Instance;
        var fields = grid._columns.Select(c => c.Field).ToList();

        // All three fields should appear exactly once
        Assert.Equal(3, fields.Count);
        Assert.Contains("Id", fields);
        Assert.Contains("Name", fields);
        Assert.Contains("CreatedAt", fields);

        // The explicit "Id" column wins — its title is "Custom Id", not the auto-generated one
        var idCol = grid._columns.First(c => c.Field == "Id");
        Assert.Equal("Custom Id", idCol.Title);

        // The explicit column is NOT in the auto-generated list
        Assert.DoesNotContain(idCol, grid._autoGeneratedColumns);
    }

    /// <summary>A2.5 — Enum properties are included in auto-generated columns.</summary>
    [Fact]
    public void AutoGenerateColumns_IncludesEnumProperties()
    {
        var cut = Render<SunfishDataGrid<EnumItem>>(p => p
            .Add(x => x.AutoGenerateColumns, true));

        var grid = cut.Instance;
        var fields = grid._columns.Select(c => c.Field).ToList();

        Assert.Contains("Id", fields);
        Assert.Contains("Status", fields);
    }

    /// <summary>A2.5 — Guid and DateOnly properties are included.</summary>
    [Fact]
    public void AutoGenerateColumns_IncludesGuidAndDateOnly()
    {
        var cut = Render<SunfishDataGrid<GuidAndDateOnlyItem>>(p => p
            .Add(x => x.AutoGenerateColumns, true));

        var grid = cut.Instance;
        var fields = grid._columns.Select(c => c.Field).ToList();

        Assert.Contains("Id", fields);
        Assert.Contains("Date", fields);
    }

    /// <summary>When no [Display] attribute is present, the column title is split from the property name (CamelCase → words).</summary>
    [Fact]
    public void AutoGenerateColumns_SplitsCamelCasePropertyName_WhenNoDisplayAttribute()
    {
        var cut = Render<SunfishDataGrid<SimpleItem>>(p => p
            .Add(x => x.AutoGenerateColumns, true));

        var grid = cut.Instance;
        var createdAtCol = grid._columns.FirstOrDefault(c => c.Field == "CreatedAt");
        Assert.NotNull(createdAtCol);
        Assert.Equal("Created At", createdAtCol.Title);
    }
}
