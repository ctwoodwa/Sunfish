using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Sunfish.Kernel.Sync.Discovery;
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

    /// <summary>
    /// Register <see cref="MdnsPeerDiscovery"/> as the <see cref="IPeerDiscovery"/>
    /// implementation (paper §6.1 tier-1, LAN-only). Call after
    /// <see cref="AddSunfishKernelSync"/> — the gossip daemon is resolved
    /// lazily, so registration order only matters for options binding.
    /// </summary>
    /// <remarks>
    /// This registration does not wire the discovery source into the gossip
    /// daemon automatically — the caller must call
    /// <see cref="GossipDaemonDiscoveryExtensions.AttachDiscovery"/> in their
    /// startup path. The bridge is explicit because the lifecycle (when to
    /// start advertising, what <see cref="PeerAdvertisement"/> to publish) is
    /// application-owned.
    /// </remarks>
    public static IServiceCollection AddMdnsPeerDiscovery(
        this IServiceCollection services,
        Action<PeerDiscoveryOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.AddOptions<PeerDiscoveryOptions>();
        }

        services.TryAddSingleton<IPeerDiscovery, MdnsPeerDiscovery>();
        return services;
    }

    /// <summary>
    /// Register <see cref="InMemoryPeerDiscovery"/> as the
    /// <see cref="IPeerDiscovery"/> implementation. Tests and integration
    /// harnesses only — the shared broker is process-wide.
    /// </summary>
    public static IServiceCollection AddInMemoryPeerDiscovery(
        this IServiceCollection services,
        Action<PeerDiscoveryOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.AddOptions<PeerDiscoveryOptions>();
        }

        services.TryAddSingleton<InMemoryPeerDiscoveryBroker>(_ => InMemoryPeerDiscoveryBroker.Shared);
        services.TryAddSingleton<IPeerDiscovery>(sp =>
            new InMemoryPeerDiscovery(
                sp.GetRequiredService<InMemoryPeerDiscoveryBroker>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PeerDiscoveryOptions>>().Value));
        return services;
    }
}
