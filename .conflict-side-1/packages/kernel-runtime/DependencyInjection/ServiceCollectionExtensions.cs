using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Kernel.Runtime.Notifications;
using Sunfish.Kernel.Runtime.Scheduling;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Sync.Identity;

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

    /// <summary>
    /// Registers the Wave 6.4 <see cref="IResourceGovernor"/> and its
    /// <see cref="ResourceGovernorOptions"/>. The governor caps concurrent
    /// gossip rounds per tick (default 2 per ADR 0032) so a user in 4+ teams
    /// does not stampede the network + CPU every 30 seconds.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT called from <see cref="AddSunfishKernelRuntime"/> —
    /// the composition root opts in so each deployment shape (Anchor desktop,
    /// Bridge hosted-node, tests) can configure its own cap.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="configure">Optional callback to tune
    /// <see cref="ResourceGovernorOptions"/>.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishResourceGovernor(
        this IServiceCollection services,
        Action<ResourceGovernorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.AddOptions<ResourceGovernorOptions>();
        }

        services.TryAddSingleton<IResourceGovernor, ResourceGovernor>();
        return services;
    }

    /// <summary>
    /// Installs <c>AddSunfishMultiTeam</c> with the stock
    /// <see cref="DefaultTeamServiceRegistrar"/> composed against the install's
    /// root identity + the per-install data directory. Composition roots that
    /// want the standard Wave 6.3 behaviour call this instead of assembling
    /// the registrar themselves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// We take the install-level dependencies as explicit arguments rather
    /// than resolving them from the outer service provider because
    /// <see cref="TeamServiceRegistrar"/> is a synchronous
    /// <c>delegate void (IServiceCollection, TeamId)</c> invoked inside
    /// <c>TeamContextFactory.CreateAsync</c> — there is no
    /// <see cref="IServiceProvider"/> in scope when the registrar runs. The
    /// composition root resolves <paramref name="rootIdentity"/>,
    /// <paramref name="subkeyDerivation"/>, and
    /// <paramref name="sqlCipherKeyDerivation"/> up front (the two derivation
    /// services are pure, side-effect-free utilities, so instantiating them
    /// directly at Program-level is equivalent to resolving them from a
    /// mini-provider and avoids the two-phase DI build) and passes them in
    /// here.
    /// </para>
    /// <para>
    /// This is the Wave 6.3.E composition-root-sugar layer bundled with the
    /// <c>local-node-host</c> rewire. Callers outside <c>local-node-host</c>
    /// (tests, future composition roots) use the same overload; the
    /// <see cref="DefaultTeamServiceRegistrar"/> factory they wrap is unchanged.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="dataDirectory">Install-level data directory that
    /// <see cref="TeamPaths"/> combines with each team id to produce per-team
    /// paths. Must be non-null and non-empty.</param>
    /// <param name="rootIdentity">The install's root Ed25519 identity
    /// (<see cref="NodeIdentity.NodeId"/> + raw public + raw private key).
    /// Used together with <paramref name="subkeyDerivation"/> to produce each
    /// team-scoped keypair.</param>
    /// <param name="subkeyDerivation">Derivation that turns the root private
    /// key + team id into a team-scoped Ed25519 keypair. Must match the
    /// <see cref="ITeamSubkeyDerivation"/> implementation registered via
    /// <c>AddSunfishKernelSecurity</c> (byte-for-byte — the same HKDF info
    /// label is used on both sides of the multi-team key boundary).</param>
    /// <param name="sqlCipherKeyDerivation">Derivation that produces the
    /// 32-byte SQLCipher key from the root seed + team id. Captured inside
    /// the registrar closure for later use by the Wave 6.3.E
    /// <c>ITeamStoreActivator</c> — it is NOT invoked at registrar-compose
    /// time (the encrypted store is registered unopened).</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishDefaultTeamRegistrar(
        this IServiceCollection services,
        string dataDirectory,
        NodeIdentity rootIdentity,
        ITeamSubkeyDerivation subkeyDerivation,
        ISqlCipherKeyDerivation sqlCipherKeyDerivation)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        ArgumentNullException.ThrowIfNull(rootIdentity);
        ArgumentNullException.ThrowIfNull(subkeyDerivation);
        ArgumentNullException.ThrowIfNull(sqlCipherKeyDerivation);

        services.AddSunfishMultiTeam(DefaultTeamServiceRegistrar.Compose(
            dataDirectory, subkeyDerivation, rootIdentity, sqlCipherKeyDerivation));
        return services;
    }

    /// <summary>
    /// Registers the Wave 6.3.E <see cref="ITeamStoreActivator"/> as a
    /// singleton. The activator, on demand (typically from a hosted service
    /// that runs after team materialization), derives the 32-byte SQLCipher
    /// key for a team via <see cref="ISqlCipherKeyDerivation"/> and calls
    /// <c>IEncryptedStore.OpenAsync</c> against the per-team database path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The activator takes a copy of the 32-byte root seed in its ctor — the
    /// composition root reads it from the keystore once at startup and passes
    /// it here. The activator itself never exposes the seed; callers only see
    /// the single-method <see cref="ITeamStoreActivator.ActivateAsync"/>
    /// contract.
    /// </para>
    /// <para>
    /// Depends on <see cref="ITeamContextFactory"/> and
    /// <see cref="ISqlCipherKeyDerivation"/> being registered. Call
    /// <see cref="AddSunfishMultiTeam"/> (or
    /// <see cref="AddSunfishDefaultTeamRegistrar"/>) and
    /// <c>AddSunfishKernelSecurity</c> first.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="rootSeed">The install's 32-byte Ed25519 root seed.
    /// Captured by the activator and reused for every team's key derivation;
    /// must be exactly 32 bytes.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishTeamStoreActivator(
        this IServiceCollection services,
        ReadOnlyMemory<byte> rootSeed)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (rootSeed.Length != 32)
        {
            throw new ArgumentException(
                $"Root seed must be 32 bytes (was {rootSeed.Length}).",
                nameof(rootSeed));
        }

        // Copy the seed so the caller can zero their own buffer without
        // corrupting the activator's key material.
        var seedCopy = rootSeed.ToArray();
        services.TryAddSingleton<ITeamStoreActivator>(sp =>
        {
            var factory = sp.GetRequiredService<ITeamContextFactory>();
            var derivation = sp.GetRequiredService<ISqlCipherKeyDerivation>();
            return new TeamStoreActivator(factory, derivation, seedCopy);
        });
        return services;
    }

    /// <summary>
    /// Registers the Wave 6.5 <see cref="INotificationAggregator"/> as a
    /// singleton backed by <see cref="NotificationAggregator"/>. Per ADR 0032's
    /// all-teams-sync / one-renders model, the aggregator fans every
    /// registered <see cref="ITeamNotificationStream"/> into a single feed plus
    /// per-team + aggregate unread counts for UI binding.
    /// </summary>
    /// <remarks>
    /// Opt-in by the composition root — the caller composes which streams are
    /// registered (Wave 6.3 does the per-team wiring once real gossip /
    /// event-log streams exist). No <see cref="ITeamNotificationStream"/>
    /// instances are registered here.
    /// </remarks>
    /// <param name="services">The service collection to add to.</param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddSunfishNotificationAggregator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<INotificationAggregator, NotificationAggregator>();
        return services;
    }
}
