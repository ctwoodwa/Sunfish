namespace Sunfish.Kernel.Lease.Tests;

/// <summary>
/// Record-shape sanity checks for <see cref="Lease"/>. Not intended to
/// cover behaviour — see <see cref="FleaseLeaseCoordinatorTests"/> for that.
/// </summary>
public class LeaseRecordTests
{
    [Fact]
    public void Lease_With_Equal_Fields_Are_Equal()
    {
        var acquired = DateTimeOffset.UtcNow;
        var expires = acquired.AddSeconds(30);
        var a = new Lease("ABCD", "r1", "node-0", acquired, expires, new List<string> { "node-0", "node-1" });
        var b = new Lease("ABCD", "r1", "node-0", acquired, expires, new List<string> { "node-0", "node-1" });

        Assert.Equal(a.LeaseId, b.LeaseId);
        Assert.Equal(a.ResourceId, b.ResourceId);
        Assert.Equal(a.HolderNodeId, b.HolderNodeId);
        Assert.Equal(a.AcquiredAt, b.AcquiredAt);
        Assert.Equal(a.ExpiresAt, b.ExpiresAt);
        Assert.Equal(a.QuorumParticipants, b.QuorumParticipants);
    }

    [Fact]
    public void Lease_With_Operator_Returns_New_Instance_With_Change()
    {
        var original = new Lease(
            "ABCD", "r1", "node-0",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddSeconds(30),
            new List<string> { "node-0" });
        var renewed = original with { ExpiresAt = DateTimeOffset.UnixEpoch.AddSeconds(60) };

        Assert.Equal(original.LeaseId, renewed.LeaseId);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddSeconds(60), renewed.ExpiresAt);
        Assert.NotEqual(original.ExpiresAt, renewed.ExpiresAt);
    }

    [Fact]
    public void Lease_QuorumParticipants_Snapshot_Preserved()
    {
        var participants = new List<string> { "node-0", "node-1", "node-2" };
        var lease = new Lease(
            "ABCD", "r1", "node-0",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddSeconds(30),
            participants);
        Assert.Equal(3, lease.QuorumParticipants.Count);
        Assert.Contains("node-1", lease.QuorumParticipants);
    }
}
