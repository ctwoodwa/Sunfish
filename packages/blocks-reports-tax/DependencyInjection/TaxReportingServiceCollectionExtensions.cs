using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.TaxReporting.Rendering;
using Sunfish.Blocks.TaxReporting.Services;
using Sunfish.Foundation.Localization;

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
    /// Also contributes the open-generic <see cref="ISunfishLocalizer{T}"/> binding so
    /// consumers can resolve the tax-reporting <c>SharedResource</c> bundle. Caller
    /// is responsible for wiring <c>services.AddLocalization()</c> in the composition
    /// root (matches the Bridge pattern; class libraries don't take a hard
    /// PackageReference on <c>Microsoft.Extensions.Localization</c>).
    /// Suitable for testing, prototyping, and kitchen-sink demos.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddInMemoryTaxReporting(this IServiceCollection services)
    {
        services.AddSingleton<ITaxReportingService, InMemoryTaxReportingService>();
        services.AddSingleton<ITaxReportTextRenderer, TaxReportTextRenderer>();
        services.TryAdd(ServiceDescriptor.Singleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>)));
        return services;
    }
}
