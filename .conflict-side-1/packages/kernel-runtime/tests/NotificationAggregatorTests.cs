using System.Runtime.CompilerServices;

using Sunfish.Kernel.Runtime.Notifications;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Kernel.Runtime.Tests;

public sealed class NotificationAggregatorTests
{
    [Fact]
    public async Task Aggregates_from_multiple_streams()
    {
        var teamA = TeamId.New();
        var teamB = TeamId.New();
        var gateA = new SemaphoreSlim(0, 2);
        var gateB = new SemaphoreSlim(0, 2);

        var a = new TestNotificationStream(teamA, gateA,
            Note(teamA, "a1"),
            Note(teamA, "a2"));
        var b = new TestNotificationStream(teamB, gateB,
            Note(teamB, "b1"),
            Note(teamB, "b2"));

        await using var agg = new NotificationAggregator(new ITeamNotificationStream[] { a, b });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var collected = new List<TeamNotification>();
        var reader = Task.Run(async () =>
        {
            await foreach (var n in agg.SubscribeAll(cts.Token))
            {
                collected.Add(n);
                if (collected.Count == 4)
                {
                    break;
                }
            }
        });

        // Release all preloaded items from both streams.
        gateA.Release(2);
        gateB.Release(2);

        await reader.WaitAsync(TimeSpan.FromSeconds(5));

        var aIds = collected.Where(n => n.TeamId == teamA).Select(n => n.Id).ToArray();
        var bIds = collected.Where(n => n.TeamId == teamB).Select(n => n.Id).ToArray();
        Assert.Equal(new[] { "a1", "a2" }, aIds);
        Assert.Equal(new[] { "b1", "b2" }, bIds);
    }

    [Fact]
    public async Task Per_team_unread_count_tracks_added_notifications()
    {
        var teamA = TeamId.New();
        var gate = new SemaphoreSlim(0, 3);
        var stream = new TestNotificationStream(teamA, gate,
            Note(teamA, "1"),
            Note(teamA, "2"),
            Note(teamA, "3"));

        await using var agg = new NotificationAggregator(new[] { stream });

        gate.Release(3);
        await WaitForCount(() => agg.GetUnreadCount(teamA), 3);

        Assert.Equal(3, agg.GetUnreadCount(teamA));
    }

    [Fact]
    public async Task Aggregate_unread_count_is_sum_of_per_team()
    {
        var teamA = TeamId.New();
        var teamB = TeamId.New();
        var gateA = new SemaphoreSlim(0, 2);
        var gateB = new SemaphoreSlim(0, 1);

        var a = new TestNotificationStream(teamA, gateA,
            Note(teamA, "a1"),
            Note(teamA, "a2"));
        var b = new TestNotificationStream(teamB, gateB,
            Note(teamB, "b1"));

        await using var agg = new NotificationAggregator(new ITeamNotificationStream[] { a, b });

        gateA.Release(2);
        gateB.Release(1);

        await WaitForCount(() => agg.GetAggregateUnreadCount(), 3);

        Assert.Equal(2, agg.GetUnreadCount(teamA));
        Assert.Equal(1, agg.GetUnreadCount(teamB));
        Assert.Equal(3, agg.GetAggregateUnreadCount());
    }

    [Fact]
    public async Task MarkReadAsync_decrements_counts()
    {
        var teamA = TeamId.New();
        var gate = new SemaphoreSlim(0, 2);
        var stream = new TestNotificationStream(teamA, gate,
            Note(teamA, "n1"),
            Note(teamA, "n2"));

        await using var agg = new NotificationAggregator(new[] { stream });

        gate.Release(2);
        await WaitForCount(() => agg.GetUnreadCount(teamA), 2);

        await agg.MarkReadAsync(teamA, "n1", CancellationToken.None);
        Assert.Equal(1, agg.GetUnreadCount(teamA));
        Assert.Equal(1, agg.GetAggregateUnreadCount());

        await agg.MarkReadAsync(teamA, "n2", CancellationToken.None);
        Assert.Equal(0, agg.GetUnreadCount(teamA));
        Assert.Equal(0, agg.GetAggregateUnreadCount());

        // Unknown id — no-op.
        await agg.MarkReadAsync(teamA, "missing", CancellationToken.None);
        Assert.Equal(0, agg.GetUnreadCount(teamA));

        // Unknown team — no-op.
        await agg.MarkReadAsync(TeamId.New(), "whatever", CancellationToken.None);
        Assert.Equal(0, agg.GetAggregateUnreadCount());
    }

