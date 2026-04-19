using System.Text.Json;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.Assets.Entities;

namespace Sunfish.Foundation.Tests.Assets.Entities;

/// <summary>
/// Tests for <see cref="IEntityStore.CreateBatchAsync"/> covering the atomic
/// <see cref="InMemoryEntityStore"/> override (G21).
/// </summary>
public sealed class CreateBatchAsyncTests
{
    private static readonly SchemaId Schema = new("property.v1");

    private static JsonDocument Body(string json) => JsonDocument.Parse(json);

    private static CreateOptions Opts(string nonce, string authority = "acme", string issuer = "alice")
        => new("property", authority, nonce, new ActorId(issuer), TenantId.Default);

    private static (InMemoryEntityStore store, InMemoryAssetStorage storage) NewStore()
    {
        var storage = new InMemoryAssetStorage();
        return (new InMemoryEntityStore(storage), storage);
    }

    // -------------------------------------------------------------------------
    // Empty batch
    // -------------------------------------------------------------------------

    [Fact]
    public async Task EmptyBatch_ReturnsEmptyList_AndDoesNotMutateStore()
    {
        var (store, storage) = NewStore();
        var result = await store.CreateBatchAsync(Array.Empty<EntityDraft>());
        Assert.Empty(result);
        Assert.Empty(storage.Entities);
    }

    // -------------------------------------------------------------------------
    // Happy-path: batch creates all entities
    // -------------------------------------------------------------------------

    [Fact]
    public async Task BatchCreatesAllEntities_OnSuccess()
    {
        var (store, storage) = NewStore();
        var drafts = Enumerable.Range(1, 5).Select(i =>
            new EntityDraft(Schema, Body($"{{\"i\":{i}}}"), Opts($"nonce-{i}"))).ToList();

        var ids = await store.CreateBatchAsync(drafts);

        Assert.Equal(5, ids.Count);
        // Every id should be findable.
        foreach (var id in ids)
        {
            var entity = await store.GetAsync(id);
            Assert.NotNull(entity);
        }
        Assert.Equal(5, storage.Entities.Count);
    }

    [Fact]
    public async Task BatchReturnsIds_InSameOrderAsDrafts()
    {
        var (store, _) = NewStore();
        var drafts = Enumerable.Range(1, 3).Select(i =>
            new EntityDraft(Schema, Body($"{{\"i\":{i}}}"), Opts($"nonce-{i}"))).ToList();

        var ids = await store.CreateBatchAsync(drafts);

        // Each id must match what a solo CreateAsync would return for the same draft.
        for (int i = 0; i < drafts.Count; i++)
        {
            var soloStore = NewStore().store;
            var expectedId = await soloStore.CreateAsync(drafts[i].Schema, drafts[i].Body, drafts[i].Options);
            Assert.Equal(expectedId, ids[i]);
        }
    }

    // -------------------------------------------------------------------------
    // Rollback: mid-batch failure leaves store unchanged
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MidBatchFailure_RollsBackAllInserts()
    {
        var (store, storage) = NewStore();

        // Pre-seed one entity with body A.
        using var preBody = Body("""{"pre":true}""");
        var preOpts = Opts("pre-nonce");
        await store.CreateAsync(Schema, preBody, preOpts);
        var snapshotBefore = storage.Entities.Keys.ToHashSet();

        // Build a 10-draft batch where draft[5] conflicts with an existing entity
        // using the SAME nonce but a DIFFERENT body — this triggers IdempotencyConflictException.
        var conflictOpts = Opts("pre-nonce"); // same nonce → same id
        var drafts = Enumerable.Range(0, 10).Select(i =>
        {
            if (i == 5)
            {
                // Different body, same id → conflict.
                return new EntityDraft(Schema, Body("""{"conflict":true}"""), conflictOpts);
            }
            return new EntityDraft(Schema, Body($"{{\"i\":{i}}}"), Opts($"batch-nonce-{i}"));
        }).ToList();

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            store.CreateBatchAsync(drafts));

        // (a) No entities from the batch are visible.
        var snapshotAfter = storage.Entities.Keys.ToHashSet();
        Assert.Equal(snapshotBefore, snapshotAfter);

