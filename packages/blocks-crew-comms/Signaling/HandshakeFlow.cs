using System;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;
using NSec.Cryptography;
using Sunfish.Blocks.CrewComms.Crypto;
using Sunfish.Blocks.CrewComms.Protocol;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Blocks.CrewComms.Signaling;

/// <summary>
/// Shared handshake-flow primitives used by both <see cref="SessionInitiator"/>
/// and <see cref="SessionListener"/>. Each method drives one step of the
/// 9-step ADR 0076 §Encryption-handshake; callers compose them into the
/// initiator vs responder ordering.
/// </summary>
internal static class HandshakeFlow
{
    /// <summary>
    /// Generates a fresh X25519 ephemeral key pair. Returned <see cref="Key"/>
    /// is owned by the caller; dispose it after deriving the session key.
    /// </summary>
    public static Key GenerateEphemeralKey()
    {
        return Key.Create(KeyAgreementAlgorithm.X25519,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
    }

    /// <summary>
    /// Sends a signed HELLO over the supplied frame protocol. Returns the
    /// HELLO payload that was sent (caller passes it to the transcript-hash
    /// calculation).
    /// </summary>
    public static async Task<HelloPayload> SendHelloAsync(
        FrameProtocol frames,
        EncryptionHandshake handshake,
        Key ephemeralKey,
        ChannelCapability caps,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var ephemeralPub = ephemeralKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);
        var hello = handshake.BuildHello(ephemeralPub, caps, now);
        var payload = MessagePackSerializer.Serialize(hello, CrewCommsResolver.Options);
        await frames.WriteFrameAsync(MessageType.Hello, payload, ct).ConfigureAwait(false);
        return hello;
    }

    /// <summary>
    /// Reads a remote HELLO and verifies its signature, tenant, and roster
    /// membership. Returns the verified payload + the resolved peer.
    /// </summary>
    public static async Task<(HelloPayload hello, PeerId peer)> ReadAndVerifyHelloAsync(
        FrameProtocol frames,
        EncryptionHandshake handshake,
        CancellationToken ct)
    {
        var (type, payload) = await frames.ReadFrameAsync(ct).ConfigureAwait(false);
        if (type != MessageType.Hello)
            throw new InvalidOperationException($"Expected HELLO frame; got 0x{type:X2}.");
        var hello = MessagePackSerializer.Deserialize<HelloPayload>(payload, CrewCommsResolver.Options);
        var peer = await handshake.VerifyHelloAsync(hello, ct).ConfigureAwait(false);
        return (hello, peer);
    }

