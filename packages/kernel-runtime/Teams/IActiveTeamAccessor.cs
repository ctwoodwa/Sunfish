namespace Sunfish.Kernel.Runtime.Teams;

/// <summary>
/// The UI's handle on "which team is currently foreground." Per ADR 0032.
/// </summary>
/// <remarks>
/// UI components bind <see cref="Active"/> and subscribe to
/// <see cref="ActiveChanged"/> to re-render when the user switches teams via
/// the team-switcher. Switching teams is a view rebind, not a service restart:
/// the new team's <see cref="TeamContext"/> is already materialized (all joined
/// teams sync in the background per ADR 0032 §Concurrency model).
/// </remarks>
public interface IActiveTeamAccessor
{
    /// <summary>Currently active team context; <c>null</c> if no team is active yet.</summary>
    TeamContext? Active { get; }

    /// <summary>
    /// Switch the UI's active team. Resolves the target context from the factory,
    /// fires <see cref="ActiveChanged"/>, and returns.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The team has not been materialized via
    /// <c>ITeamContextFactory.GetOrCreateAsync</c> yet.
    /// </exception>
    Task SetActiveAsync(TeamId teamId, CancellationToken ct);

    /// <summary>Fires after <see cref="Active"/> changes. Args carry previous + current.</summary>
    event EventHandler<ActiveTeamChangedEventArgs>? ActiveChanged;
}

/// <summary>Event args for <see cref="IActiveTeamAccessor.ActiveChanged"/>.</summary>
/// <param name="Previous">Team context that was active before the change, or <c>null</c>.</param>
/// <param name="Current">Team context that is now active, or <c>null</c> if explicitly cleared.</param>
public sealed record ActiveTeamChangedEventArgs(TeamContext? Previous, TeamContext? Current);
