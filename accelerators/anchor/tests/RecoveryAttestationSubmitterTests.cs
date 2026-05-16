using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Anchor.Services;
using Sunfish.Foundation.Recovery;
using Sunfish.Kernel.Security.Session;
using Sunfish.Kernel.Sync.Identity;
using Xunit;

namespace Sunfish.Anchor.Tests;

/// <summary>
/// W#66 — `RecoveryAttestationSubmitter` contract tests. Verifies the
/// composition of `ISessionSignerAccessor` (W#65), `INodeIdentityProvider`,
/// `IRecoveryCoordinator`, and `TimeProvider` produces a well-formed
/// `TrusteeAttestation` and surfaces the coordinator outcome correctly.
///
/// Per the established `accelerators/anchor/tests/tests.csproj` MAUI-free
/// posture (see `CrewChatPageTests.cs`), the Razor page itself is not
/// instantiated; the helper service it delegates to IS unit-tested here.
/// </summary>
public sealed class RecoveryAttestationSubmitterTests
{
    private const string TrusteeNodeId = "0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task SubmitAsync_PopulatesAttestation_FromSignerAndNodeIdentity()
    {
        var (submitter, signer, recovery, captured) = NewSubmitterWithCapture(
            attestedAt: new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero),
            signResult: new byte[64],
            outcome: new RecoveryAttestationOutcome(Accepted: true, Events: Array.Empty<RecoveryEvent>()));

        var request = NewRequest();
        var result = await submitter.SubmitAsync(request);

        Assert.True(result.Accepted);
        Assert.False(result.QuorumReached);
        Assert.Null(result.GracePeriodStartedAt);

