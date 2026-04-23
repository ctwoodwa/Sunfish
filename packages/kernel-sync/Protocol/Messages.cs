using System.Formats.Cbor;

using Sunfish.Kernel.Security.Attestation;

namespace Sunfish.Kernel.Sync.Protocol;

/// <summary>
/// Enumeration of message type discriminators used on the sync-daemon wire. The
/// string values match sync-daemon-protocol §3 exactly.
/// </summary>
internal static class MessageTypes
{
    public const string Hello = "HELLO";
    public const string CapabilityNeg = "CAPABILITY_NEG";
    public const string Ack = "ACK";
    public const string DeltaStream = "DELTA_STREAM";
    public const string LeaseRequest = "LEASE_REQUEST";
    public const string LeaseGrant = "LEASE_GRANT";
    public const string LeaseRelease = "LEASE_RELEASE";
    public const string LeaseDenied = "LEASE_DENIED";
    public const string GossipPing = "GOSSIP_PING";
    public const string Error = "ERROR";
}

/// <summary>
/// Shared CBOR helpers. All messages are serialized as a two-key outer map
/// <c>{ "type": &lt;discriminator&gt;, "body": &lt;map&gt; }</c> per spec §3. Canonical
/// conformance is used so the emitted bytes are deterministic — this matters for
/// the replay-protection signatures that wrap HELLO (§8).
/// </summary>
internal static class CborEnvelope
{
    public const string TypeKey = "type";
    public const string BodyKey = "body";

    /// <summary>
    /// Open a canonical CBOR writer positioned at the start of the outer envelope
    /// map; caller writes the <c>body</c> contents and must close with
    /// <see cref="CloseEnvelope"/>.
    /// </summary>
    public static CborWriter OpenEnvelope(string type)
    {
        var writer = new CborWriter(CborConformanceMode.Canonical, convertIndefiniteLengthEncodings: true);
        writer.WriteStartMap(2);
        writer.WriteTextString(TypeKey);
        writer.WriteTextString(type);
        writer.WriteTextString(BodyKey);
        return writer;
    }

    public static byte[] CloseEnvelope(CborWriter writer)
    {
        writer.WriteEndMap();
        return writer.Encode();
    }

    /// <summary>
    /// Open a reader positioned on the body map of an envelope with the given
    /// expected type discriminator. Throws <see cref="CborContentException"/> if
    /// the type does not match.
    /// </summary>
    /// <remarks>
    /// Canonical CBOR sorts map keys by length-then-lex, which means <c>body</c>
    /// (4 bytes, 'b') appears before <c>type</c> (4 bytes, 't'). The reader
    /// therefore accepts the two keys in either order rather than hardcoding
    /// the spec §3's diagnostic-notation order.
    /// </remarks>
    public static CborReader OpenBody(ReadOnlySpan<byte> cbor, string expectedType)
    {
        var reader = new CborReader(cbor.ToArray(), CborConformanceMode.Canonical);
        var outerCount = reader.ReadStartMap();
        if (outerCount != 2)
        {
            throw new CborContentException(
                $"Envelope must have exactly 2 keys (got {outerCount}).");
        }

        // Walk both key/value pairs; capture the body bytes when we see the
        // body key and the type string when we see the type key. This handles
        // canonical ordering (body first) and spec diagnostic ordering (type
        // first) transparently.
        string? actualType = null;
        byte[]? bodyBytes = null;
        for (var i = 0; i < 2; i++)
        {
            var key = reader.ReadTextString();
            switch (key)
            {
                case TypeKey:
                    actualType = reader.ReadTextString();
                    break;
                case BodyKey:
                    bodyBytes = reader.ReadEncodedValue().ToArray();
                    break;
                default:
                    throw new CborContentException($"Unexpected envelope key '{key}'.");
            }
        }

        if (actualType is null || bodyBytes is null)
        {
            throw new CborContentException("Envelope missing 'type' or 'body'.");
        }
        if (actualType != expectedType)
        {
            throw new CborContentException(
                $"Expected message type '{expectedType}' (got '{actualType}').");
        }

        // Return a fresh reader positioned at the beginning of the body tree.
        return new CborReader(bodyBytes, CborConformanceMode.Canonical);
    }

