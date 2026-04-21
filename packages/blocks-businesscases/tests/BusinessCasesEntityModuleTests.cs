using Microsoft.EntityFrameworkCore;
using Sunfish.Blocks.BusinessCases.Data;
using Xunit;

namespace Sunfish.Blocks.BusinessCases.Tests;

public class BusinessCasesEntityModuleTests
{
    [Fact]
    public void ModuleKey_IsStableReverseDns()
    {
        var module = new BusinessCasesEntityModule();
        Assert.Equal("sunfish.blocks.businesscases", module.ModuleKey);
        Assert.Equal(BusinessCasesEntityModule.Key, module.ModuleKey);
    }

    [Fact]
    public void Configure_DoesNotThrow()
    {
        var module = new BusinessCasesEntityModule();
        var options = new DbContextOptionsBuilder<BusinessCasesTestDbContext>()
            .UseInMemoryDatabase($"bc-tests-{Guid.NewGuid()}")
            .Options;

        using var ctx = new BusinessCasesTestDbContext(options, module);

        // Force model build — will invoke ISunfishEntityModule.Configure.
        var model = ctx.Model;

        Assert.NotNull(model);
    }

    private sealed class BusinessCasesTestDbContext : DbContext
    {
        private readonly BusinessCasesEntityModule _module;

        public BusinessCasesTestDbContext(
            DbContextOptions<BusinessCasesTestDbContext> options,
            BusinessCasesEntityModule module)
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
