using Sunfish.Bridge.Data;
using Sunfish.Bridge.Data.Seeding;
using Sunfish.Foundation.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit;

public class SeederSmokeTests
{
    private sealed class TestTenant : ITenantContext
    {
        public string TenantId => "demo-tenant";
        public string UserId => "test-user";
        public IReadOnlyList<string> Roles { get; } = [Sunfish.Bridge.Data.Authorization.Roles.Manager];
        public bool HasPermission(string permission) => true;
    }

    [Fact]
    public async Task Seeder_populates_demo_data()
    {
        // Explicit InMemoryDatabaseRoot guarantees all DbContext instances created
        // from this provider share the same store across scopes.
        var dbRoot = new InMemoryDatabaseRoot();
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext, TestTenant>();
        services.AddDbContext<SunfishBridgeDbContext>(o => o.UseInMemoryDatabase("seed-test", dbRoot));
        var sp = services.BuildServiceProvider();

        var seeder = new BridgeSeeder(sp, NullLogger<BridgeSeeder>.Instance);
        await seeder.StartAsync(CancellationToken.None);

        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SunfishBridgeDbContext>();
        Assert.Equal(20, await db.Tasks.IgnoreQueryFilters().CountAsync());
        Assert.Equal(8, await db.Risks.CountAsync());
        Assert.Equal(3, await db.Milestones.CountAsync());
    }
}
