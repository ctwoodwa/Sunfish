using Sunfish.Ingestion.Core;
using Sunfish.Ingestion.Core.Middleware;
using Sunfish.Ingestion.Core.Quota;
using Xunit;

namespace Sunfish.Ingestion.Core.Tests;

/// <summary>
/// Tests for G27 — <see cref="InMemoryIngestionQuotaStore"/>,
/// <see cref="QuotaMiddleware{TInput}"/>, and <see cref="QuotaStatus"/>.
/// Covers:
/// <list type="bullet">
///   <item>Bucket starts at capacity.</item>
///   <item>Sequential consumes deplete the bucket.</item>
///   <item>Refill is applied after the interval elapses.</item>
///   <item>Concurrent access is thread-safe.</item>
///   <item>Different tenants get independent buckets.</item>
///   <item>Middleware short-circuits on exhaustion.</item>
///   <item>Middleware passes through when tokens are available.</item>
///   <item>TimeProvider-controlled clock governs refill.</item>
/// </list>
/// </summary>
public class QuotaMiddlewareTests
{
    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IngestedEntity StubEntity() =>
        new("e1", "schema/v1",
            new Dictionary<string, object?>(),
            Array.Empty<IngestedEvent>(),
            Array.Empty<Sunfish.Foundation.Blobs.Cid>());

    private static IngestionContext Ctx(string tenantId = "tenant-a") =>
        IngestionContext.NewCorrelation(tenantId, "actor-a");

    private static IngestionDelegate<string> AlwaysSuccess() =>
        (_, _, _) => new ValueTask<IngestionResult<IngestedEntity>>(
            IngestionResult<IngestedEntity>.Success(StubEntity()));

    /// <summary>
    /// Minimal controllable <see cref="TimeProvider"/> — advances time only when
    /// <see cref="Advance"/> is called explicitly.
    /// </summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;

        public ManualTimeProvider(DateTimeOffset startAt) => _now = startAt;

        public override DateTimeOffset GetUtcNow() => _now;

