using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Kernel.Runtime.DependencyInjection;

/// <summary>
/// DI extensions for registering the Sunfish kernel runtime (paper §5.1).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IPluginRegistry"/> and <see cref="INodeHost"/> as
    /// singletons backed by <see cref="PluginRegistry"/> and
    /// <see cref="NodeHost"/>. Uses <c>TryAddSingleton</c> so prior
    /// registrations (test doubles, alternative hosts) win.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishKernelRuntime(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<IPluginRegistry, PluginRegistry>();
        services.TryAddSingleton<INodeHost, NodeHost>();
        return services;
    }

    /// <summary>
    /// Registers the multi-team runtime surface from ADR 0032:
    /// <see cref="ITeamContextFactory"/> and <see cref="IActiveTeamAccessor"/>,
    /// both as singletons.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="registrar">Optional per-team service registrar. Wave 6.1 defaults
    /// to <see cref="TeamContextFactory.DefaultRegistrar"/> (empty service provider).
    /// Wave 6.3 will pass a real registrar that wires per-team <c>IGossipDaemon</c>,
    /// <c>ILeaseCoordinator</c>, <c>IEventLog</c>, <c>IEncryptedStore</c>,
    /// <c>IQuarantineQueue</c>, <c>IBucketRegistry</c>, etc.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishMultiTeam(
        this IServiceCollection services,
        TeamServiceRegistrar? registrar = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<ITeamContextFactory>(_ => new TeamContextFactory(registrar));
        services.TryAddSingleton<IActiveTeamAccessor, ActiveTeamAccessor>();
        return services;
    }
}
