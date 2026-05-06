using System.Text.Json.Serialization;

namespace Sunfish.Foundation.Ship.Common;

/// <summary>
/// Per-department location surface per ADR 0077 §2 + W#35 discovery. Each
/// W#35 cohort UI ADR maps to one of these locations; resolver step 3 refuses
/// access to <see cref="SupplyOffice"/> (Phase 2 deferred), <see cref="Wardroom"/>
/// (v2 deferred), and <see cref="Brig"/> (v2 deferred) at the deferral check
/// gate.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ShipLocation
{
    /// <summary>Entry-point + executive summary.</summary>
    Quarterdeck,

    /// <summary>Configuration department per ADR 0065 + W#34.</summary>
    Wayfinder,

    /// <summary>Technical operations.</summary>
    EngineRoom,

    /// <summary>Monitoring + threat awareness.</summary>
    Tactical,

    /// <summary>Recovery + identity.</summary>
    SickBay,

    /// <summary>Content management.</summary>
    ShipsOffice,

    /// <summary>Billing / commercial (Phase 2 deferred).</summary>
    SupplyOffice,

    /// <summary>v2 deferred.</summary>
    Wardroom,

    /// <summary>v2 deferred.</summary>
    Brig,
}
