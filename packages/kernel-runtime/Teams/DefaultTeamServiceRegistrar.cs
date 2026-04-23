using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sunfish.Foundation.LocalFirst;
using Sunfish.Kernel.Buckets.DependencyInjection;
using Sunfish.Kernel.Events.DependencyInjection;
using Sunfish.Kernel.Lease.DependencyInjection;
using Sunfish.Kernel.Runtime.Notifications;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;
using Sunfish.Kernel.Sync.DependencyInjection;
using Sunfish.Kernel.Sync.Gossip;
using Sunfish.Kernel.Sync.Identity;
using Sunfish.Kernel.Sync.Protocol;

namespace Sunfish.Kernel.Runtime.Teams;

/// <summary>
/// Factory for the stock per-team <see cref="TeamServiceRegistrar"/> that
/// <c>AddSunfishMultiTeam</c> invokes when materializing a fresh
/// <see cref="TeamContext"/>. Wave 6.3.A lands the scaffold shell; Waves 6.3.B,
/// 6.3.C, and 6.3.D fill in the ledger trio, the sync pair, and the bucket
/// registry respectively. 6.3.E remains pending (composition-root rewire).
/// <list type="bullet">
///   <item><description>Wave 6.3.B — ledger trio: <c>IEventLog</c>,
///     <c>IQuarantineQueue</c>, <c>IEncryptedStore</c> — LANDED.</description></item>
///   <item><description>Wave 6.3.C — sync pair: per-team
///     <c>INodeIdentityProvider</c>, <c>ISyncDaemonTransport</c>,
///     <c>IGossipDaemon</c>, <c>ILeaseCoordinator</c> — LANDED.</description></item>
///   <item><description>Wave 6.3.D — <c>IBucketRegistry</c> + manifest
///     loader bound to <see cref="TeamPaths.BucketsDirectory"/> — LANDED.</description></item>
///   <item><description>Wave 6.3.E — <c>local-node-host</c> composition-root
///     rewire + <c>AddSunfishDefaultTeamRegistrar</c> sugar — PENDING.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// The composition-root helper <c>AddSunfishDefaultTeamRegistrar</c> is not
/// shipped in 6.3.A/B/C/D — callers wire
/// <c>AddSunfishMultiTeam(DefaultTeamServiceRegistrar.Compose(...))</c>
/// themselves. The extension-method sugar is bundled with the
/// <c>local-node-host</c> composition-root rewire in Wave 6.3.E.
/// </para>
/// </remarks>
public static class DefaultTeamServiceRegistrar
{
    /// <summary>
    /// Compose the per-team service registration callback for
    /// <c>AddSunfishMultiTeam</c>. Fills in the ledger trio (event log,
    /// quarantine queue, encrypted store), the sync pair (team-scoped node
    /// identity, per-team transport endpoint, gossip daemon, lease
    /// coordinator), and the bucket registry.
    /// </summary>
    /// <param name="dataDirectory">Install-level data directory that
    /// <see cref="TeamPaths"/> combines with each team id to produce the
    /// per-team SQLCipher DB path, event-log directory, bucket-manifest
    /// directory, and transport endpoint. Captured in the returned closure
    /// and passed through to the per-team registrations.</param>
    /// <param name="subkeyDerivation">The installed
    /// <see cref="ITeamSubkeyDerivation"/> — used to derive a team-scoped
    /// Ed25519 keypair from the root identity (via
    /// <see cref="TeamScopedNodeIdentity.Derive(NodeIdentity, string, ITeamSubkeyDerivation)"/>)
    /// before the per-team <see cref="INodeIdentityProvider"/> is registered
    /// (Wave 6.3.C).</param>
    /// <param name="rootIdentity">The install's root Ed25519 identity
    /// (<see cref="NodeIdentity.NodeId"/> + raw 32-byte public key + raw 32-byte
    /// private key). Used together with <paramref name="subkeyDerivation"/> to produce
    /// the team-scoped keypair per ADR 0032 §Device identity. The closure
    /// captures the reference; derivation runs once per team at
    /// registrar-invocation time.</param>
    /// <param name="sqlCipherKeyDerivation">The installed
    /// <see cref="ISqlCipherKeyDerivation"/> — used at store-activation time
    /// (Wave 6.3.E's <c>ITeamStoreActivator</c>) to derive the 32-byte
    /// SQLCipher key from the root seed + team id. Captured in the closure
    /// so the activator can resolve it alongside
    /// <see cref="Sunfish.Foundation.LocalFirst.Encryption.IEncryptedStore"/>
    /// from the per-team provider. The derivation itself is NOT invoked during
    /// registrar wiring — the store is registered unopened; see
    /// <c>_shared/product/wave-6.3-decomposition.md</c> §6.3.B for the
    /// Option-A rationale (deferred <c>OpenAsync</c>).</param>
    /// <returns>A <see cref="TeamServiceRegistrar"/> whose body wires the
    /// per-team ledger trio (6.3.B), sync pair (6.3.C), and bucket registry
    /// (6.3.D).</returns>
    /// <exception cref="ArgumentException"><paramref name="dataDirectory"/>
    /// is <c>null</c> or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="subkeyDerivation"/>,
    /// <paramref name="rootIdentity"/>, or <paramref name="sqlCipherKeyDerivation"/>
    /// is <c>null</c>.</exception>
    public static TeamServiceRegistrar Compose(
        string dataDirectory,
        ITeamSubkeyDerivation subkeyDerivation,
        NodeIdentity rootIdentity,
        ISqlCipherKeyDerivation sqlCipherKeyDerivation)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        ArgumentNullException.ThrowIfNull(subkeyDerivation);
        ArgumentNullException.ThrowIfNull(rootIdentity);
        ArgumentNullException.ThrowIfNull(sqlCipherKeyDerivation);

