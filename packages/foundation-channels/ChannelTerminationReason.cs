namespace Sunfish.Foundation.Channels;

/// <summary>
/// Why an <see cref="IChannelSession"/> reached the
/// <see cref="ChannelSessionState.Terminated"/> state. Per ADR 0076 +
/// the W#45 hand-off (which adds <see cref="TranscriptMismatch"/> on
/// top of the ADR's 4-value enum).
/// </summary>
public enum ChannelTerminationReason
{
    /// <summary>Local side initiated the close (sent BYE).</summary>
    LocalBye,

    /// <summary>Remote peer sent BYE.</summary>
    RemoteBye,

    /// <summary>INVITE was not answered (ACCEPT or REJECT) within the timeout.</summary>
    InviteTimeout,

    /// <summary>Underlying transport failed (network drop, peer crash, mesh-VPN partition).</summary>
    TransportError,

    /// <summary>CONFIRM transcript-hash did not match between peers — handshake forgery suspected.</summary>
    TranscriptMismatch,
}
