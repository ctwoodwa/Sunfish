using Sunfish.Blocks.Properties.Models;
using Sunfish.Blocks.PropertyAssets.Models;
using Sunfish.Blocks.PropertyAssets.Services;
using Sunfish.Foundation.Assets.Common;
using Xunit;

namespace Sunfish.Blocks.PropertyAssets.Tests;

public class InMemoryAssetLifecycleEventStoreTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");

    private static AssetLifecycleEvent NewEvent(TenantId tenant, AssetId asset, PropertyId property, AssetLifecycleEventType type, DateTimeOffset occurredAt)
        => new()
        {
            EventId = Guid.NewGuid(),
            Asset = asset,
            Property = property,
            TenantId = tenant,
            EventType = type,
            OccurredAt = occurredAt,
            RecordedBy = "operator-1",
        };

    [Fact]
    public async Task GetForAssetAsync_returns_appended_events_chronologically()
    {
        var store = new InMemoryAssetLifecycleEventStore();
        var asset = AssetId.NewId();
        var property = PropertyId.NewId();
        var earlier = new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero);
        var later = new DateTimeOffset(2026, 4, 15, 0, 0, 0, TimeSpan.Zero);

        // Append out of order
        await store.AppendAsync(NewEvent(TenantA, asset, property, AssetLifecycleEventType.Serviced, later));
        await store.AppendAsync(NewEvent(TenantA, asset, property, AssetLifecycleEventType.Installed, earlier));

        var events = await store.GetForAssetAsync(TenantA, asset);

        Assert.Equal(2, events.Count);
        Assert.Equal(AssetLifecycleEventType.Installed, events[0].EventType);
        Assert.Equal(AssetLifecycleEventType.Serviced, events[1].EventType);
    }

    [Fact]
    public async Task GetForAssetAsync_returns_empty_for_unknown_asset()
    {
        var store = new InMemoryAssetLifecycleEventStore();
        var events = await store.GetForAssetAsync(TenantA, AssetId.NewId());
        Assert.Empty(events);
    }

    [Fact]
    public async Task GetForAssetAsync_isolates_tenants()
    {
        var store = new InMemoryAssetLifecycleEventStore();
        var asset = AssetId.NewId();
        var property = PropertyId.NewId();
        await store.AppendAsync(NewEvent(TenantA, asset, property, AssetLifecycleEventType.Installed, DateTimeOffset.UtcNow));

        var fromB = await store.GetForAssetAsync(TenantB, asset);

        Assert.Empty(fromB);
    }

    [Fact]
    public async Task GetForPropertyAsync_returns_events_across_assets_chronologically()
    {
        var store = new InMemoryAssetLifecycleEventStore();
        var property = PropertyId.NewId();
        var assetA = AssetId.NewId();
        var assetB = AssetId.NewId();

        await store.AppendAsync(NewEvent(TenantA, assetA, property, AssetLifecycleEventType.Installed, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        await store.AppendAsync(NewEvent(TenantA, assetB, property, AssetLifecycleEventType.Installed, new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero)));
        await store.AppendAsync(NewEvent(TenantA, assetA, property, AssetLifecycleEventType.Serviced, new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)));

        var events = await store.GetForPropertyAsync(TenantA, property);

        Assert.Equal(3, events.Count);
        Assert.True(events[0].OccurredAt < events[1].OccurredAt);
        Assert.True(events[1].OccurredAt < events[2].OccurredAt);
    }

    [Fact]
    public async Task GetForPropertyAsync_isolates_tenants()
    {
        var store = new InMemoryAssetLifecycleEventStore();
        var property = PropertyId.NewId();
        await store.AppendAsync(NewEvent(TenantA, AssetId.NewId(), property, AssetLifecycleEventType.Installed, DateTimeOffset.UtcNow));

        var fromB = await store.GetForPropertyAsync(TenantB, property);

        Assert.Empty(fromB);
    }

    [Fact]
    public async Task AppendAsync_throws_on_null_event()
    {
        var store = new InMemoryAssetLifecycleEventStore();
        await Assert.ThrowsAsync<ArgumentNullException>(() => store.AppendAsync(null!));
    }
}
