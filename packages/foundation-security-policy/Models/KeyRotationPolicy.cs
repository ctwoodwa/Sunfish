using System.Collections.ObjectModel;
using Sunfish.Foundation.Ship.Common;

namespace Sunfish.Foundation.SecurityPolicy.Models;

/// <summary>
/// Per-role key-rotation cadence + grace + emergency-rotation flag +
/// auto-trigger set per ADR 0068 §1.4.
/// </summary>
/// <remarks>
/// See §GC.1 in ADR 0068 (docs/adrs/0068-tenant-security-policy.md).
/// Enforcement behavior in this package intersects HIPAA, PCI-DSS,
/// SOC 2, GDPR, and the EU AI Act. The presets and defaults are
/// informed guidance, NOT legal advice. Deployers MUST obtain
/// qualified legal counsel before configuring enforcement behavior
/// for production use.
/// <para>
/// Default cadence is 90 days; <see cref="ShipRole.Captain"/> tightens
/// to 30 days (§1.4 Default). Roles absent from
/// <see cref="PerRoleOverrides"/> fall through to
/// <see cref="DefaultRotationCadence"/>. Emergency rotation still
/// requires the multi-actor approval floor (§1.4.2) — note that
/// <c>EmergencyOverride</c> is intentionally absent from
/// <see cref="AutoTriggers"/> in the default because it MUST go
/// through Captain + officer co-approval rather than firing
/// automatically.
/// </para>
/// </remarks>
public sealed record KeyRotationPolicy(
    TimeSpan DefaultRotationCadence,
    IReadOnlyDictionary<ShipRole, TimeSpan> PerRoleOverrides,
    TimeSpan RotationGracePeriod,
    bool AllowEmergencyRotation,
    IReadOnlyList<KeyRotationTrigger> AutoTriggers)
{
    public static readonly KeyRotationPolicy Default = new(
        DefaultRotationCadence: TimeSpan.FromDays(90),
        PerRoleOverrides: new ReadOnlyDictionary<ShipRole, TimeSpan>(
            new Dictionary<ShipRole, TimeSpan>
            {
                [ShipRole.Captain] = TimeSpan.FromDays(30),
            }),
        RotationGracePeriod: TimeSpan.FromDays(7),
        AllowEmergencyRotation: true,
        AutoTriggers: Array.AsReadOnly(new[]
        {
            KeyRotationTrigger.CadenceExpired,
            KeyRotationTrigger.CompromiseIndicatorFlagged,
            KeyRotationTrigger.MfaFactorRevoked,
            KeyRotationTrigger.RecoveryCompleted,
            KeyRotationTrigger.AttestationTierDowngrade,
            KeyRotationTrigger.PolicyTightening,
        }));
}
