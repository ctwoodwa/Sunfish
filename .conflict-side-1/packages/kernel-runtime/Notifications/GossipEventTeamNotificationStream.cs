using System.Runtime.CompilerServices;
using System.Threading.Channels;

using Sunfish.Kernel.Runtime.Teams;
using Sunfish.Kernel.Sync.Gossip;

namespace Sunfish.Kernel.Runtime.Notifications;

/// <summary>
/// Wave 6.5 real <see cref="ITeamNotificationStream"/> — subscribes to a
/// team's <see cref="IGossipDaemon.FrameReceived"/> event and translates
/// each observed frame into a <see cref="TeamNotification"/>. Replaces the
/// <see cref="EmptyTeamNotificationStream"/> placeholder in the per-team
/// service registrar so badges in <c>SunfishTeamSwitcher</c> and the
/// <see cref="INotificationAggregator"/> reflect real inter-peer activity.
/// </summary>
/// <remarks>
/// <para>
/// <b>Buffering model.</b> Each call to <see cref="Subscribe"/> allocates
/// its own unbounded <see cref="Channel{T}"/> plus a handler registration
/// against the daemon's event. Multiple subscribers therefore each observe
/// every frame — the stream is a fan-out broadcast, not a queue. On
/// cancellation the handler is unsubscribed and the channel is completed;
/// the enumerator then drains any buffered items before returning.
/// </para>
/// <para>
/// <b>Severity mapping.</b> See <see cref="MapSeverity"/>. The default
/// (<see cref="GossipFrameType.Hello"/>, <see cref="GossipFrameType.GossipPing"/>,
/// <see cref="GossipFrameType.DeltaStream"/>) is <see cref="NotificationSeverity.Info"/>
/// — routine traffic. <see cref="GossipFrameType.GossipError"/> maps to
/// <see cref="NotificationSeverity.Warning"/>; <see cref="GossipFrameType.HandshakeFailure"/>
/// maps to <see cref="NotificationSeverity.Error"/> because it typically
/// indicates a misconfigured peer or a signature-level integrity failure
/// the user probably needs to act on.
/// </para>
/// </remarks>
public sealed class GossipEventTeamNotificationStream : ITeamNotificationStream
{
    private readonly IGossipDaemon _gossipDaemon;

    /// <summary>
    /// Build a stream bound to <paramref name="teamId"/> that subscribes to
    /// <paramref name="gossipDaemon"/>'s per-team
    /// <see cref="IGossipDaemon.FrameReceived"/>. Both arguments are captured
    /// — the stream does not own their lifetime; the caller (typically the
    /// per-team service provider) is responsible for disposing them.
    /// </summary>
    public GossipEventTeamNotificationStream(TeamId teamId, IGossipDaemon gossipDaemon)
    {
        ArgumentNullException.ThrowIfNull(gossipDaemon);
        TeamId = teamId;
        _gossipDaemon = gossipDaemon;
    }

    /// <inheritdoc />
    public TeamId TeamId { get; }

    /// <inheritdoc />
    public async IAsyncEnumerable<TeamNotification> Subscribe(
        [EnumeratorCancellation] CancellationToken ct)
    {
        var channel = Channel.CreateUnbounded<TeamNotification>(
            new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
            });

        var teamId = TeamId;
        void Handler(object? sender, GossipFrameEventArgs e)
        {
            var notification = new TeamNotification(
                TeamId: teamId,
                Id: Guid.NewGuid().ToString("N"),
                Title: BuildTitle(e),
                Summary: e.Summary ?? e.FrameType.ToString(),
                OccurredAt: e.OccurredAt,
                Severity: MapSeverity(e.FrameType));

            // TryWrite on an unbounded channel never fails unless the writer
            // has been completed. If it has (cancellation raced the handler),
            // silently drop — the enumerator has already returned.
            channel.Writer.TryWrite(notification);
        }

        _gossipDaemon.FrameReceived += Handler;

        // Cancellation callback: unsubscribe the handler and close the
        // channel. Registering via the token means we don't race with the
        // enumerator's outer cancellation check.
        using var registration = ct.Register(() =>
        {
            _gossipDaemon.FrameReceived -= Handler;
            channel.Writer.TryComplete();
        });

        try
        {
            await foreach (var n in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return n;
            }
        }
        finally
        {
            // Belt-and-braces unsubscribe — covers the "enumerator disposed
            // without cancellation" path (e.g. early `break` from the
            // consumer). Double-unsubscribe is a no-op on event handlers.
            _gossipDaemon.FrameReceived -= Handler;
            channel.Writer.TryComplete();
        }
    }

    private static string BuildTitle(GossipFrameEventArgs e)
    {
        // Keep titles short — they surface in tray tooltips. The peer-node-id
        // is already hex; truncate to 8 chars for readability while keeping
        // it unique enough to distinguish a handful of peers at a glance.
        var peer = string.IsNullOrEmpty(e.PeerNodeId)
            ? e.PeerEndpoint
            : e.PeerNodeId.Length > 8
                ? e.PeerNodeId[..8]
                : e.PeerNodeId;
        return $"{peer} sent {e.FrameType}";
    }

    /// <summary>
    /// Frame-type → severity mapping. Exposed internally so the test suite
    /// can pin the table without having to instantiate a full channel.
    /// </summary>
    internal static NotificationSeverity MapSeverity(GossipFrameType frameType) => frameType switch
    {
        GossipFrameType.HandshakeFailure => NotificationSeverity.Error,
        GossipFrameType.GossipError => NotificationSeverity.Warning,
        GossipFrameType.Hello => NotificationSeverity.Info,
        GossipFrameType.GossipPing => NotificationSeverity.Info,
        GossipFrameType.DeltaStream => NotificationSeverity.Info,
        _ => NotificationSeverity.Info,
    };
}
