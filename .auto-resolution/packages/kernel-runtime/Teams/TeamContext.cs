using Microsoft.Extensions.DependencyInjection;

namespace Sunfish.Kernel.Runtime.Teams;

/// <summary>
/// Per-team runtime scope. Holds references to the team-scoped kernel services so
/// switching teams is a view rebind, not a service restart. Per ADR 0032.
/// </summary>
/// <remarks>
/// <para>
/// Each TeamContext owns its own child <see cref="IServiceProvider"/>. Services
/// resolved through <see cref="Services"/> are intended to be those that are
/// inherently team-scoped per ADR 0032 §Default: <c>IGossipDaemon</c>,
/// <c>ILeaseCoordinator</c>, <c>IEventLog</c>, <c>IEncryptedStore</c>,
/// <c>IQuarantineQueue</c>, <c>IBucketRegistry</c>, per-team <c>IPluginRegistry</c>,
/// etc. Wave 6.1 ships the scope container; Wave 6.3 populates it through the
/// <c>TeamServiceRegistrar</c> delegate passed to the factory.
/// </para>
/// <para>
/// Disposal cascades: <see cref="DisposeAsync"/> disposes the underlying
/// service provider, which in turn disposes any <see cref="IAsyncDisposable"/>
/// or <see cref="IDisposable"/> services that were resolved through it.
/// </para>
/// </remarks>
public sealed class TeamContext : IAsyncDisposable
{
    private int _disposed;

    /// <summary>The team this context scopes.</summary>
    public TeamId TeamId { get; }

    /// <summary>Human-readable display name (shown in the team-switcher UI).</summary>
    public string DisplayName { get; }

    /// <summary>Wall-clock time at which this context was materialized. UTC.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// The team-scoped DI service provider. Services resolved here are specific
    /// to this team's SQLCipher DB, event log, keystore entries, gossip daemon,
    /// etc. See <see cref="TeamContext"/>'s remarks.
    /// </summary>
    public IServiceProvider Services { get; }

    /// <summary>
    /// Construct a team context. Normally called only by <see cref="TeamContextFactory"/>
    /// — external code should prefer <c>ITeamContextFactory.GetOrCreateAsync</c>.
    /// </summary>
    /// <param name="teamId">Team this context is for.</param>
    /// <param name="displayName">Human-readable name for UIs.</param>
    /// <param name="services">Pre-built team-scoped service provider. The context takes
    /// ownership — it will dispose <paramref name="services"/> when the context is disposed,
    /// but <em>only</em> if the provider implements <see cref="IAsyncDisposable"/> or
    /// <see cref="IDisposable"/>.</param>
    public TeamContext(TeamId teamId, string displayName, IServiceProvider services)
    {
        ArgumentException.ThrowIfNullOrEmpty(displayName);
        ArgumentNullException.ThrowIfNull(services);

        TeamId = teamId;
        DisplayName = displayName;
        Services = services;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Disposes the team-scoped service provider. Idempotent — calling twice is a no-op.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        switch (Services)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().ConfigureAwait(false);
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }
}
