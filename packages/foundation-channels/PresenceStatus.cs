namespace Sunfish.Foundation.Channels;

/// <summary>
/// Operator-visible presence state surfaced by <see cref="ICrewRoster"/>
/// + <see cref="IChannelProvider.GetPresentCrewAsync"/>. Per ADR 0076.
/// </summary>
public enum PresenceStatus
{
    /// <summary>Peer has not been heard from within the TTL window.</summary>
    Offline,

    /// <summary>Peer is reachable + accepting invitations.</summary>
    Available,

    /// <summary>Peer is reachable but in an active session and not accepting new invitations.</summary>
    Busy,
}
