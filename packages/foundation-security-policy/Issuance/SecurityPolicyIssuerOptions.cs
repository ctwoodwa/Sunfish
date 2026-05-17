namespace Sunfish.Foundation.SecurityPolicy.Issuance;

/// <summary>
/// Configuration knobs for
/// <see cref="ISecurityPolicyIssuer"/> implementations. Per ADR 0068
/// §3.1.1.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Deployers MUST obtain qualified legal counsel before
/// configuring enforcement behavior for production use.
/// <para>
/// Wire via <c>services.AddOptions&lt;SecurityPolicyIssuerOptions&gt;()</c>
/// and bind from configuration. Defaults are tuned for compliance-
/// adjacent workloads; tightening (shorter expiry) is always safe,
/// loosening (longer expiry) requires legal review per
/// <c>_shared/engineering/pre-legal-research-prompt.md</c>.
/// </para>
/// </remarks>
public sealed class SecurityPolicyIssuerOptions
{
    /// <summary>
    /// Maximum age of a <see cref="CapabilityProof"/> at the time of
    /// <see cref="ISecurityPolicyIssuer.ApproveAsync"/>. Proofs older
    /// than this window are rejected with reason code
    /// <c>FLOOR_EXPIRED_PROOF</c>. Default: 24 hours per ADR 0068
    /// §3.1.1.
    /// </summary>
    public System.TimeSpan ApprovalProofMaxAge { get; init; } = System.TimeSpan.FromHours(24);
}
