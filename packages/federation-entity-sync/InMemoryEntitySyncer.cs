using System.Text.Json;
using Sunfish.Federation.Common;
using Sunfish.Federation.EntitySync.Protocol;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Federation.EntitySync;

/// <summary>
/// In-process <see cref="IEntitySyncer"/>. Registers a <see cref="ISyncTransport"/> handler for the
/// local peer id derived from the signer, and implements the head-announcement / change-exchange
/// protocol. Every received envelope is signature-verified against the sending peer's principal
/// (derived from the peer id's base64url form), and every received change is signature-verified
/// against its embedded <see cref="SignedOperation{ChangeRecord}.IssuerId"/>.
/// </summary>
/// <remarks>
/// <para>
/// The local peer id is derived as <c>PeerId.From(signer.IssuerId)</c>. The handler registration on
/// the transport is owned by this syncer; call <see cref="Dispose"/> to unregister.
/// </para>
/// <para>
/// Protocol flows:
/// </para>
/// <list type="bullet">
///   <item>
///     <description><see cref="PullFromAsync"/> — send
///     <see cref="SyncMessageKind.EntityChangesRequest"/> carrying local heads; receive
///     <see cref="SyncMessageKind.EntityChangesResponse"/>; apply.</description>
///   </item>
///   <item>
///     <description><see cref="PushToAsync"/> — send
///     <see cref="SyncMessageKind.EntityHeadsAnnouncement"/> to learn peer's heads; compute delta;
///     send <see cref="SyncMessageKind.EntityChangesResponse"/>; receive ack.</description>
///   </item>
///   <item>
///     <description>As receiver — respond to
///     <see cref="SyncMessageKind.EntityChangesRequest"/> with matching reachable changes; respond
///     to <see cref="SyncMessageKind.EntityHeadsAnnouncement"/> with our own heads; accept
///     <see cref="SyncMessageKind.EntityChangesResponse"/> as a push and ack with an empty response.</description>
///   </item>
/// </list>
/// </remarks>
public sealed class InMemoryEntitySyncer : IEntitySyncer, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    private readonly IChangeStore _local;
    private readonly ISyncTransport _transport;
    private readonly IOperationSigner _localSigner;
    private readonly IOperationVerifier _verifier;
    private readonly PeerId _localPeerId;
    private readonly IDisposable? _handlerRegistration;
    private int _disposed;

    /// <summary>Creates a syncer and eagerly registers its handler on <paramref name="transport"/>.</summary>
    public InMemoryEntitySyncer(
        IChangeStore local,
        ISyncTransport transport,
        IOperationSigner localSigner,
        IOperationVerifier verifier)
    {
        _local = local ?? throw new ArgumentNullException(nameof(local));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _localSigner = localSigner ?? throw new ArgumentNullException(nameof(localSigner));
        _verifier = verifier ?? throw new ArgumentNullException(nameof(verifier));
        _localPeerId = PeerId.From(localSigner.IssuerId);
        _handlerRegistration = _transport.RegisterHandler(_localPeerId, HandleAsync);
    }

    /// <inheritdoc />
    public async ValueTask<SyncResult> PullFromAsync(PeerDescriptor peer, EntityId? scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(peer);

        var myHeads = _local.GetHeads(scope);
        var request = new ChangesRequest(scope, myHeads, Array.Empty<VersionId>());
        var requestBytes = SerializeToJsonUtf8(request);

        var envelope = SyncEnvelope.SignAndCreate(
            _localSigner, peer.Id, SyncMessageKind.EntityChangesRequest, requestBytes);

        var responseEnvelope = await _transport.SendAsync(peer, envelope, ct).ConfigureAwait(false);

        var peerPrincipal = PeerPrincipal(peer.Id);
        if (peerPrincipal is null || !responseEnvelope.Verify(_verifier, peerPrincipal.Value))
        {
            return new SyncResult(
                0, 0, 0,
                new[] { new SyncRejection(default, "Response envelope signature invalid.") });
        }

        var response = DeserializeFromJsonUtf8<ChangesResponse>(responseEnvelope.Payload);
        return ApplyReceivedChanges(response);
    }

    /// <inheritdoc />
    public async ValueTask<SyncResult> PushToAsync(PeerDescriptor peer, EntityId? scope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(peer);

        // Step 1: ask the peer for its heads.
        var myHeads = _local.GetHeads(scope);
        var announce = new HeadsAnnouncement(scope, myHeads);
        var announceBytes = SerializeToJsonUtf8(announce);
        var announceEnvelope = SyncEnvelope.SignAndCreate(
            _localSigner, peer.Id, SyncMessageKind.EntityHeadsAnnouncement, announceBytes);

        var headsResponseEnvelope = await _transport.SendAsync(peer, announceEnvelope, ct).ConfigureAwait(false);

        var peerPrincipal = PeerPrincipal(peer.Id);
        if (peerPrincipal is null || !headsResponseEnvelope.Verify(_verifier, peerPrincipal.Value))
        {
            return new SyncResult(
                0, 0, 0,
                new[] { new SyncRejection(default, "Heads response envelope signature invalid.") });
        }

        var peerHeads = DeserializeFromJsonUtf8<HeadsAnnouncement>(headsResponseEnvelope.Payload);

        // Step 2: compute the delta the peer needs and send it.
        var needed = _local.GetReachableFrom(myHeads, peerHeads.LocalHeads);
        if (needed.Count == 0)
            return new SyncResult(0, 0, 0, Array.Empty<SyncRejection>());

        var pushResponse = new ChangesResponse(needed.Select(SignedChangeRecordDto.FromSigned).ToList());
        var pushBytes = SerializeToJsonUtf8(pushResponse);
        var pushEnvelope = SyncEnvelope.SignAndCreate(
            _localSigner, peer.Id, SyncMessageKind.EntityChangesResponse, pushBytes);

        var ackEnvelope = await _transport.SendAsync(peer, pushEnvelope, ct).ConfigureAwait(false);

        if (!ackEnvelope.Verify(_verifier, peerPrincipal.Value))
        {
            return new SyncResult(
                needed.Count, 0, 0,
                new[] { new SyncRejection(default, "Ack envelope signature invalid.") });
        }

        return new SyncResult(needed.Count, 0, 0, Array.Empty<SyncRejection>());
    }

    private ValueTask<SyncEnvelope> HandleAsync(SyncEnvelope incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);

        var peerPrincipal = PeerPrincipal(incoming.FromPeer);
        if (peerPrincipal is null || !incoming.Verify(_verifier, peerPrincipal.Value))
        {
            // Sender is unverifiable. Respond with an empty ChangesResponse so the transport
            // round-trip completes; the caller side will fail its own envelope verification
            // when this reply is signed by us but the incoming was malformed.
            var errorPayload = SerializeToJsonUtf8(new ChangesResponse(Array.Empty<SignedChangeRecordDto>()));
            return ValueTask.FromResult(SyncEnvelope.SignAndCreate(
                _localSigner, incoming.FromPeer, SyncMessageKind.EntityChangesResponse, errorPayload));
        }

        return incoming.Kind switch
        {
            SyncMessageKind.EntityChangesRequest => HandleChangesRequest(incoming),
            SyncMessageKind.EntityHeadsAnnouncement => HandleHeadsAnnouncement(incoming),
            SyncMessageKind.EntityChangesResponse => HandlePushedChanges(incoming),
            _ => HandleUnsupported(incoming),
        };
    }

    private ValueTask<SyncEnvelope> HandleChangesRequest(SyncEnvelope incoming)
    {
        var request = DeserializeFromJsonUtf8<ChangesRequest>(incoming.Payload);
        var myHeads = _local.GetHeads(request.Scope);
        var needed = _local.GetReachableFrom(myHeads, request.LocalHeads);
        var response = new ChangesResponse(needed.Select(SignedChangeRecordDto.FromSigned).ToList());
        var responseBytes = SerializeToJsonUtf8(response);
        return ValueTask.FromResult(SyncEnvelope.SignAndCreate(
            _localSigner, incoming.FromPeer, SyncMessageKind.EntityChangesResponse, responseBytes));
    }

    private ValueTask<SyncEnvelope> HandleHeadsAnnouncement(SyncEnvelope incoming)
    {
        var incomingAnnouncement = DeserializeFromJsonUtf8<HeadsAnnouncement>(incoming.Payload);
        var responseAnnouncement = new HeadsAnnouncement(incomingAnnouncement.Scope, _local.GetHeads(incomingAnnouncement.Scope));
        var responseBytes = SerializeToJsonUtf8(responseAnnouncement);
        return ValueTask.FromResult(SyncEnvelope.SignAndCreate(
            _localSigner, incoming.FromPeer, SyncMessageKind.EntityHeadsAnnouncement, responseBytes));
    }

    private ValueTask<SyncEnvelope> HandlePushedChanges(SyncEnvelope incoming)
    {
        var response = DeserializeFromJsonUtf8<ChangesResponse>(incoming.Payload);
        ApplyReceivedChanges(response);
        var ack = new ChangesResponse(Array.Empty<SignedChangeRecordDto>());
        var ackBytes = SerializeToJsonUtf8(ack);
        return ValueTask.FromResult(SyncEnvelope.SignAndCreate(
            _localSigner, incoming.FromPeer, SyncMessageKind.EntityChangesResponse, ackBytes));
    }

    private ValueTask<SyncEnvelope> HandleUnsupported(SyncEnvelope incoming)
    {
        var ack = new ChangesResponse(Array.Empty<SignedChangeRecordDto>());
        var ackBytes = SerializeToJsonUtf8(ack);
        return ValueTask.FromResult(SyncEnvelope.SignAndCreate(
            _localSigner, incoming.FromPeer, SyncMessageKind.EntityChangesResponse, ackBytes));
    }

    private SyncResult ApplyReceivedChanges(ChangesResponse response)
    {
        int transferred = 0, alreadyPresent = 0, rejected = 0;
        var rejections = new List<SyncRejection>();

        foreach (var dto in response.Changes)
        {
            SignedOperation<ChangeRecord> signedChange;
            try
            {
                signedChange = dto.ToSigned();
            }
            catch (FormatException ex)
            {
                rejected++;
                rejections.Add(new SyncRejection(default, $"Malformed DTO: {ex.Message}"));
                continue;
            }

            if (_local.Contains(signedChange.Payload.VersionId))
            {
                alreadyPresent++;
                continue;
            }

            if (!_verifier.Verify(signedChange))
            {
                rejected++;
                rejections.Add(new SyncRejection(signedChange.Payload.VersionId, "Invalid signature."));
                continue;
            }

            _local.Put(signedChange);
            transferred++;
        }

        return new SyncResult(transferred, alreadyPresent, rejected, rejections);
    }

    /// <summary>
    /// Derives the <see cref="PrincipalId"/> of a peer from its <see cref="PeerId.Value"/>
    /// base64url form. Returns <c>null</c> when the peer id is not a well-formed Ed25519 public key.
    /// </summary>
    private static PrincipalId? PeerPrincipal(PeerId peerId)
    {
        try
        {
            return PrincipalId.FromBase64Url(peerId.Value);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentNullException)
        {
            return null;
        }
    }

    private static byte[] SerializeToJsonUtf8<T>(T value)
        => JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);

    private static T DeserializeFromJsonUtf8<T>(ReadOnlyMemory<byte> bytes)
        => JsonSerializer.Deserialize<T>(bytes.Span, JsonOptions)
            ?? throw new InvalidOperationException($"Deserialization of {typeof(T).Name} produced null.");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
            _handlerRegistration?.Dispose();
    }
}
