using System.Text.Json;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Tests.Assets.Audit;

public sealed class InMemoryAuditLogTests
{
    private static readonly EntityId Entity = new("property", "acme", "42");
    private static readonly ActorId Alice = new("alice");
    private static readonly ActorId Bob = new("bob");
    private static readonly TenantId T1 = new("t1");
    private static readonly TenantId T2 = new("t2");

    private static InMemoryAuditLog NewLog(out InMemoryAssetStorage storage)
    {
        storage = new InMemoryAssetStorage();
        return new InMemoryAuditLog(storage);
    }

    private static AuditAppend Mint(DateTimeOffset at, EntityId? entity = null, ActorId? actor = null, TenantId? tenant = null, Op op = Op.Mint)
    {
        var payload = JsonDocument.Parse("""{"evt":"mint"}""");
        return new AuditAppend(entity ?? Entity, null, op, actor ?? Alice, tenant ?? T1, at, payload);
    }

    [Fact]
    public async Task AppendAsync_StoresRecord()
    {
        var log = NewLog(out var storage);
        var id = await log.AppendAsync(Mint(DateTimeOffset.UtcNow));
        Assert.Equal(1, id.Value);
        Assert.Single(storage.Audit[Entity]);
    }

    [Fact]
    public async Task AppendAsync_LinksPrevToPreviousRecord()
    {
        var log = NewLog(out var storage);
        var id1 = await log.AppendAsync(Mint(DateTimeOffset.UtcNow));
        var id2 = await log.AppendAsync(Mint(DateTimeOffset.UtcNow.AddSeconds(1), op: Op.Write));
        Assert.Null(storage.Audit[Entity][0].Prev);
        Assert.Equal(id1, storage.Audit[Entity][1].Prev);
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public async Task AppendAsync_ComputesHashCorrectly()
    {
        var log = NewLog(out var storage);
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow));
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow.AddSeconds(1), op: Op.Write));
        var chain = storage.Audit[Entity].OrderBy(r => r.At).ThenBy(r => r.Id.Value).ToList();
        Assert.True(HashChain.Verify(chain));
    }

    [Fact]
    public async Task QueryAsync_FiltersByEntity()
    {
        var log = NewLog(out _);
        var other = new EntityId("property", "acme", "99");
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow));
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow, entity: other));

        var results = new List<AuditRecord>();
        await foreach (var r in log.QueryAsync(new AuditQuery(Entity: Entity))) results.Add(r);
        Assert.Single(results);
    }

    [Fact]
    public async Task QueryAsync_FiltersByActor()
    {
        var log = NewLog(out _);
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow, actor: Alice));
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow.AddSeconds(1), actor: Bob, op: Op.Write));

        var results = new List<AuditRecord>();
        await foreach (var r in log.QueryAsync(new AuditQuery(Actor: Alice))) results.Add(r);
        Assert.Single(results);
        Assert.Equal(Alice, results[0].Actor);
    }

    [Fact]
    public async Task QueryAsync_FiltersByTenant()
    {
        var log = NewLog(out _);
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow, tenant: T1));
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow.AddSeconds(1), tenant: T2, op: Op.Write));

        var results = new List<AuditRecord>();
        await foreach (var r in log.QueryAsync(new AuditQuery(Tenant: T2))) results.Add(r);
        Assert.Single(results);
        Assert.Equal(T2, results[0].Tenant);
    }

    [Fact]
    public async Task QueryAsync_FiltersByTimeRange()
    {
        var log = NewLog(out _);
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        await log.AppendAsync(Mint(t0));
        await log.AppendAsync(Mint(t0.AddDays(10), op: Op.Write));
        await log.AppendAsync(Mint(t0.AddDays(20), op: Op.Write));

        var results = new List<AuditRecord>();
        await foreach (var r in log.QueryAsync(new AuditQuery(
            FromInclusive: t0.AddDays(5), ToExclusive: t0.AddDays(15)))) results.Add(r);
        Assert.Single(results);
    }

    [Fact]
    public async Task VerifyChainAsync_ReturnsTrue_ForUnalteredChain()
    {
        var log = NewLog(out _);
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow));
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow.AddSeconds(1), op: Op.Write));
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow.AddSeconds(2), op: Op.Write));

        var ok = await log.VerifyChainAsync(Entity);
        Assert.True(ok);
    }

    [Fact]
    public async Task VerifyChainAsync_ReturnsFalse_AfterExternalMutation()
    {
        var log = NewLog(out var storage);
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow));
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow.AddSeconds(1), op: Op.Write));

        // Simulate tamper: replace the second record's payload with a mutated one but keep the hash.
        var original = storage.Audit[Entity][1];
        var tampered = original with { Payload = JsonDocument.Parse("""{"evt":"tampered"}""") };
        storage.Audit[Entity][1] = tampered;

        var ok = await log.VerifyChainAsync(Entity);
        Assert.False(ok);
    }

    [Fact]
    public async Task AppendAsync_IsThreadSafe_UnderConcurrency()
    {
        var log = NewLog(out var storage);
        const int n = 100;
        var baseline = DateTimeOffset.UtcNow;
        await Task.WhenAll(Enumerable.Range(0, n).Select(i =>
            log.AppendAsync(Mint(baseline.AddMilliseconds(i), op: i == 0 ? Op.Mint : Op.Write))));

        Assert.Equal(n, storage.Audit[Entity].Count);
        var ok = await log.VerifyChainAsync(Entity);
        Assert.True(ok);
    }

    [Fact]
    public async Task AppendAsync_AllocatesUniqueMonotonicIds()
    {
        var log = NewLog(out _);
        var id1 = await log.AppendAsync(Mint(DateTimeOffset.UtcNow));
        var id2 = await log.AppendAsync(Mint(DateTimeOffset.UtcNow.AddSeconds(1), op: Op.Write));
        var id3 = await log.AppendAsync(Mint(DateTimeOffset.UtcNow.AddSeconds(2), op: Op.Write));
        Assert.True(id1.Value < id2.Value);
        Assert.True(id2.Value < id3.Value);
    }

    [Fact]
    public async Task QueryAsync_AcrossAllEntities_WhenNoEntityFilter()
    {
        var log = NewLog(out _);
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow));
        await log.AppendAsync(Mint(DateTimeOffset.UtcNow.AddSeconds(1),
            entity: new EntityId("property", "acme", "other"), op: Op.Mint));

        var results = new List<AuditRecord>();
        await foreach (var r in log.QueryAsync(new AuditQuery())) results.Add(r);
        Assert.Equal(2, results.Count);
    }
}
