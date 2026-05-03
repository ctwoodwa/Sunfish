using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.Inspections.Services;
using Sunfish.Foundation.Localization;

namespace Sunfish.Blocks.Inspections.DependencyInjection;

/// <summary>
/// DI extension methods for registering Sunfish inspection-management services.
/// </summary>
public static class InspectionsServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IInspectionsService"/> as a singleton backed by
    /// <see cref="InMemoryInspectionsService"/>.
    /// Also contributes the open-generic <see cref="ISunfishLocalizer{T}"/> binding
    /// so consumers can resolve the inspections <c>SharedResource</c> bundle. Caller
    /// is responsible for wiring <c>services.AddLocalization()</c> in the composition
    /// root (matches the cluster-A sentinel pattern; class libraries don't take a
    /// hard PackageReference on <c>Microsoft.Extensions.Localization</c>).
    /// Suitable for development, testing, and demo scenarios.
    /// Replace with a persistence-backed implementation for production.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddInMemoryInspections(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IInspectionsService, InMemoryInspectionsService>();
        services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));
        return services;
    }
}
