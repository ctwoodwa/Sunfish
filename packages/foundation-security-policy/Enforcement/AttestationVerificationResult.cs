using Sunfish.Foundation.SecurityPolicy.Models;

namespace Sunfish.Foundation.SecurityPolicy.Enforcement;

/// <summary>
/// Result of an <see cref="IAttestationVerifier"/> verification call
/// per ADR 0068 §4.3.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public sealed record AttestationVerificationResult(
    bool IsVerified,
    AttestationTier VerifiedTier,
    string? FailureReason);
