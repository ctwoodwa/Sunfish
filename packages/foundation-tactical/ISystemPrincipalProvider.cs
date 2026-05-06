using System.Threading;
using System.Threading.Tasks;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Capabilities;

namespace Sunfish.Foundation.Tactical;

/// <summary>
/// Resolves the system principal for a tenant per ADR 0081 §4.1.
/// Used exclusively by
/// <see cref="IThreatTriggerService.TryIssueAsync"/> to obtain the
/// Standing-Order issuer principal — emergency Standing Orders may
/// only be issued by the system, never by a human actor. Phase 1
/// ships the seam; Phase 2 wires the concrete implementation
/// alongside <c>DefaultTacticalRuleEngine</c>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Authority contract:</b> the returned <see cref="Principal"/>
/// MUST carry a system-level role (the W#46 cohort's
/// <c>ShipRole.System</c> if/when added; otherwise a tenant-scoped
/// service principal granted
/// <c>ShipAction.IssueEmergencyStandingOrder</c> exclusively at DI
/// bootstrap). Per ADR 0081 §4.1, the principal MUST NOT be
/// assignable to human actors at the
/// <c>Sunfish.Foundation.Ship.Common.IPermissionResolver</c>
/// layer.
/// </para>
/// <para>
/// <b>Phase 2 wiring note:</b> at this writing,
/// <c>Sunfish.Foundation.Ship.Common.ShipRole</c> does NOT include
/// a <c>System</c> value (the v1 enum is Captain / XO /
/// EngineerOfficer / TacticalOfficer / DivisionOfficer / IDC /
/// Scribe / SUPPO / OOD / EOOW). Phase 2 needs either an ADR 0077
/// amendment adding <c>ShipRole.System</c> OR a non-role-based
/// authority gate keyed on the system principal's
/// <see cref="Sunfish.Foundation.Crypto.PrincipalId"/>. Filed for
/// W#46 / W#52 council disposition.
/// </para>
/// </remarks>
public interface ISystemPrincipalProvider
{
    /// <summary>Resolve the system principal for the tenant.</summary>
    ValueTask<Principal> GetSystemPrincipalAsync(
        TenantId tenantId,
        CancellationToken ct = default);
}
