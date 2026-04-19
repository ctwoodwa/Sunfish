using Microsoft.Extensions.DependencyInjection;
using Sunfish.Blocks.TaxReporting.Rendering;
using Sunfish.Blocks.TaxReporting.Services;

namespace Sunfish.Blocks.TaxReporting.DependencyInjection;

/// <summary>
/// Extension methods for registering tax-reporting services in a
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class TaxReportingServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="InMemoryTaxReportingService"/> as the
    /// <see cref="ITaxReportingService"/> implementation, a singleton
    /// <see cref="TaxReportTextRenderer"/> as <see cref="ITaxReportTextRenderer"/>.
    /// <see cref="TaxReportCanonicalJson"/> is a static helper and does not need DI registration.
    /// Suitable for testing, prototyping, and kitchen-sink demos.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddInMemoryTaxReporting(this IServiceCollection services)
    {
        services.AddSingleton<ITaxReportingService, InMemoryTaxReportingService>();
        services.AddSingleton<ITaxReportTextRenderer, TaxReportTextRenderer>();
        return services;
    }
}
