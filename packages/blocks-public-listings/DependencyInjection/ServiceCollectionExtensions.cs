using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.PublicListings.Data;
using Sunfish.Blocks.PublicListings.Services;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.PublicListings.DependencyInjection;

/// <summary>DI registration for the in-memory public-listings substrate.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers the in-memory public-listings substrate (repository + renderer + entity-module contribution).</summary>
    public static IServiceCollection AddInMemoryPublicListings(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IListingRepository, InMemoryListingRepository>();
        services.TryAddSingleton<IListingRenderer, DefaultListingRenderer>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISunfishEntityModule, PublicListingsEntityModule>());
        return services;
    }
}
