using Sunfish.Kernel.Buckets;
using Sunfish.Kernel.Buckets.Storage;

namespace Sunfish.Kernel.Buckets.Tests;

public sealed class InMemoryStorageBudgetManagerTests
{
    private static BucketDefinition Def(string name, ReplicationMode mode) =>
        new(
            Name: name,
            RecordTypes: new[] { "projects" },
            Filter: null,
            Replication: mode,
            RequiredAttestation: "team_member",
            MaxLocalAgeDays: null);

    private static (IBucketRegistry registry, IStorageBudgetManager mgr, StorageBudget budget) Build(long maxBytes = 10_000)
    {
        var registry = new BucketRegistry();
        registry.Register(Def("archived_projects", ReplicationMode.Lazy));
        registry.Register(Def("team_core", ReplicationMode.Eager));
        var budget = new StorageBudget { MaxBytes = maxBytes };
        var mgr = new InMemoryStorageBudgetManager(registry, budget);
        return (registry, mgr, budget);
    }

    [Fact]
    public void Constructor_rejects_null_registry()
    {
        Assert.Throws<ArgumentNullException>(() => new InMemoryStorageBudgetManager(null!));
    }

    [Fact]
    public void Constructor_rejects_null_budget()
    {
        var registry = new BucketRegistry();

        Assert.Throws<ArgumentNullException>(() =>
            new InMemoryStorageBudgetManager(registry, null!));
    }

    [Fact]
    public void Constructor_default_budget_is_ten_gigabytes()
    {
        var registry = new BucketRegistry();
        var mgr = new InMemoryStorageBudgetManager(registry);

        Assert.Equal(10L * 1024 * 1024 * 1024, mgr.Current.MaxBytes);
        Assert.Equal(0, mgr.Current.CurrentBytes);
    }

    [Fact]
    public void Track_rejects_null_record()
    {
        var (_, mgr, _) = Build();

        Assert.Throws<ArgumentNullException>(() => mgr.Track(null!));
    }

