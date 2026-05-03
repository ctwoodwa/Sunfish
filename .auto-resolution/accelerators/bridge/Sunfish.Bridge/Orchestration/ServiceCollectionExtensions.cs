using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sunfish.Kernel.Security.Keys;

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

    /// <summary>
    /// Registers Wave 5.2.D's health-monitoring surface:
    /// <see cref="ITenantEndpointRegistry"/> (singleton
    /// <see cref="InMemoryTenantEndpointRegistry"/>) and
    /// <see cref="TenantHealthMonitor"/> (hosted service + exposed as a
    /// singleton for 5.2.C's supervisor to subscribe to
    /// <see cref="TenantHealthMonitor.HealthChanged"/>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Callers MUST have already invoked
    /// <see cref="AddBridgeOrchestration"/> — the monitor depends on
    /// <see cref="BridgeOrchestrationOptions"/> being bound. Kept as a
    /// separate call so the Relay posture (ADR 0026 Posture B) can register
    /// the 5.2.A contracts without also pulling the hosted service.
    /// </para>
    /// <para>
    /// Registers the monitor both as an <see cref="IHostedService"/> (so the
    /// generic host drives its lifecycle) and as
    /// <see cref="TenantHealthMonitor"/> (so consumers — 5.2.C supervisor,
    /// admin UI — can resolve the same instance and subscribe to
    /// <see cref="TenantHealthMonitor.HealthChanged"/>).
    /// </para>
    /// </remarks>
    public static IServiceCollection AddBridgeOrchestrationHealth(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ITenantEndpointRegistry, InMemoryTenantEndpointRegistry>();
        services.AddSingleton<TenantHealthMonitor>();
        services.AddHostedService(sp => sp.GetRequiredService<TenantHealthMonitor>());
        return services;
    }

    /// <summary>
    /// Registers Wave 5.2.C.1's supervisor surface:
    /// <see cref="ITenantProcessSupervisor"/> (singleton
    /// <see cref="TenantProcessSupervisor"/>),
    /// <see cref="IProcessStarter"/> (singleton
    /// <see cref="SystemDiagnosticsProcessStarter"/>), and
    /// <see cref="TenantLifecycleCoordinator"/> as an
    /// <see cref="IHostedService"/> that subscribes to the lifecycle event bus
    /// and drives supervisor transitions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Opt-in — kept separate from <see cref="AddBridgeOrchestration"/> so
    /// the Relay posture (ADR 0026 Posture B) can register the 5.2.A contracts
    /// without pulling the supervisor. Callers MUST have already invoked
    /// <see cref="AddBridgeOrchestration"/> (the supervisor depends on
    /// <see cref="BridgeOrchestrationOptions"/> and
    /// <see cref="ITenantRegistryEventBus"/>) and
    /// <see cref="AddBridgeOrchestrationHealth"/> (the supervisor depends on
    /// <see cref="ITenantEndpointRegistry"/>; the coordinator optionally reads
    /// <see cref="TenantHealthMonitor"/>). The supervisor also depends on
    /// <see cref="ITenantSeedProvider"/> — which in turn requires
    /// <see cref="IRootSeedProvider"/> to already be registered. This method
    /// registers the default <see cref="TenantSeedProvider"/>; callers MUST
    /// invoke <c>AddSunfishRootSeedProvider</c> (or otherwise register an
    /// <see cref="IRootSeedProvider"/>) first.
    /// </para>
    /// <para>
    /// Registers <see cref="ITenantProcessSupervisor"/> both as the interface
    /// and as the concrete <see cref="TenantProcessSupervisor"/> so callers
    /// (the coordinator, the admin UI in Wave 5.3) can resolve the same
    /// singleton instance.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddBridgeOrchestrationSupervisor(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IProcessStarter, SystemDiagnosticsProcessStarter>();
        // Wave 5.2 stop-work #1: per-tenant seed derivation. Requires
        // IRootSeedProvider to be registered first (Program.cs does this
        // via AddSunfishRootSeedProvider before invoking
        // AddBridgeOrchestrationSupervisor).
        services.AddSingleton<ITenantSeedProvider, TenantSeedProvider>();
        services.AddSingleton<TenantProcessSupervisor>();
        services.AddSingleton<ITenantProcessSupervisor>(
            sp => sp.GetRequiredService<TenantProcessSupervisor>());

        // Wave 5.2.E — the coordinator accepts an optional IServiceProvider
        // so unit tests can construct it without DI. Use an explicit factory
        // here so production wiring always supplies the provider (required for
        // the startup-rebuild Resume Protocol to read ITenantRegistry).
        services.AddHostedService(sp => new TenantLifecycleCoordinator(
            sp.GetRequiredService<ITenantRegistryEventBus>(),
            sp.GetRequiredService<ITenantProcessSupervisor>(),
            sp.GetService<TenantHealthMonitor>(),
            sp,
            sp.GetService<ILogger<TenantLifecycleCoordinator>>()));
        return services;
    }
}
