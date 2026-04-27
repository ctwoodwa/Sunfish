using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Sync.Application;
using Sunfish.Kernel.Sync.Discovery;
using Sunfish.Kernel.Sync.Gossip;
using Sunfish.Kernel.Sync.Identity;
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
    /// <see cref="VectorClock"/>, <see cref="IEd25519Signer"/>, and
    /// <see cref="INodeIdentityProvider"/> as singletons. Uses
    /// <c>TryAddSingleton</c> so a preceding registration (an
    /// <see cref="InMemorySyncDaemonTransport"/> for tests, a custom
    /// Unix-socket path, a keystore-backed identity provider, etc.) wins.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default transport is <see cref="UnixSocketSyncDaemonTransport"/>
    /// with no listen endpoint (outbound-only). Applications that need an
    /// inbound listener — the typical daemon deployment — must register
    /// <see cref="ISyncDaemonTransport"/> themselves before calling this.
    /// The default <see cref="VectorClock"/> is an empty instance; the gossip
    /// daemon mutates it in place.
    /// </para>
    /// <para>
    /// <b>Identity fallback.</b> If no <see cref="INodeIdentityProvider"/> is
    /// already registered, one is generated on first resolve by calling
    /// <see cref="IEd25519Signer.GenerateKeyPair"/> and deriving a hex
    /// <c>NodeId</c> from the first 16 bytes of the public key. This is
    /// suitable for tests, bootstrap, and single-node CLI harnesses only.
    /// Production composition roots (for example <c>apps/local-node-host</c>)
    /// register their own <see cref="INodeIdentityProvider"/> backed by an
    /// <c>IKeystore</c> lookup so the keypair survives restarts.
    /// </para>
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
        services.TryAddSingleton<IEd25519Signer, Ed25519Signer>();

        // Fallback node-identity provider: generates a fresh keypair the
        // first time someone asks. Production composition roots register
        // their own IKeystore-backed INodeIdentityProvider before calling
        // AddSunfishKernelSync, so this factory only runs in tests and the
        // scaffolding-CLI bootstrap path.
        services.TryAddSingleton<INodeIdentityProvider>(sp =>
        {
            var signer = sp.GetRequiredService<IEd25519Signer>();
            var (publicKey, privateKey) = signer.GenerateKeyPair();
            // Derive a hex NodeId from the first 16 bytes of the public key.
            // Real deployments persist this — the per-process generation here
            // would give each restart a new identity, which is the correct
            // behaviour for the test/bootstrap fallback.
            var nodeIdBytes = new byte[16];
            Buffer.BlockCopy(publicKey, 0, nodeIdBytes, 0, 16);
            var nodeIdHex = Convert.ToHexString(nodeIdBytes).ToLowerInvariant();
            return new InMemoryNodeIdentityProvider(
                new NodeIdentity(nodeIdHex, publicKey, privateKey));
        });

        // Wave 2.5 — DELTA_STREAM application defaults. The gossip daemon's
        // round loop dispatches outbound delta encoding to IDeltaProducer and
        // inbound delta application to IDeltaSink. Defaults are no-ops so
        // PING-only deployments behave unchanged; Anchor / local-node-host
        // register concrete implementations (backed by ICrdtDocument) before
        // calling AddSunfishKernelSync.
        services.TryAddSingleton<IDeltaProducer, NoopDeltaProducer>();
        services.TryAddSingleton<IDeltaSink, NoopDeltaSink>();

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
