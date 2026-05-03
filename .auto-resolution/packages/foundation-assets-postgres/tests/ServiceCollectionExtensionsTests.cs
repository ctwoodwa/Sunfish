using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Entities;
using Sunfish.Foundation.Assets.Hierarchy;
using Sunfish.Foundation.Assets.Postgres.DependencyInjection;
using Sunfish.Foundation.Assets.Versions;

namespace Sunfish.Foundation.Assets.Postgres.Tests;

public sealed class ServiceCollectionExtensionsTests : IClassFixture<PostgresAssetStoreFixture>
{
    private readonly PostgresAssetStoreFixture _fixture;

    public ServiceCollectionExtensionsTests(PostgresAssetStoreFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void AddSunfishAssetsPostgres_RegistersAllFourPrimitives()
    {
        var services = new ServiceCollection();
        services.AddSunfishAssetsPostgres(_fixture.ConnectionString);
        var sp = services.BuildServiceProvider();

        Assert.NotNull(sp.GetRequiredService<IEntityStore>());
        Assert.NotNull(sp.GetRequiredService<IVersionStore>());
        Assert.NotNull(sp.GetRequiredService<IAuditLog>());
        Assert.NotNull(sp.GetRequiredService<IHierarchyService>());
        Assert.NotNull(sp.GetRequiredService<HierarchyOperations>());
        Assert.NotNull(sp.GetRequiredService<IDbContextFactory<AssetStoreDbContext>>());
    }

    [Fact]
    public void AddSunfishAssetsPostgres_RegistersNullObjectSeams()
    {
        var services = new ServiceCollection();
        services.AddSunfishAssetsPostgres(_fixture.ConnectionString);
        var sp = services.BuildServiceProvider();

        Assert.IsType<NullEntityValidator>(sp.GetRequiredService<IEntityValidator>());
        Assert.IsType<NullVersionObserver>(sp.GetRequiredService<IVersionObserver>());
        Assert.IsType<NullAuditContextProvider>(sp.GetRequiredService<IAuditContextProvider>());
    }
}
