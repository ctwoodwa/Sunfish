using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sunfish.Foundation.Assets.Audit;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Postgres.Audit;

namespace Sunfish.Foundation.Assets.Postgres.Tests;

public sealed class PostgresAuditLogTests : IClassFixture<PostgresAssetStoreFixture>
{
    private readonly PostgresAssetStoreFixture _fixture;

    public PostgresAuditLogTests(PostgresAssetStoreFixture fixture)
    {
        _fixture = fixture;
    }

    private PostgresAuditLog NewLog() => new(_fixture.CreateFactory());

    private static AuditAppend Append(EntityId entity, Op op, string actor = "alice")
        => new(
            EntityId: entity,
            VersionId: null,
            Op: op,
            Actor: new ActorId(actor),
            Tenant: TenantId.Default,
            At: DateTimeOffset.UtcNow,
            Payload: JsonDocument.Parse($$"""{"op":"{{op}}"}"""),
            Justification: "test");

    [Fact]
    public async Task AppendAsync_ChainsHashesAcrossMultipleRecords()
    {
        var log = NewLog();
        var entity = new EntityId("audit-chain", "acme", $"e-{Guid.NewGuid():N}");

        var id1 = await log.AppendAsync(Append(entity, Op.Mint));
        await Task.Delay(5);
        var id2 = await log.AppendAsync(Append(entity, Op.Write));
        await Task.Delay(5);
        var id3 = await log.AppendAsync(Append(entity, Op.Delete));

        var records = new List<AuditRecord>();
        await foreach (var r in log.QueryAsync(new AuditQuery(Entity: entity)))
            records.Add(r);

        Assert.Equal(3, records.Count);
        Assert.Null(records[0].Prev);
        Assert.Equal(records[0].Id, records[1].Prev);
        Assert.Equal(records[1].Id, records[2].Prev);
        Assert.Equal(id1, records[0].Id);
        Assert.Equal(id2, records[1].Id);
        Assert.Equal(id3, records[2].Id);
    }

    [Fact]
    public async Task VerifyChainAsync_ReturnsTrue_ForUntamperedChain()
    {
        var log = NewLog();
        var entity = new EntityId("audit-ok", "acme", $"e-{Guid.NewGuid():N}");

        await log.AppendAsync(Append(entity, Op.Mint));
        await log.AppendAsync(Append(entity, Op.Write));
        await log.AppendAsync(Append(entity, Op.Correct));

        Assert.True(await log.VerifyChainAsync(entity));
    }

    [Fact]
    public async Task VerifyChainAsync_ReturnsFalse_WhenRecordIsTampered()
    {
        var log = NewLog();
        var entity = new EntityId("audit-tamper", "acme", $"e-{Guid.NewGuid():N}");

        await log.AppendAsync(Append(entity, Op.Mint));
        await log.AppendAsync(Append(entity, Op.Write));

        // Tamper: mutate the payload of the first record in the database directly.
        var factory = _fixture.CreateFactory();
        await using (var db = factory.CreateDbContext())
        {
            var row = await db.AuditRecords
                .Where(a => a.EntityScheme == entity.Scheme &&
                            a.EntityAuthority == entity.Authority &&
                            a.EntityLocalPart == entity.LocalPart)
                .OrderBy(a => a.At)
                .FirstAsync();
            row.PayloadJson = """{"op":"TAMPERED"}""";
            await db.SaveChangesAsync();
        }

        Assert.False(await log.VerifyChainAsync(entity));
    }

    [Fact]
    public async Task QueryAsync_PreservesInsertionOrder_AndFiltersByOp()
    {
        var log = NewLog();
        var entity = new EntityId("audit-filter", "acme", $"e-{Guid.NewGuid():N}");

        await log.AppendAsync(Append(entity, Op.Mint));
        await Task.Delay(5);
        await log.AppendAsync(Append(entity, Op.Write));
        await Task.Delay(5);
        await log.AppendAsync(Append(entity, Op.Write));
        await Task.Delay(5);
        await log.AppendAsync(Append(entity, Op.Delete));

        var all = new List<AuditRecord>();
        await foreach (var r in log.QueryAsync(new AuditQuery(Entity: entity)))
            all.Add(r);

        Assert.Equal(4, all.Count);
        // Ordered by time ascending.
        for (int i = 1; i < all.Count; i++)
            Assert.True(all[i].At >= all[i - 1].At);

        var writesOnly = new List<AuditRecord>();
        await foreach (var r in log.QueryAsync(new AuditQuery(Entity: entity, Op: Op.Write)))
            writesOnly.Add(r);
        Assert.Equal(2, writesOnly.Count);
        Assert.All(writesOnly, r => Assert.Equal(Op.Write, r.Op));
    }
}
