using System.Text.Json;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;
using Sunfish.Foundation.MultiTenancy;
using Xunit;

namespace Sunfish.Foundation.Tests.Assets;

/// <summary>
/// ADR 0085: TenantSelection integration tests for InMemoryAuditLog +
/// InMemoryEntityStore. Verifies ForMultiple, AllAccessible, and null-filter
/// behaviours after the TenantId → TenantSelection migration.
/// </summary>
public sealed class TenantSelectionQueryTests
{
    private static readonly TenantId TenantA = new("tenant-a");
    private static readonly TenantId TenantB = new("tenant-b");
    private static readonly TenantId TenantC = new("tenant-c");
    private static readonly SchemaId Schema = new("property.v1");
    private static readonly EntityId Entity = new("property", "acme", "e1");
    private static readonly ActorId Actor = new("alice");
    private static readonly DateTimeOffset At = new(2026, 5, 13, 0, 0, 0, TimeSpan.Zero);

    private static JsonDocument Body() => JsonDocument.Parse("""{"x":1}""");

    private static CreateOptions Opts(string nonce, TenantId tenant) =>
        new("property", "acme", nonce, Actor, tenant);

    // ===== InMemoryEntityStore — ForMultiple =====

    [Fact]
    public async Task EntityQuery_ForMultiple_FiltersToSet()
    {
        var storage = new InMemoryAssetStorage();
        var store = new InMemoryEntityStore(storage);

        using var body = Body();
        await store.CreateAsync(Schema, body, Opts("n1", TenantA));
        await store.CreateAsync(Schema, body, Opts("n2", TenantB));
        await store.CreateAsync(Schema, body, Opts("n3", TenantC));

        var query = new EntityQuery(Tenant: TenantSelection.Of(TenantA, TenantB));
        var results = new System.Collections.Generic.List<Entity>();
        await foreach (var e in store.QueryAsync(query))
            results.Add(e);

        Assert.Equal(2, results.Count);
        Assert.All(results, e => Assert.True(
            e.Tenant == TenantA || e.Tenant == TenantB,
            $"Unexpected tenant: {e.Tenant}"));
    }

    [Fact]
    public async Task InMemoryEntityStore_NullTenant_ReturnsAllTenants()
    {
        var storage = new InMemoryAssetStorage();
        var store = new InMemoryEntityStore(storage);

        using var body = Body();
        await store.CreateAsync(Schema, body, Opts("n1", TenantA));
        await store.CreateAsync(Schema, body, Opts("n2", TenantB));

        var query = new EntityQuery(Tenant: null);
        int count = 0;
        await foreach (var _ in store.QueryAsync(query))
            count++;

        Assert.Equal(2, count);
    }

    // ===== InMemoryAuditLog — AllAccessible =====

    [Fact]
    public async Task AuditQuery_AllAccessible_NoFilter()
    {
        var storage = new InMemoryAssetStorage();
        var log = new InMemoryAuditLog(storage);
        using var payload = Body();

        await log.AppendAsync(new AuditAppend(Entity, null, Op.Mint, Actor, TenantA, At, payload));
        await log.AppendAsync(new AuditAppend(Entity, null, Op.Write, Actor, TenantB, At, payload));

        var query = new AuditQuery(Tenant: TenantSelection.All);
        int count = 0;
        await foreach (var _ in log.QueryAsync(query))
            count++;

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task AuditQuery_AllAccessible_ExcludesSystemSentinelRows()
    {
        var storage = new InMemoryAssetStorage();
        var log = new InMemoryAuditLog(storage);
        using var payload = Body();

        await log.AppendAsync(new AuditAppend(Entity, null, Op.Mint, Actor, TenantA, At, payload));
        await log.AppendAsync(new AuditAppend(Entity, null, Op.Mint, Actor, TenantId.System, At, payload));

        var query = new AuditQuery(Tenant: TenantSelection.All);
        int count = 0;
        await foreach (var _ in log.QueryAsync(query))
            count++;

        Assert.Equal(1, count);
    }

    // ===== TenantSelection.Matches =====

    [Fact]
    public void TenantSelection_Matches_ForMultiple_NonMember_ReturnsFalse()
    {
        var selection = TenantSelection.Of(TenantA, TenantB);

        Assert.False(selection.Matches(TenantC));
        Assert.True(selection.Matches(TenantA));
        Assert.True(selection.Matches(TenantB));
    }
}