    /// <summary>
    /// Peek the <c>type</c> discriminator of a CBOR envelope without consuming
    /// the body. Used by dispatch paths that need to route frames to the right
    /// <c>FromCbor</c> static.
    /// </summary>
    public static string PeekType(ReadOnlySpan<byte> cbor)
    {
        var reader = new CborReader(cbor.ToArray(), CborConformanceMode.Canonical);
        reader.ReadStartMap();
        var firstKey = reader.ReadTextString();
        if (firstKey == TypeKey)
        {
            return reader.ReadTextString();
        }
        // Canonical order: body first. Skip the body, then read type.
        reader.SkipValue();
        var secondKey = reader.ReadTextString();
        if (secondKey != TypeKey)
        {
            throw new CborContentException($"Envelope missing '{TypeKey}' key.");
        }
        return reader.ReadTextString();
    }
}

/// <summary>
/// Sync-daemon-protocol §3.1 HELLO. Exchanged by both initiator and responder
/// at session start.
/// </summary>
public sealed record HelloMessage(
    byte[] NodeId,
    string SchemaVersion,
    IReadOnlyList<string> SupportedVersions,
    byte[] PublicKey,
    ulong Timestamp,
    byte[] Signature)
{
    public byte[] ToCbor()
    {
        var writer = CborEnvelope.OpenEnvelope(MessageTypes.Hello);
        writer.WriteStartMap(6);
        writer.WriteTextString("node_id");
        writer.WriteByteString(NodeId);
        writer.WriteTextString("schema_version");
        writer.WriteTextString(SchemaVersion);
        writer.WriteTextString("supported_versions");
        writer.WriteStartArray(SupportedVersions.Count);
        foreach (var v in SupportedVersions)
        {
            writer.WriteTextString(v);
        }
        writer.WriteEndArray();
        writer.WriteTextString("public_key");
        writer.WriteByteString(PublicKey);
        writer.WriteTextString("sent_at");
        writer.WriteUInt64(Timestamp);
        writer.WriteTextString("hello_sig");
        writer.WriteByteString(Signature);
        writer.WriteEndMap();
        return CborEnvelope.CloseEnvelope(writer);
    }

    public static HelloMessage FromCbor(ReadOnlySpan<byte> cbor)
    {
        var reader = CborEnvelope.OpenBody(cbor, MessageTypes.Hello);
        var count = reader.ReadStartMap();
        if (count != 6)
        {
            throw new CborContentException(
                $"HELLO body must have 6 fields (got {count}).");
        }

        byte[]? nodeId = null;
        string? schemaVersion = null;
        List<string>? supportedVersions = null;
        byte[]? publicKey = null;
        ulong? sentAt = null;
        byte[]? signature = null;

        for (var i = 0; i < 6; i++)
        {
            var key = reader.ReadTextString();
            switch (key)
            {
                case "node_id":
                    nodeId = reader.ReadByteString();
                    break;
                case "schema_version":
                    schemaVersion = reader.ReadTextString();
                    break;
                case "supported_versions":
                    var arrCount = reader.ReadStartArray();
                    supportedVersions = new List<string>(arrCount ?? 0);
                    while (reader.PeekState() != CborReaderState.EndArray)
                    {
                        supportedVersions.Add(reader.ReadTextString());
                    }
                    reader.ReadEndArray();
                    break;
                case "public_key":
                    publicKey = reader.ReadByteString();
                    break;
                case "sent_at":
                    sentAt = reader.ReadUInt64();
                    break;
                case "hello_sig":
                    signature = reader.ReadByteString();
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }
        reader.ReadEndMap();

        if (nodeId is null || schemaVersion is null || supportedVersions is null
            || publicKey is null || sentAt is null || signature is null)
        {
            throw new CborContentException("HELLO body missing required field(s).");
        }

        return new HelloMessage(nodeId, schemaVersion, supportedVersions, publicKey, sentAt.Value, signature);
    }
}

/// <summary>
/// A bucket-subscription request carried inside <see cref="CapabilityNegMessage"/>.
/// Sync-daemon-protocol §3.2.
/// </summary>
public sealed record BucketRequest(string BucketName, AttestationBundle Attestation);

/// <summary>
/// Sync-daemon-protocol §3.2 CAPABILITY_NEG. Sent by initiator after both HELLOs.
/// </summary>
public sealed record CapabilityNegMessage(
    IReadOnlyList<string> ProposedStreams,
    IReadOnlyList<string> CpLeases,
    IReadOnlyList<BucketRequest> BucketSubscriptions)
{
    public byte[] ToCbor()
    {
        var writer = CborEnvelope.OpenEnvelope(MessageTypes.CapabilityNeg);
        writer.WriteStartMap(3);
        writer.WriteTextString("crdt_streams");
        writer.WriteStartArray(ProposedStreams.Count);
        foreach (var s in ProposedStreams)
        {
            writer.WriteTextString(s);
        }
        writer.WriteEndArray();
        writer.WriteTextString("cp_leases");
        writer.WriteStartArray(CpLeases.Count);
        foreach (var c in CpLeases)
        {
            writer.WriteTextString(c);
        }
        writer.WriteEndArray();
        writer.WriteTextString("bucket_subscriptions");
        writer.WriteStartArray(BucketSubscriptions.Count);
        foreach (var b in BucketSubscriptions)
        {
            writer.WriteStartMap(2);
            writer.WriteTextString("bucket_name");
            writer.WriteTextString(b.BucketName);
            writer.WriteTextString("attestation_bundle");
            writer.WriteByteString(b.Attestation.ToCbor());
            writer.WriteEndMap();
        }
        writer.WriteEndArray();
        writer.WriteEndMap();
        return CborEnvelope.CloseEnvelope(writer);
    }

    public static CapabilityNegMessage FromCbor(ReadOnlySpan<byte> cbor)
    {
        var reader = CborEnvelope.OpenBody(cbor, MessageTypes.CapabilityNeg);
        reader.ReadStartMap();

        List<string>? streams = null;
        List<string>? leases = null;
        List<BucketRequest>? buckets = null;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            switch (key)
            {
                case "crdt_streams":
                    streams = ReadStringArray(reader);
                    break;
                case "cp_leases":
                    leases = ReadStringArray(reader);
                    break;
                case "bucket_subscriptions":
                    var bcount = reader.ReadStartArray();
                    buckets = new List<BucketRequest>(bcount ?? 0);
                    while (reader.PeekState() != CborReaderState.EndArray)
                    {
                        reader.ReadStartMap();
                        string? name = null;
                        byte[]? bundle = null;
                        while (reader.PeekState() != CborReaderState.EndMap)
                        {
                            var bk = reader.ReadTextString();
                            if (bk == "bucket_name") name = reader.ReadTextString();
                            else if (bk == "attestation_bundle") bundle = reader.ReadByteString();
                            else reader.SkipValue();
                        }
                        reader.ReadEndMap();
                        if (name is null || bundle is null)
                        {
                            throw new CborContentException("BucketRequest missing required field.");
                        }
                        buckets.Add(new BucketRequest(name, AttestationBundle.FromCbor(bundle)));
                    }
                    reader.ReadEndArray();
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }
        reader.ReadEndMap();

        return new CapabilityNegMessage(
            streams ?? new List<string>(),
            leases ?? new List<string>(),
            buckets ?? new List<BucketRequest>());
    }

    private static List<string> ReadStringArray(CborReader reader)
    {
        var count = reader.ReadStartArray();
        var list = new List<string>(count ?? 0);
        while (reader.PeekState() != CborReaderState.EndArray)
        {
            list.Add(reader.ReadTextString());
        }
        reader.ReadEndArray();
        return list;
    }
}

/// <summary>
/// One entry in the <see cref="AckMessage.Rejected"/> list.
/// Reason codes per sync-daemon-protocol §5.
/// </summary>
public sealed record Rejection(string Subscription, string Reason);

/// <summary>Reason-code enumeration for <see cref="Rejection.Reason"/>. The wire-level
/// representation is the string constant; the enum is purely a consumer convenience.</summary>
public static class RejectionReasons
{
    public const string MissingAttestation = "MISSING_ATTESTATION";
    public const string ExpiredAttestation = "EXPIRED_ATTESTATION";
    public const string InvalidSignature = "INVALID_SIGNATURE";
    public const string UnsupportedStream = "UNSUPPORTED_STREAM";
    public const string QuotaExceeded = "QUOTA_EXCEEDED";
    public const string PolicyBlocked = "POLICY_BLOCKED";
}

/// <summary>
/// Sync-daemon-protocol §3.3 ACK. Sent by the receiver of CAPABILITY_NEG.
/// </summary>
public sealed record AckMessage(
    IReadOnlyList<string> GrantedSubscriptions,
    IReadOnlyList<Rejection> Rejected,
    uint TickIntervalSeconds = 30,
    uint MaxDeltasPerSecond = 1000)
{
    public byte[] ToCbor()
    {
        var writer = CborEnvelope.OpenEnvelope(MessageTypes.Ack);
        writer.WriteStartMap(4);
        writer.WriteTextString("granted_subscriptions");
        writer.WriteStartArray(GrantedSubscriptions.Count);
        foreach (var s in GrantedSubscriptions)
        {
            writer.WriteTextString(s);
        }
        writer.WriteEndArray();
        writer.WriteTextString("rejected");
        writer.WriteStartArray(Rejected.Count);
        foreach (var r in Rejected)
        {
            writer.WriteStartMap(2);
            writer.WriteTextString("subscription");
            writer.WriteTextString(r.Subscription);
            writer.WriteTextString("reason");
            writer.WriteTextString(r.Reason);
            writer.WriteEndMap();
        }
        writer.WriteEndArray();
        writer.WriteTextString("tick_interval_s");
        writer.WriteUInt32(TickIntervalSeconds);
        writer.WriteTextString("max_deltas_per_sec");
        writer.WriteUInt32(MaxDeltasPerSecond);
        writer.WriteEndMap();
        return CborEnvelope.CloseEnvelope(writer);
    }

    public static AckMessage FromCbor(ReadOnlySpan<byte> cbor)
    {
        var reader = CborEnvelope.OpenBody(cbor, MessageTypes.Ack);
        reader.ReadStartMap();

        List<string>? granted = null;
        List<Rejection>? rejected = null;
        uint tick = 30;
        uint maxRate = 1000;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            switch (key)
            {
                case "granted_subscriptions":
                    var gcount = reader.ReadStartArray();
                    granted = new List<string>(gcount ?? 0);
                    while (reader.PeekState() != CborReaderState.EndArray)
                    {
                        granted.Add(reader.ReadTextString());
                    }
                    reader.ReadEndArray();
                    break;
                case "rejected":
                    var rcount = reader.ReadStartArray();
                    rejected = new List<Rejection>(rcount ?? 0);
                    while (reader.PeekState() != CborReaderState.EndArray)
                    {
                        reader.ReadStartMap();
                        string? sub = null;
                        string? reason = null;
                        while (reader.PeekState() != CborReaderState.EndMap)
                        {
                            var k = reader.ReadTextString();
                            if (k == "subscription") sub = reader.ReadTextString();
                            else if (k == "reason") reason = reader.ReadTextString();
                            else reader.SkipValue();
                        }
                        reader.ReadEndMap();
                        if (sub is null || reason is null)
                        {
                            throw new CborContentException("Rejection missing required field.");
                        }
                        rejected.Add(new Rejection(sub, reason));
                    }
                    reader.ReadEndArray();
                    break;
                case "tick_interval_s":
                    tick = reader.ReadUInt32();
                    break;
                case "max_deltas_per_sec":
                    maxRate = reader.ReadUInt32();
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }
        reader.ReadEndMap();

        return new AckMessage(
            granted ?? new List<string>(),
            rejected ?? new List<Rejection>(),
            tick,
            maxRate);
    }
}

/// <summary>
/// Sync-daemon-protocol §3.4 DELTA_STREAM. Continuous stream after handshake.
/// </summary>
public sealed record DeltaStreamMessage(string StreamId, ulong OpSequence, byte[] CrdtOps)
{
    public byte[] ToCbor()
    {
        var writer = CborEnvelope.OpenEnvelope(MessageTypes.DeltaStream);
        writer.WriteStartMap(3);
        writer.WriteTextString("stream_id");
        writer.WriteTextString(StreamId);
        writer.WriteTextString("op_sequence");
        writer.WriteUInt64(OpSequence);
        writer.WriteTextString("crdt_ops");
        writer.WriteByteString(CrdtOps);
        writer.WriteEndMap();
        return CborEnvelope.CloseEnvelope(writer);
    }

    public static DeltaStreamMessage FromCbor(ReadOnlySpan<byte> cbor)
    {
        var reader = CborEnvelope.OpenBody(cbor, MessageTypes.DeltaStream);
        reader.ReadStartMap();

        string? streamId = null;
        ulong? opSeq = null;
        byte[]? crdtOps = null;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            switch (key)
            {
                case "stream_id":
                    streamId = reader.ReadTextString();
                    break;
                case "op_sequence":
                    opSeq = reader.ReadUInt64();
                    break;
                case "crdt_ops":
                    crdtOps = reader.ReadByteString();
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }
        reader.ReadEndMap();

        if (streamId is null || opSeq is null || crdtOps is null)
        {
            throw new CborContentException("DELTA_STREAM body missing required field.");
        }
        return new DeltaStreamMessage(streamId, opSeq.Value, crdtOps);
    }
}

/// <summary>Sync-daemon-protocol §3.5 LEASE_REQUEST.</summary>
public sealed record LeaseRequestMessage(
    byte[] LeaseId,
    string ResourceId,
    string LeaseClass,
    uint RequestedDurationSeconds,
    byte[] RequesterNodeId)
{
    public byte[] ToCbor()
    {
        var writer = CborEnvelope.OpenEnvelope(MessageTypes.LeaseRequest);
        writer.WriteStartMap(5);
        writer.WriteTextString("lease_id");
        writer.WriteByteString(LeaseId);
        writer.WriteTextString("resource_id");
        writer.WriteTextString(ResourceId);
        writer.WriteTextString("lease_class");
        writer.WriteTextString(LeaseClass);
        writer.WriteTextString("requested_duration_s");
        writer.WriteUInt32(RequestedDurationSeconds);
        writer.WriteTextString("requester_node_id");
        writer.WriteByteString(RequesterNodeId);
        writer.WriteEndMap();
        return CborEnvelope.CloseEnvelope(writer);
    }

    public static LeaseRequestMessage FromCbor(ReadOnlySpan<byte> cbor)
    {
        var reader = CborEnvelope.OpenBody(cbor, MessageTypes.LeaseRequest);
        reader.ReadStartMap();

        byte[]? leaseId = null;
        string? resource = null;
        string? cls = null;
        uint? duration = null;
        byte[]? requester = null;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            switch (key)
            {
                case "lease_id": leaseId = reader.ReadByteString(); break;
                case "resource_id": resource = reader.ReadTextString(); break;
                case "lease_class": cls = reader.ReadTextString(); break;
                case "requested_duration_s": duration = reader.ReadUInt32(); break;
                case "requester_node_id": requester = reader.ReadByteString(); break;
                default: reader.SkipValue(); break;
            }
        }
        reader.ReadEndMap();

        if (leaseId is null || resource is null || cls is null || duration is null || requester is null)
        {
            throw new CborContentException("LEASE_REQUEST body missing required field.");
        }
        return new LeaseRequestMessage(leaseId, resource, cls, duration.Value, requester);
    }
}

