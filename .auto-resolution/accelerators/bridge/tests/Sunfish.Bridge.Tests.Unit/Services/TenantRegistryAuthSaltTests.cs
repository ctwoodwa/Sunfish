using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Sunfish.Bridge.Data;
using Sunfish.Bridge.Orchestration;
using Sunfish.Bridge.Services;
using Sunfish.Foundation.Authorization;
using Xunit;

namespace Sunfish.Bridge.Tests.Unit.Services;

/// <summary>
/// Verifies the Wave 5.3.A <c>AuthSalt</c> behaviour on
/// <see cref="TenantRegistry.CreateAsync"/> — 16 random bytes per fresh
/// tenant, distinct across tenants. See
/// <c>_shared/product/wave-5.3-decomposition.md</c> §5.3.A.
/// </summary>
public class TenantRegistryAuthSaltTests
{
    private sealed class TestTenant : ITenantContext
    {
        public string TenantId => "unit-tenant";
        public string UserId => "unit-user";
        public IReadOnlyList<string> Roles { get; } = ["Admin"];
        public bool HasPermission(string permission) => true;
    }

    private static IServiceProvider BuildProvider(
        [System.Runtime.CompilerServices.CallerMemberName] string dbName = "")
    {
        var root = new InMemoryDatabaseRoot();
        var services = new ServiceCollection();
        services.AddSingleton<ITenantContext, TestTenant>();
        services.AddDbContext<SunfishBridgeDbContext>(o => o.UseInMemoryDatabase(dbName, root));
        services.AddSingleton<ITenantRegistryEventBus, InMemoryTenantRegistryEventBus>();
        services.AddScoped<ITenantRegistry, TenantRegistry>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task CreateAsync_populates_AuthSalt_with_16_bytes()
    {
        var sp = BuildProvider();
        using var scope = sp.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();

        var tenant = await registry.CreateAsync("saltco", "SaltCo", "Team", CancellationToken.None);

        Assert.NotNull(tenant.AuthSalt);
        Assert.Equal(16, tenant.AuthSalt!.Length);
        // RandomNumberGenerator output is not all-zero with overwhelming probability;
        // this is a sanity check that we didn't accidentally assign `new byte[16]`.
        Assert.Contains(tenant.AuthSalt, b => b != 0);
    }

    [Fact]
    public async Task CreateAsync_generates_unique_AuthSalt_per_tenant()
    {
        var sp = BuildProvider();
        using var scope = sp.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<ITenantRegistry>();

        var a = await registry.CreateAsync("alpha", "Alpha", "Team", CancellationToken.None);
        var b = await registry.CreateAsync("beta", "Beta", "Team", CancellationToken.None);

        Assert.NotNull(a.AuthSalt);
        Assert.NotNull(b.AuthSalt);
        Assert.NotEqual(a.AuthSalt, b.AuthSalt);
    }
}
