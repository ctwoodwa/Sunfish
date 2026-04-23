using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Bridge.Orchestration;

/// <summary>
/// DI composition root for Bridge's per-tenant orchestration contracts
/// (Wave 5.2.A). Registers the options surface and the default
/// <see cref="ITenantRegistryEventBus"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ITenantProcessSupervisor"/> is deliberately NOT registered here
/// — its implementation ships in Wave 5.2.C. Wave 5.2.A is contracts only.
/// </para>
/// <para>
/// <see cref="BridgeOrchestrationOptions"/> is bound via
/// <see cref="OptionsServiceCollectionExtensions.Configure{TOptions}(IServiceCollection, Action{TOptions})"/>
/// — callers in <c>Program.cs</c> pass a lambda that reads
/// <c>IConfigurationRoot.GetSection("Bridge:Orchestration")</c> into the options
/// instance.
/// </para>
/// </remarks>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Wave 5.2.A's orchestration contracts:
    /// <see cref="BridgeOrchestrationOptions"/> (optional configuration) and
    /// <see cref="ITenantRegistryEventBus"/> (singleton
    /// <see cref="InMemoryTenantRegistryEventBus"/>).
    /// </summary>
    /// <param name="services">The service collection to extend.</param>
    /// <param name="configure">Optional configuration delegate applied to
    /// <see cref="BridgeOrchestrationOptions"/>. Pass <see langword="null"/>
    /// when the caller binds options from <c>IConfiguration</c> separately
    /// (e.g. via <c>services.Configure&lt;BridgeOrchestrationOptions&gt;(config)</c>).</param>
    /// <returns>The same <paramref name="services"/> for chaining.</returns>
    public static IServiceCollection AddBridgeOrchestration(
        this IServiceCollection services,
        Action<BridgeOrchestrationOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<ITenantRegistryEventBus, InMemoryTenantRegistryEventBus>();
        return services;
    }
}
