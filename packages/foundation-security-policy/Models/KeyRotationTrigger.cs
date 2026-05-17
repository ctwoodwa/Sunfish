namespace Sunfish.Foundation.SecurityPolicy.Models;

/// <summary>Auto-trigger conditions for key rotation per ADR 0068 §1.4.</summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// <see cref="CompromiseIndicatorFlagged"/> MUST collapse
/// <c>RotationGracePeriod</c> to zero (§1.4.1) — the floor validator
/// (subsequent PR) enforces. <see cref="EmergencyOverride"/> requires
/// Captain + 1 officer multi-actor approval; rate-limited to 1 per
/// 24h per actor (§1.4.2).
/// </para>
/// </remarks>
public enum KeyRotationTrigger
{
    CadenceExpired,
    CompromiseIndicatorFlagged,
    MfaFactorRevoked,
    AttestationTierDowngrade,
    RecoveryCompleted,
    RoleChange,
    ApproverRevoked,
    RecoveryContactRemoved,
    PolicyTightening,
    EmergencyOverride,
}
