using System;
using System.Threading;
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
/// Drives the initiator side of the crew-comms handshake: connects via the
/// transport selector, performs HELLO exchange, sends INVITE, awaits ACCEPT
/// (60s budget), exchanges CONFIRM, and returns an active
/// <see cref="IChannelSession"/>. Per ADR 0076.
/// </summary>
public sealed class SessionInitiator
{
    private readonly KeyPair _identity;
    private readonly ICrewRoster _roster;
    private readonly ITransportSelector _selector;
    private readonly TimeProvider _time;

    /// <summary>Creates an initiator bound to the supplied keys + roster + transport stack.</summary>
    public SessionInitiator(
        KeyPair identity,
        ICrewRoster roster,
        ITransportSelector selector,
        TimeProvider? time = null)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _roster = roster ?? throw new ArgumentNullException(nameof(roster));
        _selector = selector ?? throw new ArgumentNullException(nameof(selector));
        _time = time ?? TimeProvider.System;
    }

    /// <summary>
    /// Opens an outbound session to <paramref name="peer"/>. On success, returns
    /// an <see cref="IChannelSession"/> in <see cref="ChannelSessionState.Active"/>.
    /// </summary>
    public async Task<IChannelSession> OpenAsync(
        TenantId tenant,
        PeerId peer,
        ChannelCapability preferredCapabilities,
        CancellationToken ct)
    {
        var transport = await _selector.SelectAsync(peer, ct).ConfigureAwait(false);
        var stream = await transport.ConnectAsync(peer, ct).ConfigureAwait(false);
        var frames = new FrameProtocol(stream);
        var handshake = new EncryptionHandshake(_identity, _roster, tenant);

        Key? localEphemeral = null;
        try
        {
            localEphemeral = HandshakeFlow.GenerateEphemeralKey();
            var localPeer = PeerId.From(_identity.PrincipalId);

            var localHello = await HandshakeFlow.SendHelloAsync(
                frames, handshake, localEphemeral, preferredCapabilities, _time.GetUtcNow(), ct)
                .ConfigureAwait(false);

            var (remoteHello, remotePeer) = await HandshakeFlow.ReadAndVerifyHelloAsync(
                frames, handshake, ct).ConfigureAwait(false);

            if (remotePeer != peer)
                throw new InvalidOperationException(
                    $"Remote HELLO peer {remotePeer} does not match expected {peer}.");

            var negotiated = await HandshakeFlow.InitiatorPostHelloAsync(
                frames, handshake, localEphemeral,
                localHello, remoteHello, tenant, preferredCapabilities,
                localPeer, remotePeer, ct).ConfigureAwait(false);

            if (negotiated == ChannelCapability.None)
                throw new InvalidOperationException("Capability negotiation produced an empty intersection.");

            var session = new NativeChannelSession(frames, handshake, peer, negotiated, _time);
            session.Activate();
            return session;
        }
        catch
        {
            // Tear down on any handshake failure — caller never gets a half-open session.
            await frames.DisposeAsync().ConfigureAwait(false);
            handshake.Dispose();
            throw;
        }
        finally
        {
            localEphemeral?.Dispose();
        }
    }
}
