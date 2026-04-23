using Microsoft.Extensions.DependencyInjection;
using Sunfish.Kernel.Buckets.DependencyInjection;
using Sunfish.Kernel.Security.Crypto;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Kernel.Runtime.Teams;

/// <summary>
/// Factory for the stock per-team <see cref="TeamServiceRegistrar"/> that
/// <c>AddSunfishMultiTeam</c> invokes when materializing a fresh
/// <see cref="TeamContext"/>. Wave 6.3.A lands the scaffold shell only —
/// the callback body is an explicit no-op, and the real per-team service
/// registrations are filled in by the three parallel sub-waves:
/// <list type="bullet">
///   <item><description>Wave 6.3.B — ledger trio: <c>IEventLog</c>,
///     <c>IQuarantineQueue</c>, <c>IEncryptedStore</c>.</description></item>
///   <item><description>Wave 6.3.C — sync pair: per-team
///     <c>INodeIdentityProvider</c>, <c>IGossipDaemon</c>,
///     <c>ILeaseCoordinator</c>.</description></item>
///   <item><description>Wave 6.3.D — <c>IBucketRegistry</c> + manifest
///     loader bound to <see cref="TeamPaths.BucketsDirectory"/>.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// The composition-root helper <c>AddSunfishDefaultTeamRegistrar</c> is not
/// shipped in 6.3.A — callers wire
/// <c>AddSunfishMultiTeam(DefaultTeamServiceRegistrar.Compose(...))</c>
/// themselves. The extension-method sugar is bundled with the
/// <c>local-node-host</c> composition-root rewire in Wave 6.3.E.
/// </para>
/// </remarks>
public static class DefaultTeamServiceRegistrar
{
    /// <summary>
    /// Compose the per-team service registration callback for
    /// <c>AddSunfishMultiTeam</c>. Wave 6.3.A ships the scaffold shell —
    /// actual service registrations (event log, encrypted store, gossip,
    /// lease, buckets) land in Waves 6.3.B / 6.3.C / 6.3.D.
    /// </summary>
    /// <param name="dataDirectory">Install-level data directory that
    /// <see cref="TeamPaths"/> combines with each team id to produce the
    /// per-team SQLCipher DB path, event-log directory, and bucket-manifest
    /// directory. Captured in the returned closure and passed through to the
    /// per-team registrations in 6.3.B / 6.3.D.</param>
    /// <param name="subkeyDerivation">The installed
    /// <see cref="ITeamSubkeyDerivation"/> — used by 6.3.C to derive a
    /// team-scoped Ed25519 keypair from the root signer (via
    /// <c>TeamScopedNodeIdentity.Derive</c> in <c>Sunfish.Kernel.Sync</c>)
    /// before the per-team <c>INodeIdentityProvider</c> is registered.</param>
    /// <param name="rootSigner">The install's root Ed25519 signer. Used
    /// together with <paramref name="subkeyDerivation"/> to produce the
    /// team-scoped keypair per ADR 0032 §Device identity. The closure only
    /// holds the reference — no signing happens until 6.3.C wires the body.</param>
    /// <returns>A <see cref="TeamServiceRegistrar"/> whose body wires the
    /// per-team bucket registry (6.3.D). 6.3.B and 6.3.C will append their
    /// registrations to the same callback in subsequent waves.</returns>
    /// <exception cref="ArgumentException"><paramref name="dataDirectory"/>
    /// is <c>null</c> or empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="subkeyDerivation"/>
    /// or <paramref name="rootSigner"/> is <c>null</c>.</exception>
    public static TeamServiceRegistrar Compose(
        string dataDirectory,
        ITeamSubkeyDerivation subkeyDerivation,
        IEd25519Signer rootSigner)
    {
        ArgumentException.ThrowIfNullOrEmpty(dataDirectory);
        ArgumentNullException.ThrowIfNull(subkeyDerivation);
        ArgumentNullException.ThrowIfNull(rootSigner);

        return (services, teamId) =>
        {
            // Wave 6.3.B: event-log + quarantine + encrypted-store
            //             registrations, keyed by TeamPaths.EventLogDirectory,
            //             TeamPaths.DatabasePath, and TeamPaths.KeystoreKeyName.
            // Wave 6.3.C: gossip + lease + per-team INodeIdentityProvider
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
