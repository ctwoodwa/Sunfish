using Sunfish.Foundation.Catalog.Bundles;
using Sunfish.Foundation.Integrations;

namespace Sunfish.Foundation.Integrations.Tests;

public class InMemoryProviderRegistryTests
{
    [Fact]
    public void Register_and_GetAll_preserve_order()
    {
        var registry = new InMemoryProviderRegistry();
        registry.Register(Descriptor("sunfish.providers.stripe", ProviderCategory.Payments));
        registry.Register(Descriptor("sunfish.providers.plaid", ProviderCategory.BankingFeed));

        var all = registry.GetAll();
        Assert.Equal(new[] { "sunfish.providers.stripe", "sunfish.providers.plaid" }, all.Select(d => d.Key).ToArray());
    }

    [Fact]
    public void GetByCategory_filters()
    {
        var registry = new InMemoryProviderRegistry();
        registry.Register(Descriptor("sunfish.providers.stripe", ProviderCategory.Payments));
        registry.Register(Descriptor("sunfish.providers.adyen", ProviderCategory.Payments));
        registry.Register(Descriptor("sunfish.providers.plaid", ProviderCategory.BankingFeed));

        var payments = registry.GetByCategory(ProviderCategory.Payments);

        Assert.Equal(2, payments.Count);
        Assert.All(payments, p => Assert.Equal(ProviderCategory.Payments, p.Category));
    }

    [Fact]
    public void Register_rejects_duplicate_keys()
    {
        var registry = new InMemoryProviderRegistry();
        registry.Register(Descriptor("sunfish.providers.stripe", ProviderCategory.Payments));

        Assert.Throws<InvalidOperationException>(
            () => registry.Register(Descriptor("sunfish.providers.stripe", ProviderCategory.Payments)));
    }

    [Fact]
    public void TryGet_returns_false_when_absent()
    {
        var registry = new InMemoryProviderRegistry();

        Assert.False(registry.TryGet("missing", out var desc));
        Assert.Null(desc);
    }

    private static ProviderDescriptor Descriptor(string key, ProviderCategory category) => new()
    {
        Key = key,
        Category = category,
        Name = key,
        Version = "0.1.0",
    };
}
