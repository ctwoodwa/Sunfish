using Microsoft.EntityFrameworkCore;
using Sunfish.Blocks.PropertyEquipment.Data;
using Xunit;

namespace Sunfish.Blocks.PropertyEquipment.Tests;

public class PropertyEquipmentEntityModuleTests
{
    [Fact]
    public void ModuleKey_is_stable_reverse_dns()
    {
        var module = new PropertyEquipmentEntityModule();
        Assert.Equal("sunfish.blocks.property-equipment", module.ModuleKey);
        Assert.Equal(PropertyEquipmentEntityModule.Key, module.ModuleKey);
    }

    [Fact]
    public void Configure_does_not_throw()
    {
        var module = new PropertyEquipmentEntityModule();
        var options = new DbContextOptionsBuilder<PropertyEquipmentTestDbContext>()
            .UseInMemoryDatabase($"property-equipment-tests-{Guid.NewGuid()}")
            .Options;

        using var ctx = new PropertyEquipmentTestDbContext(options, module);

        var model = ctx.Model;
        Assert.NotNull(model);
    }

    [Fact]
    public void Configure_registers_Equipment_entity_under_expected_table()
    {
        var module = new PropertyEquipmentEntityModule();
        var options = new DbContextOptionsBuilder<PropertyEquipmentTestDbContext>()
            .UseInMemoryDatabase($"property-equipment-tests-{Guid.NewGuid()}")
            .Options;

        using var ctx = new PropertyEquipmentTestDbContext(options, module);
        var entity = ctx.Model.FindEntityType(typeof(Sunfish.Blocks.PropertyEquipment.Models.Equipment));
        Assert.NotNull(entity);
        Assert.Equal(EquipmentEntityConfiguration.TableName, entity!.GetTableName());
    }

    [Fact]
    public void Configure_registers_EquipmentLifecycleEvent_entity_under_expected_table()
    {
        var module = new PropertyEquipmentEntityModule();
        var options = new DbContextOptionsBuilder<PropertyEquipmentTestDbContext>()
            .UseInMemoryDatabase($"property-equipment-tests-{Guid.NewGuid()}")
            .Options;

        using var ctx = new PropertyEquipmentTestDbContext(options, module);
        var entity = ctx.Model.FindEntityType(typeof(Sunfish.Blocks.PropertyEquipment.Models.EquipmentLifecycleEvent));
        Assert.NotNull(entity);
        Assert.Equal(EquipmentLifecycleEventEntityConfiguration.TableName, entity!.GetTableName());
    }

    private sealed class PropertyEquipmentTestDbContext : DbContext
    {
        private readonly PropertyEquipmentEntityModule _module;

        public PropertyEquipmentTestDbContext(
            DbContextOptions<PropertyEquipmentTestDbContext> options,
            PropertyEquipmentEntityModule module)
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
