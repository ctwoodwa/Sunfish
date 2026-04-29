namespace Sunfish.Blocks.PropertyAssets.Models;

/// <summary>
/// Coarse classification for an <see cref="Asset"/>. Drives downstream
/// behaviour such as inspection cadence, depreciation defaults, and
/// warranty-claim routing. Schema-registry-backed AssetClass per cluster
/// intake OQ-A2 is a Phase 2.3+ amendment; the enum suffices for first-slice.
/// </summary>
public enum AssetClass
{
    /// <summary>Tank or tankless water heater.</summary>
    WaterHeater,

    /// <summary>Heating, ventilation, and air-conditioning system.</summary>
    HVAC,

    /// <summary>Generic large/major appliance: fridge, dishwasher, range, washer, dryer, microwave.</summary>
    Appliance,

    /// <summary>Roof structure (covering, decking, flashing).</summary>
    Roof,

    /// <summary>Vehicle. Reserved; full subtype (VIN, mileage Trip events) gated on follow-up hand-off.</summary>
    Vehicle,

    /// <summary>Plumbing fixtures and pipes.</summary>
    Plumbing,

    /// <summary>Electrical panels, sub-panels, and fixed fixtures.</summary>
    Electrical,

    /// <summary>Catch-all for assets that don't fit the above buckets; tag context with <c>Asset.Notes</c>.</summary>
    Other,
}
