using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NSec.Cryptography;
using Sunfish.Blocks.CrewComms.Crypto;
using Sunfish.Blocks.CrewComms.Protocol;
using Sunfish.Blocks.CrewComms.Session;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Crypto;
using Sunfish.Foundation.Transport;

namespace Sunfish.Blocks.CrewComms.Signaling;

/// <summary>
/// Surfaces incoming channel invitations as an <see cref="IAsyncEnumerable{T}"/>
/// of <see cref="IChannelInvitation"/>. Backed by a bounded
/// <see cref="System.Threading.Channels.Channel{T}"/> of capacity 16; INVITEs
/// arriving when the channel is full are dropped (DropNewest) and a
/// <c>ChannelInviteDropped</c> audit event is emitted via
/// <see cref="OnInviteDropped"/>. Per ADR 0076.
/// </summary>
public sealed class SessionListener
{
    private const int InviteChannelCapacity = 16;

    private readonly KeyPair _identity;
    private readonly ICrewRoster _roster;
    private readonly TimeProvider _time;
    private readonly Channel<IChannelInvitation> _invitations =
        Channel.CreateBounded<IChannelInvitation>(new BoundedChannelOptions(InviteChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropNewest,
            SingleReader = false,
            SingleWriter = false,
        });
    private long _droppedCount;

    /// <summary>Creates a listener bound to the supplied identity + roster.</summary>
    public SessionListener(KeyPair identity, ICrewRoster roster, TimeProvider? time = null)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _roster = roster ?? throw new ArgumentNullException(nameof(roster));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Optional drop-event callback. When set, fires once per dropped INVITE
    /// — wire to <c>IAuditTrail</c> in production. Best-effort, fire-and-forget.
    /// </summary>
    public Action<DateTimeOffset>? OnInviteDropped { get; set; }

    /// <summary>Total number of INVITEs dropped due to bounded-channel saturation.</summary>
    public long DroppedCount => System.Threading.Interlocked.Read(ref _droppedCount);

    /// <summary>Streams every queued <see cref="IChannelInvitation"/> until the listener stops or <paramref name="ct"/> fires.</summary>
    public IAsyncEnumerable<IChannelInvitation> ListenAsync(TenantId tenant, CancellationToken ct)
        => _invitations.Reader.ReadAllAsync(ct);

    /// <summary>
    /// Invoked by the transport adapter (or the integration test) when a
    /// new inbound stream arrives. Reads the remote HELLO, verifies it,
    /// sends our HELLO, awaits the INVITE, and queues an
    /// <see cref="IChannelInvitation"/>. Returns when the invitation is
    /// queued (or dropped) — the eventual ACCEPT/REJECT happens when the
    /// caller of <see cref="ListenAsync"/> invokes the invitation's
    /// AcceptAsync / RejectAsync.
    /// </summary>
    public async Task AcceptIncomingAsync(
        IDuplexStream stream, TenantId tenant, ChannelCapability localCapabilities, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var frames = new FrameProtocol(stream);
        var handshake = new EncryptionHandshake(_identity, _roster, tenant);

        Key? localEphemeral = null;
        try
        {
            // 1. Read remote HELLO first (initiator sends first).
            var (remoteHello, initiatorPeer) = await HandshakeFlow.ReadAndVerifyHelloAsync(
                frames, handshake, ct).ConfigureAwait(false);

            // 2. Send our HELLO.
            localEphemeral = HandshakeFlow.GenerateEphemeralKey();
            var responderHello = await HandshakeFlow.SendHelloAsync(
                frames, handshake, localEphemeral, localCapabilities, _time.GetUtcNow(), ct)
                .ConfigureAwait(false);

            // 3. Read INVITE; derive session key.
            var responderPeer = PeerId.From(_identity.PrincipalId);
            var offered = await HandshakeFlow.ResponderReadInviteAsync(
                frames, handshake, localEphemeral, remoteHello, initiatorPeer, responderPeer, ct)
                .ConfigureAwait(false);

            // 4. Hand off to a deferred IChannelInvitation. The eventual
            //    AcceptAsync / RejectAsync sends ACCEPT/REJECT and (on accept)
            //    drives the CONFIRM exchange.
            var negotiated = HandshakeFlow.NegotiateHighestCommon(offered, localCapabilities);
            var invitation = new DeferredInvitation(
                frames, handshake, remoteHello, responderHello, initiatorPeer, tenant, negotiated, offered, _time);

            if (!_invitations.Writer.TryWrite(invitation))
            {
                // Channel full → drop (per BoundedChannelFullMode.DropNewest, the new
                // write is rejected; we tear down the would-be invitation immediately).
                System.Threading.Interlocked.Increment(ref _droppedCount);
                OnInviteDropped?.Invoke(_time.GetUtcNow());
                await invitation.RejectAsync("Listener invitation queue is at capacity.", ct).ConfigureAwait(false);
            }
        }
        catch
        {
            await frames.DisposeAsync().ConfigureAwait(false);
            handshake.Dispose();
            throw;
        }
        finally
        {
            localEphemeral?.Dispose();
        }
    }

    /// <summary>Stops accepting new invitations. Existing ones remain available to consumers.</summary>
    public void Stop() => _invitations.Writer.TryComplete();
}

internal sealed class DeferredInvitation : IChannelInvitation
{
    private readonly FrameProtocol _frames;
    private readonly EncryptionHandshake _handshake;
    private readonly HelloPayload _initiatorHello;
    private readonly HelloPayload _responderHello;
    private readonly TenantId _tenantId;
    private readonly ChannelCapability _negotiated;
    private readonly TimeProvider _time;
    private int _settled;

    public DeferredInvitation(
        FrameProtocol frames,
        EncryptionHandshake handshake,
        HelloPayload initiatorHello,
        HelloPayload responderHello,
        PeerId initiatorPeer,
        TenantId tenantId,
        ChannelCapability negotiated,
        ChannelCapability offered,
        TimeProvider time)
    {
        _frames = frames;
        _handshake = handshake;
        _initiatorHello = initiatorHello;
        _responderHello = responderHello;
        FromPeer = initiatorPeer;
        _tenantId = tenantId;
        _negotiated = negotiated;
        OfferedCapabilities = offered;
        _time = time;
    }

    public PeerId FromPeer { get; }
    public ChannelCapability OfferedCapabilities { get; }

    public async Task<IChannelSession> AcceptAsync(CancellationToken ct)
    {
        if (System.Threading.Interlocked.Exchange(ref _settled, 1) != 0)
            throw new InvalidOperationException("Invitation already settled.");
        if (_negotiated == ChannelCapability.None)
            throw new InvalidOperationException("No common capability — cannot accept.");

        await HandshakeFlow.ResponderAcceptAsync(
            _frames, _negotiated, _initiatorHello, _responderHello, _tenantId, ct)
            .ConfigureAwait(false);

        var session = new NativeChannelSession(_frames, _handshake, FromPeer, _negotiated, _time);
        session.Activate();
        return session;
    }

    public async Task RejectAsync(string? reason, CancellationToken ct)
    {
        if (System.Threading.Interlocked.Exchange(ref _settled, 1) != 0)
            return;
        try
        {
            await _frames.WriteFrameAsync(MessageType.Reject, Array.Empty<byte>(), ct).ConfigureAwait(false);
        }
        catch { /* best-effort */ }
        await _frames.DisposeAsync().ConfigureAwait(false);
        _handshake.Dispose();
    }
}
