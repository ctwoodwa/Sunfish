using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Sunfish.Foundation.Configuration;
using Sunfish.Foundation.Services;
using Sunfish.Kernel.Runtime.Notifications;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.UIAdapters.Blazor.Components.LocalFirst;
using Sunfish.UIAdapters.Blazor.Internal.Interop;
using Sunfish.UICore.Contracts;
using Xunit;

namespace Sunfish.UIAdapters.Blazor.Tests.Components.LocalFirst;

/// <summary>
/// Wave 6.6 — Bunit tests for <see cref="SunfishTeamSwitcher"/>. Uses fakes
/// rather than NSubstitute because <see cref="ITeamContextFactory"/>'s
/// <c>Active</c> enumerates <see cref="TeamContext"/> which requires a real
/// <see cref="IServiceProvider"/> to construct — fakes keep the test bodies
/// direct and avoid mocking non-virtual behaviour.
/// </summary>
public class SunfishTeamSwitcherTests : BunitContext
{
    private readonly FakeTeamContextFactory _factory = new();
    private readonly FakeActiveTeamAccessor _active;
    private readonly FakeNotificationAggregator _notifications = new();

    public SunfishTeamSwitcherTests()
    {
        _active = new FakeActiveTeamAccessor(_factory);

        Services.AddSingleton<SunfishOptions>();
        Services.AddScoped<ISunfishThemeService, SunfishThemeService>();
        Services.AddScoped<ISunfishCssProvider, StubCssProvider>();
        Services.AddScoped<ISunfishIconProvider, StubIconProvider>();
        Services.AddScoped<ISunfishJsModuleLoader>(_ => Substitute.For<ISunfishJsModuleLoader>());

        Services.AddSingleton<ITeamContextFactory>(_factory);
        Services.AddSingleton<IActiveTeamAccessor>(_active);
        Services.AddSingleton<INotificationAggregator>(_notifications);
    }

    [Fact]
    public void Renders_one_row_per_team()
    {
        _factory.AddTeam("Acme Corp");
        _factory.AddTeam("Globex");
        _factory.AddTeam("Initech");

        var cut = Render<SunfishTeamSwitcher>();

        Assert.Equal(3, cut.FindAll(".sf-team-switcher__row").Count);
        Assert.Contains("Acme Corp", cut.Markup);
        Assert.Contains("Globex", cut.Markup);
        Assert.Contains("Initech", cut.Markup);
    }

    [Fact]
    public void Highlights_active_team()
    {
        var a = _factory.AddTeam("Acme");
        _factory.AddTeam("Globex");
        _active.SetActiveSync(a);

        var cut = Render<SunfishTeamSwitcher>();

        var activeRows = cut.FindAll(".sf-team-switcher__item--active");
        Assert.Single(activeRows);
        Assert.Contains("Acme", activeRows[0].TextContent);
    }

    [Fact]
    public void Shows_per_team_unread_count()
    {
        var a = _factory.AddTeam("Acme");
        var g = _factory.AddTeam("Globex");
        _notifications.SetUnread(a, 3);
        _notifications.SetUnread(g, 0);

        var cut = Render<SunfishTeamSwitcher>();

        var counts = cut.FindAll(".sf-team-switcher__count");
        // Only teams with unread > 0 render a count badge.
        Assert.Single(counts);
        Assert.Equal("3", counts[0].TextContent.Trim());
    }

    [Fact]
    public void Shows_aggregate_unread_count()
    {
        var a = _factory.AddTeam("Acme");
        var g = _factory.AddTeam("Globex");
        _notifications.SetUnread(a, 3);
        _notifications.SetUnread(g, 2);

        var cut = Render<SunfishTeamSwitcher>();

        var aggregate = cut.Find(".sf-team-switcher__aggregate-count");
        Assert.Equal("5", aggregate.TextContent.Trim());
    }

    [Fact]
    public void Aggregate_count_clamps_at_99_plus()
    {
        var a = _factory.AddTeam("Acme");
        _notifications.SetUnread(a, 142);

        var cut = Render<SunfishTeamSwitcher>();

        Assert.Equal("99+", cut.Find(".sf-team-switcher__aggregate-count").TextContent.Trim());
        Assert.Equal("99+", cut.Find(".sf-team-switcher__count").TextContent.Trim());
    }

    [Fact]
    public void Click_row_invokes_SetActiveAsync_and_OnTeamChanged()
    {
        var a = _factory.AddTeam("Acme");
        var g = _factory.AddTeam("Globex");
        TeamId? received = null;

        var cut = Render<SunfishTeamSwitcher>(p => p
            .Add(x => x.OnTeamChanged,
                EventCallback.Factory.Create<TeamId>(this, id => received = id)));

        // Click the Globex row (second alphabetically).
        var button = cut.FindAll(".sf-team-switcher__item").First(b =>
            b.GetAttribute("data-team-id") == g.ToString());
        button.Click();

        Assert.Equal(g, _active.Active?.TeamId);
        Assert.Equal(g, received);
    }

