using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Blocks.BusinessCases.Data;
using Sunfish.Blocks.BusinessCases.FeatureManagement;
using Sunfish.Blocks.BusinessCases.Services;
using Sunfish.Foundation.FeatureManagement;
using Sunfish.Foundation.Localization;
using Sunfish.Foundation.Persistence;

namespace Sunfish.Blocks.BusinessCases.DependencyInjection;

/// <summary>
/// DI extension methods for registering Sunfish business-cases services.
/// </summary>
public static class BusinessCasesServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory business-cases surface:
    /// <list type="bullet">
    ///   <item><see cref="IBusinessCaseService"/> → <see cref="InMemoryBusinessCaseService"/></item>
    ///   <item><see cref="IBundleProvisioningService"/> → <see cref="InMemoryBundleProvisioningService"/></item>
    ///   <item><see cref="ISunfishEntityModule"/> → <see cref="BusinessCasesEntityModule"/></item>
    ///   <item><see cref="IEntitlementResolver"/> → <see cref="BundleEntitlementResolver"/></item>
    /// </list>
    /// A shared <see cref="InMemoryBundleActivationStore"/> is also registered so the
    /// read and write services see the same state. Replace with a persistence-backed
    /// implementation for production.
    /// </summary>
    /// <param name="services">The service collection to register into.</param>
    /// <returns>The same <paramref name="services"/> for fluent chaining.</returns>
    public static IServiceCollection AddInMemoryBusinessCases(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<InMemoryBundleActivationStore>();
        services.AddSingleton<IBusinessCaseService, InMemoryBusinessCaseService>();
        services.AddSingleton<IBundleProvisioningService, InMemoryBundleProvisioningService>();
        services.AddSingleton<ISunfishEntityModule, BusinessCasesEntityModule>();
        services.AddSingleton<IEntitlementResolver, BundleEntitlementResolver>();

        // Wave 2 Cluster C — Plan 2 Task 3.5: register the open-generic Sunfish localizer
        // so consumers can resolve IStringLocalizer-equivalents against this block's
        // SharedResource bundle. Idempotent via TryAddSingleton.
        services.TryAddSingleton(typeof(ISunfishLocalizer<>), typeof(SunfishLocalizer<>));

        return services;
    }
}
