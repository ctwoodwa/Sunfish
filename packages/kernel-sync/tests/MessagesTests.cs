using Sunfish.Kernel.Security.Attestation;

namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Coverage for <see cref="SyncMessageCodec"/> and each
/// <c>*Message.ToCbor()</c> / <c>FromCbor()</c> pair. The spec §10 test
/// vectors are exercised by roundtripping them — we do not assert byte
/// equality because canonical CBOR map-key ordering is library-specific
/// and the spec explicitly permits deterministic-or-not.
/// </summary>
public class MessagesTests
{
    private static byte[] NodeId(byte seed)
    {
        var id = new byte[16];
        for (var i = 0; i < 16; i++) id[i] = (byte)(seed + i);
        return id;
    }

    [Fact]
    public void HelloMessage_Roundtrip_PreservesAllFields()
    {
        var original = new HelloMessage(
            NodeId: NodeId(0x10),
            SchemaVersion: "1.0.0",
            SupportedVersions: new[] { "1.0.0", "1.1.0" },
            PublicKey: new byte[32],
            Timestamp: 1745280000,
            Signature: new byte[64]);

        var bytes = original.ToCbor();
        var decoded = HelloMessage.FromCbor(bytes);

        Assert.Equal(original.NodeId, decoded.NodeId);
        Assert.Equal(original.SchemaVersion, decoded.SchemaVersion);
        Assert.Equal(original.SupportedVersions, decoded.SupportedVersions);
        Assert.Equal(original.PublicKey, decoded.PublicKey);
        Assert.Equal(original.Timestamp, decoded.Timestamp);
        Assert.Equal(original.Signature, decoded.Signature);
    }

    [Fact]
    public void CapabilityNegMessage_Roundtrip_PreservesAttestationBundleBytes()
    {
        var ra = new RoleAttestation(
            TeamId: new byte[] { 1, 2, 3 },
            SubjectPublicKey: new byte[] { 4, 5, 6 },
            Role: "accountant",
            IssuedAt: DateTimeOffset.FromUnixTimeSeconds(1700000000),
            ExpiresAt: DateTimeOffset.FromUnixTimeSeconds(1800000000),
            IssuerPublicKey: new byte[] { 7, 8, 9 },
            Signature: new byte[] { 10, 11, 12 });
        var bundle = new AttestationBundle(new[] { ra });

        var original = new CapabilityNegMessage(
            ProposedStreams: new[] { "team_core", "financial_records" },
            CpLeases: new[] { "invoice_post" },
            BucketSubscriptions: new[]
            {
                new BucketRequest("team_core", bundle),
                new BucketRequest("financial_records", bundle),
            });

        var bytes = original.ToCbor();
        var decoded = CapabilityNegMessage.FromCbor(bytes);

        Assert.Equal(original.ProposedStreams, decoded.ProposedStreams);
        Assert.Equal(original.CpLeases, decoded.CpLeases);
        Assert.Equal(2, decoded.BucketSubscriptions.Count);
        Assert.Equal("team_core", decoded.BucketSubscriptions[0].BucketName);
        Assert.Single(decoded.BucketSubscriptions[0].Attestation.Attestations);
        Assert.Equal("accountant", decoded.BucketSubscriptions[0].Attestation.Attestations[0].Role);
    }

    [Fact]
    public void AckMessage_Roundtrip_PreservesGrantsAndRejections()
    {
        var original = new AckMessage(
            GrantedSubscriptions: new[] { "team_core" },
            Rejected: new[] { new Rejection("financial_records", RejectionReasons.MissingAttestation) },
            TickIntervalSeconds: 30,
            MaxDeltasPerSecond: 1000);

        var decoded = AckMessage.FromCbor(original.ToCbor());

        Assert.Equal(original.GrantedSubscriptions, decoded.GrantedSubscriptions);
        Assert.Single(decoded.Rejected);
        Assert.Equal("financial_records", decoded.Rejected[0].Subscription);
        Assert.Equal("MISSING_ATTESTATION", decoded.Rejected[0].Reason);
        Assert.Equal(30u, decoded.TickIntervalSeconds);
        Assert.Equal(1000u, decoded.MaxDeltasPerSecond);
    }

    [Fact]
    public void DeltaStreamMessage_Roundtrip_PreservesOpaqueOps()
    {
        var original = new DeltaStreamMessage(
            StreamId: "team_core",
            OpSequence: 42ul,
            CrdtOps: new byte[] { 0xDE, 0xAD, 0xBE, 0xEF });

        var decoded = DeltaStreamMessage.FromCbor(original.ToCbor());

        Assert.Equal("team_core", decoded.StreamId);
        Assert.Equal(42ul, decoded.OpSequence);
        Assert.Equal(original.CrdtOps, decoded.CrdtOps);
    }