    [Fact]
    public void LegacyPrimaryTeamId_adds_primary_tag()
    {
        var a = _factory.AddTeam("Acme");
        var g = _factory.AddTeam("Globex");

        var cut = Render<SunfishTeamSwitcher>(p => p
            .Add(x => x.LegacyPrimaryTeamId, a));

        var tags = cut.FindAll(".sf-team-switcher__tag");
        Assert.Single(tags);
        Assert.Contains("primary", tags[0].TextContent);
        // Tag lives on the Acme row, not the Globex one.
        var acmeButton = cut.FindAll(".sf-team-switcher__item").First(b =>
            b.GetAttribute("data-team-id") == a.ToString());
        Assert.Contains("(primary)", acmeButton.TextContent);
    }

    [Fact]
    public async Task Add_team_button_fires_OnAddTeamRequested()
    {
        var fired = false;
        var cut = Render<SunfishTeamSwitcher>(p => p
            .Add(x => x.OnAddTeamRequested,
                EventCallback.Factory.Create(this, () => { fired = true; return Task.CompletedTask; })));

        cut.Find(".sf-team-switcher__add").Click();

        await Task.Yield();
        Assert.True(fired);
    }

    [Fact]
    public void Badge_initials_use_first_two_chars_of_display_name()
    {
        _factory.AddTeam("Acme Corp");
        _factory.AddTeam("Zeta");

        var cut = Render<SunfishTeamSwitcher>();

        var badges = cut.FindAll(".sf-team-switcher__badge").Select(n => n.TextContent.Trim()).ToArray();
        // "Acme Corp" → A + C; "Zeta" → Ze (first two chars, single word).
        Assert.Contains("AC", badges);
        Assert.Contains("ZE", badges);
    }

    // -------------------------------------------------------------------------
    // Test fakes
    // -------------------------------------------------------------------------

    private sealed class FakeTeamContextFactory : ITeamContextFactory
    {
        private readonly List<TeamContext> _contexts = new();
        public IReadOnlyCollection<TeamContext> Active => _contexts;

        public TeamId AddTeam(string displayName, TeamId? idOverride = null)
        {
            var id = idOverride ?? TeamId.New();
            var sp = new ServiceCollection().BuildServiceProvider();
            _contexts.Add(new TeamContext(id, displayName, sp));
            return id;
        }

        public Task<TeamContext> GetOrCreateAsync(TeamId teamId, string displayName, CancellationToken ct)
        {
            var existing = _contexts.FirstOrDefault(c => c.TeamId.Equals(teamId));
            if (existing is not null) return Task.FromResult(existing);
            var sp = new ServiceCollection().BuildServiceProvider();
            var ctx = new TeamContext(teamId, displayName, sp);
            _contexts.Add(ctx);
            return Task.FromResult(ctx);
        }

        public Task RemoveAsync(TeamId teamId, CancellationToken ct)
        {
            _contexts.RemoveAll(c => c.TeamId.Equals(teamId));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeActiveTeamAccessor : IActiveTeamAccessor
    {
        private readonly FakeTeamContextFactory _factory;
        private TeamContext? _active;

        public FakeActiveTeamAccessor(FakeTeamContextFactory factory)
        {
            _factory = factory;
        }

        public TeamContext? Active => _active;
        public event EventHandler<ActiveTeamChangedEventArgs>? ActiveChanged;

        public Task SetActiveAsync(TeamId teamId, CancellationToken ct)
        {
            var target = _factory.Active.FirstOrDefault(c => c.TeamId.Equals(teamId))
                ?? throw new InvalidOperationException($"Team {teamId} not materialized.");
            var previous = _active;
            _active = target;
            if (!ReferenceEquals(previous, target))
            {
                ActiveChanged?.Invoke(this, new ActiveTeamChangedEventArgs(previous, target));
            }
            return Task.CompletedTask;
        }

        /// <summary>Test-only helper that skips the Task await.</summary>
        public void SetActiveSync(TeamId teamId)
        {
            SetActiveAsync(teamId, CancellationToken.None).GetAwaiter().GetResult();
        }
    }

    private sealed class FakeNotificationAggregator : INotificationAggregator
    {
        private readonly Dictionary<TeamId, int> _counts = new();
        public event EventHandler<TeamNotification>? NotificationReceived;

        public void SetUnread(TeamId teamId, int count) => _counts[teamId] = count;

        public int GetUnreadCount(TeamId teamId) => _counts.TryGetValue(teamId, out var c) ? c : 0;
        public int GetAggregateUnreadCount() => _counts.Values.Sum();

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

        // Test helper — tests that want to exercise the subscription path can call this.
        internal void RaiseReceived(TeamNotification note) => NotificationReceived?.Invoke(this, note);
    }
}
