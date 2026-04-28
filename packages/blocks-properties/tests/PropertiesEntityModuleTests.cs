using Microsoft.EntityFrameworkCore;
using Sunfish.Blocks.Properties.Data;
using Xunit;

namespace Sunfish.Blocks.Properties.Tests;

public class PropertiesEntityModuleTests
{
    [Fact]
    public void ModuleKey_is_stable_reverse_dns()
    {
        var module = new PropertiesEntityModule();
        Assert.Equal("sunfish.blocks.properties", module.ModuleKey);
        Assert.Equal(PropertiesEntityModule.Key, module.ModuleKey);
    }

    [Fact]
    public void Configure_does_not_throw()
    {
        var module = new PropertiesEntityModule();
        var options = new DbContextOptionsBuilder<PropertiesTestDbContext>()
            .UseInMemoryDatabase($"properties-tests-{Guid.NewGuid()}")
            .Options;

        using var ctx = new PropertiesTestDbContext(options, module);

        // Force model build — invokes ISunfishEntityModule.Configure.
        var model = ctx.Model;

        Assert.NotNull(model);
    }

    [Fact]
    public void Configure_registers_Property_entity_under_expected_table()
    {
        var module = new PropertiesEntityModule();
        var options = new DbContextOptionsBuilder<PropertiesTestDbContext>()
            .UseInMemoryDatabase($"properties-tests-{Guid.NewGuid()}")
            .Options;

        using var ctx = new PropertiesTestDbContext(options, module);

        var entity = ctx.Model.FindEntityType(typeof(Sunfish.Blocks.Properties.Models.Property));
        Assert.NotNull(entity);
        Assert.Equal(PropertyEntityConfiguration.TableName, entity!.GetTableName());
    }

    private sealed class PropertiesTestDbContext : DbContext
    {
        private readonly PropertiesEntityModule _module;

        public PropertiesTestDbContext(
            DbContextOptions<PropertiesTestDbContext> options,
            PropertiesEntityModule module)
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
