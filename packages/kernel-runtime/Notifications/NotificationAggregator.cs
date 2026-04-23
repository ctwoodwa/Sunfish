using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Sunfish.Kernel.Runtime.Teams;

namespace Sunfish.Kernel.Runtime.Notifications;

/// <summary>
/// In-memory <see cref="INotificationAggregator"/> that multiplexes every
/// injected <see cref="ITeamNotificationStream"/> onto a single fan-in channel
/// and tracks per-team unread sets. Scaffold implementation for Wave 6.5; Wave
/// 6.3 wires one stream per registered <c>TeamContext</c>.
/// </summary>
public sealed class NotificationAggregator : INotificationAggregator, IAsyncDisposable
{
    private readonly ConcurrentDictionary<TeamId, ITeamNotificationStream> _streams;
    private readonly ConcurrentDictionary<TeamId, HashSet<string>> _unread = new();
    private readonly Channel<TeamNotification> _fanIn =
        Channel.CreateUnbounded<TeamNotification>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false,
        });
    private readonly CancellationTokenSource _cts = new();
    private readonly Task[] _pumps;
    private int _disposed;

    /// <inheritdoc />
    public event EventHandler<TeamNotification>? NotificationReceived;

    /// <summary>
    /// Builds an aggregator over <paramref name="streams"/>. Kicks off one
    /// background pump per stream immediately.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="streams"/> is <c>null</c>.</exception>
    public NotificationAggregator(IEnumerable<ITeamNotificationStream> streams)
    {
        ArgumentNullException.ThrowIfNull(streams);

        _streams = new ConcurrentDictionary<TeamId, ITeamNotificationStream>();
        var pumps = new List<Task>();
        foreach (var stream in streams)
        {
            if (stream is null)
            {
                continue;
            }

            // Last-write-wins on duplicate TeamId; Wave 6.3 registrar must not register twice.
            _streams[stream.TeamId] = stream;
            _unread.TryAdd(stream.TeamId, new HashSet<string>(StringComparer.Ordinal));
            pumps.Add(Task.Run(() => PumpAsync(stream, _cts.Token)));
        }

        _pumps = pumps.ToArray();
    }

    /// <inheritdoc />
    public int GetUnreadCount(TeamId teamId)
    {
        if (!_unread.TryGetValue(teamId, out var set))
        {
            return 0;
        }

        lock (set)
        {
            return set.Count;
        }
    }

    /// <inheritdoc />
    public int GetAggregateUnreadCount()
    {
        var total = 0;
        foreach (var kv in _unread)
        {
            lock (kv.Value)
            {
                total += kv.Value.Count;
            }
        }
        return total;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TeamNotification> SubscribeAll(
        [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var n in _fanIn.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return n;
        }
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<TeamNotification> SubscribeTeam(
        TeamId teamId,
        [EnumeratorCancellation] CancellationToken ct)
    {
        if (!_streams.TryGetValue(teamId, out var stream))
        {
            yield break;
        }

        await foreach (var n in stream.Subscribe(ct).ConfigureAwait(false))
        {
            yield return n;
        }
    }

    /// <inheritdoc />
    public ValueTask MarkReadAsync(TeamId teamId, string notificationId, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrEmpty(notificationId);
        ct.ThrowIfCancellationRequested();

        if (_unread.TryGetValue(teamId, out var set))
        {
            lock (set)
            {
                set.Remove(notificationId);
            }
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Cancels pump tasks and completes the fan-in channel. Idempotent.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed — nothing to do.
        }

        try
        {
            await Task.WhenAll(_pumps).ConfigureAwait(false);
        }
        catch
        {
            // Pumps absorb their own cancellation; swallow defensively so
            // disposal never throws.
        }

        _fanIn.Writer.TryComplete();
        _cts.Dispose();
    }

    private async Task PumpAsync(ITeamNotificationStream stream, CancellationToken ct)
    {
        try
        {
            await foreach (var n in stream.Subscribe(ct).WithCancellation(ct).ConfigureAwait(false))
            {
                var set = _unread.GetOrAdd(n.TeamId, _ => new HashSet<string>(StringComparer.Ordinal));
                lock (set)
                {
                    set.Add(n.Id);
                }

                NotificationReceived?.Invoke(this, n);
                await _fanIn.Writer.WriteAsync(n, ct).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }
}
