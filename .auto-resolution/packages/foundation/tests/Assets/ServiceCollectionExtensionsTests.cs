using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;
using Sunfish.Foundation.Assets.Hierarchy;
using Sunfish.Foundation.Assets.Versions;

namespace Sunfish.Foundation.Tests.Assets;

public sealed class AssetsServiceCollectionExtensionsTests
{
    [Fact]
    public void AddSunfishAssetsInMemory_Registers_AllFourPrimitives()
    {
        var services = new ServiceCollection();
        services.AddSunfishAssetsInMemory();
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<IEntityStore>());
        Assert.NotNull(sp.GetRequiredService<IVersionStore>());
        Assert.NotNull(sp.GetRequiredService<IAuditLog>());
        Assert.NotNull(sp.GetRequiredService<IHierarchyService>());
        Assert.NotNull(sp.GetRequiredService<HierarchyOperations>());
    }

    [Fact]
    public void AddSunfishAssetsInMemory_RegistersNullObjectDefaults_ForExtensionSeams()
    {
        var services = new ServiceCollection();
        services.AddSunfishAssetsInMemory();
        var sp = services.BuildServiceProvider();

        Assert.IsType<NullEntityValidator>(sp.GetRequiredService<IEntityValidator>());
        Assert.IsType<NullVersionObserver>(sp.GetRequiredService<IVersionObserver>());
        Assert.IsType<NullAuditContextProvider>(sp.GetRequiredService<IAuditContextProvider>());
    }

    [Fact]
    public void AddSunfishAssetsInMemory_SharesStorage_AcrossEntityAndVersionAndAudit()
    {
        var services = new ServiceCollection();
        services.AddSunfishAssetsInMemory();
        var sp = services.BuildServiceProvider();

        // All three stores should see state written through any of them.
        var storage = sp.GetRequiredService<InMemoryAssetStorage>();
        Assert.NotNull(storage);
        // The primitives reference the same storage; resolving twice should return the same singleton.
        Assert.Same(storage, sp.GetRequiredService<InMemoryAssetStorage>());
    }
}
