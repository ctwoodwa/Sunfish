using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Sunfish.Kernel.Sync.Gossip;
using Sunfish.Kernel.Sync.Protocol;

namespace Sunfish.Kernel.Lease.DependencyInjection;

/// <summary>
/// DI extensions for registering the Flease lease coordinator (paper §6.3,
/// sync-daemon-protocol §6).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register <see cref="ILeaseCoordinator"/> as a singleton.
    /// </summary>
    /// <remarks>
    /// Depends on <see cref="ISyncDaemonTransport"/> and
    /// <see cref="IGossipDaemon"/> being registered — call
    /// <c>AddSunfishKernelSync()</c> first. The coordinator's node id and
    /// optional listen endpoint are supplied via
    /// <paramref name="localNodeId"/> and
    /// <paramref name="localListenEndpoint"/>; in a real deployment the
    /// node id comes from the Wave 1.6 IdentityPersistence output and the
    /// listen endpoint is the transport's configured Unix-socket path.
    /// </remarks>
    public static IServiceCollection AddSunfishKernelLease(
        this IServiceCollection services,
        string localNodeId,
        string? localListenEndpoint = null,
        Action<LeaseCoordinatorOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(localNodeId);

        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }
        else
        {
            services.AddOptions<LeaseCoordinatorOptions>();
        }

        services.TryAddSingleton<ILeaseCoordinator>(sp =>
        {
            var transport = sp.GetRequiredService<ISyncDaemonTransport>();
            var gossip = sp.GetRequiredService<IGossipDaemon>();
            var options = sp.GetRequiredService<IOptions<LeaseCoordinatorOptions>>();
            var logger = sp.GetService<ILogger<FleaseLeaseCoordinator>>();
            return new FleaseLeaseCoordinator(
                transport,
                gossip,
                options,
                localNodeId,
                localListenEndpoint,
                logger);
        });

        return services;
    }
}
