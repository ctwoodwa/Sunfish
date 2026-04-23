using Microsoft.AspNetCore.Components;
using Sunfish.Kernel.Runtime.Notifications;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.UIAdapters.Blazor.Base;

namespace Sunfish.UIAdapters.Blazor.Components.LocalFirst;

/// <summary>
/// Wave 6.6 Slack-style team switcher sidebar. Binds to the kernel-runtime
/// multi-team services (<see cref="ITeamContextFactory"/>,
/// <see cref="IActiveTeamAccessor"/>, <see cref="INotificationAggregator"/>)
/// and exposes per-team unread counts plus a cross-team aggregate per ADR 0032.
/// </summary>
/// <remarks>
/// Subscribes to <see cref="INotificationAggregator.NotificationReceived"/> in
/// <see cref="OnInitializedAsync"/> and re-renders via <see cref="ComponentBase.InvokeAsync(System.Action)"/>
/// so badges refresh live. Also subscribes to <see cref="IActiveTeamAccessor.ActiveChanged"/>
/// so external team switches (e.g. from a hotkey) update the highlighted row.
/// The subscription is removed in <see cref="Dispose(bool)"/>.
/// </remarks>
public partial class SunfishTeamSwitcher : SunfishComponentBase
{
    [Inject] private ITeamContextFactory Factory { get; set; } = default!;
    [Inject] private IActiveTeamAccessor ActiveTeam { get; set; } = default!;
    [Inject] private INotificationAggregator Notifications { get; set; } = default!;

    /// <summary>
    /// Fired after the user clicks a team row. Raised <em>after</em>
    /// <see cref="IActiveTeamAccessor.SetActiveAsync"/> resolves so the consumer
    /// sees the new active state.
    /// </summary>
    [Parameter] public EventCallback<TeamId> OnTeamChanged { get; set; }

    /// <summary>
    /// Fired when the user clicks the "+ Add team" button. The component does
    /// not own the join flow (Wave 6.8 wires it); the consumer is expected to
    /// surface the QR-onboarding entry point in response.
    /// </summary>
    [Parameter] public EventCallback OnAddTeamRequested { get; set; }

    /// <summary>
    /// When set, the team with this id renders a <c>(primary)</c> tag per
    /// ADR 0032 line 187 — used by migrated v1 installs to mark the single
    /// legacy team until the user joins a second one.
    /// </summary>
    [Parameter] public TeamId? LegacyPrimaryTeamId { get; set; }

    /// <summary>Snapshot of all materialized team contexts, stable for the current render.</summary>
    internal IReadOnlyList<TeamContext> Teams { get; private set; } = Array.Empty<TeamContext>();

    /// <summary>The currently active team id (from <see cref="IActiveTeamAccessor"/>), or <c>null</c>.</summary>
    internal TeamId? ActiveTeamId => ActiveTeam.Active?.TeamId;

    /// <summary>Cross-team aggregate unread count, re-read every render.</summary>
    internal int AggregateUnread => Notifications.GetAggregateUnreadCount();

    private string Classes => CombineClasses("sf-team-switcher");

    /// <inheritdoc />
    protected override Task OnInitializedAsync()
    {
        RefreshTeams();
        Notifications.NotificationReceived += OnNotificationReceived;
        ActiveTeam.ActiveChanged += OnActiveTeamChanged;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Rebuilds the <see cref="Teams"/> snapshot from the factory. Exposed
    /// internally so tests can drive a refresh after mutating the factory.
    /// </summary>
    internal void RefreshTeams()
    {
        Teams = Factory.Active
            .OrderBy(t => t.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal async Task HandleRowClickAsync(TeamId teamId)
    {
        await ActiveTeam.SetActiveAsync(teamId, CancellationToken.None).ConfigureAwait(false);
        if (OnTeamChanged.HasDelegate)
        {
            await OnTeamChanged.InvokeAsync(teamId).ConfigureAwait(false);
        }
    }

    internal Task HandleAddClickAsync()
    {
        return OnAddTeamRequested.HasDelegate
            ? OnAddTeamRequested.InvokeAsync()
            : Task.CompletedTask;
    }

    /// <summary>
    /// Two-character upper-cased initials for the badge. Takes the first two
    /// non-whitespace characters of <see cref="TeamContext.DisplayName"/>;
    /// falls back to the GUID prefix if the display name is effectively empty.
    /// </summary>
    internal static string InitialsFor(TeamContext team)
    {
        var name = team.DisplayName?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            var id = team.TeamId.Value.ToString("N");
            return id.Length >= 2 ? id[..2].ToUpperInvariant() : id.ToUpperInvariant();
        }

        var parts = name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2 && parts[0].Length > 0 && parts[1].Length > 0)
        {
            return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[1][0])}";
        }

        return name.Length >= 2
            ? name[..2].ToUpperInvariant()
            : name.ToUpperInvariant();
    }

    /// <summary>Format unread counts — clamps at <c>99+</c> to keep the badge compact.</summary>
    internal static string FormatCount(int count)
        => count >= 100 ? "99+" : count.ToString();

    private void OnNotificationReceived(object? sender, TeamNotification e)
    {
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnActiveTeamChanged(object? sender, ActiveTeamChangedEventArgs e)
    {
        _ = InvokeAsync(StateHasChanged);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Notifications.NotificationReceived -= OnNotificationReceived;
            ActiveTeam.ActiveChanged -= OnActiveTeamChanged;
        }
        base.Dispose(disposing);
    }
}
