using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.SecurityPolicy.Issuance;

/// <summary>
/// Approval-time freshness token bound to a specific
/// <see cref="StandingOrderId"/> as nonce. Per ADR 0068 §3.1.1. Default
/// expiry is 24 hours (configurable via
/// <see cref="SecurityPolicyIssuerOptions.ApprovalProofMaxAge"/>).
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// Prevents stored-credential replay against the approval flow — an
/// attacker who steals an approver's <see cref="CapabilityProof"/>
/// cannot use it past the 24h window NOR against a different
/// <see cref="StandingOrderId"/> than the one it was minted for.
/// </para>
/// <para>
/// <see cref="ProofBytes"/> is OPAQUE. The issuer never decodes it in
/// Phase 1 — cryptographic verification against
/// <c>ICapabilityGraph</c> ships in Phase 2 (W#37 Phase 2 +
/// Cap-graph workstream). Phase 1 is more conservative than the spec:
/// the bytes are NEVER persisted into any audit payload (the
/// <c>SecurityPolicyApprovalReceived</c> payload carries only
/// approver / proposal / timestamp) and NEVER inspected by the
/// issuer. The bytes live only on the in-memory in-flight record and
/// fall out of scope when the proposal is Applied or Rescinded.
/// </para>
/// </remarks>
/// <param name="Approver">The actor whose capability is being attested.</param>
/// <param name="BoundTo">Nonce — proof is only valid for THIS proposal.</param>
/// <param name="IssuedAt">Wall-clock time at which the proof was minted.</param>
/// <param name="ExpiresAt">Wall-clock time after which the proof is stale.</param>
/// <param name="ProofBytes">Opaque platform-attestation blob. Treat as sensitive (never log).</param>
public sealed record CapabilityProof(
    ActorId Approver,
    StandingOrderId BoundTo,
    System.DateTimeOffset IssuedAt,
    System.DateTimeOffset ExpiresAt,
    System.ReadOnlyMemory<byte> ProofBytes)
{
    /// <summary>Returns <c>true</c> when the proof has not yet expired at <paramref name="now"/>.</summary>
    public bool IsFresh(System.DateTimeOffset now) => ExpiresAt > now;

    /// <summary>Returns <c>true</c> when the proof was minted for <paramref name="proposal"/>.</summary>
    public bool IsBoundTo(StandingOrderId proposal) => BoundTo == proposal;
}
