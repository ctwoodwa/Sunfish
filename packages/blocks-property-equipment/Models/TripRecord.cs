using Sunfish.Blocks.Properties.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Blocks.PropertyEquipment.Models;

/// <summary>
/// Append-only record of a vehicle trip. <see cref="Miles"/> is computed
/// from <see cref="StartOdometer"/> and <see cref="EndOdometer"/>. The
/// parent <see cref="VehicleMetadata.CurrentOdometer"/> is updated to
/// <see cref="EndOdometer"/> when the trip is appended via
/// <see cref="Services.ITripStore.AppendAsync"/>.
/// </summary>
public sealed record TripRecord : IMustHaveTenant
{
    /// <summary>Stable identifier for this trip.</summary>
    public required TripRecordId Id { get; init; }

    /// <summary>Owning tenant (per <see cref="IMustHaveTenant"/>).</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>FK to the <see cref="Equipment"/> record. Must have <c>Class = Vehicle</c>.</summary>
    public required EquipmentId EquipmentId { get; init; }

    /// <summary>FK to the property the trip terminated at (or started from).</summary>
    public required PropertyId PropertyId { get; init; }

    /// <summary>UTC date of the trip.</summary>
    public required DateTimeOffset TripDate { get; init; }

    /// <summary>Odometer reading at trip start (miles).</summary>
    public required decimal StartOdometer { get; init; }

    /// <summary>Odometer reading at trip end (miles).</summary>
    public required decimal EndOdometer { get; init; }

    /// <summary>
    /// Distance driven (miles), always non-negative. Returns 0 when
    /// <see cref="EndOdometer"/> &lt;= <see cref="StartOdometer"/>; the
    /// <see cref="Services.ITripStore.AppendAsync"/> contract additionally
    /// rejects records where end &lt; start (negative-miles guard).
    /// </summary>
    public decimal Miles => Math.Max(0m, EndOdometer - StartOdometer);

    /// <summary>Purpose of the trip (e.g. <c>"maintenance"</c>, <c>"inspection"</c>, <c>"delivery"</c>).</summary>
    public string? Purpose { get; init; }

    /// <summary>Optional free-text notes.</summary>
    public string? Notes { get; init; }
}
