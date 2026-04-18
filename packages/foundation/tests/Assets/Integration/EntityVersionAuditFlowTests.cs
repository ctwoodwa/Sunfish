using System.Text.Json;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;

namespace Sunfish.Foundation.Tests.Assets.Integration;

/// <summary>
/// End-to-end flows across <see cref="IEntityStore"/>, IVersionStore and <see cref="IAuditLog"/>.
/// </summary>
public sealed class EntityVersionAuditFlowTests
{
    private static readonly SchemaId Schema = new("thing.v1");
    private static readonly ActorId Actor = new("tester");
    private static readonly TenantId Tenant = new("acme");

    private static (InMemoryAssetStorage storage, InMemoryEntityStore entities, InMemoryAuditLog audit) New()
    {
        var storage = new InMemoryAssetStorage();
        return (storage, new InMemoryEntityStore(storage), new InMemoryAuditLog(storage));
    }

    private static async Task<EntityId> MintAsync(InMemoryEntityStore store, InMemoryAuditLog audit, string nonce, DateTimeOffset at)
    {
        using var body = JsonDocument.Parse("""{"state":"new"}""");
        var id = await store.CreateAsync(Schema, body,
            new CreateOptions("thing", "acme", nonce, Actor, Tenant, ValidFrom: at));
        await audit.AppendAsync(new AuditAppend(id, null, Op.Mint, Actor, Tenant, at,
            JsonDocument.Parse("""{"evt":"mint"}""")));
        return id;
    }

    [Fact]
    public async Task FullFlow_CreateEntity_AppendsMintAuditRecord_AndFirstVersion()
    {
        var (_, entities, audit) = New();
        var t = DateTimeOffset.UtcNow;
        var id = await MintAsync(entities, audit, "n1", t);

        var ent = await entities.GetAsync(id);
        Assert.NotNull(ent);
        Assert.Equal(1, ent!.CurrentVersion.Sequence);

        var recs = new List<AuditRecord>();
        await foreach (var r in audit.QueryAsync(new AuditQuery(Entity: id))) recs.Add(r);
        Assert.Single(recs);
        Assert.Equal(Op.Mint, recs[0].Op);
    }

    [Fact]
    public async Task FullFlow_UpdateEntity_AppendsWriteAudit_AndBumpsVersion()
    {
        var (_, entities, audit) = New();
        var t0 = DateTimeOffset.UtcNow;
        var id = await MintAsync(entities, audit, "n1", t0);
        await entities.UpdateAsync(id, JsonDocument.Parse("""{"state":"live"}"""),
            new UpdateOptions(Actor, ValidFrom: t0.AddSeconds(1)));
        await audit.AppendAsync(new AuditAppend(id, null, Op.Write, Actor, Tenant, t0.AddSeconds(1),
            JsonDocument.Parse("""{"field":"state"}""")));

        var ent = await entities.GetAsync(id);
        Assert.Equal(2, ent!.CurrentVersion.Sequence);

        Assert.True(await audit.VerifyChainAsync(id));
    }

    [Fact]
    public async Task FullFlow_DeleteEntity_AppendsDeleteAudit_AndTombstonesLatestVersion()
    {
        var (_, entities, audit) = New();
        var t0 = DateTimeOffset.UtcNow;
        var id = await MintAsync(entities, audit, "n1", t0);
        await entities.DeleteAsync(id, new DeleteOptions(Actor, ValidFrom: t0.AddSeconds(1)));
        await audit.AppendAsync(new AuditAppend(id, null, Op.Delete, Actor, Tenant, t0.AddSeconds(1),
            JsonDocument.Parse("""{"evt":"delete"}""")));

        Assert.Null(await entities.GetAsync(id));
        Assert.True(await audit.VerifyChainAsync(id));
    }

    [Fact]
    public async Task FullFlow_QueryAsOf_ReturnsConsistentEntity()
    {
        var (_, entities, audit) = New();
        var t0 = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var id = await MintAsync(entities, audit, "n1", t0);
        await entities.UpdateAsync(id, JsonDocument.Parse("""{"state":"live"}"""),
            new UpdateOptions(Actor, ValidFrom: t0.AddDays(30)));

        var ent = await entities.GetAsync(id, new VersionSelector(AsOf: t0.AddDays(10)));
        Assert.NotNull(ent);
        Assert.Equal(1, ent!.CurrentVersion.Sequence);
    }

    [Fact]
    public async Task FullFlow_AuditChain_VerifiesAfter_100MutationsToSameEntity()
    {
        var (_, entities, audit) = New();
        var t = DateTimeOffset.UtcNow;
        var id = await MintAsync(entities, audit, "nx", t);
        for (int i = 0; i < 100; i++)
        {
            await entities.UpdateAsync(id,
                JsonDocument.Parse($$"""{"i":{{i}}}"""),
                new UpdateOptions(Actor, ValidFrom: t.AddSeconds(i + 1)));
            await audit.AppendAsync(new AuditAppend(id, null, Op.Write, Actor, Tenant,
                t.AddSeconds(i + 1), JsonDocument.Parse($$"""{"i":{{i}}}""")));
        }

        Assert.True(await audit.VerifyChainAsync(id));
    }

    [Fact]
    public async Task FullFlow_GetHistoryCount_MatchesMutations()
    {
        var (storage, entities, audit) = New();
        var t = DateTimeOffset.UtcNow;
        var id = await MintAsync(entities, audit, "n", t);
        await entities.UpdateAsync(id, JsonDocument.Parse("""{"v":2}"""),
            new UpdateOptions(Actor, ValidFrom: t.AddSeconds(1)));
        await entities.UpdateAsync(id, JsonDocument.Parse("""{"v":3}"""),
            new UpdateOptions(Actor, ValidFrom: t.AddSeconds(2)));
        await entities.DeleteAsync(id, new DeleteOptions(Actor, ValidFrom: t.AddSeconds(3)));

        Assert.Equal(4, storage.Versions[id].Count);
    }
}
