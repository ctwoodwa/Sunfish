namespace Sunfish.Kernel.Runtime.Teams;

/// <summary>
/// Wave 6.3.E hosted-initialization helper for per-team encrypted stores.
/// </summary>
/// <remarks>
/// <para>
/// Wave 6.3.B deferred <c>IEncryptedStore.OpenAsync</c> out of the per-team
/// registrar: the registrar is a synchronous <see cref="TeamServiceRegistrar"/>
/// delegate and the SQLCipher key derivation + <c>OpenAsync</c> call are
/// asynchronous + need access to the install's root seed. 6.3.E's activator
/// closes that loop. A hosted service (or any bootstrap path that materializes
/// a team) calls <see cref="ActivateAsync"/> once per team; the activator
/// derives the team's 32-byte SQLCipher key via
/// <see cref="Sunfish.Kernel.Security.Keys.ISqlCipherKeyDerivation"/>, resolves
/// the team's unopened
/// <see cref="Sunfish.Foundation.LocalFirst.Encryption.IEncryptedStore"/> from
/// its <see cref="TeamContext"/>, and opens the store against the on-disk path
/// produced by <see cref="TeamPaths.DatabasePath(string, TeamId)"/>.
/// </para>
/// <para>
/// Idempotency: <see cref="ActivateAsync"/> is safe to call repeatedly for the
/// same <see cref="TeamId"/>. Concurrent calls for the same team coalesce on a
/// single underlying <c>OpenAsync</c> invocation; the store is opened exactly
/// once per team per process lifetime.
/// </para>
/// <para>
/// The activator does NOT create the team context itself — callers are
/// expected to drive <see cref="ITeamContextFactory.GetOrCreateAsync"/> first
/// (either directly or via a bootstrap hosted service). The activator only
/// materializes the
/// <see cref="Sunfish.Foundation.LocalFirst.Encryption.IEncryptedStore"/>
/// sitting inside an already-built <see cref="TeamContext"/>.
/// </para>
/// </remarks>
public interface ITeamStoreActivator
{
    /// <summary>
    /// Derive the SQLCipher key for <paramref name="teamId"/>, resolve the
    /// team's unopened <see cref="Sunfish.Foundation.LocalFirst.Encryption.IEncryptedStore"/>
    /// from its <see cref="TeamContext"/>, and call
    /// <c>OpenAsync</c> against <see cref="TeamPaths.DatabasePath(string, TeamId)"/>.
    /// Idempotent — subsequent calls with the same team id are no-ops.
    /// </summary>
    /// <param name="teamId">The team whose store to open.</param>
    /// <param name="ct">Cancellation token forwarded to
    /// <c>IEncryptedStore.OpenAsync</c>.</param>
    /// <returns>A task that completes once the store has been opened (or
    /// immediately if the team's store was already opened in a prior call).</returns>
    /// <exception cref="InvalidOperationException">The team has not been
    /// materialized via <see cref="ITeamContextFactory.GetOrCreateAsync"/>
    /// yet.</exception>
    ValueTask ActivateAsync(TeamId teamId, CancellationToken ct);
}