        var att = captured.Single();
        Assert.Equal(TrusteeNodeId, att.TrusteeNodeId);
        Assert.Equal(signer.PublicKey.ToArray(), att.TrusteePublicKey);
        Assert.Equal(TrusteeAttestation.HashOf(request), att.RecoveryRequestHash);
        Assert.Equal(new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero), att.AttestedAt);
        Assert.NotNull(att.Signature);
    }

    [Fact]
    public async Task SubmitAsync_SignsCanonicalBytes_NotTheRawHash()
    {
        // The submitter must sign `CanonicalBytesForSigning(nodeId, hash, attestedAt)`
        // (domain-separated by the "sunfish-trustee-attestation-v1" prefix),
        // not the bare 32-byte hash. Verify by capturing what was passed to
        // signer.SignAsync and comparing to the canonical bytes.
        var attestedAt = new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero);
        byte[]? signedBytes = null;

        var signer = Substitute.For<IBoundEd25519Signer>();
        signer.PublicKey.Returns(new ReadOnlyMemory<byte>(new byte[32]));
        signer.SignAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
              .Returns(ci =>
              {
                  signedBytes = ((ReadOnlyMemory<byte>)ci[0]!).ToArray();
                  return ValueTask.FromResult(new byte[64]);
              });

        var accessor    = Substitute.For<ISessionSignerAccessor>();
        accessor.GetCurrentAsync(Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(signer));
        var nodeId      = NewNodeIdentityProvider();
        var recovery    = Substitute.For<IRecoveryCoordinator>();
        recovery.SubmitAttestationAsync(Arg.Any<TrusteeAttestation>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult(new RecoveryAttestationOutcome(true, Array.Empty<RecoveryEvent>())));
        var time        = new FakeTimeProvider(attestedAt);

        var submitter = new RecoveryAttestationSubmitter(recovery, accessor, nodeId, time);
        var request = NewRequest();
        await submitter.SubmitAsync(request);

        var hash = TrusteeAttestation.HashOf(request);
        var expectedCanonical = TrusteeAttestation.CanonicalBytesForSigning(
            TrusteeNodeId, hash, attestedAt,
            new byte[TrusteeAttestation.TrusteeDHPublicKeyLength],
            new byte[TrusteeAttestation.SeedEnvelopeCiphertextLength],
            new byte[TrusteeAttestation.SeedEnvelopeNonceLength]);

        Assert.NotNull(signedBytes);
        Assert.Equal(expectedCanonical, signedBytes);
    }

    [Fact]
    public async Task SubmitAsync_QuorumReached_WhenGracePeriodStartedEventFires()
    {
        var graceStartedAt = new DateTimeOffset(2026, 5, 16, 12, 5, 0, TimeSpan.Zero);
        var (submitter, _, _, _) = NewSubmitterWithCapture(
            attestedAt: new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero),
            signResult: new byte[64],
            outcome: new RecoveryAttestationOutcome(
                Accepted: true,
                Events: new[]
                {
                    NewEvent(RecoveryEventType.AttestationReceived, new DateTimeOffset(2026, 5, 16, 12, 0, 1, TimeSpan.Zero)),
                    NewEvent(RecoveryEventType.GracePeriodStarted, graceStartedAt),
                }));

        var result = await submitter.SubmitAsync(NewRequest());

        Assert.True(result.Accepted);
        Assert.True(result.QuorumReached);
        Assert.Equal(graceStartedAt, result.GracePeriodStartedAt);
    }

    [Fact]
    public async Task SubmitAsync_NotAccepted_ReturnsAcceptedFalse_NoQuorum()
    {
        var (submitter, _, _, _) = NewSubmitterWithCapture(
            attestedAt: new DateTimeOffset(2026, 5, 16, 12, 0, 0, TimeSpan.Zero),
            signResult: new byte[64],
            outcome: new RecoveryAttestationOutcome(Accepted: false, Events: Array.Empty<RecoveryEvent>()));

        var result = await submitter.SubmitAsync(NewRequest());

        Assert.False(result.Accepted);
        Assert.False(result.QuorumReached);
        Assert.Null(result.GracePeriodStartedAt);
    }

    [Fact]
    public async Task SubmitAsync_PropagatesSignerAccessorThrow_WhenNoActiveTeam()
    {
        var accessor = Substitute.For<ISessionSignerAccessor>();
        accessor.GetCurrentAsync(Arg.Any<CancellationToken>())
                .Returns<ValueTask<IBoundEd25519Signer>>(_ =>
                    throw new InvalidOperationException("no active team"));

        var submitter = new RecoveryAttestationSubmitter(
            recovery:       Substitute.For<IRecoveryCoordinator>(),
            signerAccessor: accessor,
            nodeIdentity:   NewNodeIdentityProvider(),
            time:           new FakeTimeProvider(DateTimeOffset.UtcNow));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => submitter.SubmitAsync(NewRequest()));
    }

    // ----- helpers ---------------------------------------------------

    private static (RecoveryAttestationSubmitter submitter,
                    IBoundEd25519Signer signer,
                    IRecoveryCoordinator recovery,
                    List<TrusteeAttestation> capturedAttestations)
        NewSubmitterWithCapture(
            DateTimeOffset attestedAt,
            byte[] signResult,
            RecoveryAttestationOutcome outcome)
    {
        var pubKey = new byte[32];
        new Random(0).NextBytes(pubKey);

        var signer = Substitute.For<IBoundEd25519Signer>();
        signer.PublicKey.Returns(new ReadOnlyMemory<byte>(pubKey));
        signer.SignAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<CancellationToken>())
              .Returns(ValueTask.FromResult(signResult));

        var accessor = Substitute.For<ISessionSignerAccessor>();
        accessor.GetCurrentAsync(Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(signer));

        var captured = new List<TrusteeAttestation>();
        var recovery = Substitute.For<IRecoveryCoordinator>();
        recovery.SubmitAttestationAsync(Arg.Any<TrusteeAttestation>(), Arg.Any<CancellationToken>())
                .Returns(ci =>
                {
                    captured.Add((TrusteeAttestation)ci[0]!);
                    return Task.FromResult(outcome);
                });

        var submitter = new RecoveryAttestationSubmitter(
            recovery, accessor, NewNodeIdentityProvider(), new FakeTimeProvider(attestedAt));

        return (submitter, signer, recovery, captured);
    }

    private static INodeIdentityProvider NewNodeIdentityProvider()
    {
        var pubKey  = new byte[32];
        var privKey = new byte[32];
        return new InMemoryNodeIdentityProvider(
            new NodeIdentity(TrusteeNodeId, pubKey, privKey));
    }

    private static RecoveryRequest NewRequest()
    {
        // Use the request's static factory so the hash/signature stay
        // internally consistent. The actual signature isn't checked here
        // (the coordinator is faked); the submitter only reads the
        // request's hashable fields.
        var ephemeralPub  = new byte[32];
        var ephemeralDH   = new byte[RecoveryRequest.EphemeralDHPublicKeyLength];
        var signature     = new byte[64];
        return new RecoveryRequest(
            RequestingNodeId:     "rrrrrrrrrrrrrrrrrrrrrrrrrrrrrrrr",
            EphemeralPublicKey:   ephemeralPub,
            EphemeralDHPublicKey: ephemeralDH,
            RequestedAt:          new DateTimeOffset(2026, 5, 16, 11, 0, 0, TimeSpan.Zero),
            Signature:            signature);
    }

    private static RecoveryEvent NewEvent(RecoveryEventType type, DateTimeOffset occurredAt) =>
        new(Type:               type,
            ActorNodeId:        TrusteeNodeId,
            TargetNodeId:       "ssssssssssssssssssssssssssssssss",
            OccurredAt:         occurredAt,
            PreviousEventHash:  null,
            Detail:             new Dictionary<string, string>());

    /// <summary>
    /// Local FakeTimeProvider — mirrors the inline pattern used in
    /// `packages/foundation-wayfinder/tests/DefaultOodWatchServiceTests.cs`
    /// and `packages/foundation-transport/tests/MdnsPeerTransportTests.cs`.
    /// Avoids adding a `Microsoft.Extensions.Time.Testing` package dep just
    /// for a fixed-instant fake.
    /// </summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) { _now = now; }
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