        /// <summary>Moves the clock forward by <paramref name="delta"/>.</summary>
        public void Advance(TimeSpan delta) => _now += delta;
    }

    // -------------------------------------------------------------------------
    // InMemoryIngestionQuotaStore — bucket lifecycle
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Bucket_StartsAtCapacity()
    {
        var store = new InMemoryIngestionQuotaStore(new QuotaPolicy(10, 5, TimeSpan.FromSeconds(1)));
        var status = await store.GetStatusAsync("t1", CancellationToken.None);

        Assert.Equal(10, status.AvailableTokens);
        Assert.Equal(10, status.Capacity);
        Assert.False(status.IsExhausted);
        Assert.Null(status.NextRefillAt); // full bucket — no scheduled refill
    }

    [Fact]
    public async Task TryConsumeAsync_SingleToken_ReducesBucket()
    {
        var store = new InMemoryIngestionQuotaStore(new QuotaPolicy(10, 5, TimeSpan.FromSeconds(1)));

        var granted = await store.TryConsumeAsync("t1", 1, CancellationToken.None);
        var status = await store.GetStatusAsync("t1", CancellationToken.None);

        Assert.True(granted);
        Assert.Equal(9, status.AvailableTokens);
    }

    [Fact]
    public async Task TryConsumeAsync_SequentialConsumes_DepleteToZero()
    {
        var store = new InMemoryIngestionQuotaStore(new QuotaPolicy(3, 3, TimeSpan.FromSeconds(60)));

        for (var i = 0; i < 3; i++)
            Assert.True(await store.TryConsumeAsync("t1", 1, CancellationToken.None));

        Assert.False(await store.TryConsumeAsync("t1", 1, CancellationToken.None));

        var status = await store.GetStatusAsync("t1", CancellationToken.None);
        Assert.Equal(0, status.AvailableTokens);
        Assert.True(status.IsExhausted);
    }

    [Fact]
    public async Task TryConsumeAsync_MultipleTokensAtOnce_DepletesCorrectly()
    {
        var store = new InMemoryIngestionQuotaStore(new QuotaPolicy(10, 5, TimeSpan.FromSeconds(1)));

        var granted = await store.TryConsumeAsync("t1", 7, CancellationToken.None);
        var status = await store.GetStatusAsync("t1", CancellationToken.None);

        Assert.True(granted);
        Assert.Equal(3, status.AvailableTokens);
    }

    [Fact]
    public async Task TryConsumeAsync_RequestExceedsAvailable_Denied()
    {
        var store = new InMemoryIngestionQuotaStore(new QuotaPolicy(5, 5, TimeSpan.FromSeconds(60)));

        // Deplete 4 tokens
        await store.TryConsumeAsync("t1", 4, CancellationToken.None);

        // Requesting 3 more (only 1 available) → denied
        var granted = await store.TryConsumeAsync("t1", 3, CancellationToken.None);
        Assert.False(granted);

        // The 1 remaining token should still be there (partial consume must not happen)
        var status = await store.GetStatusAsync("t1", CancellationToken.None);
        Assert.Equal(1, status.AvailableTokens);
    }

    // -------------------------------------------------------------------------
    // Refill (TimeProvider-controlled)
    // -------------------------------------------------------------------------

    [Fact]
    public async Task TryConsumeAsync_AfterRefillInterval_TokensReplenished()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var policy = new QuotaPolicy(Capacity: 5, RefillTokens: 5, RefillInterval: TimeSpan.FromSeconds(10));
        var store = new InMemoryIngestionQuotaStore(policy, clock);

        // Exhaust the bucket
        for (var i = 0; i < 5; i++)
            await store.TryConsumeAsync("t1", 1, CancellationToken.None);

        Assert.False(await store.TryConsumeAsync("t1", 1, CancellationToken.None));

        // Advance just past one refill interval
        clock.Advance(TimeSpan.FromSeconds(10));

        var granted = await store.TryConsumeAsync("t1", 1, CancellationToken.None);
        Assert.True(granted);
    }

    [Fact]
    public async Task TryConsumeAsync_MultipleIntervals_CreditsMultipleRefills()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var policy = new QuotaPolicy(Capacity: 20, RefillTokens: 5, RefillInterval: TimeSpan.FromSeconds(10));
        var store = new InMemoryIngestionQuotaStore(policy, clock);

        // Drain completely
        await store.TryConsumeAsync("t1", 20, CancellationToken.None);

        // Advance 3 full intervals → +15 tokens, still capped at 20
        clock.Advance(TimeSpan.FromSeconds(30));

        var status = await store.GetStatusAsync("t1", CancellationToken.None);
        Assert.Equal(15, status.AvailableTokens); // 0 + 15
    }

    [Fact]
    public async Task TryConsumeAsync_RefillCapsAtCapacity()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var policy = new QuotaPolicy(Capacity: 5, RefillTokens: 5, RefillInterval: TimeSpan.FromSeconds(5));
        var store = new InMemoryIngestionQuotaStore(policy, clock);

        // Consume 1 token
        await store.TryConsumeAsync("t1", 1, CancellationToken.None);

        // Advance 3 intervals — would add 15, but capped at capacity=5
        clock.Advance(TimeSpan.FromSeconds(15));

        var status = await store.GetStatusAsync("t1", CancellationToken.None);
        Assert.Equal(5, status.AvailableTokens);
    }

    [Fact]
    public async Task GetStatusAsync_NextRefillAt_IsNullWhenFull()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var policy = new QuotaPolicy(Capacity: 5, RefillTokens: 5, RefillInterval: TimeSpan.FromSeconds(10));
        var store = new InMemoryIngestionQuotaStore(policy, clock);

        var status = await store.GetStatusAsync("t1", CancellationToken.None);
        Assert.Null(status.NextRefillAt); // starts full
    }

    [Fact]
    public async Task GetStatusAsync_NextRefillAt_IsSetWhenPartiallyDepleted()
    {
        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var clock = new ManualTimeProvider(start);
        var policy = new QuotaPolicy(Capacity: 5, RefillTokens: 5, RefillInterval: TimeSpan.FromSeconds(10));
        var store = new InMemoryIngestionQuotaStore(policy, clock);

        await store.TryConsumeAsync("t1", 1, CancellationToken.None);

        var status = await store.GetStatusAsync("t1", CancellationToken.None);
        Assert.NotNull(status.NextRefillAt);
        Assert.Equal(start + TimeSpan.FromSeconds(10), status.NextRefillAt!.Value);
    }

    // -------------------------------------------------------------------------
    // Tenant isolation
    // -------------------------------------------------------------------------

    [Fact]
    public async Task DifferentTenants_HaveIndependentBuckets()
    {
        var store = new InMemoryIngestionQuotaStore(new QuotaPolicy(3, 3, TimeSpan.FromSeconds(60)));

        // Exhaust tenant-a
        for (var i = 0; i < 3; i++)
            await store.TryConsumeAsync("tenant-a", 1, CancellationToken.None);

        // tenant-b should still be full
        var grantedB = await store.TryConsumeAsync("tenant-b", 1, CancellationToken.None);
        Assert.True(grantedB);

        var statusA = await store.GetStatusAsync("tenant-a", CancellationToken.None);
        var statusB = await store.GetStatusAsync("tenant-b", CancellationToken.None);

        Assert.Equal(0, statusA.AvailableTokens);
        Assert.Equal(2, statusB.AvailableTokens); // started at 3, consumed 1
    }

    [Fact]
    public async Task PerTenantPolicyResolver_AppliesCorrectPolicy()
    {
        var defaultPolicy = new QuotaPolicy(Capacity: 5, RefillTokens: 5, RefillInterval: TimeSpan.FromSeconds(1));
        var premiumPolicy = new QuotaPolicy(Capacity: 100, RefillTokens: 50, RefillInterval: TimeSpan.FromSeconds(1));

        var store = new InMemoryIngestionQuotaStore(
            defaultPolicy,
            tenantId => tenantId == "premium" ? premiumPolicy : null);

        var statusPremium = await store.GetStatusAsync("premium", CancellationToken.None);
        var statusDefault = await store.GetStatusAsync("regular", CancellationToken.None);

        Assert.Equal(100, statusPremium.Capacity);
        Assert.Equal(5, statusDefault.Capacity);
    }

    // -------------------------------------------------------------------------
    // Concurrent access
    // -------------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentConsumes_NeverGrantMoreThanCapacity()
    {
        const int capacity = 50;
        const int concurrency = 200;

        var store = new InMemoryIngestionQuotaStore(
            new QuotaPolicy(capacity, capacity, TimeSpan.FromHours(1)));

        var tasks = Enumerable
            .Range(0, concurrency)
            .Select(_ => store.TryConsumeAsync("t1", 1, CancellationToken.None).AsTask());

        var results = await Task.WhenAll(tasks);

        var granted = results.Count(r => r);
        var denied = results.Count(r => !r);

        Assert.Equal(capacity, granted);
        Assert.Equal(concurrency - capacity, denied);
    }

    [Fact]
    public async Task ConcurrentFirstAccess_MultipleTenants_EachBucketIndependent()
    {
        const int tenantCount = 20;
        const int capacity = 5;

        var store = new InMemoryIngestionQuotaStore(
            new QuotaPolicy(capacity, capacity, TimeSpan.FromHours(1)));

        var tasks = Enumerable
            .Range(0, tenantCount)
            .Select(i => store.TryConsumeAsync($"tenant-{i}", 1, CancellationToken.None).AsTask());

        var results = await Task.WhenAll(tasks);

        // Every tenant should succeed — each has its own fresh bucket
        Assert.All(results, r => Assert.True(r));
    }

    // -------------------------------------------------------------------------
    // QuotaMiddleware — middleware integration
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Middleware_WhenTokensAvailable_PassesToNext()
    {
        var store = new InMemoryIngestionQuotaStore(new QuotaPolicy(10, 5, TimeSpan.FromSeconds(1)));
        var mw = new QuotaMiddleware<string>(store);

        var callCount = 0;
        IngestionDelegate<string> next = (_, _, _) =>
        {
            callCount++;
            return new ValueTask<IngestionResult<IngestedEntity>>(
                IngestionResult<IngestedEntity>.Success(StubEntity()));
        };

        var result = await mw.InvokeAsync("payload", Ctx(), next, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task Middleware_WhenBucketExhausted_ShortCircuitsWithQuotaExceeded()
    {
        var policy = new QuotaPolicy(Capacity: 2, RefillTokens: 2, RefillInterval: TimeSpan.FromHours(1));
        var store = new InMemoryIngestionQuotaStore(policy);
        var mw = new QuotaMiddleware<string>(store);

        var ctx = Ctx("tenant-a");
        var next = AlwaysSuccess();

        // Consume all tokens
        await mw.InvokeAsync("p1", ctx, next, CancellationToken.None);
        await mw.InvokeAsync("p2", ctx, next, CancellationToken.None);

        // Third call → quota exceeded
        var callCount = 0;
        IngestionDelegate<string> countingNext = (_, _, _) =>
        {
            callCount++;
            return new ValueTask<IngestionResult<IngestedEntity>>(
                IngestionResult<IngestedEntity>.Success(StubEntity()));
        };

        var result = await mw.InvokeAsync("p3", ctx, countingNext, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IngestOutcome.QuotaExceeded, result.Outcome);
        Assert.Equal(0, callCount); // next was NOT called
        Assert.Contains("tenant-a", result.Failure!.Message);
    }

    [Fact]
    public async Task Middleware_AfterRefill_AllowsNewRequests()
    {
        var clock = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var policy = new QuotaPolicy(Capacity: 1, RefillTokens: 1, RefillInterval: TimeSpan.FromSeconds(10));
        var store = new InMemoryIngestionQuotaStore(policy, clock);
        var mw = new QuotaMiddleware<string>(store);

        var ctx = Ctx();
        var next = AlwaysSuccess();

        // Use the single token
        var r1 = await mw.InvokeAsync("p1", ctx, next, CancellationToken.None);
        Assert.True(r1.IsSuccess);

        // Second call → blocked
        var r2 = await mw.InvokeAsync("p2", ctx, next, CancellationToken.None);
        Assert.Equal(IngestOutcome.QuotaExceeded, r2.Outcome);

        // Advance past refill
        clock.Advance(TimeSpan.FromSeconds(10));

        var r3 = await mw.InvokeAsync("p3", ctx, next, CancellationToken.None);
        Assert.True(r3.IsSuccess);
    }

    [Fact]
    public async Task Middleware_DifferentTenants_EnforcedIndependently()
    {
        var policy = new QuotaPolicy(Capacity: 1, RefillTokens: 1, RefillInterval: TimeSpan.FromHours(1));
        var store = new InMemoryIngestionQuotaStore(policy);
        var mw = new QuotaMiddleware<string>(store);

        var next = AlwaysSuccess();

        var rA1 = await mw.InvokeAsync("p", Ctx("a"), next, CancellationToken.None);
        var rA2 = await mw.InvokeAsync("p", Ctx("a"), next, CancellationToken.None); // blocked
        var rB1 = await mw.InvokeAsync("p", Ctx("b"), next, CancellationToken.None); // b has its own bucket

        Assert.True(rA1.IsSuccess);
        Assert.Equal(IngestOutcome.QuotaExceeded, rA2.Outcome);
        Assert.True(rB1.IsSuccess);
    }

    // -------------------------------------------------------------------------
    // QuotaPolicy validation
    // -------------------------------------------------------------------------

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(-1, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, -1, 1)]
    [InlineData(1, 1, 0)]
    public void QuotaPolicy_InvalidValues_ThrowOnValidate(int capacity, int refillTokens, int refillIntervalSeconds)
    {
        var policy = new QuotaPolicy(capacity, refillTokens, TimeSpan.FromSeconds(refillIntervalSeconds));
        Assert.Throws<ArgumentOutOfRangeException>(policy.Validate);
    }

    [Fact]
    public void QuotaPolicy_ValidValues_DoesNotThrow()
    {
        var policy = new QuotaPolicy(10, 5, TimeSpan.FromSeconds(1));
        var ex = Record.Exception(policy.Validate);
        Assert.Null(ex);
    }

    // -------------------------------------------------------------------------
    // IngestOutcome enum — QuotaExceeded value exists
    // -------------------------------------------------------------------------

    [Fact]
    public void IngestOutcome_QuotaExceeded_IsDefinedInEnum()
    {
        Assert.True(Enum.IsDefined(typeof(IngestOutcome), IngestOutcome.QuotaExceeded));
    }
}
