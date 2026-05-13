using System;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.ShipsOffice;
using Xunit;

namespace Sunfish.Blocks.ShipsOffice.Tests;

public sealed class DocumentListItemTests : BunitContext
{
    private static ShipsOfficeDocumentView MakeDoc(
        ShipsOfficeDocumentKind kind = ShipsOfficeDocumentKind.BundleManifest,
        DocumentStatus status = DocumentStatus.Draft) =>
        new(
            new ShipsOfficeDocumentId("doc-1"),
            kind,
            "Test Document",
            status,
            DateTimeOffset.UtcNow,
            new ActorId("actor-1"),
            VersionLabel: null);

    [Fact]
    public void Kind_badge_uses_icon_and_text_dual_encoding()
    {
        var doc = MakeDoc(kind: ShipsOfficeDocumentKind.LeaseDocument);
        var cut = Render<DocumentListItem>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.OnSelect, EventCallback.Empty));

        Assert.Contains("icon-lease", cut.Markup);
        Assert.Contains("Lease Document", cut.Markup);
    }

    [Fact]
    public void Status_badge_uses_color_and_text_dual_encoding()
    {
        var doc = MakeDoc(status: DocumentStatus.Published);
        var cut = Render<DocumentListItem>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.OnSelect, EventCallback.Empty));

        Assert.Contains("status-published", cut.Markup);
        Assert.Contains("Published", cut.Markup);
    }

    [Fact]
    public async Task Row_is_keyboard_operable_with_Enter()
    {
        var doc = MakeDoc();
        var selected = false;
        var cut = Render<DocumentListItem>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.OnSelect, EventCallback.Factory.Create(this, () => selected = true)));

        var row = cut.Find("[role='button']");
        await row.KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        Assert.True(selected);
    }

    [Fact]
    public void Tab_focus_order_is_deterministic()
    {
        var doc = MakeDoc();
        var cut = Render<DocumentListItem>(p => p
            .Add(c => c.Document, doc)
            .Add(c => c.OnSelect, EventCallback.Empty));

        var row = cut.Find("[role='button']");
        Assert.Equal("0", row.GetAttribute("tabindex"));
    }
}
