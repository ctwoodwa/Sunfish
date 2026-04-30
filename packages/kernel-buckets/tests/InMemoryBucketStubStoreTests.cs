using Sunfish.Kernel.Buckets.LazyFetch;

namespace Sunfish.Kernel.Buckets.Tests;

public sealed class InMemoryBucketStubStoreTests
{
    private static BucketStub Stub(
        string bucket = "archived_projects",
        string recordId = "rec-1",
        long contentLength = 1024,
        DateTimeOffset? lastModified = null) =>
        new(
            RecordId: recordId,
            BucketName: bucket,
            LastModified: lastModified ?? DateTimeOffset.UtcNow,
            ContentHash: new byte[32],
            ContentLengthBytes: contentLength);

    [Fact]
    public void Upsert_returns_true_on_first_insert_and_false_when_replacing()
    {
        var store = new InMemoryBucketStubStore();

        Assert.True(store.Upsert(Stub(recordId: "rec-1", contentLength: 100)));
        Assert.False(store.Upsert(Stub(recordId: "rec-1", contentLength: 200)));
    }

    [Fact]
    public void Upsert_replaces_existing_stub_value()
    {
        var store = new InMemoryBucketStubStore();
        store.Upsert(Stub(recordId: "rec-1", contentLength: 100));

        store.Upsert(Stub(recordId: "rec-1", contentLength: 999));

        var found = store.Find("archived_projects", "rec-1");
        Assert.NotNull(found);
        Assert.Equal(999, found!.ContentLengthBytes);
    }

    [Fact]
    public void Upsert_rejects_null_stub()
    {
        var store = new InMemoryBucketStubStore();

        Assert.Throws<ArgumentNullException>(() => store.Upsert(null!));
    }

    [Fact]
    public void Upsert_rejects_empty_bucket_name()
    {
        var store = new InMemoryBucketStubStore();

        Assert.Throws<ArgumentException>(() => store.Upsert(Stub(bucket: string.Empty)));
    }

    [Fact]
    public void Upsert_rejects_empty_record_id()
    {
        var store = new InMemoryBucketStubStore();

        Assert.Throws<ArgumentException>(() => store.Upsert(Stub(recordId: string.Empty)));
    }

    [Fact]
    public void Find_returns_null_when_absent()
    {
        var store = new InMemoryBucketStubStore();

        Assert.Null(store.Find("archived_projects", "missing"));
    }

    [Fact]
    public void Find_returns_stored_value()
    {
        var store = new InMemoryBucketStubStore();
        var stub = Stub(recordId: "rec-1", contentLength: 512);
        store.Upsert(stub);

        var found = store.Find("archived_projects", "rec-1");

        Assert.NotNull(found);
        Assert.Equal(512, found!.ContentLengthBytes);
    }

    [Fact]
    public void Find_distinguishes_buckets_with_same_record_id()
    {
        var store = new InMemoryBucketStubStore();
        store.Upsert(Stub(bucket: "bucket-a", recordId: "shared", contentLength: 100));
        store.Upsert(Stub(bucket: "bucket-b", recordId: "shared", contentLength: 200));

        Assert.Equal(100, store.Find("bucket-a", "shared")!.ContentLengthBytes);
        Assert.Equal(200, store.Find("bucket-b", "shared")!.ContentLengthBytes);
    }

    [Fact]
    public void Find_rejects_empty_arguments()
    {
        var store = new InMemoryBucketStubStore();

        Assert.Throws<ArgumentException>(() => store.Find(string.Empty, "rec-1"));
        Assert.Throws<ArgumentException>(() => store.Find("archived_projects", string.Empty));
    }

    [Fact]
    public void ListByBucket_returns_only_matching_bucket_stubs()
    {
        var store = new InMemoryBucketStubStore();
        store.Upsert(Stub(bucket: "bucket-a", recordId: "r1"));
        store.Upsert(Stub(bucket: "bucket-a", recordId: "r2"));
        store.Upsert(Stub(bucket: "bucket-b", recordId: "r3"));

        var listA = store.ListByBucket("bucket-a");
        var listB = store.ListByBucket("bucket-b");

        Assert.Equal(2, listA.Count);
        Assert.Single(listB);
        Assert.All(listA, s => Assert.Equal("bucket-a", s.BucketName));
    }

    [Fact]
    public void ListByBucket_returns_empty_when_no_match()
    {
        var store = new InMemoryBucketStubStore();
        store.Upsert(Stub(bucket: "bucket-a", recordId: "r1"));

        var list = store.ListByBucket("bucket-empty");

        Assert.Empty(list);
    }

    [Fact]
    public void ListByBucket_rejects_empty_bucket_name()
    {
        var store = new InMemoryBucketStubStore();

        Assert.Throws<ArgumentException>(() => store.ListByBucket(string.Empty));
    }

    [Fact]
    public void Remove_returns_true_when_present_and_false_when_absent()
    {
        var store = new InMemoryBucketStubStore();
        store.Upsert(Stub(recordId: "rec-1"));

        Assert.True(store.Remove("archived_projects", "rec-1"));
        Assert.False(store.Remove("archived_projects", "rec-1"));
        Assert.Null(store.Find("archived_projects", "rec-1"));
    }

    [Fact]
    public void Remove_rejects_empty_arguments()
    {
        var store = new InMemoryBucketStubStore();

        Assert.Throws<ArgumentException>(() => store.Remove(string.Empty, "rec-1"));
        Assert.Throws<ArgumentException>(() => store.Remove("archived_projects", string.Empty));
    }

    [Fact]
    public void Concurrent_upserts_remain_consistent()
    {
        var store = new InMemoryBucketStubStore();
        const int perTask = 200;

        Parallel.For(0, 4, taskIndex =>
        {
            for (var i = 0; i < perTask; i++)
            {
                store.Upsert(Stub(recordId: $"rec-{taskIndex}-{i}", contentLength: i));
            }
        });

        var all = store.ListByBucket("archived_projects");
        Assert.Equal(4 * perTask, all.Count);
    }
}
