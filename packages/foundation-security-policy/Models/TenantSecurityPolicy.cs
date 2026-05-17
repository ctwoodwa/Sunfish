using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.SecurityPolicy.Models;

/// <summary>
/// Top-level tenant security policy per ADR 0068 §1. Immutable
/// projection of the tenant's Standing-Order log over the security
/// scope (§1.2: a new Standing Order produces a fresh projection;
/// <see cref="LastUpdatedBy"/> tracks the most recently applied
/// order). <c>null</c> <see cref="LastUpdatedBy"/> means platform
/// default — never modified by a Standing Order; the bootstrap audit
/// event <c>Sunfish.SecurityPolicy.Bootstrapped</c> (future PR) is
/// emitted at provisioning. There is NO bootstrap exemption from the
/// multi-actor approval floor (§1.1): the first Standing Order
/// modification still requires Captain + officer co-approval.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// </remarks>
public sealed record TenantSecurityPolicy(
    TenantId TenantId,
    MfaEnrollmentPolicy Mfa,
    DeviceAttestationPolicy DeviceAttestation,
    AuditRetentionPolicy AuditRetention,
    KeyRotationPolicy KeyRotation,
    RecoveryContactPolicy RecoveryContact,
    DateTimeOffset LastUpdatedAt,
    StandingOrderId? LastUpdatedBy)
{
    /// <summary>
    /// Build the platform-default <see cref="TenantSecurityPolicy"/>
    /// for a newly-provisioned tenant. <see cref="LastUpdatedBy"/> is
    /// <c>null</c> until the first Standing Order is applied.
    /// </summary>
    public static TenantSecurityPolicy DefaultFor(TenantId tenantId, DateTimeOffset now)
        => new(
            TenantId:          tenantId,
            Mfa:               MfaEnrollmentPolicy.Default,
            DeviceAttestation: DeviceAttestationPolicy.Default,
            AuditRetention:    AuditRetentionPolicy.Default,
            KeyRotation:       KeyRotationPolicy.Default,
            RecoveryContact:   RecoveryContactPolicy.Default,
            LastUpdatedAt:     now,
            LastUpdatedBy:     null);
}
