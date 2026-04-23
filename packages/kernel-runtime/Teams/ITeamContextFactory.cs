namespace Sunfish.Kernel.Runtime.Teams;

/// <summary>
/// Lifecycle manager for per-team <see cref="TeamContext"/> instances. Per ADR 0032.
/// </summary>
/// <remarks>
/// Materializes a team context lazily on first request; subsequent requests for
/// the same team return the same instance. Removal disposes the context and
/// evicts it from the live set. Implementations must be thread-safe.
/// </remarks>
public interface ITeamContextFactory
{
    /// <summary>
    /// Get-or-create a <see cref="TeamContext"/> for the given team. Lazily
    /// materializes on first call per team — subsequent calls with the same
    /// <paramref name="teamId"/> return the same instance (the <paramref name="displayName"/>
    /// passed on subsequent calls is ignored; the name set at first creation wins).
    /// </summary>
    /// <param name="teamId">The team to scope.</param>
    /// <param name="displayName">Human-readable team name. Used only on first creation.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<TeamContext> GetOrCreateAsync(TeamId teamId, string displayName, CancellationToken ct);

    /// <summary>All team contexts currently materialized. Snapshot — safe to enumerate
    /// concurrently with create/remove operations.</summary>
    IReadOnlyCollection<TeamContext> Active { get; }

    /// <summary>
    /// Dispose + remove the team context for <paramref name="teamId"/>. Used
    /// when a user leaves a team. No-op if the team was never materialized.
    /// </summary>
    Task RemoveAsync(TeamId teamId, CancellationToken ct);
}