    [Fact]
    public void Track_rejects_empty_bucket_name()
    {
        var (_, mgr, _) = Build();

        Assert.Throws<ArgumentException>(() =>
            mgr.Track(new TrackedRecord(string.Empty, "r1", 100, DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void Track_rejects_empty_record_id()
    {
        var (_, mgr, _) = Build();

        Assert.Throws<ArgumentException>(() =>
            mgr.Track(new TrackedRecord("archived_projects", string.Empty, 100, DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void Track_rejects_negative_content_length()
    {
        var (_, mgr, _) = Build();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            mgr.Track(new TrackedRecord("archived_projects", "r1", -1, DateTimeOffset.UtcNow)));
    }

    [Fact]
    public void Track_zero_content_length_is_allowed()
    {
        var (_, mgr, budget) = Build();

        mgr.Track(new TrackedRecord("archived_projects", "r1", 0, DateTimeOffset.UtcNow));

        Assert.Equal(0, budget.CurrentBytes);
    }

    [Fact]
    public void Retracking_same_key_replaces_byte_accounting()
    {
        var (_, mgr, budget) = Build();
        mgr.Track(new TrackedRecord("archived_projects", "r1", 500, DateTimeOffset.UtcNow));
        Assert.Equal(500, budget.CurrentBytes);

        mgr.Track(new TrackedRecord("archived_projects", "r1", 1_500, DateTimeOffset.UtcNow));

        Assert.Equal(1_500, budget.CurrentBytes);
    }

    [Fact]
    public void Untrack_returns_false_when_record_was_not_tracked()
    {
        var (_, mgr, budget) = Build();

        Assert.False(mgr.Untrack("archived_projects", "missing"));
        Assert.Equal(0, budget.CurrentBytes);
    }

    [Fact]
    public void Untrack_removes_byte_accounting_and_returns_true()
    {
        var (_, mgr, budget) = Build();
        mgr.Track(new TrackedRecord("archived_projects", "r1", 500, DateTimeOffset.UtcNow));
        Assert.Equal(500, budget.CurrentBytes);

        Assert.True(mgr.Untrack("archived_projects", "r1"));
        Assert.Equal(0, budget.CurrentBytes);
    }

    [Fact]
    public void Untrack_rejects_empty_arguments()
    {
        var (_, mgr, _) = Build();

        Assert.Throws<ArgumentException>(() => mgr.Untrack(string.Empty, "r1"));
        Assert.Throws<ArgumentException>(() => mgr.Untrack("archived_projects", string.Empty));
    }

    [Fact]
    public async Task TouchAccess_updates_last_accessed_changing_eviction_order()
    {
        var (_, mgr, budget) = Build();
        var t0 = DateTimeOffset.UtcNow;

        mgr.Track(new TrackedRecord("archived_projects", "oldest", 500, t0.AddMinutes(-30)));
        mgr.Track(new TrackedRecord("archived_projects", "middle", 500, t0.AddMinutes(-20)));
        mgr.Track(new TrackedRecord("archived_projects", "newest", 500, t0.AddMinutes(-10)));

        // Promote 'oldest' so 'middle' should now be the LRU candidate.
        mgr.TouchAccess("archived_projects", "oldest", t0);

        var reclaimed = await mgr.EvictLruAsync(500, CancellationToken.None);

        Assert.Equal(500, reclaimed);
        Assert.Equal(1_000, budget.CurrentBytes);
        // 'middle' was evicted (was now LRU); 'oldest' and 'newest' remain.
        Assert.True(mgr.Untrack("archived_projects", "oldest"));
        Assert.True(mgr.Untrack("archived_projects", "newest"));
        Assert.False(mgr.Untrack("archived_projects", "middle"));
    }

    [Fact]
    public void TouchAccess_is_a_noop_for_unknown_record()
    {
        var (_, mgr, budget) = Build();
        mgr.Track(new TrackedRecord("archived_projects", "r1", 500, DateTimeOffset.UtcNow));

        mgr.TouchAccess("archived_projects", "unknown", DateTimeOffset.UtcNow);

        Assert.Equal(500, budget.CurrentBytes);
    }

    [Fact]
    public void TouchAccess_rejects_empty_arguments()
    {
        var (_, mgr, _) = Build();

        Assert.Throws<ArgumentException>(() => mgr.TouchAccess(string.Empty, "r1", DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => mgr.TouchAccess("archived_projects", string.Empty, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task EvictLru_returns_zero_for_zero_or_negative_target()
    {
        var (_, mgr, budget) = Build();
        mgr.Track(new TrackedRecord("archived_projects", "r1", 500, DateTimeOffset.UtcNow));

        Assert.Equal(0, await mgr.EvictLruAsync(0, CancellationToken.None));
        Assert.Equal(0, await mgr.EvictLruAsync(-100, CancellationToken.None));
        Assert.Equal(500, budget.CurrentBytes);
    }

    [Fact]
    public async Task EvictLru_evicts_oldest_first()
    {
        var (_, mgr, budget) = Build();
        var now = DateTimeOffset.UtcNow;
        mgr.Track(new TrackedRecord("archived_projects", "oldest", 500, now.AddMinutes(-30)));
        mgr.Track(new TrackedRecord("archived_projects", "newer", 500, now));

        var reclaimed = await mgr.EvictLruAsync(500, CancellationToken.None);

        Assert.Equal(500, reclaimed);
        Assert.Equal(500, budget.CurrentBytes);
        // Untrack the survivor: should still be there (true). 'oldest' was evicted (false on its untrack).
        Assert.True(mgr.Untrack("archived_projects", "newer"));
    }

    [Fact]
    public async Task EvictLru_skips_eager_buckets_entirely()
    {
        var (_, mgr, budget) = Build();
        var now = DateTimeOffset.UtcNow;
        mgr.Track(new TrackedRecord("team_core", "eager-old", 500, now.AddDays(-7)));
        mgr.Track(new TrackedRecord("archived_projects", "lazy-new", 500, now));

        var reclaimed = await mgr.EvictLruAsync(500, CancellationToken.None);

        Assert.Equal(500, reclaimed);
        // Eager record must remain untouched even though it is older.
        Assert.Equal(500, budget.CurrentBytes);
        Assert.True(mgr.Untrack("team_core", "eager-old"));
    }

    [Fact]
    public async Task EvictLru_returns_zero_when_only_unregistered_buckets_present()
    {
        var (_, mgr, budget) = Build();
        // Record references a bucket that is not in the registry; IsLazyBucket returns false.
        mgr.Track(new TrackedRecord("ghost_bucket", "r1", 500, DateTimeOffset.UtcNow));

        var reclaimed = await mgr.EvictLruAsync(500, CancellationToken.None);

        Assert.Equal(0, reclaimed);
        Assert.Equal(500, budget.CurrentBytes);
    }

    [Fact]
    public async Task EvictLru_observes_cancellation_when_target_exceeds_available()
    {
        var (_, mgr, _) = Build();
        var now = DateTimeOffset.UtcNow;
        for (var i = 0; i < 10; i++)
        {
            mgr.Track(new TrackedRecord("archived_projects", $"r{i}", 100, now.AddMinutes(-i)));
        }

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await mgr.EvictLruAsync(10_000, cts.Token));
    }

}