/// <summary>Sync-daemon-protocol §3.5 LEASE_GRANT.</summary>
public sealed record LeaseGrantMessage(
    byte[] LeaseId,
    uint GrantedDurationSeconds,
    ulong GrantedAt,
    byte[] GrantorNodeId)
{
    public byte[] ToCbor()
    {
        var writer = CborEnvelope.OpenEnvelope(MessageTypes.LeaseGrant);
        writer.WriteStartMap(4);
        writer.WriteTextString("lease_id");
        writer.WriteByteString(LeaseId);
        writer.WriteTextString("granted_duration_s");
        writer.WriteUInt32(GrantedDurationSeconds);
        writer.WriteTextString("granted_at");
        writer.WriteUInt64(GrantedAt);
        writer.WriteTextString("grantor_node_id");
        writer.WriteByteString(GrantorNodeId);
        writer.WriteEndMap();
        return CborEnvelope.CloseEnvelope(writer);
    }

    public static LeaseGrantMessage FromCbor(ReadOnlySpan<byte> cbor)
    {
        var reader = CborEnvelope.OpenBody(cbor, MessageTypes.LeaseGrant);
        reader.ReadStartMap();

        byte[]? leaseId = null;
        uint? duration = null;
        ulong? grantedAt = null;
        byte[]? grantor = null;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            switch (key)
            {
                case "lease_id": leaseId = reader.ReadByteString(); break;
                case "granted_duration_s": duration = reader.ReadUInt32(); break;
                case "granted_at": grantedAt = reader.ReadUInt64(); break;
                case "grantor_node_id": grantor = reader.ReadByteString(); break;
                default: reader.SkipValue(); break;
            }
        }
        reader.ReadEndMap();

        if (leaseId is null || duration is null || grantedAt is null || grantor is null)
        {
            throw new CborContentException("LEASE_GRANT body missing required field.");
        }
        return new LeaseGrantMessage(leaseId, duration.Value, grantedAt.Value, grantor);
    }
}

