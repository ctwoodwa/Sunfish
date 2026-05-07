using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NSec.Cryptography;
using Sunfish.Blocks.CrewComms.Protocol;
using Sunfish.Federation.Common;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Channels;
using Sunfish.Foundation.Crypto;

namespace Sunfish.Blocks.CrewComms.Crypto;

/// <summary>
/// Performs the crew-comms encryption handshake — Ed25519-signed HELLO,
/// X25519 ephemeral DH, HKDF-SHA256 session-key derivation, and a transcript
/// CONFIRM exchange. Per ADR 0076 §Encryption handshake.
/// </summary>
/// <remarks>
/// <para>
/// Single-use: each session needs a fresh handshake instance. The owned
/// session key is zeroed on <see cref="Dispose"/>. The handshake itself is
/// stateless across calls; lifetime is tied to the session it derives.
/// </para>
/// <para>
/// Roster-binding: HELLO verification rejects any peer whose Ed25519 public
/// key is not present in the tenant <see cref="ICrewRoster"/> at handshake
/// time, even if the signature is structurally valid.
/// </para>
/// </remarks>
public sealed class EncryptionHandshake : IDisposable
{
    private const string SaltLiteral = "sunfish-crew-comms-v1";
    private const int TranscriptHashByteLength = 32;

    private readonly KeyPair _identity;
    private readonly ICrewRoster _roster;
    private readonly TenantId _tenantId;
    private Key? _sessionKey;
    private bool _disposed;

    /// <summary>Creates a handshake bound to the supplied identity and tenant roster.</summary>
    public EncryptionHandshake(KeyPair identity, ICrewRoster roster, TenantId tenantId)
    {
        _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        _roster = roster ?? throw new ArgumentNullException(nameof(roster));
        _tenantId = tenantId;
    }

    /// <summary>
    /// The session key derived after a successful handshake (<c>null</c> until completion).
    /// </summary>
    /// <remarks>
    /// The returned <see cref="Key"/> is owned by this handshake — callers MUST
    /// NOT dispose it. The key becomes invalid after <see cref="Dispose"/>.
    /// </remarks>
    public Key? SessionKey => _sessionKey;

    /// <summary>Builds a HELLO payload signed by the local identity, advertising the supplied capabilities.</summary>
    public HelloPayload BuildHello(byte[] ephemeralPublicKey, ChannelCapability caps, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ephemeralPublicKey);
        if (ephemeralPublicKey.Length != 32)
            throw new ArgumentException("EphemeralPublicKey must be 32 bytes.", nameof(ephemeralPublicKey));

        var identityPublicKey = _identity.PrincipalId.AsSpan().ToArray();
        var tenantBytes = TenantBytes(_tenantId);

        var helloSignable = ConcatHelloSignable(ephemeralPublicKey, identityPublicKey, tenantBytes);
        var helloSig = _identity.Sign(helloSignable);

        var heartbeatTimestamp = now.ToUnixTimeMilliseconds();
        var heartbeatSignable = ConcatHeartbeatSignable(identityPublicKey, tenantBytes, (byte)caps, heartbeatTimestamp);
        var heartbeatSig = _identity.Sign(heartbeatSignable);

