using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Sunfish.Kernel.Sync.Gossip;
using Sunfish.Kernel.Sync.Protocol;

namespace Sunfish.Kernel.Sync.DependencyInjection;

/// <summary>
/// DI extensions for registering the Sunfish intra-team gossip daemon
/// (paper §6.1–6.2; ADR 0029; sync-daemon-protocol spec).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="ISyncDaemonTransport"/>, <see cref="IGossipDaemon"/>,
    /// and <see cref="VectorClock"/> as singletons. Uses <c>TryAddSingleton</c>
    /// so a preceding registration (an <see cref="InMemorySyncDaemonTransport"/>
    /// for tests, a custom Unix-socket path, an in-tree stub, etc.) wins.
    /// </summary>
    /// <remarks>
    /// The default transport is <see cref="UnixSocketSyncDaemonTransport"/>
    /// with no listen endpoint (outbound-only). Applications that need an
    /// inbound listener — the typical daemon deployment — must register
    /// <see cref="ISyncDaemonTransport"/> themselves before calling this.
    /// The default <see cref="VectorClock"/> is an empty instance; the gossip
    /// daemon mutates it in place.
    /// </remarks>
    public static IServiceCollection AddSunfishKernelSync(
        this IServiceCollection services,
        Action<GossipDaemonOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.AddOptions<GossipDaemonOptions>();
        }

        services.TryAddSingleton<ISyncDaemonTransport>(_ => new UnixSocketSyncDaemonTransport());
        services.TryAddSingleton<VectorClock>(_ => new VectorClock());
        services.TryAddSingleton<IGossipDaemon, GossipDaemon>();

        return services;
    }
}