/// <summary>Sync-daemon-protocol §3.5 LEASE_RELEASE.</summary>
public sealed record LeaseReleaseMessage(byte[] LeaseId, ulong ReleasedAt)
{
    public byte[] ToCbor()
    {
        var writer = CborEnvelope.OpenEnvelope(MessageTypes.LeaseRelease);
        writer.WriteStartMap(2);
        writer.WriteTextString("lease_id");
        writer.WriteByteString(LeaseId);
        writer.WriteTextString("released_at");
        writer.WriteUInt64(ReleasedAt);
        writer.WriteEndMap();
        return CborEnvelope.CloseEnvelope(writer);
    }

    public static LeaseReleaseMessage FromCbor(ReadOnlySpan<byte> cbor)
    {
        var reader = CborEnvelope.OpenBody(cbor, MessageTypes.LeaseRelease);
        reader.ReadStartMap();

        byte[]? leaseId = null;
        ulong? releasedAt = null;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            switch (key)
            {
                case "lease_id": leaseId = reader.ReadByteString(); break;
                case "released_at": releasedAt = reader.ReadUInt64(); break;
                default: reader.SkipValue(); break;
            }
        }
        reader.ReadEndMap();

        if (leaseId is null || releasedAt is null)
        {
            throw new CborContentException("LEASE_RELEASE body missing required field.");
        }
        return new LeaseReleaseMessage(leaseId, releasedAt.Value);
    }
}

