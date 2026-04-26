using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.RentCollection.Services;
using Sunfish.Foundation.Localization;

namespace Sunfish.Blocks.RentCollection.DependencyInjection;

/// <summary>
/// Extension methods for registering rent-collection services in a
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class RentCollectionServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="InMemoryRentCollectionService"/> as the
    /// <see cref="IRentCollectionService"/> implementation. Also contributes the
    /// open-generic <see cref="ISunfishLocalizer{T}"/> binding so consumers can
    /// resolve the rent-collection <c>SharedResource</c> bundle. Caller is
    /// responsible for wiring <c>services.AddLocalization()</c> in the composition
    /// root (matches the Bridge pattern; class libraries don't take a hard
    /// PackageReference on <c>Microsoft.Extensions.Localization</c>).
    /// Suitable for testing, prototyping, and kitchen-sink demos.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddInMemoryRentCollection(this IServiceCollection services)
    {
        services.AddSingleton<IRentCollectionService, InMemoryRentCollectionService>();
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>)));
        return services;
    }
}
