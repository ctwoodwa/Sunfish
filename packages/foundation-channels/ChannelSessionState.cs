namespace Sunfish.Foundation.Channels;

/// <summary>
/// Lifecycle state of an <see cref="IChannelSession"/>. Per ADR 0076.
/// </summary>
public enum ChannelSessionState
{
    /// <summary>Handshake in progress (HELLO / INVITE / ACCEPT / CONFIRM exchange).</summary>
    Connecting,

    /// <summary>Handshake complete; bidirectional frames flowing.</summary>
    Active,

    /// <summary>Session closed; <see cref="IChannelSession.Completed"/> has surfaced a <see cref="ChannelTerminationReason"/>.</summary>
    Terminated,
}