    [Fact]
    public async Task NotificationReceived_event_fires_synchronously()
    {
        var teamA = TeamId.New();
        var gate = new SemaphoreSlim(0, 1);
        var stream = new TestNotificationStream(teamA, gate, Note(teamA, "only"));

        await using var agg = new NotificationAggregator(new[] { stream });

        using var signal = new ManualResetEventSlim(initialState: false);
        TeamNotification? seen = null;
        agg.NotificationReceived += (_, n) =>
        {
            seen = n;
            signal.Set();
        };

        gate.Release(1);

        Assert.True(signal.Wait(TimeSpan.FromSeconds(5)));
        Assert.NotNull(seen);
        Assert.Equal("only", seen!.Value.Id);
        Assert.Equal(teamA, seen.Value.TeamId);
    }

    [Fact]
    public async Task Cancellation_stops_pumps_cleanly()
    {
        var teamA = TeamId.New();
        var stream = new EmptyTeamNotificationStream(teamA);

        var agg = new NotificationAggregator(new ITeamNotificationStream[] { stream });

        // Kick off a SubscribeAll read that's parked on the channel.
        using var cts = new CancellationTokenSource();
        var readerDone = Task.Run(async () =>
        {
            try
            {
                await foreach (var _ in agg.SubscribeAll(cts.Token))
                {
                    // No items expected.
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when cts fires.
            }
        });

        // Dispose should cancel the pump without any OperationCanceledException
        // leaking out of Subscribe's internal await.
        await agg.DisposeAsync();

        cts.Cancel();
        await readerDone.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Empty_stream_produces_no_notifications()
    {
        var teamA = TeamId.New();
        var stream = new EmptyTeamNotificationStream(teamA);

        await using var agg = new NotificationAggregator(new ITeamNotificationStream[] { stream });

        // Give the pump a moment to run (it should just park on Task.Delay).
        await Task.Delay(100);

        Assert.Equal(0, agg.GetUnreadCount(teamA));
        Assert.Equal(0, agg.GetAggregateUnreadCount());
    }

    // -- helpers -----------------------------------------------------------

    private static TeamNotification Note(TeamId team, string id) => new(
        TeamId: team,
        Id: id,
        Title: $"title-{id}",
        Summary: $"summary-{id}",
        OccurredAt: DateTimeOffset.UnixEpoch,
        Severity: NotificationSeverity.Info);

    private static async Task WaitForCount(Func<int> probe, int expected)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            if (probe() >= expected)
            {
                return;
            }
            await Task.Delay(10);
        }

        Assert.Equal(expected, probe());
    }

    /// <summary>
    /// Test stream that yields from a preloaded list, gated by a semaphore so
    /// tests can control emit timing deterministically.
    /// </summary>
    private sealed class TestNotificationStream : ITeamNotificationStream
    {
        private readonly SemaphoreSlim _gate;
        private readonly TeamNotification[] _items;

        public TestNotificationStream(TeamId teamId, SemaphoreSlim gate, params TeamNotification[] items)
        {
            TeamId = teamId;
            _gate = gate;
            _items = items;
        }

        public TeamId TeamId { get; }

        public async IAsyncEnumerable<TeamNotification> Subscribe(
            [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var item in _items)
            {
                try
                {
                    await _gate.WaitAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    yield break;
                }

                yield return item;
            }

            // Park until cancellation so the pump keeps running like a real stream.
            try
            {
                await Task.Delay(Timeout.Infinite, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Expected on shutdown.
            }
        }
    }
}
