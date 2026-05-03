using Microsoft.EntityFrameworkCore;
using Sunfish.Blocks.TenantAdmin.Data;
using Xunit;

namespace Sunfish.Blocks.TenantAdmin.Tests;

public class TenantAdminEntityModuleTests
{
    [Fact]
    public void ModuleKey_IsReverseDns()
    {
        var module = new TenantAdminEntityModule();

        Assert.Equal("sunfish.blocks.tenant-admin", module.ModuleKey);
    }

    [Fact]
    public void Configure_DoesNotThrow_ForEmptyModelBuilder()
    {
        var module = new TenantAdminEntityModule();
        var modelBuilder = new ModelBuilder();

        var ex = Record.Exception(() => module.Configure(modelBuilder));

        Assert.Null(ex);
    }

    [Fact]
    public void Configure_ThrowsOnNull_ModelBuilder()
    {
        var module = new TenantAdminEntityModule();

        Assert.Throws<ArgumentNullException>(() => module.Configure(null!));
    }
}