        // (b) Store state exactly matches the pre-batch snapshot.
        Assert.Equal(snapshotBefore.Count, snapshotAfter.Count);
        foreach (var id in snapshotBefore)
            Assert.Contains(id, snapshotAfter);
    }

    [Fact]
    public async Task MidBatchFailure_StoreIsEmpty_WhenNothingPreexisted()
    {
        var (store, storage) = NewStore();

        // Pre-seed entity that will collide at draft[3].
        using var preBody = Body("""{"name":"conflict-target"}""");
        var collisionOpts = Opts("collision");
        var collisionId = await store.CreateAsync(Schema, preBody, collisionOpts);

        // Build batch: drafts 0-2 are clean, draft 3 re-uses the same nonce with different body.
        var drafts = new List<EntityDraft>
        {
            new(Schema, Body("""{"x":0}"""), Opts("b-0")),
            new(Schema, Body("""{"x":1}"""), Opts("b-1")),
            new(Schema, Body("""{"x":2}"""), Opts("b-2")),
            // Same nonce as pre-seeded entity, different body → conflict.
            new(Schema, Body("""{"name":"different"}"""), collisionOpts),
        };

        await Assert.ThrowsAsync<IdempotencyConflictException>(() =>
            store.CreateBatchAsync(drafts));

        // Drafts b-0, b-1, b-2 must NOT exist.
        foreach (var nonce in new[] { "b-0", "b-1", "b-2" })
        {
            var id = InMemoryEntityStore.DeriveEntityId(Schema, Opts(nonce));
            Assert.Null(await store.GetAsync(id));
        }

        // The pre-existing collision target must still be intact.
        var still = await store.GetAsync(collisionId);
        Assert.NotNull(still);
    }

    // -------------------------------------------------------------------------
    // Idempotency: duplicate draft in batch (same nonce, same body) is allowed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IdempotentDraftInBatch_IsAccepted()
    {
        var (store, storage) = NewStore();

        // Pre-seed.
        using var body = Body("""{"v":1}""");
        var opts = Opts("idempotent-nonce");
        var existingId = await store.CreateAsync(Schema, body, opts);

        // Batch contains a draft that exactly matches the pre-existing entity.
        using var sameBody = Body("""{"v":1}""");
        var drafts = new List<EntityDraft>
        {
            new(Schema, Body("""{"new":true}"""), Opts("new-nonce")),
            new(Schema, sameBody, opts), // idempotent re-hit
        };

        var ids = await store.CreateBatchAsync(drafts);

        Assert.Equal(2, ids.Count);
        Assert.Equal(existingId, ids[1]);
        // Pre-existing entity must be unchanged (still 1 version).
        Assert.Single(storage.Versions[existingId]);
    }

    // -------------------------------------------------------------------------
    // Per-draft CreateOptions (ValidFrom, ExplicitLocalPart, etc.) are honoured
    // -------------------------------------------------------------------------

    [Fact]
    public async Task PerDraftOptions_AreHonoured()
    {
        var (store, storage) = NewStore();
        var t0 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t1 = new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);

        var drafts = new List<EntityDraft>
        {
            new(Schema, Body("""{"a":1}"""),
                new CreateOptions("property", "acme", "n-a", new ActorId("alice"), TenantId.Default, ValidFrom: t0)),
            new(Schema, Body("""{"b":2}"""),
                new CreateOptions("property", "acme", "n-b", new ActorId("bob"), TenantId.Default, ValidFrom: t1,
                    ExplicitLocalPart: "building-99")),
        };

        var ids = await store.CreateBatchAsync(drafts);

        Assert.Equal(2, ids.Count);
        Assert.Equal("building-99", ids[1].LocalPart);

        var entityA = await store.GetAsync(ids[0]);
        var entityB = await store.GetAsync(ids[1]);
        Assert.NotNull(entityA);
        Assert.NotNull(entityB);
        Assert.Equal(t0, entityA!.CreatedAt);
        Assert.Equal(t1, entityB!.CreatedAt);
    }

    // -------------------------------------------------------------------------
    // Concurrent batches: no lost writes
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentBatches_NeitherLosesWrites()
    {
        var (store, storage) = NewStore();
        const int batchSize = 20;
        const int parallelism = 4;

        // Each parallel task gets its own non-overlapping nonce range.
        var tasks = Enumerable.Range(0, parallelism).Select(task =>
            Task.Run(async () =>
            {
                var drafts = Enumerable.Range(0, batchSize).Select(i =>
                    new EntityDraft(Schema, Body($"{{\"t\":{task},\"i\":{i}}}"),
                        Opts($"t{task}-n{i}"))).ToList();
                return await store.CreateBatchAsync(drafts);
            })).ToArray();

        var allResults = await Task.WhenAll(tasks);

        // Total entities: parallelism * batchSize (all non-overlapping).
        var allIds = allResults.SelectMany(r => r).ToList();
        Assert.Equal(parallelism * batchSize, allIds.Count);
        Assert.Equal(allIds.Count, allIds.Distinct().Count()); // no duplicates
        Assert.Equal(parallelism * batchSize, storage.Entities.Count);
    }
}
