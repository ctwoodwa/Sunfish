using System.Runtime.CompilerServices;

using Sunfish.Kernel.Runtime.Notifications;
using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Sync.Gossip;

namespace Sunfish.Kernel.Runtime.Tests;

/// <summary>
/// Wave 6.5 coverage for <see cref="GossipEventTeamNotificationStream"/> —
/// the real notification producer that translates <see cref="IGossipDaemon.FrameReceived"/>
/// events into per-team <see cref="TeamNotification"/>s. Uses a
/// <see cref="FakeGossipDaemon"/> so each test can deterministically raise
/// frames without standing up a transport.
/// </summary>
public sealed class GossipEventTeamNotificationStreamTests
{
    [Fact]
    public async Task Subscribe_yields_notification_per_event()
    {
        var teamId = TeamId.New();
        var daemon = new FakeGossipDaemon();
        var stream = new GossipEventTeamNotificationStream(teamId, daemon);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var collected = new List<TeamNotification>();
        var reader = Task.Run(async () =>
        {
            await foreach (var n in stream.Subscribe(cts.Token))
            {
                collected.Add(n);
                if (collected.Count == 3) break;
            }
        });

        // Give the subscribe loop a tick to register the handler.
        await WaitUntil(() => daemon.HandlerCount > 0, TimeSpan.FromSeconds(2));

        daemon.Raise(FrameArgs(GossipFrameType.Hello, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "alice joined"));
        daemon.Raise(FrameArgs(GossipFrameType.GossipPing, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", null));
        daemon.Raise(FrameArgs(GossipFrameType.DeltaStream, "cccccccccccccccccccccccccccccccc", "3 task updates"));

        await reader.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, collected.Count);
        Assert.All(collected, n => Assert.Equal(teamId, n.TeamId));
        // Ids are random guids — assert uniqueness rather than value.
        Assert.Equal(3, collected.Select(n => n.Id).Distinct().Count());
    }

    [Fact]
    public async Task Notification_fields_map_from_event_args()
    {
        var teamId = TeamId.New();
        var daemon = new FakeGossipDaemon();
        var stream = new GossipEventTeamNotificationStream(teamId, daemon);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        TeamNotification? seen = null;
        var reader = Task.Run(async () =>
        {
            await foreach (var n in stream.Subscribe(cts.Token))
            {
                seen = n;
                break;
            }
        });

        await WaitUntil(() => daemon.HandlerCount > 0, TimeSpan.FromSeconds(2));

        var occurred = new DateTimeOffset(2026, 4, 23, 12, 0, 0, TimeSpan.Zero);
        daemon.Raise(new GossipFrameEventArgs(
            PeerEndpoint: "unix:///tmp/teamA.sock",
            PeerNodeId: "deadbeefcafef00d0123456789abcdef",
            FrameType: GossipFrameType.GossipPing,
            OccurredAt: occurred,
            Summary: "deadbeef sent gossip ping"));

        await reader.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(seen);
        Assert.Equal(teamId, seen!.Value.TeamId);
        Assert.Equal(occurred, seen.Value.OccurredAt);
        // Summary pass-through when event supplies one.
        Assert.Equal("deadbeef sent gossip ping", seen.Value.Summary);
        // Title uses the first 8 chars of the node id + frame type.
        Assert.Equal("deadbeef sent GossipPing", seen.Value.Title);
        Assert.Equal(NotificationSeverity.Info, seen.Value.Severity);
        Assert.False(string.IsNullOrEmpty(seen.Value.Id));
    }

    [Fact]
    public async Task Summary_falls_back_to_frame_type_when_event_has_none()
    {
        var daemon = new FakeGossipDaemon();
        var stream = new GossipEventTeamNotificationStream(TeamId.New(), daemon);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        TeamNotification? seen = null;
        var reader = Task.Run(async () =>
        {
            await foreach (var n in stream.Subscribe(cts.Token))
            {
                seen = n;
                break;
            }
        });

        await WaitUntil(() => daemon.HandlerCount > 0, TimeSpan.FromSeconds(2));
        daemon.Raise(FrameArgs(GossipFrameType.GossipPing, "11111111222222223333333344444444", null));

        await reader.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(seen);
        Assert.Equal("GossipPing", seen!.Value.Summary);
    }

    [Fact]
    public async Task Cancellation_unsubscribes_handler()
    {
        var daemon = new FakeGossipDaemon();
        var stream = new GossipEventTeamNotificationStream(TeamId.New(), daemon);

        using var cts = new CancellationTokenSource();
        var reader = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in stream.Subscribe(cts.Token))
                {
                    // No-op — we cancel before raising anything.
                }
            }
            catch (OperationCanceledException)
            {
                // Expected on cancellation of the outer enumerator.
            }
        });

        await WaitUntil(() => daemon.HandlerCount == 1, TimeSpan.FromSeconds(2));
        Assert.Equal(1, daemon.HandlerCount);

        cts.Cancel();
        await reader.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(0, daemon.HandlerCount);
    }

    [Fact]
    public void Severity_mapping_is_correct()
    {
        // HandshakeFailure → Error (users probably need to act on a bad sig).
        // GossipError → Warning (transient, user likely wants to know).
        // Hello / GossipPing / DeltaStream → Info (routine traffic).
        Assert.Equal(NotificationSeverity.Error,
            GossipEventTeamNotificationStream.MapSeverity(GossipFrameType.HandshakeFailure));
        Assert.Equal(NotificationSeverity.Warning,
            GossipEventTeamNotificationStream.MapSeverity(GossipFrameType.GossipError));
        Assert.Equal(NotificationSeverity.Info,
            GossipEventTeamNotificationStream.MapSeverity(GossipFrameType.Hello));
        Assert.Equal(NotificationSeverity.Info,
            GossipEventTeamNotificationStream.MapSeverity(GossipFrameType.GossipPing));
        Assert.Equal(NotificationSeverity.Info,
            GossipEventTeamNotificationStream.MapSeverity(GossipFrameType.DeltaStream));
    }

    [Fact]
    public async Task Multiple_subscribers_each_receive_all_events()
    {
        var daemon = new FakeGossipDaemon();
        var stream = new GossipEventTeamNotificationStream(TeamId.New(), daemon);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var bagA = new List<TeamNotification>();
        var bagB = new List<TeamNotification>();
        var readerA = Task.Run(async () =>
        {
            await foreach (var n in stream.Subscribe(cts.Token))
            {
                bagA.Add(n);
                if (bagA.Count == 2) break;
            }
        });
        var readerB = Task.Run(async () =>
        {
            await foreach (var n in stream.Subscribe(cts.Token))
            {
                bagB.Add(n);
                if (bagB.Count == 2) break;
            }
        });

        // Wait until both handlers are registered — each Subscribe call adds
        // a separate handler to the daemon.
        await WaitUntil(() => daemon.HandlerCount == 2, TimeSpan.FromSeconds(2));

        daemon.Raise(FrameArgs(GossipFrameType.Hello, "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", null));
        daemon.Raise(FrameArgs(GossipFrameType.GossipPing, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", null));

        await Task.WhenAll(readerA, readerB).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(2, bagA.Count);
        Assert.Equal(2, bagB.Count);
    }

    [Fact]
    public async Task Events_raised_after_cancellation_do_not_throw()
    {
        // Drop-safe: if the daemon raises a frame after the subscriber's
        // token has been cancelled, the stream must neither throw nor leak
        // a stale handler. Regression guard for "channel writer already
        // completed" races.
        var daemon = new FakeGossipDaemon();
        var stream = new GossipEventTeamNotificationStream(TeamId.New(), daemon);

        using var cts = new CancellationTokenSource();
        var reader = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in stream.Subscribe(cts.Token))
                {
                }
            }
            catch (OperationCanceledException)
            {
                // Expected.
            }
        });

        await WaitUntil(() => daemon.HandlerCount == 1, TimeSpan.FromSeconds(2));
        cts.Cancel();
        await reader.WaitAsync(TimeSpan.FromSeconds(5));

        // After cancellation the handler should be gone — a subsequent raise
        // is a no-op (no handler to invoke).
        Assert.Equal(0, daemon.HandlerCount);

        // But even if the daemon still had a reference to the handler, Raise
        // must be safe. Re-attach a fresh handler count check before this
        // would be overkill; we simply assert no exception escapes.
        var ex = Record.Exception(() =>
            daemon.Raise(FrameArgs(GossipFrameType.GossipPing, "00000000000000000000000000000000", null)));
        Assert.Null(ex);
    }

    // -- helpers -----------------------------------------------------------

    private static GossipFrameEventArgs FrameArgs(
        GossipFrameType type, string peerNodeId, string? summary) =>
        new(
            PeerEndpoint: $"endpoint-{peerNodeId[..4]}",
            PeerNodeId: peerNodeId,
            FrameType: type,
            OccurredAt: DateTimeOffset.UtcNow,
            Summary: summary);

    private static async Task WaitUntil(Func<bool> probe, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (probe()) return;
            await Task.Delay(10);
        }
        Assert.True(probe(), "Condition never became true within timeout.");
    }

    /// <summary>
    /// Test-only <see cref="IGossipDaemon"/> that exposes a
    /// <see cref="Raise(GossipFrameEventArgs)"/> helper so tests can drive
    /// <see cref="FrameReceived"/> deterministically. All other members are
    /// no-ops — we do not exercise them from these tests.
    /// </summary>
    private sealed class FakeGossipDaemon : IGossipDaemon
    {
        private EventHandler<GossipFrameEventArgs>? _frameReceived;

        public event EventHandler<GossipRoundCompletedEventArgs>? RoundCompleted
        {
            add { }
            remove { }
        }

        public event EventHandler<GossipFrameEventArgs>? FrameReceived
        {
            add { _frameReceived += value; }
            remove { _frameReceived -= value; }
        }

        public int HandlerCount =>
            _frameReceived?.GetInvocationList().Length ?? 0;

        public void Raise(GossipFrameEventArgs args) =>
            _frameReceived?.Invoke(this, args);

        public IReadOnlyCollection<PeerInfo> KnownPeers => Array.Empty<PeerInfo>();

        public bool IsRunning => false;

        public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

        public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

        public void AddPeer(string peerEndpoint, byte[] peerPublicKey) { }

        public void RemovePeer(string peerEndpoint) { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