    [Fact]
    public void LeaseRequestMessage_Roundtrip_PreservesAllFields()
    {
        var original = new LeaseRequestMessage(
            LeaseId: NodeId(0x20),
            ResourceId: "invoice:4f2a",
            LeaseClass: "invoice_post",
            RequestedDurationSeconds: 30,
            RequesterNodeId: NodeId(0x30));

        var decoded = LeaseRequestMessage.FromCbor(original.ToCbor());

        Assert.Equal(original.LeaseId, decoded.LeaseId);
        Assert.Equal("invoice:4f2a", decoded.ResourceId);
        Assert.Equal("invoice_post", decoded.LeaseClass);
        Assert.Equal(30u, decoded.RequestedDurationSeconds);
        Assert.Equal(original.RequesterNodeId, decoded.RequesterNodeId);
    }

    [Fact]
    public void LeaseGrantMessage_Roundtrip_PreservesAllFields()
    {
        var original = new LeaseGrantMessage(
            LeaseId: NodeId(0x40),
            GrantedDurationSeconds: 25,
            GrantedAt: 1745280045ul,
            GrantorNodeId: NodeId(0x50));

        var decoded = LeaseGrantMessage.FromCbor(original.ToCbor());

        Assert.Equal(original.LeaseId, decoded.LeaseId);
        Assert.Equal(25u, decoded.GrantedDurationSeconds);
        Assert.Equal(1745280045ul, decoded.GrantedAt);
        Assert.Equal(original.GrantorNodeId, decoded.GrantorNodeId);
    }

    [Fact]
    public void LeaseReleaseMessage_Roundtrip_PreservesLeaseIdAndTimestamp()
    {
        var original = new LeaseReleaseMessage(
            LeaseId: NodeId(0x60),
            ReleasedAt: 1745280100ul);

        var decoded = LeaseReleaseMessage.FromCbor(original.ToCbor());

        Assert.Equal(original.LeaseId, decoded.LeaseId);
        Assert.Equal(1745280100ul, decoded.ReleasedAt);
    }

    [Fact]
    public void LeaseDeniedMessage_WithHeldBy_Roundtrips()
    {
        var original = new LeaseDeniedMessage(
            LeaseId: NodeId(0x70),
            Reason: "LEASE_HELD_BY_OTHER",
            HeldBy: NodeId(0x80));

        var decoded = LeaseDeniedMessage.FromCbor(original.ToCbor());

        Assert.Equal("LEASE_HELD_BY_OTHER", decoded.Reason);
        Assert.NotNull(decoded.HeldBy);
        Assert.Equal(original.HeldBy, decoded.HeldBy);
    }

    [Fact]
    public void LeaseDeniedMessage_WithoutHeldBy_Roundtrips()
    {
        var original = new LeaseDeniedMessage(
            LeaseId: NodeId(0x71),
            Reason: "QUORUM_UNAVAILABLE",
            HeldBy: null);

        var decoded = LeaseDeniedMessage.FromCbor(original.ToCbor());

        Assert.Equal("QUORUM_UNAVAILABLE", decoded.Reason);
        Assert.Null(decoded.HeldBy);
    }

    [Fact]
    public void GossipPingMessage_Roundtrip_PreservesVectorClockAndMembership()
    {
        var clock = new Dictionary<string, ulong>
        {
            ["nodeA"] = 10,
            ["nodeB"] = 99,
        };
        var original = new GossipPingMessage(
            VectorClock: clock,
            PeerMembershipDelta: new MembershipDelta(
                Added: new[] { NodeId(0x90) },
                Removed: new[] { NodeId(0xA0) }),
            MonotonicNonce: 12345);

        var decoded = GossipPingMessage.FromCbor(original.ToCbor());

        Assert.Equal(10ul, decoded.VectorClock["nodeA"]);
        Assert.Equal(99ul, decoded.VectorClock["nodeB"]);
        Assert.Single(decoded.PeerMembershipDelta.Added);
        Assert.Single(decoded.PeerMembershipDelta.Removed);
        Assert.Equal(12345ul, decoded.MonotonicNonce);
    }

    [Fact]
    public void ErrorMessage_Roundtrip_PreservesCodeAndRecoverableFlag()
    {
        var original = new ErrorMessage(
            Code: ErrorCode.RateLimitExceeded,
            Message: "slow down there, cowboy",
            Recoverable: true);

        var decoded = ErrorMessage.FromCbor(original.ToCbor());

        Assert.Equal(ErrorCode.RateLimitExceeded, decoded.Code);
        Assert.Equal("slow down there, cowboy", decoded.Message);
        Assert.True(decoded.Recoverable);
    }

    [Fact]
    public void SyncMessageCodec_Decode_RoutesToCorrectMessageType()
    {
        var hello = new HelloMessage(
            NodeId: NodeId(1), SchemaVersion: "1.0.0",
            SupportedVersions: new[] { "1.0.0" }, PublicKey: new byte[32],
            Timestamp: 0, Signature: new byte[64]);
        var ack = new AckMessage(
            GrantedSubscriptions: Array.Empty<string>(),
            Rejected: Array.Empty<Rejection>());

        var decodedHello = SyncMessageCodec.Decode(hello.ToCbor());
        var decodedAck = SyncMessageCodec.Decode(ack.ToCbor());

        Assert.IsType<HelloMessage>(decodedHello);
        Assert.IsType<AckMessage>(decodedAck);
    }
}
