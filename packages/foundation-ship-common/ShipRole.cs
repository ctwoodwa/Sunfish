using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Canonical Sunfish ship-role taxonomy per ADR 0077 §1 + W#35 discovery §6.
/// Sealed enum for v1; tenant-defined custom roles register through
/// <see cref="IShipRoleRegistry"/> as composite roles bundling a v1 base role
/// with tenant-specific scope restrictions (extension via composition, not via
/// enum extension). The enum is the closed set of authority gradients; the
/// registry is the open set of tenant-named labels.
/// </summary>
/// <remarks>
/// Authority gradient (descending): Captain &gt; XO &gt;
/// EngineerOfficer/Navigator/TacticalOfficer (department heads) &gt;
/// DivisionOfficer/IDC/Scribe/SUPPO (specialists) &gt; OOD/EOOW (watch).
/// <see cref="DefaultPermissionResolver"/>'s promotion-target guard enforces
/// strict hierarchy on <see cref="ShipAction.PromoteRole"/> requests.
/// <para>
/// SUPPO is structurally valid but operationally inert per §1.6 (Phase 2
/// commercial deferred); OOD/EOOW are temporally-bounded watch designations
/// per §1.5 backed by ADR 0078.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShipRole
{
    /// <summary>Tenant owner (BDFL).</summary>
    Captain,

    /// <summary>Deputy.</summary>
    XO,

    /// <summary>ENG → Engine Room.</summary>
    EngineerOfficer,

    /// <summary>NAV → Wayfinder.</summary>
    Navigator,

    /// <summary>TAC → Tactical.</summary>
    TacticalOfficer,

    /// <summary>Junior officer in rotation (MPA / DCA / Comms / Sonar / Electrical / QA).</summary>
    DivisionOfficer,

    /// <summary>Independent Duty Corpsman → Sick Bay.</summary>
    IDC,

    /// <summary>→ Ship's Office.</summary>
    Scribe,

    /// <summary>Supply Officer (Phase 2 deferred per §1.6).</summary>
    SUPPO,

    /// <summary>Officer of the Deck (currently-on-watch admin).</summary>
    OOD,

    /// <summary>Engineering Officer of the Watch.</summary>
    EOOW,
}
