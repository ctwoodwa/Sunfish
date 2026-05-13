using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.ShipsOffice;
using Sunfish.UICore.Primitives;
using Xunit;

namespace Sunfish.Blocks.ShipsOffice.Tests;

public sealed class DocumentDiffServiceTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private readonly IDocumentDiffService _sut = new DocumentDiffService();

    [Fact]
    public async Task ComputeDiffAsync_ReturnsEmptyEntries_Phase2Stub()
    {
        var baseId = new ShipsOfficeDocumentId("doc-1");
        var compareId = new ShipsOfficeDocumentId("doc-2");

        var result = await _sut.ComputeDiffAsync(TenantA, baseId, compareId, ct: CancellationToken.None);

        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task ComputeDiffAsync_ReturnsSummary_Phase2Stub()
    {
        var baseId = new ShipsOfficeDocumentId("doc-1");
        var compareId = new ShipsOfficeDocumentId("doc-2");

        var result = await _sut.ComputeDiffAsync(TenantA, baseId, compareId, ct: CancellationToken.None);

        Assert.NotNull(result.Summary);
        Assert.NotEmpty(result.Summary);
    }

    [Fact]
    public async Task ComputeDiffAsync_SameIds_ReturnsStub()
    {
        var id = new ShipsOfficeDocumentId("doc-1");

        var result = await _sut.ComputeDiffAsync(TenantA, id, id, ct: CancellationToken.None);

        Assert.Empty(result.Entries);
    }

    [Fact]
    public async Task ComputeDiffAsync_CompactView_DoesNotThrow()
    {
        var result = await _sut.ComputeDiffAsync(
            TenantA,
            new ShipsOfficeDocumentId("a"),
            new ShipsOfficeDocumentId("b"),
            DiffPreviewView.Compact,
            CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ComputeDiffAsync_ExpandedView_DoesNotThrow()
    {
        var result = await _sut.ComputeDiffAsync(
            TenantA,
            new ShipsOfficeDocumentId("a"),
            new ShipsOfficeDocumentId("b"),
            DiffPreviewView.Expanded,
            CancellationToken.None);

        Assert.NotNull(result);
    }
}
