using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyEquipment.Models;
using Sunfish.Blocks.PropertyEquipment.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.PropertyEquipment.Tests;

public class InMemoryEquipmentRepositoryTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");

    private static (InMemoryEquipmentRepository repo, InMemoryEquipmentLifecycleEventStore events) NewSubject()
    {
        var events = new InMemoryEquipmentLifecycleEventStore();
        var repo = new InMemoryEquipmentRepository(events);
        return (repo, events);
    }

    private static Equipment NewEquipment(TenantId tenant, PropertyId property, EquipmentClass cls, string name)
        => new()
        {
            Id = EquipmentId.NewId(),
            TenantId = tenant,
            Property = property,
            Class = cls,
            DisplayName = name,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    [Fact]
    public async Task Upsert_then_GetByIdAsync_round_trips()
    {
        var (repo, _) = NewSubject();
        var property = PropertyId.NewId();
        var equipment = NewEquipment(TenantA, property, EquipmentClass.WaterHeater, "Heater");
        await repo.UpsertAsync(equipment);
        Assert.Equal(equipment, await repo.GetByIdAsync(TenantA, equipment.Id));
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_unknown_id()
    {
        var (repo, _) = NewSubject();
        Assert.Null(await repo.GetByIdAsync(TenantA, EquipmentId.NewId()));
    }

    [Fact]
    public async Task GetByIdAsync_isolates_tenants()
    {
        var (repo, _) = NewSubject();
        var property = PropertyId.NewId();
        var equipment = NewEquipment(TenantA, property, EquipmentClass.HVAC, "HVAC");
        await repo.UpsertAsync(equipment);
        Assert.Null(await repo.GetByIdAsync(TenantB, equipment.Id));
    }

    [Fact]
    public async Task ListByPropertyAsync_returns_only_equipment_attached_to_that_property()
    {
        var (repo, _) = NewSubject();
        var p1 = PropertyId.NewId();
        var p2 = PropertyId.NewId();
        await repo.UpsertAsync(NewEquipment(TenantA, p1, EquipmentClass.WaterHeater, "p1-wh"));
        await repo.UpsertAsync(NewEquipment(TenantA, p1, EquipmentClass.HVAC, "p1-hvac"));
        await repo.UpsertAsync(NewEquipment(TenantA, p2, EquipmentClass.WaterHeater, "p2-wh"));

        var p1Equipment = await repo.ListByPropertyAsync(TenantA, p1);
        Assert.Equal(2, p1Equipment.Count);
        Assert.All(p1Equipment, e => Assert.Equal(p1, e.Property));
    }

    [Fact]
    public async Task ListByPropertyAsync_isolates_tenants()
    {
        var (repo, _) = NewSubject();
        var property = PropertyId.NewId();
        await repo.UpsertAsync(NewEquipment(TenantA, property, EquipmentClass.HVAC, "A-HVAC"));

        var fromB = await repo.ListByPropertyAsync(TenantB, property);
        Assert.Empty(fromB);
    }

    [Fact]
    public async Task ListByTenantAsync_returns_only_owning_tenants_equipment()
    {
        var (repo, _) = NewSubject();
        await repo.UpsertAsync(NewEquipment(TenantA, PropertyId.NewId(), EquipmentClass.WaterHeater, "A-1"));
        await repo.UpsertAsync(NewEquipment(TenantA, PropertyId.NewId(), EquipmentClass.HVAC, "A-2"));
        await repo.UpsertAsync(NewEquipment(TenantB, PropertyId.NewId(), EquipmentClass.WaterHeater, "B-1"));

        Assert.Equal(2, (await repo.ListByTenantAsync(TenantA)).Count);
        Assert.Single(await repo.ListByTenantAsync(TenantB));
    }

    [Fact]
    public async Task ListByClassAsync_filters_to_class()
    {
        var (repo, _) = NewSubject();
        var property = PropertyId.NewId();
        await repo.UpsertAsync(NewEquipment(TenantA, property, EquipmentClass.WaterHeater, "WH"));
        await repo.UpsertAsync(NewEquipment(TenantA, property, EquipmentClass.HVAC, "HVAC"));
        await repo.UpsertAsync(NewEquipment(TenantA, property, EquipmentClass.HVAC, "HVAC2"));

        Assert.Single(await repo.ListByClassAsync(TenantA, EquipmentClass.WaterHeater));
        Assert.Equal(2, (await repo.ListByClassAsync(TenantA, EquipmentClass.HVAC)).Count);
    }

    [Fact]
    public async Task SoftDeleteAsync_stamps_disposal_and_emits_Disposed_lifecycle_event()
    {
        var (repo, events) = NewSubject();
        var property = PropertyId.NewId();
        var equipment = NewEquipment(TenantA, property, EquipmentClass.WaterHeater, "WH");
        await repo.UpsertAsync(equipment);
        var disposedAt = new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero);

        await repo.SoftDeleteAsync(TenantA, equipment.Id, "replaced with tankless", disposedAt, "operator-1");

        var fetched = await repo.GetByIdAsync(TenantA, equipment.Id);
        Assert.NotNull(fetched);
        Assert.Equal(disposedAt, fetched!.DisposedAt);
        Assert.Equal("replaced with tankless", fetched.DisposalReason);

        var emitted = await events.GetForEquipmentAsync(TenantA, equipment.Id);
        Assert.Single(emitted);
        Assert.Equal(EquipmentLifecycleEventType.Disposed, emitted[0].EventType);
        Assert.Equal(property, emitted[0].Property);
        Assert.Equal("operator-1", emitted[0].RecordedBy);
        Assert.Equal("replaced with tankless", emitted[0].Notes);
    }

    [Fact]
    public async Task SoftDeleteAsync_excludes_from_default_List_includes_with_flag()
    {
        var (repo, _) = NewSubject();
        var property = PropertyId.NewId();
        var live = NewEquipment(TenantA, property, EquipmentClass.WaterHeater, "Live");
        var disposed = NewEquipment(TenantA, property, EquipmentClass.WaterHeater, "Disposed");
        await repo.UpsertAsync(live);
        await repo.UpsertAsync(disposed);
        await repo.SoftDeleteAsync(TenantA, disposed.Id, "replaced", DateTimeOffset.UtcNow, "operator-1");

        Assert.Single(await repo.ListByPropertyAsync(TenantA, property));
        Assert.Equal(2, (await repo.ListByPropertyAsync(TenantA, property, includeDisposed: true)).Count);
    }

    [Fact]
    public async Task SoftDeleteAsync_does_not_cross_tenants()
    {
        var (repo, events) = NewSubject();
        var property = PropertyId.NewId();
        var equipment = NewEquipment(TenantA, property, EquipmentClass.HVAC, "HVAC");
        await repo.UpsertAsync(equipment);

        await repo.SoftDeleteAsync(TenantB, equipment.Id, "wrong tenant", DateTimeOffset.UtcNow, "operator-2");

        var fetched = await repo.GetByIdAsync(TenantA, equipment.Id);
        Assert.Null(fetched!.DisposedAt);
        Assert.Empty(await events.GetForEquipmentAsync(TenantA, equipment.Id));
    }

    [Fact]
    public async Task SoftDeleteAsync_throws_on_blank_reason_or_recorder()
    {
        var (repo, _) = NewSubject();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repo.SoftDeleteAsync(TenantA, EquipmentId.NewId(), "  ", DateTimeOffset.UtcNow, "op"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repo.SoftDeleteAsync(TenantA, EquipmentId.NewId(), "reason", DateTimeOffset.UtcNow, "  "));
    }

    [Fact]
    public async Task SoftDeleteAsync_no_op_for_unknown_id()
    {
        var (repo, events) = NewSubject();
        await repo.SoftDeleteAsync(TenantA, EquipmentId.NewId(), "reason", DateTimeOffset.UtcNow, "op");
        Assert.Empty(await events.GetForEquipmentAsync(TenantA, EquipmentId.NewId()));
    }

    [Fact]
    public async Task UpsertAsync_overwrites_prior_record_with_same_key()
    {
        var (repo, _) = NewSubject();
        var v1 = NewEquipment(TenantA, PropertyId.NewId(), EquipmentClass.WaterHeater, "Original");
        await repo.UpsertAsync(v1);

        var v2 = v1 with { DisplayName = "Updated" };
        await repo.UpsertAsync(v2);

        var fetched = await repo.GetByIdAsync(TenantA, v1.Id);
        Assert.Equal("Updated", fetched!.DisplayName);
    }
}
