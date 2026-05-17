using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.SecurityPolicy.Models;
using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Foundation.SecurityPolicy.Enforcement;

/// <summary>
/// Phase 1 reference <see cref="ISecurityPolicyEnforcer"/>. Resolves
/// the tenant's <see cref="TenantSecurityPolicy"/> via a caller-
/// supplied factory (no SQLite/event-stream wiring yet) and applies
/// the four check methods. Per ADR 0068 §4.3, all hardware-tier
/// attestation evidence fails closed unless an
/// <see cref="IAttestationVerifier"/> is registered for the claimed
/// tier — Phase 1 ships zero verifiers, so every privileged action
/// requiring a hardware tier returns
/// <see cref="PolicyViolationKind.DeviceAttestationRequired"/>.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// Phase 1 minimum behavior: MFA + recovery-contact checks short-
/// circuit to Compliant absent an actor-claim integration; key-
/// rotation status returns <see cref="KeyRotationStatus.Current"/>.
/// The full set of checks lights up when the issuer (§3) +
/// key-tracker + recovery-contact-tracker land in PR 3b.
/// </para>
/// </remarks>
public sealed class DefaultSecurityPolicyEnforcer : ISecurityPolicyEnforcer
{
    private readonly Func<TenantId, CancellationToken, ValueTask<TenantSecurityPolicy>> _policyLoader;
    private readonly IReadOnlyDictionary<AttestationTier, IAttestationVerifier> _verifiers;

    public DefaultSecurityPolicyEnforcer(
        Func<TenantId, CancellationToken, ValueTask<TenantSecurityPolicy>> policyLoader,
        IEnumerable<IAttestationVerifier>? verifiers = null)
    {
        _policyLoader = policyLoader ?? throw new ArgumentNullException(nameof(policyLoader));
        _verifiers = (verifiers ?? Array.Empty<IAttestationVerifier>())
            .ToDictionary(v => v.SupportedTier);
    }

    /// <inheritdoc />
    public ValueTask<PolicyCheckResult> CheckMfaComplianceAsync(
        TenantId tenant, ActorId actor, ShipRole role,
        CancellationToken cancellationToken = default)
    {
        // Phase 1 — no actor-claim integration. Returns Compliant
        // unconditionally; the issuer (PR 3b) wires actor MFA state and
        // turns this into a real check.
        return new ValueTask<PolicyCheckResult>(PolicyCheckResult.Compliant());
    }

    /// <inheritdoc />
    public async ValueTask<PolicyCheckResult> CheckDeviceAttestationAsync(
        TenantId tenant, ActorId actor, AttestationEvidence evidence,
        bool isPrivilegedAction,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var policy = await _policyLoader(tenant, cancellationToken).ConfigureAwait(false);

        var accepted = isPrivilegedAction
            ? policy.DeviceAttestation.AcceptedTiersForPrivilegedActions
            : policy.DeviceAttestation.AcceptedTiersForReadActions;

        // Tier-below-acceptable fail-closed.
        if (!accepted.Contains(evidence.Tier))
            return PolicyCheckResult.ViolationResult(
                PolicyViolationKind.DeviceAttestationBelowTier,
                accessibleMessage: $"Device attestation tier {evidence.Tier} is not accepted for this action.",
                suggestedAction: $"Enroll a device with an accepted attestation tier: {string.Join(", ", accepted)}.");

        // Hardware-tier proof MUST go through a registered verifier
        // (§4.3 + §1.2.1). No verifier for the claimed tier → fail-
        // closed per ADR 0068 §4.3.
        if (evidence.Tier > AttestationTier.SoftwareSandbox)
        {
            if (!_verifiers.TryGetValue(evidence.Tier, out var verifier))
                return PolicyCheckResult.ViolationResult(
                    PolicyViolationKind.DeviceAttestationRequired,
                    accessibleMessage: $"No attestation verifier is registered for tier {evidence.Tier}; cannot accept hardware-tier evidence.",
                    suggestedAction: "Phase 1 ships no platform verifiers. Wire a per-platform IAttestationVerifier via DI (Phase 2 deliverable).");
            var result = await verifier.VerifyAsync(evidence.PlatformProof, evidence.EvidenceAt, cancellationToken)
                                       .ConfigureAwait(false);
            if (!result.IsVerified)
            {
                // Verifier-supplied FailureReason may contain device internals
                // (PCR mismatches, byte ranges, signer-cert fingerprints).
                // It MUST NOT surface to the UI-bound AccessibleMessage —
                // route into AttestationViolationPayload.FailureReason only.
                // UI gets an opaque safe summary.
                return PolicyCheckResult.ViolationResult(
                    PolicyViolationKind.DeviceAttestationRequired,
                    accessibleMessage: "Attestation verification failed. The device's hardware attestation could not be confirmed.",
                    suggestedAction: "Re-enroll the device's attestation evidence and retry.");
            }
        }

        return PolicyCheckResult.Compliant();
    }

    /// <inheritdoc />
    public ValueTask<KeyRotationStatus> GetKeyRotationStatusAsync(
        TenantId tenant, ActorId actor, ShipRole role,
        CancellationToken cancellationToken = default)
    {
        // Phase 1 — no key-tracker integration; defaults to Current.
        return new ValueTask<KeyRotationStatus>(KeyRotationStatus.Current);
    }

    /// <inheritdoc />
    public ValueTask<RecoveryContactComplianceStatus> GetRecoveryContactComplianceAsync(
        TenantId tenant, ActorId actor,
        CancellationToken cancellationToken = default)
    {
        // Phase 1 — no recovery-contact-tracker integration; defaults to Compliant.
        return new ValueTask<RecoveryContactComplianceStatus>(RecoveryContactComplianceStatus.Compliant);
    }
}