/// <summary>Sync-daemon-protocol §3.5 LEASE_DENIED.</summary>
public sealed record LeaseDeniedMessage(byte[] LeaseId, string Reason, byte[]? HeldBy = null)
{
    public byte[] ToCbor()
    {
        var writer = CborEnvelope.OpenEnvelope(MessageTypes.LeaseDenied);
        writer.WriteStartMap(HeldBy is null ? 2 : 3);
        writer.WriteTextString("lease_id");
        writer.WriteByteString(LeaseId);
        writer.WriteTextString("reason");
        writer.WriteTextString(Reason);
        if (HeldBy is not null)
        {
            writer.WriteTextString("held_by");
            writer.WriteByteString(HeldBy);
        }
        writer.WriteEndMap();
        return CborEnvelope.CloseEnvelope(writer);
    }

    public static LeaseDeniedMessage FromCbor(ReadOnlySpan<byte> cbor)
    {
        var reader = CborEnvelope.OpenBody(cbor, MessageTypes.LeaseDenied);
        reader.ReadStartMap();

        byte[]? leaseId = null;
        string? reason = null;
        byte[]? heldBy = null;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            switch (key)
            {
                case "lease_id": leaseId = reader.ReadByteString(); break;
                case "reason": reason = reader.ReadTextString(); break;
                case "held_by": heldBy = reader.ReadByteString(); break;
                default: reader.SkipValue(); break;
            }
        }
        reader.ReadEndMap();

