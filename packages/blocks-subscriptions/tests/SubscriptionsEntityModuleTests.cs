using Microsoft.EntityFrameworkCore;
using Sunfish.Blocks.Subscriptions.Data;
using Sunfish.Foundation.Persistence;
using Xunit;

namespace Sunfish.Blocks.Subscriptions.Tests;

public class SubscriptionsEntityModuleTests
{
    [Fact]
    public void ModuleKey_IsStable_ReverseDnsString()
    {
        var module = new SubscriptionsEntityModule();

        Assert.Equal("sunfish.blocks.subscriptions", module.ModuleKey);
    }

    [Fact]
    public void Module_IsAssignableTo_ISunfishEntityModule()
    {
        ISunfishEntityModule module = new SubscriptionsEntityModule();

        Assert.NotNull(module);
    }

    [Fact]
    public void Configure_AppliesEntityConfigurations_WithoutThrowing()
    {
        var module = new SubscriptionsEntityModule();
        using var ctx = new BareContext();

        var exception = Record.Exception(() => module.Configure(new ModelBuilder()));

        Assert.Null(exception);
    }

    [Fact]
    public void Configure_Registers_ExpectedEntityTypes_OnDbContext()
    {
        using var ctx = new SubscriptionsModuleDbContext();

        Assert.NotNull(ctx.Model.FindEntityType(typeof(Sunfish.Blocks.Subscriptions.Models.Plan)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(Sunfish.Blocks.Subscriptions.Models.Subscription)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(Sunfish.Blocks.Subscriptions.Models.AddOn)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(Sunfish.Blocks.Subscriptions.Models.UsageMeter)));
        Assert.NotNull(ctx.Model.FindEntityType(typeof(Sunfish.Blocks.Subscriptions.Models.MeteredUsage)));
    }

    private sealed class BareContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase("subscriptions-module-bare");
        }
    }

    private sealed class SubscriptionsModuleDbContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseInMemoryDatabase("subscriptions-module-full");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            new SubscriptionsEntityModule().Configure(modelBuilder);
        }
    }
}
