using Microsoft.EntityFrameworkCore;
using Sunfish.Blocks.PropertyAssets.Data;
using Xunit;

namespace Sunfish.Blocks.PropertyAssets.Tests;

public class PropertyAssetsEntityModuleTests
{
    [Fact]
    public void ModuleKey_is_stable_reverse_dns()
    {
        var module = new PropertyAssetsEntityModule();
        Assert.Equal("sunfish.blocks.property-assets", module.ModuleKey);
        Assert.Equal(PropertyAssetsEntityModule.Key, module.ModuleKey);
    }

    [Fact]
    public void Configure_does_not_throw()
    {
        var module = new PropertyAssetsEntityModule();
        var options = new DbContextOptionsBuilder<PropertyAssetsTestDbContext>()
            .UseInMemoryDatabase($"property-assets-tests-{Guid.NewGuid()}")
            .Options;

        using var ctx = new PropertyAssetsTestDbContext(options, module);

        var model = ctx.Model;
        Assert.NotNull(model);
    }

    [Fact]
    public void Configure_registers_Asset_entity_under_expected_table()
    {
        var module = new PropertyAssetsEntityModule();
        var options = new DbContextOptionsBuilder<PropertyAssetsTestDbContext>()
            .UseInMemoryDatabase($"property-assets-tests-{Guid.NewGuid()}")
            .Options;

        using var ctx = new PropertyAssetsTestDbContext(options, module);
        var entity = ctx.Model.FindEntityType(typeof(Sunfish.Blocks.PropertyAssets.Models.Asset));
        Assert.NotNull(entity);
        Assert.Equal(AssetEntityConfiguration.TableName, entity!.GetTableName());
    }

    [Fact]
    public void Configure_registers_AssetLifecycleEvent_entity_under_expected_table()
    {
        var module = new PropertyAssetsEntityModule();
        var options = new DbContextOptionsBuilder<PropertyAssetsTestDbContext>()
            .UseInMemoryDatabase($"property-assets-tests-{Guid.NewGuid()}")
            .Options;

        using var ctx = new PropertyAssetsTestDbContext(options, module);
        var entity = ctx.Model.FindEntityType(typeof(Sunfish.Blocks.PropertyAssets.Models.AssetLifecycleEvent));
        Assert.NotNull(entity);
        Assert.Equal(AssetLifecycleEventEntityConfiguration.TableName, entity!.GetTableName());
    }

    private sealed class PropertyAssetsTestDbContext : DbContext
    {
        private readonly PropertyAssetsEntityModule _module;

        public PropertyAssetsTestDbContext(
            DbContextOptions<PropertyAssetsTestDbContext> options,
            PropertyAssetsEntityModule module)
            : base(options)
        {
            _module = module;
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            _module.Configure(modelBuilder);
        }
    }
}
