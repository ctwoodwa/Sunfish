using Microsoft.Extensions.DependencyInjection;
using Sunfish.Foundation.LocalFirst;
using Sunfish.Kernel.Buckets.DependencyInjection;
using Sunfish.Kernel.Events.DependencyInjection;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Kernel.Runtime.Teams;

/// <summary>
/// Factory for the stock per-team <see cref="TeamServiceRegistrar"/> that
/// <c>AddSunfishMultiTeam</c> invokes when materializing a fresh
/// <see cref="TeamContext"/>. Wave 6.3.A lands the scaffold shell; Waves 6.3.B
/// and 6.3.D fill in the ledger trio and the bucket registry respectively.
/// 6.3.C (gossip + lease + per-team identity) remains TODO.
/// <list type="bullet">
///   <item><description>Wave 6.3.B — ledger trio: <c>IEventLog</c>,
///     <c>IQuarantineQueue</c>, <c>IEncryptedStore</c> — LANDED.</description></item>
///   <item><description>Wave 6.3.C — sync pair: per-team
///     <c>INodeIdentityProvider</c>, <c>IGossipDaemon</c>,
///     <c>ILeaseCoordinator</c> — PENDING.</description></item>
///   <item><description>Wave 6.3.D — <c>IBucketRegistry</c> + manifest
///     loader bound to <see cref="TeamPaths.BucketsDirectory"/> — LANDED.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// The composition-root helper <c>AddSunfishDefaultTeamRegistrar</c> is not
/// shipped in 6.3.A/B/D — callers wire
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
    /// quarantine queue, encrypted store) and the bucket registry. The sync
    /// pair (gossip + lease) lands in Wave 6.3.C.
    /// </summary>
    /// <param name="dataDirectory">Install-level data directory that
    /// <see cref="TeamPaths"/> combines with each team id to produce the
    /// per-team SQLCipher DB path, event-log directory, and bucket-manifest
    /// directory. Captured in the returned closure and passed through to the
    /// per-team registrations.</param>
    /// <param name="subkeyDerivation">The installed
    /// <see cref="ITeamSubkeyDerivation"/> — used by 6.3.C to derive a
    /// team-scoped Ed25519 keypair from the root signer (via
    /// <c>TeamScopedNodeIdentity.Derive</c> in <c>Sunfish.Kernel.Sync</c>)
    /// before the per-team <c>INodeIdentityProvider</c> is registered.</param>
    /// <param name="rootSigner">The install's root Ed25519 signer. Used
    /// together with <paramref name="subkeyDerivation"/> to produce the
    /// team-scoped keypair per ADR 0032 §Device identity. The closure only
    /// holds the reference — no signing happens until 6.3.C wires the body.</param>
    /// <param name="sqlCipherKeyDerivation">The installed
    /// <see cref="ISqlCipherKeyDerivation"/> — used at store-activation time
    /// (Wave 6.3.E's <c>ITeamStoreActivator</c>) to derive the 32-byte
    /// SQLCipher key from the root seed + team id. Captured in the closure
    /// so the activator can resolve it alongside <see cref="Sunfish.Foundation.LocalFirst.Encryption.IEncryptedStore"/>
    /// from the per-team provider. The derivation itself is NOT invoked during
    /// registrar wiring — the store is registered unopened; see
    /// <c>_shared/product/wave-6.3-decomposition.md</c> §6.3.B for the
    /// Option-A rationale (deferred <c>OpenAsync</c>).</param>
    /// <returns>A <see cref="TeamServiceRegistrar"/> whose body wires the
    /// per-team ledger trio (6.3.B) and bucket registry (6.3.D). 6.3.C will
    /// append the sync-pair registrations to the same callback.</returns>
    /// <exception cref="ArgumentException"><paramref name="dataDirectory"/>
    /// is <c>null</c> or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="subkeyDerivation"/>,
    /// <paramref name="rootSigner"/>, or <paramref name="sqlCipherKeyDerivation"/>
    /// is <c>null</c>.</exception>
    public static TeamServiceRegistrar Compose(
        string dataDirectory,
        ITeamSubkeyDerivation subkeyDerivation,
        IEd25519Signer rootSigner,
        ISqlCipherKeyDerivation sqlCipherKeyDerivation)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        ArgumentNullException.ThrowIfNull(subkeyDerivation);
        ArgumentNullException.ThrowIfNull(rootSigner);
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

            // Wave 6.3.C — gossip + lease + per-team INodeIdentityProvider
            //             derived via subkeyDerivation + rootSigner.

            // Wave 6.3.D — buckets per-team. Manifest source directory is
            // TeamPaths.BucketsDirectory(dataDirectory, teamId). IBucketRegistry,
            // IBucketYamlLoader, IBucketFilterEvaluator, IBucketStubStore, and
            // IStorageBudgetManager are all installed per-team (TryAddSingleton
            // against this team's fresh ServiceCollection yields per-team
            // singletons; each TeamContext's provider owns its own instances).
            var bucketsDirectory = TeamPaths.BucketsDirectory(dataDirectory, teamId);
            services.AddSunfishKernelBuckets(o => o.SourceDirectory = bucketsDirectory);
        };
    }
}
