using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sunfish.Anchor.Services;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Transport;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#59 Phase 3 — listener hosted service + invitation bus tests.
/// Per the hand-off, ≥5 cases: starts on app boot, drains invitations,
/// disposes cleanly on shutdown, handles ListenAsync exception (logs +
/// retries with backoff), forwards through CrewCommsInvitationBus.
/// </summary>
public sealed class CrewCommsListenerHostedServiceTests
{
    private static readonly TeamId Team = new(Guid.Parse("00000000-0000-0000-0000-00000000000a"));
    private static readonly TenantId Tenant = new(Team.Value.ToString("D"));

    [Fact]
    public async Task StartAsync_BeginsListening_AfterShortDelay()
    {
        var provider = new FakeChannelProvider();
        var bus = new CrewCommsInvitationBus();
        var accessor = new FakeActiveTeamAccessor(active: ContextFor(Team));
        var listener = new CrewCommsListenerHostedService(
            provider, accessor, bus, NullLogger<CrewCommsListenerHostedService>.Instance);

        await listener.StartAsync(CancellationToken.None);
        await provider.WaitForListenStartedAsync();

        Assert.True(provider.ListenStarted);
        Assert.Equal(Tenant, provider.LastListenTenant);

        await listener.StopAsync(CancellationToken.None);
        await listener.DisposeAsync();
    }

    [Fact]
    public async Task IncomingInvitations_ArePublishedThroughBus()
    {
        var provider = new FakeChannelProvider();
        var bus = new CrewCommsInvitationBus();
        var observed = new List<IChannelInvitation>();
        using var sub = bus.InboundInvitations.Subscribe(new ListObserver<IChannelInvitation>(observed));
        var accessor = new FakeActiveTeamAccessor(active: ContextFor(Team));
        var listener = new CrewCommsListenerHostedService(
            provider, accessor, bus, NullLogger<CrewCommsListenerHostedService>.Instance);

        await listener.StartAsync(CancellationToken.None);
        await provider.WaitForListenStartedAsync();

        var invitation = new FakeInvitation();
        provider.PushInvitation(invitation);
        await WaitFor(() => observed.Count >= 1);

        Assert.Single(observed);
        Assert.Same(invitation, observed[0]);

        await listener.StopAsync(CancellationToken.None);
        await listener.DisposeAsync();
    }

    [Fact]
    public async Task StopAsync_DrainsListenerCleanly()
    {
        var provider = new FakeChannelProvider();
        var bus = new CrewCommsInvitationBus();
        var accessor = new FakeActiveTeamAccessor(active: ContextFor(Team));
        var listener = new CrewCommsListenerHostedService(
            provider, accessor, bus, NullLogger<CrewCommsListenerHostedService>.Instance);

        await listener.StartAsync(CancellationToken.None);
        await provider.WaitForListenStartedAsync();
        Assert.True(listener.IsRunning);

        await listener.StopAsync(CancellationToken.None);

        Assert.False(listener.IsRunning);
        await listener.DisposeAsync();
    }

    [Fact]
    public async Task ListenAsync_ThrowingTransientException_RetriesWithBackoff()
    {
        var provider = new FakeChannelProvider
        {
            FailFirstNListens = 2, // throw twice, then yield normally
        };
        var bus = new CrewCommsInvitationBus();
        var accessor = new FakeActiveTeamAccessor(active: ContextFor(Team));
        var listener = new CrewCommsListenerHostedService(
            provider, accessor, bus, NullLogger<CrewCommsListenerHostedService>.Instance);

        await listener.StartAsync(CancellationToken.None);

        // Wait for the listener to retry past the failures and reach a
        // healthy ListenAsync iteration. Transient failures mean the
        // counter increments at each call; we expect ≥3 attempts (2 fails
        // + 1 success).
        await WaitFor(() => provider.ListenAttemptCount >= 3, timeoutMs: 5000);

        Assert.True(provider.ListenAttemptCount >= 3);

        await listener.StopAsync(CancellationToken.None);
        await listener.DisposeAsync();
    }

    [Fact]
    public async Task NoActiveTeam_DoesNotThrow_AndResumesAfterTeamSelected()
    {
        var provider = new FakeChannelProvider();
        var bus = new CrewCommsInvitationBus();
        var accessor = new FakeActiveTeamAccessor(active: null);
        var listener = new CrewCommsListenerHostedService(
            provider, accessor, bus, NullLogger<CrewCommsListenerHostedService>.Instance);

        await listener.StartAsync(CancellationToken.None);

        // No active team — listener idles without calling ListenAsync.
        await Task.Delay(200);
        Assert.False(provider.ListenStarted);

        // Operator selects a team — listener picks it up next tick.
        accessor.SetActiveImmediate(ContextFor(Team));
        await provider.WaitForListenStartedAsync(timeoutMs: 5000);
        Assert.True(provider.ListenStarted);

        await listener.StopAsync(CancellationToken.None);
        await listener.DisposeAsync();
    }

    [Fact]
    public async Task BusUnsubscribe_StopsDeliveringNewItems()
    {
        var bus = new CrewCommsInvitationBus();
        var observed = new List<IChannelInvitation>();
        var sub = bus.InboundInvitations.Subscribe(new ListObserver<IChannelInvitation>(observed));

        bus.PublishInvitation(new FakeInvitation());
        Assert.Single(observed);

        sub.Dispose();
        bus.PublishInvitation(new FakeInvitation());

        Assert.Single(observed); // not 2 — unsubscribed observer didn't receive.
        await Task.Yield();
    }

