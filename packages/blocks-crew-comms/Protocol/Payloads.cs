using System;
using MessagePack;

namespace Sunfish.Blocks.CrewComms.Protocol;

/// <summary>
/// HELLO payload (frame type 0x01). Carries the sender's ephemeral X25519 key,
/// long-lived Ed25519 identity key, tenant binding, an Ed25519 signature over
/// <c>(EphemeralPublicKey || IdentityPublicKey || TenantIdUtf8)</c>, and an
/// embedded presence beacon so a single message both opens the session and
/// announces availability. Per ADR 0076 §Encryption handshake.
/// </summary>
[MessagePackObject]
public sealed record HelloPayload
{
    /// <summary>X25519 ephemeral public key (32 bytes); used in DH agreement.</summary>
    [Key(0)] public byte[] EphemeralPublicKey { get; init; } = Array.Empty<byte>();

    /// <summary>Ed25519 long-lived identity public key (32 bytes); equals <c>PeerId</c> bytes.</summary>
    [Key(1)] public byte[] IdentityPublicKey { get; init; } = Array.Empty<byte>();

    /// <summary>Tenant binding (the string-backed <c>TenantId.Value</c>).</summary>
    [Key(2)] public string TenantId { get; init; } = string.Empty;

    /// <summary>Ed25519 signature (64 bytes) over the canonical HELLO byte stream.</summary>
    [Key(3)] public byte[] Signature { get; init; } = Array.Empty<byte>();

    /// <summary>Embedded presence beacon for the same peer (saves a HEARTBEAT round-trip).</summary>
    [Key(4)] public PresenceHeartbeat Presence { get; init; } = new();
}

/// <summary>
/// Periodic presence beacon (frame type 0x02). Sent every 30s; receivers TTL-evict
/// at 45s. Signature covers <c>(IdentityPublicKey || TenantIdUtf8 || Caps || Timestamp)</c>
/// so heartbeats survive replay-or-tamper between the same pair of peers.
/// </summary>
[MessagePackObject]
public sealed record PresenceHeartbeat
{
    /// <summary>The sender's PeerId (base64url Ed25519 public key).</summary>
    [Key(0)] public string PeerId { get; init; } = string.Empty;

    /// <summary>Tenant binding (the string-backed <c>TenantId.Value</c>).</summary>
    [Key(1)] public string TenantId { get; init; } = string.Empty;

    /// <summary>Capability flags bit-mask (matches <c>ChannelCapability</c>).</summary>
    [Key(2)] public byte Caps { get; init; }

    /// <summary>Unix milliseconds (UTC) at the moment the heartbeat was minted.</summary>
    [Key(3)] public long Timestamp { get; init; }

    /// <summary>Ed25519 signature (64 bytes) over the canonical heartbeat byte stream.</summary>
    [Key(4)] public byte[] Signature { get; init; } = Array.Empty<byte>();
}

/// <summary>Invitation to open a session (frame type 0x03). Caller offers a flag-bitmask of capabilities.</summary>
[MessagePackObject]
public sealed record InvitePayload
{
    /// <summary>Offered capability flags (initiator-side).</summary>
    [Key(0)] public byte Capabilities { get; init; }
}

/// <summary>Acceptance of a prior INVITE (frame type 0x04). Conveys the highest common capability.</summary>
[MessagePackObject]
public sealed record AcceptPayload
{
    /// <summary>Single negotiated capability (highest-common bit, NOT a flag-mask).</summary>
    [Key(0)] public byte Capability { get; init; }
}

/// <summary>
/// Transcript-hash confirmation (frame type 0x0A). Sent after ACCEPT by both
/// peers; SHA-256 over the canonical handshake transcript. A mismatch terminates
/// the session with <c>ChannelTerminationReason.TranscriptMismatch</c>.
/// </summary>
[MessagePackObject]
public sealed record ConfirmPayload
{
    /// <summary>SHA-256 of the canonical transcript (32 bytes).</summary>
    [Key(0)] public byte[] TranscriptHash { get; init; } = Array.Empty<byte>();
}

/// <summary>Plain UTF-8 chat text (frame type 0x10). Encrypted under the session AEAD.</summary>
[MessagePackObject]
public sealed record TextPayload
{
    /// <summary>Caller-generated message identifier (used by the matching DELIVERED frame).</summary>
    [Key(0)] public Guid MessageId { get; init; }

    /// <summary>UTF-8 message body.</summary>
    [Key(1)] public string Message { get; init; } = string.Empty;
}

/// <summary>Read receipt for a prior TEXT (frame type 0x08).</summary>
[MessagePackObject]
public sealed record DeliveredPayload
{
    /// <summary>The MessageId of the TEXT frame this acknowledges.</summary>
    [Key(0)] public Guid MessageId { get; init; }
}
