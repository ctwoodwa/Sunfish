using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.ShipsOffice;
using Xunit;

namespace Sunfish.Blocks.ShipsOffice.Tests;

public class ShipsOfficeProviderTests
{
    private static readonly TenantId TenantA = new("alpha");

    private static ShipsOfficeDataProvider Build(ShipsOfficeOptions? options = null) =>
        new(Options.Create(options ?? new ShipsOfficeOptions()));

    [Fact]
    public async Task GetSnapshotAsync_ReturnsEmptyDocuments()
    {
        var snapshot = await Build().GetSnapshotAsync(TenantA);
        Assert.Empty(snapshot.Documents);
        Assert.Equal(0, snapshot.TotalCount);
    }

    [Fact]
    public async Task GetSnapshotAsync_Cancellation_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => Build().GetSnapshotAsync(TenantA, cts.Token));
    }

    [Fact]
    public async Task SearchAsync_NullQuery_Throws()
    {
        var provider = Build();
        var enumerator = provider.SearchAsync(TenantA, null!, default).GetAsyncEnumerator();
        await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            await enumerator.MoveNextAsync());
    }

    [Fact]
    public async Task SearchAsync_EmptyQuery_YieldsNoResults()
    {
        var provider = Build();
        var query = new ShipsOfficeSearchQuery(
            TextQuery: null, KindFilter: null, StatusFilter: null,
            PageSize: 50, PageToken: null);
        var count = 0;
        await foreach (var _ in provider.SearchAsync(TenantA, query))
        {
            count++;
        }
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task NoopContentEditorSurface_ReturnsCancelledNotSaved()
    {
        var surface = new NoopContentEditorSurface();
        var result = await surface.EditAsync(TenantA, new ShipsOfficeDocumentId("doc-1"));
        Assert.False(result.WasSaved);
        Assert.Null(result.NewVersionLabel);
    }

    [Fact]
    public void AddSunfishShipsOfficeDefaults_RegistersStubProviderAndEditor()
    {
        var services = new ServiceCollection();
        services.AddSunfishShipsOffice();
        services.AddSunfishShipsOfficeDefaults();
        using var sp = services.BuildServiceProvider();

        Assert.IsType<ShipsOfficeDataProvider>(sp.GetService<IShipsOfficeDataProvider>());
        Assert.IsType<NoopContentEditorSurface>(sp.GetService<IContentEditorSurface>());
    }

    /// <summary>
    /// W#55 Phase 2a §Trust invariant — the implementation assembly does
    /// NOT carry a <c>Sunfish.Foundation.Recovery</c> reference. Phase 2b
    /// will add the IW9DocumentService.GetAsync integration (NEVER
    /// GetWithDecryptedTinAsync); the stub emits zero documents so the
    /// invariant trivially holds for now.
    /// </summary>
    [Fact]
    public void Provider_DoesNotReference_FoundationRecovery()
    {
        var assembly = typeof(ShipsOfficeDataProvider).Assembly;
        var refs = assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        Assert.DoesNotContain("Sunfish.Foundation.Recovery", refs);
    }
}