        return (services, teamId) =>
        {
            // Wave 6.3.B — ledger trio. The three services are grouped because
            // IQuarantineQueue's only per-team state is transitive through
            // IEventLog, and both it and IEncryptedStore share the same
            // directory-layout shape. All three flow from TeamPaths.
            services.AddSunfishEventLog(o =>
            {
                o.Directory = TeamPaths.EventLogDirectory(dataDirectory, teamId);
                o.EpochId = "epoch-0";
            });
            services.AddSunfishQuarantineQueue();
            services.AddSunfishEncryptedStore(o =>
            {
                o.DatabasePath = TeamPaths.DatabasePath(dataDirectory, teamId);
                o.KeystoreKeyName = TeamPaths.KeystoreKeyName(teamId);
            });

            // Wave 6.3.B stop-work resolution — SQLCipher key provisioning.
            //
            // The registrar does NOT call OpenAsync here (it would need the
            // root seed + an async context, and the registrar is a sync
            // delegate invoked inside TeamContextFactory.CreateAsync). Instead,
            // the 32-byte SQLCipher key is derived on demand by a later hosted
            // service (Wave 6.3.E's ITeamStoreActivator) that resolves
            // ISqlCipherKeyDerivation from the outer provider, reads the root
            // seed from whatever keystore facade the composition root wires,
            // and calls
            //   IEncryptedStore.OpenAsync(
            //       TeamPaths.DatabasePath(dataDirectory, teamId),
            //       sqlCipherKeyDerivation.DeriveSqlCipherKey(rootSeed, teamId.Value.ToString("D")),
            //       ct);
            //
            // sqlCipherKeyDerivation is captured in this closure so that when
            // 6.3.E's activator is dispatched it has a single, stable reference
            // to the exact derivation used at registrar-compose time — no
            // need to double-resolve it from the outer provider.
            _ = sqlCipherKeyDerivation; // reserved for 6.3.E activator wiring.

            // Wave 6.3.C — sync pair. Derives a team-scoped Ed25519 keypair
            // from the root identity + team id via TeamScopedNodeIdentity.Derive
            // (kernel-sync; ADR 0032 §Device identity), registers a per-team
            // INodeIdentityProvider seeded from it, then wires the gossip
            // daemon and lease coordinator against that identity. The team id
            // is rendered in GUID "D" form for the HKDF info string so it
            // matches the derivation contract in TeamSubkeyDerivation
            // (kernel-security) byte-for-byte.
            var teamIdentity = TeamScopedNodeIdentity.Derive(
                rootIdentity, teamId.Value.ToString("D"), subkeyDerivation);

            // Guard per 6.3.C risk note "Root/team identity conflation": the
            // install-level INodeIdentityProvider fallback in
            // AddSunfishKernelSync registers itself via TryAddSingleton and
            // would win if the outer container had already leaked one in. We
            // defensively clear any inherited registration and install the
            // team-scoped one ahead of AddSunfishKernelSync.
            services.RemoveAll<INodeIdentityProvider>();
            services.AddSingleton<INodeIdentityProvider>(
                new InMemoryNodeIdentityProvider(teamIdentity));

            // Per-team transport endpoint resolves the decomposition plan's
            // stop-work item #1: each team speaks on its own socket / named
            // pipe, so the install-level daemon does not need HELLO-level
            // team-id multiplexing. AddSunfishKernelSync's default transport
            // registration is TryAddSingleton, so we RemoveAll first and
            // install a concrete listening endpoint for this team.
            var transportEndpoint = TeamPaths.TransportEndpoint(dataDirectory, teamId);
            services.RemoveAll<ISyncDaemonTransport>();
            services.AddSingleton<ISyncDaemonTransport>(_ =>
                new UnixSocketSyncDaemonTransport(transportEndpoint));

            // AddSunfishKernelSync fills in VectorClock, IEd25519Signer, and
            // IGossipDaemon against the pre-registered INodeIdentityProvider
            // and ISyncDaemonTransport. Its TryAddSingleton guards mean our
            // earlier registrations are honored.
            services.AddSunfishKernelSync();

            // Lease coordinator's localNodeId is deterministic from the team
            // subkey's public key (first 16 bytes, lowercase hex). Identically
            // configured installs on different machines produce the same
            // localNodeId for the same root seed + team id — the test suite
            // pins that round-trip.
            var localNodeId = Convert.ToHexString(
                teamIdentity.PublicKey.AsSpan(0, 16)).ToLowerInvariant();
            services.AddSunfishKernelLease(
                localNodeId: localNodeId,
                localListenEndpoint: transportEndpoint);

            // Wave 6.3.D — buckets per-team. Manifest source directory is
            // TeamPaths.BucketsDirectory(dataDirectory, teamId). IBucketRegistry,
            // IBucketYamlLoader, IBucketFilterEvaluator, IBucketStubStore, and
            // IStorageBudgetManager are all installed per-team (TryAddSingleton
            // against this team's fresh ServiceCollection yields per-team
            // singletons; each TeamContext's provider owns its own instances).
            var bucketsDirectory = TeamPaths.BucketsDirectory(dataDirectory, teamId);
            services.AddSunfishKernelBuckets(o => o.SourceDirectory = bucketsDirectory);

            // Wave 6.5 — real notification producer. Subscribes to this
            // team's IGossipDaemon (registered above by AddSunfishKernelSync)
            // and emits TeamNotifications into the INotificationAggregator.
            // Without this the aggregator fan-in would still see only the
            // EmptyTeamNotificationStream placeholder and badge counts would
            // be pinned at zero even as inter-peer traffic flowed. The
            // placeholder stays in the package for test harnesses that do
            // not wire a gossip daemon.
            services.AddSingleton<ITeamNotificationStream>(sp =>
                new GossipEventTeamNotificationStream(
                    teamId, sp.GetRequiredService<IGossipDaemon>()));
        };
    }
}