    /// <summary>
    /// Drives the remaining handshake steps after both HELLOs have been
    /// exchanged: derive session key, send INVITE, await ACCEPT (60s
    /// budget), exchange CONFIRM. Used by the initiator.
    /// </summary>
    public static async Task<ChannelCapability> InitiatorPostHelloAsync(
        FrameProtocol frames,
        EncryptionHandshake handshake,
        Key localEphemeralKey,
        HelloPayload localHello,
        HelloPayload remoteHello,
        TenantId tenantId,
        ChannelCapability preferredCapabilities,
        PeerId localPeer,
        PeerId remotePeer,
        CancellationToken ct)
    {
        handshake.DeriveSessionKey(
            localEphemeralKey, remoteHello.EphemeralPublicKey, localPeer, remotePeer);

        // Per ADR 0076 §step 8 — all post-HELLO frames are AEAD-wrapped under the
        // derived session key. Initiator role: nonce bit 63 = 0.
        frames.EnableAead(handshake.SessionKey!, isInitiator: true);

        // Initiator sends INVITE
        var invitePayload = MessagePackSerializer.Serialize(
            new InvitePayload { Capabilities = (byte)preferredCapabilities },
            CrewCommsResolver.Options);
        await frames.WriteFrameAsync(MessageType.Invite, invitePayload, ct).ConfigureAwait(false);

        // Wait for ACCEPT (60s budget)
        using var acceptCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        acceptCts.CancelAfter(TimeSpan.FromSeconds(60));
        AcceptPayload accept;
        try
        {
            var (acceptType, acceptBytes) = await frames.ReadFrameAsync(acceptCts.Token).ConfigureAwait(false);
            if (acceptType == MessageType.Reject)
                throw new ChannelInvitationRejectedException("Peer rejected the invitation.");
            if (acceptType != MessageType.Accept)
                throw new InvalidOperationException($"Expected ACCEPT/REJECT; got 0x{acceptType:X2}.");
            accept = MessagePackSerializer.Deserialize<AcceptPayload>(acceptBytes, CrewCommsResolver.Options);
        }
        catch (OperationCanceledException) when (acceptCts.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new TimeoutException("ACCEPT was not received within the 60s INVITE budget.");
        }
        var negotiated = (ChannelCapability)accept.Capability;

        // Per ADR 0076-A2: initiator MUST verify ACCEPT.capability ⊆ sent INVITE.capabilities.
        // A relay-MitM that downgrades INVITE could otherwise propose an ACCEPT.capability
        // not in the initiator's offered set; without this check the initiator would
        // silently use the downgraded capability.
        if ((accept.Capability & (byte)preferredCapabilities) != accept.Capability)
        {
            try
            {
                await frames.WriteFrameAsync(MessageType.Reject, Array.Empty<byte>(), ct).ConfigureAwait(false);
            }
            catch { /* best-effort */ }
            throw new CapabilityNegotiationException(
                $"ACCEPT.capability 0x{accept.Capability:X2} is not a subset of INVITE.capabilities 0x{(byte)preferredCapabilities:X2}.");
        }

        // Compute transcript hash; both peers compute identically.
        // Per ADR 0076-A1+A2: the transcript binds offered (INVITE.capabilities)
        // + negotiated (ACCEPT.capability) + each peer's broadcast presence caps,
        // closing the relay-MITM capability-downgrade vector.
        var tenantBytes = EncryptionHandshake.TenantBytes(tenantId);
        var transcript = EncryptionHandshake.ComputeTranscriptHash(
            localHello.EphemeralPublicKey, localHello.IdentityPublicKey,
            remoteHello.EphemeralPublicKey, remoteHello.IdentityPublicKey,
            tenantBytes,
            (byte)preferredCapabilities,
            accept.Capability,
            localHello.Presence.Caps,
            remoteHello.Presence.Caps);

        // Send CONFIRM
        var confirmPayload = MessagePackSerializer.Serialize(
            new ConfirmPayload { TranscriptHash = transcript },
            CrewCommsResolver.Options);
        await frames.WriteFrameAsync(MessageType.Confirm, confirmPayload, ct).ConfigureAwait(false);

        // Read remote CONFIRM
        var (confirmType, confirmBytes) = await frames.ReadFrameAsync(ct).ConfigureAwait(false);
        if (confirmType != MessageType.Confirm)
            throw new InvalidOperationException($"Expected CONFIRM; got 0x{confirmType:X2}.");
        var remoteConfirm = MessagePackSerializer.Deserialize<ConfirmPayload>(confirmBytes, CrewCommsResolver.Options);
        if (!EncryptionHandshake.TranscriptsMatch(transcript, remoteConfirm.TranscriptHash))
            throw new TranscriptMismatchException("Remote CONFIRM transcript hash does not match local transcript.");

        return negotiated;
    }

    /// <summary>
    /// Drives the responder side after both HELLOs have been exchanged:
    /// derive session key, await INVITE. Returns the offered capabilities.
    /// The caller (SessionListener / IChannelInvitation.AcceptAsync) is
    /// responsible for sending ACCEPT and exchanging CONFIRM.
    /// </summary>
    public static async Task<ChannelCapability> ResponderReadInviteAsync(
        FrameProtocol frames,
        EncryptionHandshake handshake,
        Key localEphemeralKey,
        HelloPayload remoteHello,
        PeerId initiatorPeer,
        PeerId responderPeer,
        CancellationToken ct)
    {
        handshake.DeriveSessionKey(
            localEphemeralKey, remoteHello.EphemeralPublicKey, initiatorPeer, responderPeer);

        // Per ADR 0076 §step 8 — all post-HELLO frames are AEAD-wrapped under the
        // derived session key. Responder role: nonce bit 63 = 1.
        frames.EnableAead(handshake.SessionKey!, isInitiator: false);

        var (type, payload) = await frames.ReadFrameAsync(ct).ConfigureAwait(false);
        if (type != MessageType.Invite)
            throw new InvalidOperationException($"Expected INVITE; got 0x{type:X2}.");
        var invite = MessagePackSerializer.Deserialize<InvitePayload>(payload, CrewCommsResolver.Options);
        return (ChannelCapability)invite.Capabilities;
    }

