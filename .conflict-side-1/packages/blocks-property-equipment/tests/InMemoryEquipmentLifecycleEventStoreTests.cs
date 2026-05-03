using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Blocks.PropertyEquipment.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.PropertyEquipment.Tests;

public class InMemoryEquipmentLifecycleEventStoreTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");

    private static EquipmentLifecycleEvent NewEvent(TenantId tenant, EquipmentId equipment, PropertyId property, EquipmentLifecycleEventType type, DateTimeOffset occurredAt)
        => new()
        {
            EventId = Guid.NewGuid(),
            Equipment = equipment,
            Property = property,
            TenantId = tenant,
            EventType = type,
            OccurredAt = occurredAt,
            RecordedBy = "operator-1",
        };

    [Fact]
    public async Task GetForEquipmentAsync_returns_appended_events_chronologically()
    {
        var store = new InMemoryEquipmentLifecycleEventStore();
        var equipment = EquipmentId.NewId();
        var property = PropertyId.NewId();
        var earlier = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var later = new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero);

        // Append out of order
        await store.AppendAsync(NewEvent(TenantA, equipment, property, EquipmentLifecycleEventType.Serviced, later));
        await store.AppendAsync(NewEvent(TenantA, equipment, property, EquipmentLifecycleEventType.Installed, earlier));

        var events = await store.GetForEquipmentAsync(TenantA, equipment);

        Assert.Equal(2, events.Count);
        Assert.Equal(EquipmentLifecycleEventType.Installed, events[0].EventType);
        Assert.Equal(EquipmentLifecycleEventType.Serviced, events[1].EventType);
    }

    [Fact]
    public async Task GetForEquipmentAsync_returns_empty_for_unknown_equipment()
    {
        var store = new InMemoryEquipmentLifecycleEventStore();
        var events = await store.GetForEquipmentAsync(TenantA, EquipmentId.NewId());
        Assert.Empty(events);
    }

    [Fact]
    public async Task GetForEquipmentAsync_isolates_tenants()
    {
        var store = new InMemoryEquipmentLifecycleEventStore();
        var equipment = EquipmentId.NewId();
        var property = PropertyId.NewId();
        await store.AppendAsync(NewEvent(TenantA, equipment, property, EquipmentLifecycleEventType.Installed, DateTimeOffset.UtcNow));

        var fromB = await store.GetForEquipmentAsync(TenantB, equipment);

        Assert.Empty(fromB);
    }

    [Fact]
    public async Task GetForPropertyAsync_returns_events_across_equipment_items_chronologically()
    {
        var store = new InMemoryEquipmentLifecycleEventStore();
        var property = PropertyId.NewId();
        var equipmentA = EquipmentId.NewId();
        var equipmentB = EquipmentId.NewId();

        await store.AppendAsync(NewEvent(TenantA, equipmentA, property, EquipmentLifecycleEventType.Installed, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        await store.AppendAsync(NewEvent(TenantA, equipmentB, property, EquipmentLifecycleEventType.Installed, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)));
        await store.AppendAsync(NewEvent(TenantA, equipmentA, property, EquipmentLifecycleEventType.Serviced, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)));

        var events = await store.GetForPropertyAsync(TenantA, property);

        Assert.Equal(3, events.Count);
        Assert.True(events[0].OccurredAt < events[1].OccurredAt);
        Assert.True(events[1].OccurredAt < events[2].OccurredAt);
    }

    [Fact]
    public async Task GetForPropertyAsync_isolates_tenants()
    {
        var store = new InMemoryEquipmentLifecycleEventStore();
        var property = PropertyId.NewId();
        await store.AppendAsync(NewEvent(TenantA, EquipmentId.NewId(), property, EquipmentLifecycleEventType.Installed, DateTimeOffset.UtcNow));

        var fromB = await store.GetForPropertyAsync(TenantB, property);

        Assert.Empty(fromB);
    }

    [Fact]
    public async Task AppendAsync_throws_on_null_event()
    {
        var store = new InMemoryEquipmentLifecycleEventStore();
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.AppendAsync(null!));
    }
}
