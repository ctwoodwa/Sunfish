namespace Sunfish.Kernel.Sync.Tests;

/// <summary>
/// Coverage for the Ed25519 sign/verify + ±30 s replay-window hardening
/// added to <see cref="HandshakeProtocol"/>. Each case drives the
/// handshake end-to-end on the in-memory transport and asserts on the
/// wire-observable outcome (success record, ERROR frame, or thrown
/// exception) rather than inspecting private state.
/// </summary>
public class HandshakeSignatureTests
{
    // ------------------------------------------------------------------
    // Helpers — a tiny paired-connection driver so each test is a few
    // lines rather than boilerplate-heavy.
    // ------------------------------------------------------------------

    private static async Task<(ISyncDaemonConnection client, ISyncDaemonConnection server)>
        CreatePairAsync()
    {
        var endpoint = $"handshake-{Guid.NewGuid():N}";
        var serverTransport = new InMemorySyncDaemonTransport(endpoint);
        var clientTransport = new InMemorySyncDaemonTransport();
        var acceptTask = Task.Run(async () =>
        {
            await foreach (var conn in serverTransport.ListenAsync(CancellationToken.None))
            {
                return conn;
            }
            throw new InvalidOperationException("No inbound connection.");
        });
        var client = await clientTransport.ConnectAsync(endpoint, CancellationToken.None);
        var server = await acceptTask;
        // Transports are kept alive by the connections; they can be disposed
        // by the test's finally via the connection disposal chain.
        return (client, server);
    }

    private static AckMessage GrantEverythingPolicy(CapabilityNegMessage p) =>
        new(GrantedSubscriptions: p.ProposedStreams, Rejected: Array.Empty<Rejection>());

    // ------------------------------------------------------------------
    // Positive cases
    // ------------------------------------------------------------------

    [Fact]
    public async Task Valid_Signed_HELLO_Completes_Handshake()
    {
        var (client, server) = await CreatePairAsync();
        await using var _c = client;
        await using var _s = server;

        var signerA = TestIdentityFactory.NewSigner();
        var signerB = TestIdentityFactory.NewSigner();
        var idA = TestIdentityFactory.NewLocalIdentity(signerA);
        var idB = TestIdentityFactory.NewLocalIdentity(signerB);

        var initTask = HandshakeProtocol.InitiateAsync(client, idA, CancellationToken.None);
        var respTask = HandshakeProtocol.RespondAsync(server, idB, GrantEverythingPolicy, CancellationToken.None);

        var initResult = await initTask;
        var respResult = await respTask;

        Assert.Equal(HandshakeProtocol.DefaultSchemaVersion, initResult.AgreedSchemaVersion);
        Assert.Equal(HandshakeProtocol.DefaultSchemaVersion, respResult.AgreedSchemaVersion);
        Assert.Equal(idB.PublicKey, initResult.PeerPublicKey);
        Assert.Equal(idA.PublicKey, respResult.PeerPublicKey);
    }

    [Fact]
    public async Task Two_Consecutive_Signed_HELLOs_From_Same_Node_Both_Accepted()
    {
        // No nonce constraint applies to HELLO — only GOSSIP_PING carries
        // the monotonic_nonce. Back-to-back HELLOs from the same identity
        // (freshly-signed, current-timestamp each time) must both succeed.
        var signerA = TestIdentityFactory.NewSigner();
        var idA = TestIdentityFactory.NewLocalIdentity(signerA);
        var signerB = TestIdentityFactory.NewSigner();
        var idB = TestIdentityFactory.NewLocalIdentity(signerB);

        for (var i = 0; i < 2; i++)
        {
            var (client, server) = await CreatePairAsync();
            await using var _c = client;
            await using var _s = server;

            var initTask = HandshakeProtocol.InitiateAsync(client, idA, CancellationToken.None);
            var respTask = HandshakeProtocol.RespondAsync(server, idB, GrantEverythingPolicy, CancellationToken.None);

            var initResult = await initTask;
            var respResult = await respTask;
            Assert.Equal(HandshakeProtocol.DefaultSchemaVersion, initResult.AgreedSchemaVersion);
            Assert.Equal(HandshakeProtocol.DefaultSchemaVersion, respResult.AgreedSchemaVersion);
        }
    }

