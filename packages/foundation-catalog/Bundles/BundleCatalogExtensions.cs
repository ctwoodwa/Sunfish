using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Foundation.Catalog.Bundles;

/// <summary>DI conveniences for <see cref="IBundleCatalog"/>.</summary>
public static class BundleCatalogExtensions
{
    /// <summary>Registers <see cref="BundleCatalog"/> as a singleton <see cref="IBundleCatalog"/>.</summary>
    public static IServiceCollection AddSunfishBundleCatalog(this IServiceCollection services)
    {
        services.AddSingleton<IBundleCatalog, BundleCatalog>();
        return services;
    }
}