        if (leaseId is null || reason is null)
        {
            throw new CborContentException("LEASE_DENIED body missing required field.");
        }
        return new LeaseDeniedMessage(leaseId, reason, heldBy);
    }
}

/// <summary>
/// Membership delta carried inside <see cref="GossipPingMessage"/>.
/// Sync-daemon-protocol §3.6.
/// </summary>
public sealed record MembershipDelta(IReadOnlyList<byte[]> Added, IReadOnlyList<byte[]> Removed);

/// <summary>
/// Sync-daemon-protocol §3.6 GOSSIP_PING. Emitted every tick
/// (default 30 s, configurable via ACK).
/// </summary>
public sealed record GossipPingMessage(
    IReadOnlyDictionary<string, ulong> VectorClock,
    MembershipDelta PeerMembershipDelta,
    ulong MonotonicNonce = 0)
{
    public byte[] ToCbor()
    {
        var writer = CborEnvelope.OpenEnvelope(MessageTypes.GossipPing);
        writer.WriteStartMap(3);

        writer.WriteTextString("vector_clock");
        writer.WriteStartMap(VectorClock.Count);
        // CBOR canonical-mode sorts map keys at encode time — emit in insertion
        // order and trust the writer to canonicalize. VectorClock keys are the
        // hex-encoded node_id strings rather than raw bstr for deterministic
        // textual debugging; see VectorClock.cs for the symmetric decoder.
        foreach (var kvp in VectorClock)
        {
            writer.WriteTextString(kvp.Key);
            writer.WriteUInt64(kvp.Value);
        }
        writer.WriteEndMap();

        writer.WriteTextString("membership_delta");
        writer.WriteStartMap(2);
        writer.WriteTextString("added");
        writer.WriteStartArray(PeerMembershipDelta.Added.Count);
        foreach (var id in PeerMembershipDelta.Added)
        {
            writer.WriteByteString(id);
        }
        writer.WriteEndArray();
        writer.WriteTextString("removed");
        writer.WriteStartArray(PeerMembershipDelta.Removed.Count);
        foreach (var id in PeerMembershipDelta.Removed)
        {
            writer.WriteByteString(id);
        }
        writer.WriteEndArray();
        writer.WriteEndMap();

        writer.WriteTextString("monotonic_nonce");
        writer.WriteUInt64(MonotonicNonce);

        writer.WriteEndMap();
        return CborEnvelope.CloseEnvelope(writer);
    }

    public static GossipPingMessage FromCbor(ReadOnlySpan<byte> cbor)
    {
        var reader = CborEnvelope.OpenBody(cbor, MessageTypes.GossipPing);
        reader.ReadStartMap();

        Dictionary<string, ulong>? clock = null;
        MembershipDelta? delta = null;
        ulong nonce = 0;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            switch (key)
            {
                case "vector_clock":
                    var vcCount = reader.ReadStartMap();
                    clock = new Dictionary<string, ulong>(vcCount ?? 0);
                    while (reader.PeekState() != CborReaderState.EndMap)
                    {
                        var k = reader.ReadTextString();
                        var v = reader.ReadUInt64();
                        clock[k] = v;
                    }
                    reader.ReadEndMap();
                    break;
                case "membership_delta":
                    reader.ReadStartMap();
                    List<byte[]>? added = null;
                    List<byte[]>? removed = null;
                    while (reader.PeekState() != CborReaderState.EndMap)
                    {
                        var mk = reader.ReadTextString();
                        if (mk == "added")
                        {
                            var c = reader.ReadStartArray();
                            added = new List<byte[]>(c ?? 0);
                            while (reader.PeekState() != CborReaderState.EndArray)
                            {
                                added.Add(reader.ReadByteString());
                            }
                            reader.ReadEndArray();
                        }
                        else if (mk == "removed")
                        {
                            var c = reader.ReadStartArray();
                            removed = new List<byte[]>(c ?? 0);
                            while (reader.PeekState() != CborReaderState.EndArray)
                            {
                                removed.Add(reader.ReadByteString());
                            }
                            reader.ReadEndArray();
                        }
                        else
                        {
                            reader.SkipValue();
                        }
                    }
                    reader.ReadEndMap();
                    delta = new MembershipDelta(added ?? new List<byte[]>(), removed ?? new List<byte[]>());
                    break;
                case "monotonic_nonce":
                    nonce = reader.ReadUInt64();
                    break;
                default:
                    reader.SkipValue();
                    break;
            }
        }
        reader.ReadEndMap();

        if (clock is null || delta is null)
        {
            throw new CborContentException("GOSSIP_PING body missing required field.");
        }
        return new GossipPingMessage(clock, delta, nonce);
    }
}

