namespace Sunfish.Blocks.CrewComms.Session;

/// <summary>
/// Internal state machine for <see cref="NativeChannelSession"/>. Distinct
/// from the public <c>ChannelSessionState</c> contract enum because the
/// signaling protocol needs finer-grained transitions (Inviting / Confirming)
/// that callers should not see directly. Per ADR 0076 §Encryption handshake.
/// </summary>
internal enum SessionState
{
    /// <summary>Session created but no INVITE/HELLO exchanged yet.</summary>
    Idle,

    /// <summary>Initiator has sent INVITE; waiting on ACCEPT (60s budget).</summary>
    Inviting,

    /// <summary>ACCEPT received; both sides exchange CONFIRM with transcript hash.</summary>
    Confirming,

    /// <summary>CONFIRM verified on both sides; data frames flowing.</summary>
    Active,

    /// <summary>Terminal — BYE sent/received, transport error, transcript mismatch, or invite timeout.</summary>
    Terminated,
}
