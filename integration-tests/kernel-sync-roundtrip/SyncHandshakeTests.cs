using Sunfish.Integration.KernelSyncRoundtrip.Harness;

namespace Sunfish.Integration.KernelSyncRoundtrip;

/// <summary>
/// End-to-end handshake tests over a real <see cref="UnixSocketSyncDaemonTransport"/>.
/// Each test stands up <see cref="TwoNodeHarness"/> with the lease responder
/// disabled so the test itself owns <see cref="ISyncDaemonTransport.ListenAsync"/>
/// on each node.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real signatures.</b> Wave 2.5 closed the spec §8 hardening gap: HELLO
/// carries a real Ed25519 detached signature and both sides verify before
/// trusting handshake state. These tests therefore exercise that path end to
/// end — <see cref="Tampered_Hello_Signature_Is_Rejected"/> flips one byte of
/// the signature on the wire and asserts the responder rejects the session.
/// </para>
/// </remarks>
public class SyncHandshakeTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task Hello_CapabilityNeg_Ack_Completes_Over_Real_Socket()
    {
        using var timeoutCts = new CancellationTokenSource(TestTimeout);
        var ct = timeoutCts.Token;

        await using var harness = await TwoNodeHarness.StartAsync(ct, enableLeaseResponder: false);

        using var responderCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var responderTcs = new TaskCompletionSource<CapabilityResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var conn in harness.NodeB.Transport.ListenAsync(responderCts.Token))
                {
                    await using (conn)
                    {
                        var result = await HandshakeProtocol.RespondAsync(
                            conn,
                            harness.NodeB.BuildLocalIdentity(),
                            policy: proposal => new AckMessage(
                                GrantedSubscriptions: proposal.ProposedStreams,
                                Rejected: Array.Empty<Rejection>(),
                                TickIntervalSeconds: 5,
                                MaxDeltasPerSecond: 100),
                            responderCts.Token);
                        responderTcs.TrySetResult(result);
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                responderTcs.TrySetException(ex);
            }
        }, responderCts.Token);

        // Initiator side: A dials B, runs the full ladder.
        await using var conn = await harness.NodeA.Transport
            .ConnectAsync(harness.SharedSocketPathB, ct);

        var initiatorResult = await HandshakeProtocol.InitiateAsync(
            conn,
            harness.NodeA.BuildLocalIdentity(),
            ct);

        var responderResult = await responderTcs.Task.WaitAsync(TestTimeout, ct);
        responderCts.Cancel();

        Assert.Equal(harness.NodeB.PublicKey, initiatorResult.PeerPublicKey);
        Assert.Equal(harness.NodeA.PublicKey, responderResult.PeerPublicKey);
        Assert.Equal(HandshakeProtocol.DefaultSchemaVersion, initiatorResult.AgreedSchemaVersion);
        Assert.Equal(HandshakeProtocol.DefaultSchemaVersion, responderResult.AgreedSchemaVersion);
    }

    [Fact]
    public async Task Tampered_Hello_Signature_Is_Rejected()
    {
        // Wave 2.5 hardened the handshake with real Ed25519 verification
        // (see HandshakeProtocol xmldoc). This test constructs a valid HELLO
        // for node A, flips one byte of the Signature field, and ships it
        // directly on the wire. The responder's VerifyHelloSignature must
        // fail, and RespondAsync must throw + the responder-sent ErrorMessage
        // must carry HelloSignatureInvalid.
        using var timeoutCts = new CancellationTokenSource(TestTimeout);
        var ct = timeoutCts.Token;

        await using var harness = await TwoNodeHarness.StartAsync(ct, enableLeaseResponder: false);

        using var responderCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var responderFaultedTcs = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var conn in harness.NodeB.Transport.ListenAsync(responderCts.Token))
                {
                    await using (conn)
                    {
                        try
                        {
                            await HandshakeProtocol.RespondAsync(
                                conn,
                                harness.NodeB.BuildLocalIdentity(),
                                policy: proposal => new AckMessage(
                                    GrantedSubscriptions: proposal.ProposedStreams,
                                    Rejected: Array.Empty<Rejection>()),
                                responderCts.Token);
                            responderFaultedTcs.TrySetException(
                                new Xunit.Sdk.XunitException(
                                    "RespondAsync should have rejected the tampered HELLO."));
                        }
                        catch (Exception ex)
                        {
                            responderFaultedTcs.TrySetResult(ex);
                        }
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                responderFaultedTcs.TrySetException(ex);
            }
        }, responderCts.Token);

        // Build a HELLO for node A, sign it validly, then flip a byte.
        var localIdentity = harness.NodeA.BuildLocalIdentity();
        var ts = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signingPayload = HandshakeSigningHelper.BuildSigningPayload(
            localIdentity.NodeId, localIdentity.SchemaVersion, localIdentity.PublicKey, ts);
        var realSig = harness.NodeA.Signer.Sign(signingPayload, harness.NodeA.PrivateKey);
        var tamperedSig = (byte[])realSig.Clone();
        tamperedSig[0] ^= 0xFF; // flip one byte

        var tamperedHello = new HelloMessage(
            NodeId: localIdentity.NodeId,
            SchemaVersion: localIdentity.SchemaVersion,
            SupportedVersions: localIdentity.SupportedVersions,
            PublicKey: localIdentity.PublicKey,
            Timestamp: ts,
            Signature: tamperedSig);

        await using var conn = await harness.NodeA.Transport
            .ConnectAsync(harness.SharedSocketPathB, ct);

        // Send the tampered HELLO directly on the wire instead of going
        // through InitiateAsync (which would sign a fresh valid HELLO).
        await conn.SendAsync(tamperedHello, ct);

        // Responder replies with ERROR before closing — read and assert.
        var reply = await conn.ReceiveAsync(ct);
        var err = Assert.IsType<ErrorMessage>(reply);
        Assert.Equal(ErrorCode.HelloSignatureInvalid, err.Code);

        var responderFault = await responderFaultedTcs.Task.WaitAsync(TestTimeout, ct);
        responderCts.Cancel();

        Assert.IsType<InvalidOperationException>(responderFault);
    }

    [Fact]
    public async Task Handshake_Survives_Transient_Disconnect_And_Resumes_On_Reconnect()
    {
        // Verifies that a connection dropped between HELLO rounds does NOT
        // poison the transport. After the first attempt fails the initiator
        // dials a fresh connection, the responder accepts it, and the full
        // ladder completes on the new connection.
        using var timeoutCts = new CancellationTokenSource(TestTimeout);
        var ct = timeoutCts.Token;

        await using var harness = await TwoNodeHarness.StartAsync(ct, enableLeaseResponder: false);

        using var responderCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var acceptCount = 0;
        var successTcs = new TaskCompletionSource<CapabilityResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var conn in harness.NodeB.Transport.ListenAsync(responderCts.Token))
                {
                    var current = Interlocked.Increment(ref acceptCount);
                    if (current == 1)
                    {
                        // Simulate a transient drop: abort before any bytes are
                        // read.
                        try { await conn.DisposeAsync(); }
                        catch { /* best-effort */ }
                        continue;
                    }

                    await using (conn)
                    {
                        var result = await HandshakeProtocol.RespondAsync(
                            conn,
                            harness.NodeB.BuildLocalIdentity(),
                            policy: proposal => new AckMessage(
                                GrantedSubscriptions: proposal.ProposedStreams,
                                Rejected: Array.Empty<Rejection>()),
                            responderCts.Token);
                        successTcs.TrySetResult(result);
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                successTcs.TrySetException(ex);
            }
        }, responderCts.Token);

        // First attempt: expect failure because the responder abandons the
        // connection before exchanging any bytes.
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await using var firstConn = await harness.NodeA.Transport
                .ConnectAsync(harness.SharedSocketPathB, ct);
            await HandshakeProtocol.InitiateAsync(
                firstConn,
                harness.NodeA.BuildLocalIdentity(),
                ct);
        });

        // Second attempt: fresh connection, full handshake completes.
        await using (var secondConn = await harness.NodeA.Transport
            .ConnectAsync(harness.SharedSocketPathB, ct))
        {
            var result = await HandshakeProtocol.InitiateAsync(
                secondConn,
                harness.NodeA.BuildLocalIdentity(),
                ct);
            Assert.Equal(harness.NodeB.PublicKey, result.PeerPublicKey);
        }

        var serverResult = await successTcs.Task.WaitAsync(TestTimeout, ct);
        responderCts.Cancel();

        Assert.Equal(harness.NodeA.PublicKey, serverResult.PeerPublicKey);
        Assert.Equal(2, acceptCount);
    }

    [Fact]
    public async Task Handshake_Rejects_Version_Mismatched_Peer()
    {
        using var timeoutCts = new CancellationTokenSource(TestTimeout);
        var ct = timeoutCts.Token;

        await using var harness = await TwoNodeHarness.StartAsync(ct, enableLeaseResponder: false);

        using var responderCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        // Responder side: announces ONLY the default version. Initiator
        // pushes a mismatched version; responder throws after emitting ERROR.
        var responderFaultedTcs = new TaskCompletionSource<Exception>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var conn in harness.NodeB.Transport.ListenAsync(responderCts.Token))
                {
                    await using (conn)
                    {
                        try
                        {
                            await HandshakeProtocol.RespondAsync(
                                conn,
                                harness.NodeB.BuildLocalIdentity(
                                    schemaVersion: HandshakeProtocol.DefaultSchemaVersion),
                                policy: proposal => new AckMessage(
                                    GrantedSubscriptions: proposal.ProposedStreams,
                                    Rejected: Array.Empty<Rejection>()),
                                responderCts.Token);
                            responderFaultedTcs.TrySetException(
                                new Xunit.Sdk.XunitException(
                                    "RespondAsync should have rejected the version-mismatched HELLO."));
                        }
                        catch (Exception ex)
                        {
                            responderFaultedTcs.TrySetResult(ex);
                        }
                    }
                    break;
                }
            }
            catch (Exception ex)
            {
                responderFaultedTcs.TrySetException(ex);
            }
        }, responderCts.Token);

        // Initiator builds a LocalIdentity whose SupportedVersions is disjoint
        // from the responder's DefaultSupportedVersions. Still carry a real
        // signer so the signature-verification step passes and the schema
        // mismatch is what drives the rejection.
        var mismatchedIdentity = new LocalIdentity(
            NodeId: harness.NodeA.NodeIdBytes,
            PublicKey: harness.NodeA.PublicKey,
            Signer: harness.NodeA.Signer,
            PrivateKey: harness.NodeA.PrivateKey,
            SchemaVersion: "9.9.9-not-real",
            SupportedVersions: new[] { "9.9.9-not-real" });

        await using var conn = await harness.NodeA.Transport
            .ConnectAsync(harness.SharedSocketPathB, ct);

        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await HandshakeProtocol.InitiateAsync(conn, mismatchedIdentity, ct);
        });

        var responderFault = await responderFaultedTcs.Task.WaitAsync(TestTimeout, ct);
        responderCts.Cancel();

        Assert.IsType<InvalidOperationException>(responderFault);
        Assert.Contains("Schema-version mismatch", responderFault.Message);
    }
}
