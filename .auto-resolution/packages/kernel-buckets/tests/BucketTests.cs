using Sunfish.Kernel.Buckets;

namespace Sunfish.Kernel.Buckets.Tests;

public sealed class BucketTests
{
    private static BucketDefinition Def(
        string name = "team_core",
        ReplicationMode mode = ReplicationMode.Eager) =>
        new(
            Name: name,
            RecordTypes: new[] { "projects" },
            Filter: null,
            Replication: mode,
            RequiredAttestation: "team_member",
            MaxLocalAgeDays: null);

    [Fact]
    public void Constructor_stores_definition_and_exposes_name()
    {
        var def = Def("team_core");
        var bucket = new Bucket(def);

        Assert.Same(def, bucket.Definition);
        Assert.Equal("team_core", bucket.Name);
    }

    [Fact]
    public void Constructor_rejects_null_definition()
    {
        Assert.Throws<ArgumentNullException>(() => new Bucket(null!));
    }

    [Fact]
    public void New_bucket_has_zero_members_and_subscribers()
    {
        var bucket = new Bucket(Def());

        Assert.Equal(0, bucket.MemberCount);
        Assert.Equal(0, bucket.SubscribedPeerCount);
        Assert.Empty(bucket.MemberRecordIds);
        Assert.Empty(bucket.SubscribedPeerIds);
    }

    [Fact]
    public void AddRecord_returns_true_on_first_add_and_false_on_duplicate()
    {
        var bucket = new Bucket(Def());

        Assert.True(bucket.AddRecord("rec-1"));
        Assert.False(bucket.AddRecord("rec-1"));
        Assert.Equal(1, bucket.MemberCount);
    }

    [Fact]
    public void AddRecord_uses_ordinal_comparison_for_record_ids()
    {
        var bucket = new Bucket(Def());

        Assert.True(bucket.AddRecord("Rec-1"));
        Assert.True(bucket.AddRecord("rec-1"));

        Assert.Equal(2, bucket.MemberCount);
    }

    [Fact]
    public void AddRecord_rejects_null_or_empty_record_id()
    {
        var bucket = new Bucket(Def());

        Assert.Throws<ArgumentNullException>(() => bucket.AddRecord(null!));
        Assert.Throws<ArgumentException>(() => bucket.AddRecord(string.Empty));
    }

    [Fact]
    public void RemoveRecord_returns_true_when_present_and_false_when_absent()
    {
        var bucket = new Bucket(Def());
        bucket.AddRecord("rec-1");

        Assert.True(bucket.RemoveRecord("rec-1"));
        Assert.False(bucket.RemoveRecord("rec-1"));
        Assert.Equal(0, bucket.MemberCount);
    }

    [Fact]
    public void RemoveRecord_rejects_null_or_empty_record_id()
    {
        var bucket = new Bucket(Def());

        Assert.Throws<ArgumentNullException>(() => bucket.RemoveRecord(null!));
        Assert.Throws<ArgumentException>(() => bucket.RemoveRecord(string.Empty));
    }

    [Fact]
    public void MemberRecordIds_returns_snapshot_decoupled_from_internal_state()
    {
        var bucket = new Bucket(Def());
        bucket.AddRecord("rec-1");
        bucket.AddRecord("rec-2");

        var snapshot = bucket.MemberRecordIds;
        bucket.AddRecord("rec-3");

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(3, bucket.MemberCount);
    }

    [Fact]
    public void AddSubscriber_returns_true_on_first_and_false_on_duplicate()
    {
        var bucket = new Bucket(Def());

        Assert.True(bucket.AddSubscriber("peer-A"));
        Assert.False(bucket.AddSubscriber("peer-A"));
        Assert.Equal(1, bucket.SubscribedPeerCount);
    }

    [Fact]
    public void AddSubscriber_rejects_null_or_empty_peer_id()
    {
        var bucket = new Bucket(Def());

        Assert.Throws<ArgumentNullException>(() => bucket.AddSubscriber(null!));
        Assert.Throws<ArgumentException>(() => bucket.AddSubscriber(string.Empty));
    }

    [Fact]
    public void RemoveSubscriber_returns_true_when_present_and_false_when_absent()
    {
        var bucket = new Bucket(Def());
        bucket.AddSubscriber("peer-A");

        Assert.True(bucket.RemoveSubscriber("peer-A"));
        Assert.False(bucket.RemoveSubscriber("peer-A"));
        Assert.Equal(0, bucket.SubscribedPeerCount);
    }

    [Fact]
    public void RemoveSubscriber_rejects_null_or_empty_peer_id()
    {
        var bucket = new Bucket(Def());

        Assert.Throws<ArgumentNullException>(() => bucket.RemoveSubscriber(null!));
        Assert.Throws<ArgumentException>(() => bucket.RemoveSubscriber(string.Empty));
    }

    [Fact]
    public void SubscribedPeerIds_returns_snapshot_decoupled_from_internal_state()
    {
        var bucket = new Bucket(Def());
        bucket.AddSubscriber("peer-A");
        bucket.AddSubscriber("peer-B");

        var snapshot = bucket.SubscribedPeerIds;
        bucket.AddSubscriber("peer-C");

        Assert.Equal(2, snapshot.Count);
        Assert.Equal(3, bucket.SubscribedPeerCount);
    }

    [Fact]
    public void Concurrent_record_and_subscriber_mutations_remain_consistent()
    {
        var bucket = new Bucket(Def());
        const int perTask = 200;

        Parallel.For(0, 4, taskIndex =>
        {
            for (var i = 0; i < perTask; i++)
            {
                bucket.AddRecord($"rec-{taskIndex}-{i}");
                bucket.AddSubscriber($"peer-{taskIndex}-{i}");
            }
        });

        Assert.Equal(4 * perTask, bucket.MemberCount);
        Assert.Equal(4 * perTask, bucket.SubscribedPeerCount);
    }
}
