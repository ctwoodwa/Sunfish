// apps/kitchen-sink/Services/FakeTeamSwitcherFixture.cs
//
// Wave 6.6 Kitchen-sink demo — in-memory fakes for ITeamContextFactory /
// IActiveTeamAccessor / INotificationAggregator. These are NOT reusable; they
// exist only to drive SunfishTeamSwitcher's three demo variants (empty /
// single-legacy / multi-team) without needing a real kernel wire-up.
//
// The fakes delegate to DemoTeamSwitcherFixture.CurrentVariant, so the three
// variants share a single DI scope but appear independent to the component.
// Switching variants fires ActiveChanged / a synthetic notification to force
// the switcher to re-render.

using Microsoft.Extensions.DependencyInjection;
using Sunfish.Kernel.Runtime.Notifications;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.KitchenSink.Services;

/// <summary>Event log entry surfaced in the demo page's event-stream pane.</summary>
public sealed record DemoTeamSwitcherEvent(DateTimeOffset At, string Kind, string Payload);

/// <summary>
/// A named fixture: the three teams (possibly zero), their unread counts,
/// the active team (if any), and the legacy-primary team (for the single-team
/// demo). The demo page renders exactly one of these at a time.
/// </summary>
public sealed class DemoTeamSwitcherVariant
{
    public string Name { get; }
    public string Description { get; }
    public List<(TeamId Id, string DisplayName, int Unread)> Teams { get; } = new();
    public TeamId? ActiveTeamId { get; set; }
    public TeamId? LegacyPrimaryTeamId { get; set; }

    public DemoTeamSwitcherVariant(string name, string description)
    {
        Name = name;
        Description = description;
    }
}

/// <summary>
/// Registered Scoped in Program.cs. Holds the three demo variants and the
/// currently-selected one. The three fake kernel-runtime services read their
/// state from here, so flipping <see cref="Current"/> rewires what the
/// switcher shows on next render.
/// </summary>
public sealed class DemoTeamSwitcherFixture
{
    public event Action? VariantChanged;
    public event Action<DemoTeamSwitcherEvent>? EventLogged;

    public List<DemoTeamSwitcherVariant> Variants { get; }
    public DemoTeamSwitcherVariant Current { get; private set; }

    public DemoTeamSwitcherFixture()
    {
        // Variant 1 — empty state (freshly onboarded node, no teams yet).
        var empty = new DemoTeamSwitcherVariant(
            "Empty",
            "Zero teams — the post-install / pre-join state.");

        // Variant 2 — single legacy team (ADR 0032 migrated v1 node).
        var legacy = new DemoTeamSwitcherVariant(
            "Single legacy",
            "One team with the (primary) tag — per ADR 0032 line 187.");
        var legacyId = TeamId.New();
        legacy.Teams.Add((legacyId, "Personal workspace", 0));
        legacy.ActiveTeamId = legacyId;
        legacy.LegacyPrimaryTeamId = legacyId;

        // Variant 3 — multi-team with mixed unread badges (0, 3, 99+, 12).
        var multi = new DemoTeamSwitcherVariant(
            "Multi-team",
            "Four teams showing badge variety: 0 / 3 / 99+ / 12.");
        var acme = TeamId.New();
        var globex = TeamId.New();
        var initech = TeamId.New();
        var hooli = TeamId.New();
        multi.Teams.Add((acme, "Acme Corp", 0));
        multi.Teams.Add((globex, "Globex Research", 3));
        multi.Teams.Add((initech, "Initech Ops", 142));  // clamped to 99+ by the component
        multi.Teams.Add((hooli, "Hooli Platform", 12));
        multi.ActiveTeamId = globex;

        Variants = new List<DemoTeamSwitcherVariant> { empty, legacy, multi };
        Current = Variants[2];
    }

    public void SetVariant(DemoTeamSwitcherVariant next)
    {
        if (ReferenceEquals(next, Current))
        {
            return;
        }
        Current = next;
        VariantChanged?.Invoke();
    }

    public void LogEvent(string kind, string payload)
    {
        EventLogged?.Invoke(new DemoTeamSwitcherEvent(DateTimeOffset.Now, kind, payload));
    }
}

