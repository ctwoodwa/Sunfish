namespace Sunfish.Blocks.CrewComms.Protocol;

/// <summary>
/// Wire-protocol frame type byte. Per ADR 0076 §Wire protocol — control
/// frames live in the low range (≤ 0x09) and data frames in the high range
/// (≥ 0x10), so the Phase 3 priority-aware send loop can short-circuit on a
/// single comparison. Phase 1 ships text only; audio + video reserved.
/// </summary>
public static class MessageType
{
    /// <summary>Initial handshake frame carrying the sender's HELLO payload.</summary>
    public const byte Hello = 0x01;

    /// <summary>Periodic presence beacon (30s cadence; 45s TTL on receiver).</summary>
    public const byte Heartbeat = 0x02;

    /// <summary>Invitation to open a session.</summary>
    public const byte Invite = 0x03;

    /// <summary>Acceptance of a prior INVITE; carries negotiated capability.</summary>
    public const byte Accept = 0x04;

    /// <summary>Rejection of a prior INVITE.</summary>
    public const byte Reject = 0x05;

    /// <summary>Polite session teardown.</summary>
    public const byte Bye = 0x06;

    /// <summary>Typing indicator.</summary>
    public const byte Typing = 0x07;

    /// <summary>Read receipt for a prior TEXT.</summary>
    public const byte Delivered = 0x08;

    /// <summary>Local mute toggle (Phase 3).</summary>
    public const byte MuteState = 0x09;

    /// <summary>Transcript-hash confirmation, sent post-ACCEPT to seal the handshake.</summary>
    public const byte Confirm = 0x0A;

    /// <summary>UTF-8 text payload.</summary>
    public const byte Text = 0x10;

    /// <summary>Single Opus-encoded audio frame (Phase 3).</summary>
    public const byte AudioFrame = 0x20;

    /// <summary>Single video frame (Phase 4).</summary>
    public const byte VideoFrame = 0x30;
}