    [Fact]
    public async Task BusObserverThrowing_DoesNotBreakSiblingDelivery()
    {
        var bus = new CrewCommsInvitationBus();
        var siblingObserved = new List<IChannelInvitation>();
        using var faulty = bus.InboundInvitations.Subscribe(new ThrowingObserver<IChannelInvitation>());
        using var sibling = bus.InboundInvitations.Subscribe(
            new ListObserver<IChannelInvitation>(siblingObserved));

        bus.PublishInvitation(new FakeInvitation());

        Assert.Single(siblingObserved);
        await Task.Yield();
    }

    [Fact]
    public async Task PublishPresence_DeliversToBusObservers()
    {
        var bus = new CrewCommsInvitationBus();
        var observed = new List<CrewPresence>();
        using var sub = bus.PresenceUpdates.Subscribe(new ListObserver<CrewPresence>(observed));

        var presence = new CrewPresence
        {
            Peer = new PeerId("test-peer"),
            TenantId = Tenant,
            DisplayName = "Alpha",
            Caps = ChannelCapability.Text,
            Status = PresenceStatus.Available,
            Via = TransportTier.LocalNetwork,
            LastSeenAt = DateTimeOffset.UtcNow,
        };
        bus.PublishPresence(presence);

        Assert.Single(observed);
        Assert.Same(presence, observed[0]);
        await Task.Yield();
    }

    [Fact]
    public async Task NullArguments_ThrowOnConstruct()
    {
        var provider = new FakeChannelProvider();
        var bus = new CrewCommsInvitationBus();
        var accessor = new FakeActiveTeamAccessor(active: null);
        var logger = NullLogger<CrewCommsListenerHostedService>.Instance;

        Assert.Throws<ArgumentNullException>(() =>
            new CrewCommsListenerHostedService(null!, accessor, bus, logger));
        Assert.Throws<ArgumentNullException>(() =>
            new CrewCommsListenerHostedService(provider, null!, bus, logger));
        Assert.Throws<ArgumentNullException>(() =>
            new CrewCommsListenerHostedService(provider, accessor, null!, logger));
        Assert.Throws<ArgumentNullException>(() =>
            new CrewCommsListenerHostedService(provider, accessor, bus, null!));
        await Task.Yield();
    }

    private static async Task WaitFor(Func<bool> predicate, int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!predicate() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    private static TeamContext ContextFor(TeamId teamId)
    {
        return new TeamContext(
            teamId,
            displayName: $"Team-{teamId.Value:N}",
            services: new ServiceCollection().BuildServiceProvider());
    }

    private sealed class FakeChannelProvider : IChannelProvider
    {
        private readonly System.Threading.Channels.Channel<IChannelInvitation> _queue =
            System.Threading.Channels.Channel.CreateUnbounded<IChannelInvitation>();
        private readonly TaskCompletionSource _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ChannelCapability Capabilities => ChannelCapability.Text;
        public bool ListenStarted { get; private set; }
        public TenantId? LastListenTenant { get; private set; }
        public int ListenAttemptCount { get; private set; }
        public int FailFirstNListens { get; set; } = 0;

        public Task<IReadOnlyList<CrewPresence>> GetPresentCrewAsync(TenantId tenant, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<CrewPresence>>(Array.Empty<CrewPresence>());

        public Task<IChannelSession> OpenAsync(TenantId tenant, PeerId peer, ChannelCapability preferredCapabilities, CancellationToken ct)
            => throw new NotSupportedException();

        public async IAsyncEnumerable<IChannelInvitation> ListenAsync(
            TenantId tenant,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            ListenAttemptCount++;
            LastListenTenant = tenant;

            if (FailFirstNListens > 0)
            {
                FailFirstNListens--;
                ListenStarted = true;
                _started.TrySetResult();
                throw new InvalidOperationException("Transient listener failure (test).");
            }

            ListenStarted = true;
            _started.TrySetResult();
            await foreach (var invitation in _queue.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return invitation;
            }
        }

        public void PushInvitation(IChannelInvitation invitation) =>
            _queue.Writer.TryWrite(invitation);

        public Task WaitForListenStartedAsync(int timeoutMs = 5000)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            return _started.Task.WaitAsync(cts.Token);
        }
    }

    private sealed class FakeInvitation : IChannelInvitation
    {
        public PeerId FromPeer { get; } = new("from-peer");
        public ChannelCapability OfferedCapabilities { get; } = ChannelCapability.Text;

        public Task<IChannelSession> AcceptAsync(CancellationToken ct) =>
            throw new NotSupportedException();

        public Task RejectAsync(string? reason, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class FakeActiveTeamAccessor : IActiveTeamAccessor
    {
        public FakeActiveTeamAccessor(TeamContext? active)
        {
            Active = active;
        }

        public TeamContext? Active { get; private set; }

        public event EventHandler<ActiveTeamChangedEventArgs>? ActiveChanged;

        public Task SetActiveAsync(TeamId teamId, CancellationToken ct) => Task.CompletedTask;

        public void SetActiveImmediate(TeamContext? next)
        {
            var prev = Active;
            Active = next;
            ActiveChanged?.Invoke(this, new ActiveTeamChangedEventArgs(prev, next));
        }
    }

    private sealed class ListObserver<T> : IObserver<T>
    {
        private readonly List<T> _list;
        public ListObserver(List<T> list) => _list = list;
        public void OnNext(T value)
        {
            lock (_list) { _list.Add(value); }
        }
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }

    private sealed class ThrowingObserver<T> : IObserver<T>
    {
        public void OnNext(T value) => throw new InvalidOperationException("Observer threw (test).");
        public void OnError(Exception error) { }
        public void OnCompleted() { }
    }
}