    [Fact]
    public void KeyPair_Generation_Roundtrip_Produces_Working_Signer()
    {
        var signer = new Ed25519Signer();
        var (publicKey, privateKey) = signer.GenerateKeyPair();

        Assert.Equal(signer.PublicKeyLength, publicKey.Length);
        Assert.Equal(signer.PrivateKeyLength, privateKey.Length);

        var message = new byte[] { 0x01, 0x02, 0x03 };
        var sig = signer.Sign(message, privateKey);
        Assert.Equal(signer.SignatureLength, sig.Length);
        Assert.True(signer.Verify(message, sig, publicKey));
    }

    // ------------------------------------------------------------------
    // Negative cases — signature failure
    // ------------------------------------------------------------------

    [Fact]
    public async Task Tampered_HELLO_Payload_Verification_Fails_And_ERROR_Sent()
    {
        // Initiator builds a valid signed HELLO then tampers the NodeId
        // on the record before send. The responder rebuilds the signing
        // payload from the fields it received and verify fails.
        var signerA = TestIdentityFactory.NewSigner();
        var idA = TestIdentityFactory.NewLocalIdentity(signerA);
        var signerB = TestIdentityFactory.NewSigner();
        var idB = TestIdentityFactory.NewLocalIdentity(signerB);

        var (client, server) = await CreatePairAsync();
        await using var _c = client;
        await using var _s = server;

        // Hand-build a signed HELLO, then mutate a field so the signature
        // no longer matches.
        var good = HandshakeProtocol.BuildHello(idA);
        var tampered = good with { NodeId = new byte[16] /* all zeros ≠ real node id */ };
        await client.SendAsync(tampered, CancellationToken.None);

        // Responder consumes the tampered HELLO → sends ERROR + throws.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => HandshakeProtocol.RespondAsync(server, idB, GrantEverythingPolicy, CancellationToken.None));

        var reply = await client.ReceiveAsync(CancellationToken.None);
        var err = Assert.IsType<ErrorMessage>(reply);
        Assert.Equal(ErrorCode.HelloSignatureInvalid, err.Code);
        Assert.False(err.Recoverable);
    }

    [Fact]
    public async Task Mismatched_PublicKey_Verification_Fails()
    {
        // Peer claims public-key P, signs with a different private key. The
        // signature won't verify against P → HelloSignatureInvalid.
        var signerA = TestIdentityFactory.NewSigner();
        var idA = TestIdentityFactory.NewLocalIdentity(signerA);
        var bogusKeys = signerA.GenerateKeyPair();
        var badIdA = new LocalIdentity(
            NodeId: idA.NodeId,
            PublicKey: bogusKeys.PublicKey,       // claim this public key
            Signer: signerA,
            PrivateKey: idA.PrivateKey,           // but sign with the other private key
            SchemaVersion: HandshakeProtocol.DefaultSchemaVersion,
            SupportedVersions: HandshakeProtocol.DefaultSupportedVersions);

        var signerB = TestIdentityFactory.NewSigner();
        var idB = TestIdentityFactory.NewLocalIdentity(signerB);

        var (client, server) = await CreatePairAsync();
        await using var _c = client;
        await using var _s = server;

        var hello = HandshakeProtocol.BuildHello(badIdA);
        await client.SendAsync(hello, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => HandshakeProtocol.RespondAsync(server, idB, GrantEverythingPolicy, CancellationToken.None));
        var reply = await client.ReceiveAsync(CancellationToken.None);
        var err = Assert.IsType<ErrorMessage>(reply);
        Assert.Equal(ErrorCode.HelloSignatureInvalid, err.Code);
    }

    // ------------------------------------------------------------------
    // Negative cases — timestamp replay window
    // ------------------------------------------------------------------