    /// <summary>
    /// Sends ACCEPT + drives the CONFIRM exchange on the responder side. Used
    /// by <c>IChannelInvitation.AcceptAsync</c>.
    /// </summary>
    /// <param name="frames">AEAD-enabled frame protocol bound to the duplex stream.</param>
    /// <param name="negotiated">Capability negotiated by the responder via <see cref="NegotiateHighestCommon"/>.</param>
    /// <param name="offeredCapabilities">
    /// The <c>INVITE.capabilities</c> byte AS RECEIVED on the wire (NOT the
    /// post-negotiation value). Per ADR 0076-A2 a relay-MITM tampering with
    /// this byte surfaces as a CONFIRM transcript mismatch on the initiator
    /// side, because the initiator's transcript is bound to the bytes it
    /// sent and the responder's transcript is bound to the (post-tamper)
    /// bytes it received.
    /// </param>
    /// <param name="initiatorHello">HELLO payload received from the remote initiator.</param>
    /// <param name="responderHello">HELLO payload this responder sent.</param>
    /// <param name="tenantId">Tenant under which the channel is opened.</param>
    /// <param name="ct">Cancellation token for the ACCEPT/CONFIRM exchange.</param>
    public static async Task ResponderAcceptAsync(
        FrameProtocol frames,
        ChannelCapability negotiated,
        byte offeredCapabilities,
        HelloPayload initiatorHello,
        HelloPayload responderHello,
        TenantId tenantId,
        CancellationToken ct)
    {
        var acceptPayload = MessagePackSerializer.Serialize(
            new AcceptPayload { Capability = (byte)negotiated },
            CrewCommsResolver.Options);
        await frames.WriteFrameAsync(MessageType.Accept, acceptPayload, ct).ConfigureAwait(false);

        // Per ADR 0076-A1+A2: bind INVITE.capabilities + ACCEPT.capability +
        // both presence-caps bytes into the transcript so a relay-MITM cannot
        // downgrade either signal post-hoc without producing a CONFIRM
        // mismatch on at least one peer.
        var tenantBytes = EncryptionHandshake.TenantBytes(tenantId);
        var transcript = EncryptionHandshake.ComputeTranscriptHash(
            initiatorHello.EphemeralPublicKey, initiatorHello.IdentityPublicKey,
            responderHello.EphemeralPublicKey, responderHello.IdentityPublicKey,
            tenantBytes,
            offeredCapabilities,
            (byte)negotiated,
            initiatorHello.Presence.Caps,
            responderHello.Presence.Caps);

        // Read initiator's CONFIRM first (initiator sends CONFIRM right after sending the ACCEPT we just sent).
        var (confirmType, confirmBytes) = await frames.ReadFrameAsync(ct).ConfigureAwait(false);
        if (confirmType != MessageType.Confirm)
            throw new InvalidOperationException($"Expected CONFIRM; got 0x{confirmType:X2}.");
        var initiatorConfirm = MessagePackSerializer.Deserialize<ConfirmPayload>(confirmBytes, CrewCommsResolver.Options);
        if (!EncryptionHandshake.TranscriptsMatch(transcript, initiatorConfirm.TranscriptHash))
            throw new TranscriptMismatchException("Initiator CONFIRM transcript hash does not match local transcript.");

        var ourConfirmPayload = MessagePackSerializer.Serialize(
            new ConfirmPayload { TranscriptHash = transcript },
            CrewCommsResolver.Options);
        await frames.WriteFrameAsync(MessageType.Confirm, ourConfirmPayload, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Negotiates the highest common capability: the highest single bit set
    /// in BOTH <paramref name="offered"/> and <paramref name="local"/>.
    /// Returns <see cref="ChannelCapability.None"/> when no overlap exists.
    /// </summary>
    public static ChannelCapability NegotiateHighestCommon(ChannelCapability offered, ChannelCapability local)
    {
        var common = offered & local;
        if ((common & ChannelCapability.Video) != 0) return ChannelCapability.Video;
        if ((common & ChannelCapability.Audio) != 0) return ChannelCapability.Audio;
        if ((common & ChannelCapability.Text) != 0) return ChannelCapability.Text;
        return ChannelCapability.None;
    }
}

/// <summary>Thrown when the peer responds with REJECT instead of ACCEPT.</summary>
public sealed class ChannelInvitationRejectedException : Exception
{
    /// <inheritdoc />
    public ChannelInvitationRejectedException(string message) : base(message) { }
}

/// <summary>
/// Thrown when CONFIRM transcript hashes diverge between peers; signals
/// active MITM tampering at the signaling layer per ADR 0076.
/// </summary>
public sealed class TranscriptMismatchException : Exception
{
    /// <inheritdoc />
    public TranscriptMismatchException(string message) : base(message) { }
}

/// <summary>
/// Thrown by <c>SessionInitiator</c> when an inbound ACCEPT proposes a
/// capability that was not in the initiator's INVITE offer set. Per
/// ADR 0076-A2 — closes the relay-MitM downgrade vector.
/// </summary>
public sealed class CapabilityNegotiationException : Exception
{
    /// <inheritdoc />
    public CapabilityNegotiationException(string message) : base(message) { }
}
