using System.Collections.Concurrent;

using Microsoft.Extensions.DependencyInjection;

using Sunfish.Foundation.LocalFirst.Encryption;
using Sunfish.Kernel.Security.Keys;

namespace Sunfish.Kernel.Runtime.Teams;

/// <summary>
/// Default <see cref="ITeamStoreActivator"/> — derives each team's SQLCipher
/// key from the install's 32-byte root seed + the team id, resolves the team's
/// unopened <see cref="IEncryptedStore"/> from its <see cref="TeamContext"/>,
/// and calls <c>OpenAsync</c>. See <see cref="ITeamStoreActivator"/> remarks
/// for the Wave 6.3.B/6.3.E design context.
/// </summary>
/// <remarks>
/// <para>
/// Idempotency and concurrency: the activator deduplicates activations per
/// <see cref="TeamId"/> using a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// of in-flight or completed <see cref="Task"/>s. Ten simultaneous calls to
/// <c>ActivateAsync(teamA, ...)</c> yield exactly one
/// <c>IEncryptedStore.OpenAsync</c> invocation.
/// </para>
/// <para>
/// Key material hygiene: the root seed is stored as a <see cref="byte"/> array
/// on the activator for the process lifetime. The derived SQLCipher key is
/// written into the store via <c>OpenAsync</c> and then handed to the GC; we
/// do NOT hold onto derived keys beyond the open call. The root seed itself is
/// the caller's responsibility to source from a keystore and pass in.
/// </para>
/// </remarks>
public sealed class TeamStoreActivator : ITeamStoreActivator
{
    private readonly ITeamContextFactory _factory;
    private readonly ISqlCipherKeyDerivation _derivation;
    private readonly byte[] _rootSeed;
    private readonly ConcurrentDictionary<TeamId, Task> _activations = new();

    /// <summary>Create an activator.</summary>
    /// <param name="factory">The team context factory from which to resolve
    /// each team's <see cref="IEncryptedStore"/>.</param>
    /// <param name="derivation">The SQLCipher key derivation (HKDF-based).</param>
    /// <param name="rootSeed">The install's 32-byte Ed25519 root seed. The
    /// activator takes a defensive copy.</param>
    /// <exception cref="ArgumentNullException">Any constructor argument is
    /// <c>null</c>.</exception>
    /// <exception cref="ArgumentException"><paramref name="rootSeed"/> is not
    /// exactly 32 bytes.</exception>
    public TeamStoreActivator(
        ITeamContextFactory factory,
        ISqlCipherKeyDerivation derivation,
        ReadOnlyMemory<byte> rootSeed)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(derivation);
        if (rootSeed.Length != 32)
        {
            throw new ArgumentException(
                $"Root seed must be 32 bytes (was {rootSeed.Length}).",
                nameof(rootSeed));
        }

        _factory = factory;
        _derivation = derivation;
        _rootSeed = rootSeed.ToArray();
    }

    /// <inheritdoc />
    public ValueTask ActivateAsync(TeamId teamId, CancellationToken ct)
    {
        // Dedupe: GetOrAdd ensures only one ActivateCoreAsync task is created
        // per TeamId. Subsequent callers await the same Task — once it
        // completes (successfully or not), they observe the same result.
        //
        // Note on failure semantics: if OpenAsync fails, the failed Task stays
        // in the dictionary and subsequent callers will observe the same
        // exception. This is intentional — a failure to open the store is a
        // persistent condition (wrong key, corrupted DB) and retrying silently
        // would hide the underlying problem. Callers that want to retry after
        // a fix must tear down the TeamContext via IntermediaryFactory
        // .RemoveAsync and re-materialize.
        var task = _activations.GetOrAdd(
            teamId,
            static (tid, state) => state.Self.ActivateCoreAsync(tid, state.Ct),
            (Self: this, Ct: ct));
        return new ValueTask(task);
    }

    private async Task ActivateCoreAsync(TeamId teamId, CancellationToken ct)
    {
        // Yield so concurrent GetOrAdd callers all observe our Task under the
        // dictionary's internal lock before we complete synchronously.
        await Task.Yield();
        ct.ThrowIfCancellationRequested();

        var context = FindContext(teamId)
            ?? throw new InvalidOperationException(
                $"Team {teamId.Value:D} has not been materialized via " +
                $"ITeamContextFactory.GetOrCreateAsync; call that first.");

        var store = context.Services.GetRequiredService<IEncryptedStore>();
        var key = _derivation.DeriveSqlCipherKey(_rootSeed, teamId.Value.ToString("D"));
        try
        {
            // The database path is recomputed from the install's data
            // directory + team id via TeamPaths — matches exactly what the
            // registrar wrote into EncryptionOptions.DatabasePath, but we
            // don't read that options value here (would require resolving
            // IOptions<EncryptionOptions> from the per-team provider, one
            // extra dependency for the same string).
            //
            // We DO read the path directly from the per-team EncryptionOptions
            // so that any test-time override of the DB path (set via the
            // registrar) is honored. Reading IOptions<EncryptionOptions> from
            // the same team-scoped provider guarantees we open exactly the
            // file the registrar configured.
            var options = context.Services
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<EncryptionOptions>>()
                .Value;
            await store.OpenAsync(options.DatabasePath, key, ct).ConfigureAwait(false);
        }
        finally
        {
            // Best-effort zeroing of the derived key. The GC-visible array is
            // still in memory until collected, but we drop our reference to
            // the live value so it's at least not in scope on this frame.
            Array.Clear(key, 0, key.Length);
        }
    }

    private TeamContext? FindContext(TeamId teamId)
    {
        foreach (var ctx in _factory.Active)
        {
            if (ctx.TeamId.Equals(teamId))
            {
                return ctx;
            }
        }
        return null;
    }
}