/// <summary>
/// Known error codes for <see cref="ErrorMessage.Code"/>. The wire-level
/// representation is the string constant; the enum is purely a consumer
/// convenience and keeps the wire shape stable even as the enum grows.
/// </summary>
public enum ErrorCode
{
    Unknown = 0,
    SchemaVersionIncompatible,
    HelloSignatureInvalid,
    HelloTimestampStale,
    NonceReplay,
    RateLimitExceeded,
    QuorumUnavailable,
    LeaseHeldByOther,
    ClassNotGranted,
    ExpiredAttestation,
}

/// <summary>
/// Mapping between <see cref="ErrorCode"/> and its wire string representation.
/// </summary>
public static class ErrorCodes
{
    public static string ToWire(ErrorCode code) => code switch
    {
        ErrorCode.SchemaVersionIncompatible => "SCHEMA_VERSION_INCOMPATIBLE",
        ErrorCode.HelloSignatureInvalid => "HELLO_SIGNATURE_INVALID",
        ErrorCode.HelloTimestampStale => "HELLO_TIMESTAMP_STALE",
        ErrorCode.NonceReplay => "NONCE_REPLAY",
        ErrorCode.RateLimitExceeded => "RATE_LIMIT_EXCEEDED",
        ErrorCode.QuorumUnavailable => "QUORUM_UNAVAILABLE",
        ErrorCode.LeaseHeldByOther => "LEASE_HELD_BY_OTHER",
        ErrorCode.ClassNotGranted => "CLASS_NOT_GRANTED",
        ErrorCode.ExpiredAttestation => "EXPIRED_ATTESTATION",
        _ => "UNKNOWN",
    };

