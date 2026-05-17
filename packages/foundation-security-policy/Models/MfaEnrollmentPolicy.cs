using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Foundation.SecurityPolicy.Models;

/// <summary>
/// Per-role MFA enrollment requirements + grace period + recovery-MFA
/// exemption flag per ADR 0068 §1.1. <see cref="Default"/> reflects
/// the platform-provisioned defaults.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// <c>OOD</c>/<c>EOOW</c> are watch designations, not
/// stable role assignments — they're absent from the default map
/// (§1.1.2; MFA requirements attach to the underlying base role).
/// <see cref="ShipRole.DivisionOfficer"/> is absent from the default
/// to leave it tenant-configurable (§1.1.3).
/// <see cref="RecoveryFlowExemptsFromMfa"/> defaults to <c>false</c>
/// because recovery restores access, not privileges — post-recovery
/// re-enrollment is required (§1.1.1, paired with
/// <c>KeyRotationTrigger.RecoveryCompleted</c>).
/// </para>
/// </remarks>
public sealed record MfaEnrollmentPolicy(
    IReadOnlyDictionary<ShipRole, IReadOnlyList<MfaFactor>> RequiredFactorsByRole,
    TimeSpan EnrollmentGracePeriod,
    bool RecoveryFlowExemptsFromMfa)
{
    public static readonly MfaEnrollmentPolicy Default = new(
        RequiredFactorsByRole: new Dictionary<ShipRole, IReadOnlyList<MfaFactor>>
        {
            [ShipRole.Captain]         = new[] { MfaFactor.WebAuthnPasskey, MfaFactor.Totp },
            [ShipRole.XO]              = new[] { MfaFactor.WebAuthnPasskey, MfaFactor.Totp },
            [ShipRole.EngineerOfficer] = new[] { MfaFactor.Totp },
            [ShipRole.Navigator]       = new[] { MfaFactor.Totp },
            [ShipRole.TacticalOfficer] = new[] { MfaFactor.Totp },
            [ShipRole.IDC]             = new[] { MfaFactor.Totp },
            [ShipRole.Scribe]          = new[] { MfaFactor.Totp },
            [ShipRole.EOOW]            = new[] { MfaFactor.Totp },
        },
        EnrollmentGracePeriod: TimeSpan.FromDays(7),
        RecoveryFlowExemptsFromMfa: false);
}
