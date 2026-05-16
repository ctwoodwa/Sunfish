using System.Linq;
using Sunfish.Foundation.Recovery;
using Sunfish.Kernel.Security.Session;
using Sunfish.Kernel.Sync.Identity;

namespace Sunfish.Anchor.Services;

/// <summary>
/// W#66 — composes <see cref="ISessionSignerAccessor"/> (W#65) +
/// <see cref="INodeIdentityProvider"/> + <see cref="IRecoveryCoordinator"/>
/// to build and submit a <see cref="TrusteeAttestation"/> for the active
/// pending recovery request. Extracted from
/// <c>ApproveRecoveryPage.razor</c> so the signing/submission logic is
/// unit-testable without dragging the MAUI workload into the Anchor
/// tests project (per the established MAUI-free test convention; see
/// <c>CrewChatPageTests.cs</c>).
/// </summary>
/// <remarks>
/// Signing key: the per-team Ed25519 subkey returned by
/// <see cref="ISessionSignerAccessor"/> (ADR 0032). The trustee's NodeId
/// comes from <see cref="INodeIdentityProvider.Current"/>; that pair
/// (NodeId + public key) must match what the owner entered on the
/// <c>TrusteeSetupPage</c> when designating this device as a trustee.
/// </remarks>
public sealed class RecoveryAttestationSubmitter
{
    private readonly IRecoveryCoordinator _recovery;
    private readonly ISessionSignerAccessor _signerAccessor;
    private readonly INodeIdentityProvider _nodeIdentity;
    private readonly TimeProvider _time;

    public RecoveryAttestationSubmitter(
        IRecoveryCoordinator recovery,
        ISessionSignerAccessor signerAccessor,
        INodeIdentityProvider nodeIdentity,
        TimeProvider time)
    {
        _recovery       = recovery       ?? throw new ArgumentNullException(nameof(recovery));
        _signerAccessor = signerAccessor ?? throw new ArgumentNullException(nameof(signerAccessor));
        _nodeIdentity   = nodeIdentity   ?? throw new ArgumentNullException(nameof(nodeIdentity));
        _time           = time           ?? throw new ArgumentNullException(nameof(time));
    }

    /// <summary>
    /// Build and submit a trustee attestation for <paramref name="request"/>.
    /// Returns the coordinator outcome flattened for UI consumption.
    /// </summary>
    public async Task<AttestationSubmissionResult> SubmitAsync(
        RecoveryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var signer        = await _signerAccessor.GetCurrentAsync(cancellationToken).ConfigureAwait(false);
        var trusteeNodeId = _nodeIdentity.Current.NodeId;
        var attestedAt    = _time.GetUtcNow();
        var requestHash   = TrusteeAttestation.HashOf(request);
        var canonical     = TrusteeAttestation.CanonicalBytesForSigning(
            trusteeNodeId, requestHash, attestedAt);
        var signature     = await signer.SignAsync(canonical, cancellationToken).ConfigureAwait(false);

        var attestation = new TrusteeAttestation(
            TrusteeNodeId:        trusteeNodeId,
            TrusteePublicKey:     signer.PublicKey.ToArray(),
            RecoveryRequestHash:  requestHash,
            AttestedAt:           attestedAt,
            Signature:            signature);

        var outcome = await _recovery.SubmitAttestationAsync(attestation, cancellationToken)
            .ConfigureAwait(false);

        var quorumEvent = outcome.Events
            .FirstOrDefault(e => e.Type == RecoveryEventType.GracePeriodStarted);

        return new AttestationSubmissionResult(
            Accepted:             outcome.Accepted,
            QuorumReached:        quorumEvent is not null,
            GracePeriodStartedAt: quorumEvent?.OccurredAt);
    }
}

/// <summary>UI-shaped outcome of <see cref="RecoveryAttestationSubmitter.SubmitAsync"/>.</summary>
/// <param name="Accepted">Mirrors <see cref="RecoveryAttestationOutcome.Accepted"/>.</param>
/// <param name="QuorumReached"><c>true</c> if a <c>GracePeriodStarted</c> event fired on this submission.</param>
/// <param name="GracePeriodStartedAt">When the grace window began, or <c>null</c> if quorum was not yet reached.</param>
public sealed record AttestationSubmissionResult(
    bool Accepted,
    bool QuorumReached,
    DateTimeOffset? GracePeriodStartedAt);