        return new HelloPayload
        {
            EphemeralPublicKey = ephemeralPublicKey,
            IdentityPublicKey = identityPublicKey,
            TenantId = _tenantId.Value,
            Signature = helloSig.AsSpan().ToArray(),
            Presence = new PresenceHeartbeat
            {
                PeerId = PeerId.From(_identity.PrincipalId).Value,
                TenantId = _tenantId.Value,
                Caps = (byte)caps,
                Timestamp = heartbeatTimestamp,
                Signature = heartbeatSig.AsSpan().ToArray(),
            },
        };
    }

    /// <summary>
    /// Verifies a remote HELLO: structural shape, Ed25519 signature, tenant
    /// match, and roster membership. Returns the resolved <see cref="PeerId"/>
    /// of the remote peer on success.
    /// </summary>
    /// <exception cref="CryptographicException">Signature verification failed.</exception>
    /// <exception cref="ArgumentException">Tenant or roster mismatch.</exception>
    public async Task<PeerId> VerifyHelloAsync(HelloPayload remoteHello, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(remoteHello);
        if (remoteHello.EphemeralPublicKey.Length != 32)
            throw new CryptographicException("Remote HELLO has malformed ephemeral key length.");
        if (remoteHello.IdentityPublicKey.Length != PrincipalId.LengthInBytes)
            throw new CryptographicException("Remote HELLO has malformed identity key length.");

        if (!string.Equals(remoteHello.TenantId, _tenantId.Value, StringComparison.Ordinal))
            throw new ArgumentException("Remote HELLO is bound to a different tenant.");

        var signable = ConcatHelloSignable(
            remoteHello.EphemeralPublicKey,
            remoteHello.IdentityPublicKey,
            TenantBytes(_tenantId));

        if (!KeyPair.VerifyRaw(remoteHello.IdentityPublicKey, signable, remoteHello.Signature))
            throw new CryptographicException("Remote HELLO signature verification failed.");

        // The embedded Presence heartbeat carries its own signature; reject HELLO
        // entirely if the inner heartbeat does not verify against the same identity.
        if (!VerifyHeartbeat(remoteHello.Presence, remoteHello.IdentityPublicKey))
            throw new CryptographicException("Remote HELLO embedded presence heartbeat signature verification failed.");

        var remotePrincipal = PrincipalId.FromBytes(remoteHello.IdentityPublicKey);
        var remotePeer = PeerId.From(remotePrincipal);

        var roster = await _roster.GetCrewAsync(_tenantId, ct).ConfigureAwait(false);
        if (!roster.Any(m => m.Peer == remotePeer))
            throw new ArgumentException($"Remote peer {remotePeer} is not in the tenant roster.");

        return remotePeer;
    }

    /// <summary>
    /// Derives the session key from the local ephemeral private key and the
    /// remote ephemeral public key via X25519 + HKDF-SHA256. The DH shared
    /// secret is disposed immediately; the session key is owned by this
    /// handshake until <see cref="Dispose"/>.
    /// </summary>
    public void DeriveSessionKey(
        Key localEphemeralPrivateKey,
        ReadOnlySpan<byte> remoteEphemeralPublicKey,
        PeerId initiatorPeer,
        PeerId responderPeer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_sessionKey is not null)
            throw new InvalidOperationException("Session key already derived; handshake is single-use.");
        ArgumentNullException.ThrowIfNull(localEphemeralPrivateKey);
        if (remoteEphemeralPublicKey.Length != 32)
            throw new CryptographicException("Remote ephemeral public key must be 32 bytes.");

        var remotePk = PublicKey.Import(
            KeyAgreementAlgorithm.X25519, remoteEphemeralPublicKey, KeyBlobFormat.RawPublicKey);

        using var sharedSecret = KeyAgreementAlgorithm.X25519.Agree(localEphemeralPrivateKey, remotePk)
            ?? throw new CryptographicException("X25519 key agreement failed — null shared secret.");

        var info = BuildKdfInfo(initiatorPeer, responderPeer);
        var salt = Encoding.UTF8.GetBytes(SaltLiteral);

        _sessionKey = KeyDerivationAlgorithm.HkdfSha256.DeriveKey(
            sharedSecret,
            salt,
            info,
            AeadAlgorithm.ChaCha20Poly1305,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
    }

    /// <summary>
    /// Computes the SHA-256 transcript hash over the canonical handshake
    /// inputs. Both peers compute identically; mismatch indicates active
    /// MITM tampering at the signaling layer. Per ADR 0076 §A2.3 / §A2.5 step 9
    /// with the §A1 presence-caps extension, the canonical form is:
    /// <c>SHA-256(ephemA[32] || idA[32] || ephemB[32] || idB[32]
    /// || uint32BE(len(tenantBytes)) || tenantBytes
    /// || inviteCap[1] || negotiatedCap[1] || presenceCapsA[1] || presenceCapsB[1])</c>.
    /// <para>
    /// The <c>uint32BE</c> length-prefix on <c>tenantBytes</c> prevents a
    /// length-extension collision via the variable-length adjacency between
    /// tenant-bytes and the trailing capability bytes. The four trailing
    /// capability bytes bind both the offer (INVITE), the negotiated outcome
    /// (ACCEPT), and each peer's broadcast presence capabilities into the
    /// transcript — closing the relay-MITM capability-downgrade vector
    /// addressed by ADR 0076 amendments A1 + A2.
    /// </para>
    /// <remarks>
    /// Pre-A1+A2 implementations (W#45 P1–P4 substrate) used a 6-parameter
    /// signature ending in <c>byte negotiatedCapability</c>; the three new
    /// trailing parameters (<c>inviteCapabilities</c>, <c>presenceCapsInitiator</c>,
    /// <c>presenceCapsResponder</c>) are the A1+A2 additions. Conformance
    /// vectors V7 / V8 / V9 in <c>tools/icm/channel-test-vectors.json</c>
    /// pin the canonical concatenation and SHA-256 outputs; see
    /// <c>tools/icm/generate-channel-vectors.py</c> <c>confirm_transcript_input</c>
    /// for the cross-language reference.
    /// </remarks>
    /// </summary>
    public static byte[] ComputeTranscriptHash(
        ReadOnlySpan<byte> initiatorHelloEphemeral,
        ReadOnlySpan<byte> initiatorHelloIdentity,
        ReadOnlySpan<byte> responderHelloEphemeral,
        ReadOnlySpan<byte> responderHelloIdentity,
        ReadOnlySpan<byte> tenantIdBytes,
        byte inviteCapabilities,
        byte negotiatedCapability,
        byte presenceCapsInitiator,
        byte presenceCapsResponder)
    {
        var totalLen = initiatorHelloEphemeral.Length + initiatorHelloIdentity.Length
            + responderHelloEphemeral.Length + responderHelloIdentity.Length
            + sizeof(uint) + tenantIdBytes.Length + 4;
        var buffer = totalLen <= 256 ? stackalloc byte[totalLen] : new byte[totalLen];
        var offset = 0;
        initiatorHelloEphemeral.CopyTo(buffer.Slice(offset, initiatorHelloEphemeral.Length));
        offset += initiatorHelloEphemeral.Length;
        initiatorHelloIdentity.CopyTo(buffer.Slice(offset, initiatorHelloIdentity.Length));
        offset += initiatorHelloIdentity.Length;
        responderHelloEphemeral.CopyTo(buffer.Slice(offset, responderHelloEphemeral.Length));
        offset += responderHelloEphemeral.Length;
        responderHelloIdentity.CopyTo(buffer.Slice(offset, responderHelloIdentity.Length));
        offset += responderHelloIdentity.Length;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(
            buffer.Slice(offset, sizeof(uint)), (uint)tenantIdBytes.Length);
        offset += sizeof(uint);
        tenantIdBytes.CopyTo(buffer.Slice(offset, tenantIdBytes.Length));
        offset += tenantIdBytes.Length;
        buffer[offset++] = inviteCapabilities;
        buffer[offset++] = negotiatedCapability;
        buffer[offset++] = presenceCapsInitiator;
        buffer[offset] = presenceCapsResponder;

        var output = new byte[TranscriptHashByteLength];
        SHA256.HashData(buffer, output);
        return output;
    }

    /// <summary>Constant-time compare of two transcript hashes (32 bytes each).</summary>
    public static bool TranscriptsMatch(ReadOnlySpan<byte> a, ReadOnlySpan<byte> b)
    {
        if (a.Length != TranscriptHashByteLength || b.Length != TranscriptHashByteLength) return false;
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    /// <summary>Verifies a HEARTBEAT signature against the embedded identity key.</summary>
    public static bool VerifyHeartbeat(PresenceHeartbeat hb, ReadOnlySpan<byte> identityPublicKey)
    {
        ArgumentNullException.ThrowIfNull(hb);
        var tenantBytes = Encoding.UTF8.GetBytes(hb.TenantId);
        var signable = ConcatHeartbeatSignable(identityPublicKey, tenantBytes, hb.Caps, hb.Timestamp);
        return KeyPair.VerifyRaw(identityPublicKey, signable, hb.Signature);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _sessionKey?.Dispose();
        _sessionKey = null;
    }

    /// <summary>UTF-8 byte representation of a TenantId for canonical signable concatenation.</summary>
    public static byte[] TenantBytes(TenantId tenantId) => Encoding.UTF8.GetBytes(tenantId.Value);

    private static byte[] ConcatHelloSignable(
        ReadOnlySpan<byte> ephemeralPublicKey,
        ReadOnlySpan<byte> identityPublicKey,
        ReadOnlySpan<byte> tenantBytes)
    {
        var buf = new byte[ephemeralPublicKey.Length + identityPublicKey.Length + tenantBytes.Length];
        var span = buf.AsSpan();
        var offset = 0;
        ephemeralPublicKey.CopyTo(span.Slice(offset, ephemeralPublicKey.Length));
        offset += ephemeralPublicKey.Length;
        identityPublicKey.CopyTo(span.Slice(offset, identityPublicKey.Length));
        offset += identityPublicKey.Length;
        tenantBytes.CopyTo(span.Slice(offset, tenantBytes.Length));
        return buf;
    }

    private static byte[] ConcatHeartbeatSignable(
        ReadOnlySpan<byte> identityPublicKey,
        ReadOnlySpan<byte> tenantBytes,
        byte caps,
        long timestamp)
    {
        var buf = new byte[identityPublicKey.Length + tenantBytes.Length + 1 + sizeof(long)];
        var span = buf.AsSpan();
        var offset = 0;
        identityPublicKey.CopyTo(span.Slice(offset, identityPublicKey.Length));
        offset += identityPublicKey.Length;
        tenantBytes.CopyTo(span.Slice(offset, tenantBytes.Length));
        offset += tenantBytes.Length;
        span[offset++] = caps;
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(span.Slice(offset, sizeof(long)), timestamp);
        return buf;
    }

    private static byte[] BuildKdfInfo(PeerId initiator, PeerId responder)
    {
        var initiatorBytes = Encoding.UTF8.GetBytes(initiator.Value);
        var responderBytes = Encoding.UTF8.GetBytes(responder.Value);
        const byte sep = (byte)':';
        var buf = new byte[initiatorBytes.Length + 1 + responderBytes.Length];
        Buffer.BlockCopy(initiatorBytes, 0, buf, 0, initiatorBytes.Length);
        buf[initiatorBytes.Length] = sep;
        Buffer.BlockCopy(responderBytes, 0, buf, initiatorBytes.Length + 1, responderBytes.Length);
        return buf;
    }
}
