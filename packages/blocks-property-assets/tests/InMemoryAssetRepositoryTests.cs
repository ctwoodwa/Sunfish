using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyAssets.Models;
using Sunfish.Blocks.PropertyAssets.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.PropertyAssets.Tests;

public class InMemoryAssetRepositoryTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");

    private static (InMemoryAssetRepository repo, InMemoryAssetLifecycleEventStore events) NewSubject()
    {
        var events = new InMemoryAssetLifecycleEventStore();
        var repo = new InMemoryAssetRepository(events);
        return (repo, events);
    }

    private static Asset NewAsset(TenantId tenant, PropertyId property, AssetClass cls, string name)
        => new()
        {
            Id = AssetId.NewId(),
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
        var asset = NewAsset(TenantA, property, AssetClass.WaterHeater, "Heater");
        await repo.UpsertAsync(asset);
        Assert.Equal(asset, await repo.GetByIdAsync(TenantA, asset.Id));
    }

    [Fact]
    public async Task GetByIdAsync_returns_null_for_unknown_id()
    {
        var (repo, _) = NewSubject();
        Assert.Null(await repo.GetByIdAsync(TenantA, AssetId.NewId()));
    }

    [Fact]
    public async Task GetByIdAsync_isolates_tenants()
    {
        var (repo, _) = NewSubject();
        var property = PropertyId.NewId();
        var asset = NewAsset(TenantA, property, AssetClass.HVAC, "HVAC");
        await repo.UpsertAsync(asset);
        Assert.Null(await repo.GetByIdAsync(TenantB, asset.Id));
    }

    [Fact]
    public async Task ListByPropertyAsync_returns_only_assets_attached_to_that_property()
    {
        var (repo, _) = NewSubject();
        var p1 = PropertyId.NewId();
        var p2 = PropertyId.NewId();
        await repo.UpsertAsync(NewAsset(TenantA, p1, AssetClass.WaterHeater, "p1-wh"));
        await repo.UpsertAsync(NewAsset(TenantA, p1, AssetClass.HVAC, "p1-hvac"));
        await repo.UpsertAsync(NewAsset(TenantA, p2, AssetClass.WaterHeater, "p2-wh"));

        var p1Assets = await repo.ListByPropertyAsync(TenantA, p1);
        Assert.Equal(2, p1Assets.Count);
        Assert.All(p1Assets, a => Assert.Equal(p1, a.Property));
    }

    [Fact]
    public async Task ListByPropertyAsync_isolates_tenants()
    {
        var (repo, _) = NewSubject();
        var property = PropertyId.NewId();
        await repo.UpsertAsync(NewAsset(TenantA, property, AssetClass.HVAC, "A-HVAC"));

        var fromB = await repo.ListByPropertyAsync(TenantB, property);
        Assert.Empty(fromB);
    }

    [Fact]
    public async Task ListByTenantAsync_returns_only_owning_tenants_assets()
    {
        var (repo, _) = NewSubject();
        await repo.UpsertAsync(NewAsset(TenantA, PropertyId.NewId(), AssetClass.WaterHeater, "A-1"));
        await repo.UpsertAsync(NewAsset(TenantA, PropertyId.NewId(), AssetClass.HVAC, "A-2"));
        await repo.UpsertAsync(NewAsset(TenantB, PropertyId.NewId(), AssetClass.WaterHeater, "B-1"));

        Assert.Equal(2, (await repo.ListByTenantAsync(TenantA)).Count);
        Assert.Single(await repo.ListByTenantAsync(TenantB));
    }

    [Fact]
    public async Task ListByClassAsync_filters_to_class()
    {
        var (repo, _) = NewSubject();
        var property = PropertyId.NewId();
        await repo.UpsertAsync(NewAsset(TenantA, property, AssetClass.WaterHeater, "WH"));
        await repo.UpsertAsync(NewAsset(TenantA, property, AssetClass.HVAC, "HVAC"));
        await repo.UpsertAsync(NewAsset(TenantA, property, AssetClass.HVAC, "HVAC2"));

        Assert.Single(await repo.ListByClassAsync(TenantA, AssetClass.WaterHeater));
        Assert.Equal(2, (await repo.ListByClassAsync(TenantA, AssetClass.HVAC)).Count);
    }

    [Fact]
    public async Task SoftDeleteAsync_stamps_disposal_and_emits_Disposed_lifecycle_event()
    {
        var (repo, events) = NewSubject();
        var property = PropertyId.NewId();
        var asset = NewAsset(TenantA, property, AssetClass.WaterHeater, "WH");
        await repo.UpsertAsync(asset);
        var disposedAt = new DateTimeOffset(2026, 4, 28, 12, 0, 0, TimeSpan.Zero);

        await repo.SoftDeleteAsync(TenantA, asset.Id, "replaced with tankless", disposedAt, "operator-1");

        var fetched = await repo.GetByIdAsync(TenantA, asset.Id);
        Assert.NotNull(fetched);
        Assert.Equal(disposedAt, fetched!.DisposedAt);
        Assert.Equal("replaced with tankless", fetched.DisposalReason);

        var emitted = await events.GetForAssetAsync(TenantA, asset.Id);
        Assert.Single(emitted);
        Assert.Equal(AssetLifecycleEventType.Disposed, emitted[0].EventType);
        Assert.Equal(property, emitted[0].Property);
        Assert.Equal("operator-1", emitted[0].RecordedBy);
        Assert.Equal("replaced with tankless", emitted[0].Notes);
    }

    [Fact]
    public async Task SoftDeleteAsync_excludes_from_default_List_includes_with_flag()
    {
        var (repo, _) = NewSubject();
        var property = PropertyId.NewId();
        var live = NewAsset(TenantA, property, AssetClass.WaterHeater, "Live");
        var disposed = NewAsset(TenantA, property, AssetClass.WaterHeater, "Disposed");
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
        var asset = NewAsset(TenantA, property, AssetClass.HVAC, "HVAC");
        await repo.UpsertAsync(asset);

        await repo.SoftDeleteAsync(TenantB, asset.Id, "wrong tenant", DateTimeOffset.UtcNow, "operator-2");

        var fetched = await repo.GetByIdAsync(TenantA, asset.Id);
        Assert.Null(fetched!.DisposedAt);
        Assert.Empty(await events.GetForAssetAsync(TenantA, asset.Id));
    }

    [Fact]
    public async Task SoftDeleteAsync_throws_on_blank_reason_or_recorder()
    {
        var (repo, _) = NewSubject();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repo.SoftDeleteAsync(TenantA, AssetId.NewId(), "  ", DateTimeOffset.UtcNow, "op"));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            repo.SoftDeleteAsync(TenantA, AssetId.NewId(), "reason", DateTimeOffset.UtcNow, "  "));
    }

    [Fact]
    public async Task SoftDeleteAsync_no_op_for_unknown_id()
    {
        var (repo, events) = NewSubject();
        await repo.SoftDeleteAsync(TenantA, AssetId.NewId(), "reason", DateTimeOffset.UtcNow, "op");
        Assert.Empty(await events.GetForAssetAsync(TenantA, AssetId.NewId()));
    }

    [Fact]
    public async Task UpsertAsync_overwrites_prior_record_with_same_key()
    {
        var (repo, _) = NewSubject();
        var v1 = NewAsset(TenantA, PropertyId.NewId(), AssetClass.WaterHeater, "Original");
        await repo.UpsertAsync(v1);

        var v2 = v1 with { DisplayName = "Updated" };
        await repo.UpsertAsync(v2);

        var fetched = await repo.GetByIdAsync(TenantA, v1.Id);
        Assert.Equal("Updated", fetched!.DisplayName);
    }
}