    [Fact]
    public async Task Expired_Timestamp_40s_Past_Triggers_TimestampStale_ERROR()
    {
        var signerA = TestIdentityFactory.NewSigner();
        var idA = TestIdentityFactory.NewLocalIdentity(signerA);
        var signerB = TestIdentityFactory.NewSigner();
        var idB = TestIdentityFactory.NewLocalIdentity(signerB);

        var (client, server) = await CreatePairAsync();
        await using var _c = client;
        await using var _s = server;

        // Build a HELLO whose sent_at is 40 s in the past (outside ±30 s).
        // Signature must still be valid over the backdated timestamp so
        // we know the receiver fails the window check, not the sig check.
        var pastTs = (ulong)DateTimeOffset.UtcNow.AddSeconds(-40).ToUnixTimeSeconds();
        var payload = HandshakeProtocol.BuildSigningPayload(
            idA.NodeId, idA.SchemaVersion, idA.PublicKey, pastTs);
        var sig = signerA.Sign(payload, idA.PrivateKey!);
        var stale = new HelloMessage(
            NodeId: idA.NodeId,
            SchemaVersion: idA.SchemaVersion,
            SupportedVersions: idA.SupportedVersions,
            PublicKey: idA.PublicKey,
            Timestamp: pastTs,
            Signature: sig);
        await client.SendAsync(stale, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => HandshakeProtocol.RespondAsync(server, idB, GrantEverythingPolicy, CancellationToken.None));
        var reply = await client.ReceiveAsync(CancellationToken.None);
        var err = Assert.IsType<ErrorMessage>(reply);
        Assert.Equal(ErrorCode.HelloTimestampStale, err.Code);
        Assert.False(err.Recoverable);
    }

    [Fact]
    public async Task Future_Timestamp_40s_Ahead_Triggers_TimestampStale_ERROR()
    {
        var signerA = TestIdentityFactory.NewSigner();
        var idA = TestIdentityFactory.NewLocalIdentity(signerA);
        var signerB = TestIdentityFactory.NewSigner();
        var idB = TestIdentityFactory.NewLocalIdentity(signerB);

        var (client, server) = await CreatePairAsync();
        await using var _c = client;
        await using var _s = server;

        var futureTs = (ulong)DateTimeOffset.UtcNow.AddSeconds(40).ToUnixTimeSeconds();
        var payload = HandshakeProtocol.BuildSigningPayload(
            idA.NodeId, idA.SchemaVersion, idA.PublicKey, futureTs);
        var sig = signerA.Sign(payload, idA.PrivateKey!);
        var future = new HelloMessage(
            NodeId: idA.NodeId,
            SchemaVersion: idA.SchemaVersion,
            SupportedVersions: idA.SupportedVersions,
            PublicKey: idA.PublicKey,
            Timestamp: futureTs,
            Signature: sig);
        await client.SendAsync(future, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => HandshakeProtocol.RespondAsync(server, idB, GrantEverythingPolicy, CancellationToken.None));
        var reply = await client.ReceiveAsync(CancellationToken.None);
        var err = Assert.IsType<ErrorMessage>(reply);
        Assert.Equal(ErrorCode.HelloTimestampStale, err.Code);
    }

    // ------------------------------------------------------------------
    // Payload-builder round-trip — deterministic signing bytes.
    // ------------------------------------------------------------------

    [Fact]
    public void Signing_Payload_Is_Deterministic_For_Same_Fields()
    {
        var nodeId = new byte[] { 1, 2, 3, 4, 5 };
        var publicKey = new byte[32];
        for (var i = 0; i < publicKey.Length; i++) publicKey[i] = (byte)i;

        var a = HandshakeProtocol.BuildSigningPayload(nodeId, "1.0.0", publicKey, 1745280000);
        var b = HandshakeProtocol.BuildSigningPayload(nodeId, "1.0.0", publicKey, 1745280000);
        Assert.Equal(a, b);

        var c = HandshakeProtocol.BuildSigningPayload(nodeId, "1.0.0", publicKey, 1745280001);
        Assert.NotEqual(a, c);
    }
}
