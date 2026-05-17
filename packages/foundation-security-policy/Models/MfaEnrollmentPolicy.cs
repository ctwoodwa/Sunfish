using System.Collections.ObjectModel;
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
/// <c>OOD</c> is a watch designation, not a stable role assignment —
/// absent from the default map (§1.1.2; MFA requirements attach to
/// the underlying base role). <c>EOOW</c> IS present in the default
/// map (the engine-officer watch equivalent — included for parity
/// with other rotating watches; tenant deployers may remove it via
/// Standing Order if they treat EOOW purely as a watch designation).
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
        RequiredFactorsByRole: new ReadOnlyDictionary<ShipRole, IReadOnlyList<MfaFactor>>(
            new Dictionary<ShipRole, IReadOnlyList<MfaFactor>>
            {
                [ShipRole.Captain]         = Array.AsReadOnly(new[] { MfaFactor.WebAuthnPasskey, MfaFactor.Totp }),
                [ShipRole.XO]              = Array.AsReadOnly(new[] { MfaFactor.WebAuthnPasskey, MfaFactor.Totp }),
                [ShipRole.EngineerOfficer] = Array.AsReadOnly(new[] { MfaFactor.Totp }),
                [ShipRole.Navigator]       = Array.AsReadOnly(new[] { MfaFactor.Totp }),
                [ShipRole.TacticalOfficer] = Array.AsReadOnly(new[] { MfaFactor.Totp }),
                [ShipRole.IDC]             = Array.AsReadOnly(new[] { MfaFactor.Totp }),
                [ShipRole.Scribe]          = Array.AsReadOnly(new[] { MfaFactor.Totp }),
                [ShipRole.EOOW]            = Array.AsReadOnly(new[] { MfaFactor.Totp }),
            }),
        EnrollmentGracePeriod: TimeSpan.FromDays(7),
        RecoveryFlowExemptsFromMfa: false);
}