/// <summary>Demo-only <see cref="ITeamContextFactory"/> — reads teams from the fixture.</summary>
internal sealed class FakeTeamSwitcherContextFactory : ITeamContextFactory
{
    private readonly DemoTeamSwitcherFixture _fixture;
    private readonly Dictionary<TeamId, TeamContext> _cache = new();

    public FakeTeamSwitcherContextFactory(DemoTeamSwitcherFixture fixture)
    {
        _fixture = fixture;
    }

    public IReadOnlyCollection<TeamContext> Active
    {
        get
        {
            var list = new List<TeamContext>(_fixture.Current.Teams.Count);
            foreach (var (id, displayName, _) in _fixture.Current.Teams)
            {
                list.Add(GetOrMaterialize(id, displayName));
            }
            return list;
        }
    }

    public Task<TeamContext> GetOrCreateAsync(TeamId teamId, string displayName, CancellationToken ct)
        => Task.FromResult(GetOrMaterialize(teamId, displayName));

    public Task RemoveAsync(TeamId teamId, CancellationToken ct)
    {
        _cache.Remove(teamId);
        return Task.CompletedTask;
    }

    private TeamContext GetOrMaterialize(TeamId id, string displayName)
    {
        if (_cache.TryGetValue(id, out var existing))
        {
            return existing;
        }
        var sp = new ServiceCollection().BuildServiceProvider();
        var ctx = new TeamContext(id, displayName, sp);
        _cache[id] = ctx;
        return ctx;
    }
}

/// <summary>Demo-only <see cref="IActiveTeamAccessor"/> — reads active team from the fixture.</summary>
internal sealed class FakeTeamSwitcherActiveAccessor : IActiveTeamAccessor
{
    private readonly DemoTeamSwitcherFixture _fixture;
    private readonly FakeTeamSwitcherContextFactory _factory;

    public FakeTeamSwitcherActiveAccessor(DemoTeamSwitcherFixture fixture, ITeamContextFactory factory)
    {
        _fixture = fixture;
        _factory = (FakeTeamSwitcherContextFactory)factory;
        _fixture.VariantChanged += () => ActiveChanged?.Invoke(this,
            new ActiveTeamChangedEventArgs(null, Active));
    }

    public TeamContext? Active
    {
        get
        {
            var id = _fixture.Current.ActiveTeamId;
            if (id is null) return null;
            return _factory.Active.FirstOrDefault(c => c.TeamId.Equals(id.Value));
        }
    }

    public event EventHandler<ActiveTeamChangedEventArgs>? ActiveChanged;

    public Task SetActiveAsync(TeamId teamId, CancellationToken ct)
    {
        var previous = Active;
        _fixture.Current.ActiveTeamId = teamId;
        var current = Active;
        _fixture.LogEvent("SetActiveAsync",
            $"teamId={teamId} -> '{current?.DisplayName ?? "(null)"}'");
        ActiveChanged?.Invoke(this, new ActiveTeamChangedEventArgs(previous, current));
        return Task.CompletedTask;
    }
}

/// <summary>Demo-only <see cref="INotificationAggregator"/> — reads unread from the fixture.</summary>
internal sealed class FakeTeamSwitcherNotificationAggregator : INotificationAggregator
{
    private readonly DemoTeamSwitcherFixture _fixture;

    public FakeTeamSwitcherNotificationAggregator(DemoTeamSwitcherFixture fixture)
    {
        _fixture = fixture;
        _fixture.VariantChanged += () =>
            NotificationReceived?.Invoke(this, default);  // nudge the switcher to re-render
    }

    public event EventHandler<TeamNotification>? NotificationReceived;

    public int GetUnreadCount(TeamId teamId)
    {
        foreach (var (id, _, unread) in _fixture.Current.Teams)
        {
            if (id.Equals(teamId)) return unread;
        }
        return 0;
    }

    public int GetAggregateUnreadCount()
        => _fixture.Current.Teams.Sum(t => t.Unread);

    public async IAsyncEnumerable<TeamNotification> SubscribeAll(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield break;
    }

    public async IAsyncEnumerable<TeamNotification> SubscribeTeam(TeamId teamId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield break;
    }

    public ValueTask MarkReadAsync(TeamId teamId, string notificationId, CancellationToken ct)
        => ValueTask.CompletedTask;
}
