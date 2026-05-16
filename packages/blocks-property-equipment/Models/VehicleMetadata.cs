namespace Sunfish.Blocks.PropertyEquipment.Models;

/// <summary>
/// Vehicle-specific metadata for <see cref="Equipment"/> records where
/// <see cref="Equipment.Class"/> is <see cref="EquipmentClass.Vehicle"/>.
/// Null on all other equipment classes.
/// </summary>
public sealed record VehicleMetadata
{
    /// <summary>Vehicle Identification Number (typically 17 characters).</summary>
    public string? Vin { get; init; }

    /// <summary>Manufacturer name (e.g. <c>"Ford"</c>, <c>"Toyota"</c>).</summary>
    public string? Make { get; init; }

    /// <summary>Model name (e.g. <c>"F-150"</c>, <c>"Camry"</c>).</summary>
    public string? Model { get; init; }

    /// <summary>Model year (e.g. <c>2019</c>).</summary>
    public int? Year { get; init; }

    /// <summary>
    /// License plate; conventionally formatted as "{state-abbr} {plate}"
    /// (e.g. <c>"WA ABC1234"</c>), but not enforced.
    /// </summary>
    public string? LicensePlate { get; init; }

    /// <summary>
    /// Current odometer reading in miles. Updated to the latest
    /// <see cref="TripRecord.EndOdometer"/> when a trip is appended via
    /// <see cref="Services.ITripStore.AppendAsync"/>.
    /// </summary>
    public decimal CurrentOdometer { get; init; }
}
