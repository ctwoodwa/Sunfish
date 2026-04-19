using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.Extensibility;

namespace Sunfish.Foundation.Catalog.ExtensionFields;

/// <summary>
/// Generic and DI conveniences for <see cref="IExtensionFieldCatalog"/>.
/// </summary>
public static class ExtensionFieldCatalogExtensions
{
    /// <summary>Registers a field scoped to <typeparamref name="TEntity"/>.</summary>
    public static void Register<TEntity>(this IExtensionFieldCatalog catalog, ExtensionFieldSpec spec)
        where TEntity : IHasExtensionData
        => catalog.Register(typeof(TEntity), spec);

    /// <summary>Returns every field registered for <typeparamref name="TEntity"/>.</summary>
    public static IReadOnlyList<ExtensionFieldSpec> GetFields<TEntity>(this IExtensionFieldCatalog catalog)
        where TEntity : IHasExtensionData
        => catalog.GetFields(typeof(TEntity));

    /// <summary>Registers <see cref="ExtensionFieldCatalog"/> as a singleton <see cref="IExtensionFieldCatalog"/>.</summary>
    public static IServiceCollection AddSunfishExtensionFieldCatalog(this IServiceCollection services)
    {
        services.AddSingleton<IExtensionFieldCatalog, ExtensionFieldCatalog>();
        return services;
    }
}
