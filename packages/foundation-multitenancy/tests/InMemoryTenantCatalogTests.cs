using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.MultiTenancy.Tests;

public class InMemoryTenantCatalogTests
{
    [Fact]
    public async Task Register_and_GetAll_preserve_order()
    {
        var catalog = new InMemoryTenantCatalog();
        catalog.Register(Tenant("acme"));
        catalog.Register(Tenant("beta"));
        catalog.Register(Tenant("gamma"));

        var all = await catalog.GetAllAsync();
        var keys = all.Select(t => t.Id.Value).ToArray();

        Assert.Equal(new[] { "acme", "beta", "gamma" }, keys);
    }

    [Fact]
    public async Task TryGetAsync_returns_registered_tenant()
    {
        var catalog = new InMemoryTenantCatalog();
        var acme = Tenant("acme");
        catalog.Register(acme);

        var resolved = await catalog.TryGetAsync(new TenantId("acme"));

        Assert.Same(acme, resolved);
    }

    [Fact]
    public async Task TryGetAsync_returns_null_when_absent()
    {
        var catalog = new InMemoryTenantCatalog();

        var resolved = await catalog.TryGetAsync(new TenantId("missing"));

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveAsync_matches_by_tenant_id_string()
    {
        var catalog = new InMemoryTenantCatalog();
        var acme = Tenant("acme");
        catalog.Register(acme);

        var resolved = await catalog.ResolveAsync("acme");

        Assert.Same(acme, resolved);
    }

    [Fact]
    public async Task ResolveAsync_returns_null_for_empty_candidate()
    {
        var catalog = new InMemoryTenantCatalog();
        catalog.Register(Tenant("acme"));

        Assert.Null(await catalog.ResolveAsync(""));
        Assert.Null(await catalog.ResolveAsync("   "));
    }

    [Fact]
    public void Register_rejects_duplicate_ids()
    {
        var catalog = new InMemoryTenantCatalog();
        catalog.Register(Tenant("acme"));

        Assert.Throws<InvalidOperationException>(() => catalog.Register(Tenant("acme")));
    }

    [Fact]
    public void AddSunfishTenantCatalog_wires_shared_singleton_for_catalog_and_resolver()
    {
        var services = new ServiceCollection();
        services.AddSunfishTenantCatalog();

        using var provider = services.BuildServiceProvider();
        var catalog = provider.GetRequiredService<ITenantCatalog>();
        var resolver = provider.GetRequiredService<ITenantResolver>();

        Assert.Same(catalog, resolver);
    }

    private static TenantMetadata Tenant(string id) => new()
    {
        Id = new TenantId(id),
        Name = id,
        Status = TenantStatus.Active,
    };
}
