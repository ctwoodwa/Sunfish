using System.Threading;
using System.Threading.Tasks;
using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Blocks.PropertyEquipment.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.PropertyEquipment.Tests;

/// <summary>
/// W#61 Phase 1 — VehicleMetadata + TripRecord + ITripStore contract coverage.
/// Five cases per hand-off acceptance criteria.
/// </summary>
public class TripRecordStoreTests
{
    private static readonly TenantId TestTenant = new("tenant-w61-test");
    private static readonly PropertyId TestProperty = new("PROP-W61");

    private static (InMemoryEquipmentRepository equipment, InMemoryTripStore trips) NewServices()
    {
        var eventStore = new InMemoryEquipmentLifecycleEventStore();
        var equipment  = new InMemoryEquipmentRepository(eventStore);
        var trips      = new InMemoryTripStore(equipment);
        return (equipment, trips);
    }

    private static Equipment NewVehicle(EquipmentId id, decimal startOdometer = 0m)
        => new()
        {
            Id          = id,
            TenantId    = TestTenant,
            Property    = TestProperty,
            Class       = EquipmentClass.Vehicle,
            DisplayName = "Fleet Truck 1",
            VehicleData = new VehicleMetadata
            {
                Vin = "1FTFW1ET5DKE12345",
                Make = "Ford",
                Model = "F-150",
                Year = 2019,
                CurrentOdometer = startOdometer,
            },
            CreatedAt = DateTimeOffset.UnixEpoch,
        };

    [Fact]
    public async Task AppendThenGetForEquipment_ReturnsTheRecord()
    {
        var (equipment, trips) = NewServices();
        var equipmentId = new EquipmentId("VEH-1");
        await equipment.UpsertAsync(NewVehicle(equipmentId));

        var trip = new TripRecord
        {
            Id            = TripRecordId.NewId(),
            TenantId      = TestTenant,
            EquipmentId   = equipmentId,
            PropertyId    = TestProperty,
            TripDate      = DateTimeOffset.UnixEpoch,
            StartOdometer = 100m,
            EndOdometer   = 142m,
            Purpose       = "maintenance",
        };
        await trips.AppendAsync(trip);

        var list = await trips.GetForEquipmentAsync(equipmentId);

        var single = Assert.Single(list);
        Assert.Equal(trip.Id, single.Id);
        Assert.Equal(42m, single.Miles);
    }

    [Fact]
    public void TripRecord_Miles_IsEndMinusStart_WhenPositive()
    {
        var trip = NewTrip(start: 1000m, end: 1075m);
        Assert.Equal(75m, trip.Miles);
    }

    [Fact]
    public void TripRecord_Miles_IsZero_WhenEndAtOrBeforeStart()
    {
        Assert.Equal(0m, NewTrip(start: 500m, end: 500m).Miles);
        // Note: the model getter clamps to 0; ITripStore.AppendAsync still
        // rejects records where end < start (negative-miles guard).
        Assert.Equal(0m, NewTrip(start: 500m, end: 400m).Miles);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_ForUnknownId()
    {
        var (_, trips) = NewServices();
        var result = await trips.GetAsync(TripRecordId.NewId());
        Assert.Null(result);
    }

    [Fact]
    public async Task AppendAsync_UpdatesParentEquipment_CurrentOdometer_ToLatestEnd()
    {
        var (equipment, trips) = NewServices();
        var equipmentId = new EquipmentId("VEH-2");
        await equipment.UpsertAsync(NewVehicle(equipmentId, startOdometer: 100m));

        await trips.AppendAsync(NewTripFor(equipmentId, start: 100m, end: 150m));
        await trips.AppendAsync(NewTripFor(equipmentId, start: 150m, end: 220m));

        var updated = await equipment.GetByIdAsync(TestTenant, equipmentId);
        Assert.NotNull(updated);
        Assert.Equal(220m, updated!.VehicleData!.CurrentOdometer);
    }

    [Fact]
    public async Task AppendAsync_Throws_WhenStartGreaterThanEnd()
    {
        var (equipment, trips) = NewServices();
        var equipmentId = new EquipmentId("VEH-3");
        await equipment.UpsertAsync(NewVehicle(equipmentId));

        await Assert.ThrowsAsync<System.ArgumentException>(async () =>
            await trips.AppendAsync(NewTripFor(equipmentId, start: 200m, end: 100m)));
    }

    private static TripRecord NewTrip(decimal start, decimal end)
        => NewTripFor(new EquipmentId("VEH-X"), start, end);

    private static TripRecord NewTripFor(EquipmentId equipmentId, decimal start, decimal end) =>
        new()
        {
            Id            = TripRecordId.NewId(),
            TenantId      = TestTenant,
            EquipmentId   = equipmentId,
            PropertyId    = TestProperty,
            TripDate      = DateTimeOffset.UnixEpoch,
            StartOdometer = start,
            EndOdometer   = end,
        };
}
