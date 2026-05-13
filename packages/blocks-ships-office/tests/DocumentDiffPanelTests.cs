using System;
using System.Collections.Generic;
using Bunit;
using Sunfish.Foundation.ShipsOffice;
using Sunfish.UICore.Primitives;
using Xunit;

namespace Sunfish.Blocks.ShipsOffice.Tests;

public sealed class DocumentDiffPanelTests : BunitContext
{
    private sealed class FakePreview(IReadOnlyList<DiffEntry> entries, string summary) : IDiffPreview
    {
        public IReadOnlyList<DiffEntry> Entries => entries;
        public string Summary => summary;
    }

    private static IDiffPreview EmptyPreview() =>
        new FakePreview([], "No changes");

    private static IDiffPreview WithEntry(DiffEntry entry) =>
        new FakePreview([entry], "One change");

    [Fact]
    public void Diff_table_has_accessible_label_via_figcaption()
    {
        var cut = Render<DocumentDiffPanel>(p => p
            .Add(c => c.Preview, WithEntry(new DiffEntry("Title", "Old", "New"))));

        // SC 2.4.6: figcaption provides the visible label; table links via aria-describedby.
        var figcaption = cut.Find("figcaption");
        Assert.NotNull(figcaption);
        var table = cut.Find("table[aria-describedby]");
        Assert.NotNull(table);
    }

    [Fact]
    public void Added_rows_labeled_Added_in_text()
    {
        var entry = new DiffEntry("Title", OldValue: null, NewValue: "Quarterly Report");
        var cut = Render<DocumentDiffPanel>(p => p
            .Add(c => c.Preview, WithEntry(entry)));

        Assert.Contains("Added:", cut.Markup);
        Assert.Contains("visually-hidden", cut.Markup);
    }

    [Fact]
    public void Removed_rows_labeled_Removed_in_text()
    {
        var entry = new DiffEntry("Title", OldValue: "Old Title", NewValue: null);
        var cut = Render<DocumentDiffPanel>(p => p
            .Add(c => c.Preview, WithEntry(entry)));

        Assert.Contains("Removed:", cut.Markup);
        Assert.Contains("visually-hidden", cut.Markup);
    }

    [Fact]
    public void Diff_uses_color_and_text_dual_encoding()
    {
        var entry = new DiffEntry("Status", OldValue: "Draft", NewValue: null);
        var cut = Render<DocumentDiffPanel>(p => p
            .Add(c => c.Preview, WithEntry(entry)));

        var row = cut.Find("tr.diff-removed");
        Assert.NotNull(row);
        Assert.Contains("Removed:", row.InnerHtml);
        Assert.NotEmpty(row.GetAttribute("aria-label") ?? string.Empty);
    }
}