    public static ErrorCode FromWire(string wire) => wire switch
    {
        "SCHEMA_VERSION_INCOMPATIBLE" => ErrorCode.SchemaVersionIncompatible,
        "HELLO_SIGNATURE_INVALID" => ErrorCode.HelloSignatureInvalid,
        "HELLO_TIMESTAMP_STALE" => ErrorCode.HelloTimestampStale,
        "NONCE_REPLAY" => ErrorCode.NonceReplay,
        "RATE_LIMIT_EXCEEDED" => ErrorCode.RateLimitExceeded,
        "QUORUM_UNAVAILABLE" => ErrorCode.QuorumUnavailable,
        "LEASE_HELD_BY_OTHER" => ErrorCode.LeaseHeldByOther,
        "CLASS_NOT_GRANTED" => ErrorCode.ClassNotGranted,
        "EXPIRED_ATTESTATION" => ErrorCode.ExpiredAttestation,
        _ => ErrorCode.Unknown,
    };
}

/// <summary>Sync-daemon-protocol §3.7 ERROR.</summary>
public sealed record ErrorMessage(ErrorCode Code, string Message, bool Recoverable)
{
    public byte[] ToCbor()
    {
        var writer = CborEnvelope.OpenEnvelope(MessageTypes.Error);
        writer.WriteStartMap(3);
        writer.WriteTextString("code");
        writer.WriteTextString(ErrorCodes.ToWire(Code));
        writer.WriteTextString("message");
        writer.WriteTextString(Message);
        writer.WriteTextString("recoverable");
        writer.WriteBoolean(Recoverable);
        writer.WriteEndMap();
        return CborEnvelope.CloseEnvelope(writer);
    }

    public static ErrorMessage FromCbor(ReadOnlySpan<byte> cbor)
    {
        var reader = CborEnvelope.OpenBody(cbor, MessageTypes.Error);
        reader.ReadStartMap();

        string? code = null;
        string? message = null;
        bool? recoverable = null;

        while (reader.PeekState() != CborReaderState.EndMap)
        {
            var key = reader.ReadTextString();
            switch (key)
            {
                case "code": code = reader.ReadTextString(); break;
                case "message": message = reader.ReadTextString(); break;
                case "recoverable": recoverable = reader.ReadBoolean(); break;
                default: reader.SkipValue(); break;
            }
        }
        reader.ReadEndMap();

        if (code is null || message is null || recoverable is null)
        {
            throw new CborContentException("ERROR body missing required field.");
        }
        return new ErrorMessage(ErrorCodes.FromWire(code), message, recoverable.Value);
    }
}

/// <summary>
/// Dispatch helper: decode an arbitrary CBOR envelope to the corresponding
/// message record. Returned as <c>object</c> — callers pattern-match or cast.
/// </summary>
public static class SyncMessageCodec
{
    public static object Decode(ReadOnlySpan<byte> cbor)
    {
        var type = CborEnvelope.PeekType(cbor);
        return type switch
        {
            MessageTypes.Hello => HelloMessage.FromCbor(cbor),
            MessageTypes.CapabilityNeg => CapabilityNegMessage.FromCbor(cbor),
            MessageTypes.Ack => AckMessage.FromCbor(cbor),
            MessageTypes.DeltaStream => DeltaStreamMessage.FromCbor(cbor),
            MessageTypes.LeaseRequest => LeaseRequestMessage.FromCbor(cbor),
            MessageTypes.LeaseGrant => LeaseGrantMessage.FromCbor(cbor),
            MessageTypes.LeaseRelease => LeaseReleaseMessage.FromCbor(cbor),
            MessageTypes.LeaseDenied => LeaseDeniedMessage.FromCbor(cbor),
            MessageTypes.GossipPing => GossipPingMessage.FromCbor(cbor),
            MessageTypes.Error => ErrorMessage.FromCbor(cbor),
            _ => throw new CborContentException($"Unknown message type '{type}'."),
        };
    }

    /// <summary>
    /// Encode any known message record to its canonical CBOR bytes. Throws
    /// <see cref="ArgumentException"/> if <paramref name="message"/> is not one
    /// of the known message types.
    /// </summary>
    public static byte[] Encode(object message)
    {
        return message switch
        {
            HelloMessage m => m.ToCbor(),
            CapabilityNegMessage m => m.ToCbor(),
            AckMessage m => m.ToCbor(),
            DeltaStreamMessage m => m.ToCbor(),
            LeaseRequestMessage m => m.ToCbor(),
            LeaseGrantMessage m => m.ToCbor(),
            LeaseReleaseMessage m => m.ToCbor(),
            LeaseDeniedMessage m => m.ToCbor(),
            GossipPingMessage m => m.ToCbor(),
            ErrorMessage m => m.ToCbor(),
            _ => throw new ArgumentException($"Unknown message type {message.GetType().FullName}.", nameof(message)),
        };
    }
}
