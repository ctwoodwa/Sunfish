namespace Sunfish.Kernel.Crdt.Tests;

/// <summary>
/// Coverage for <see cref="NeverShallowSnapshotPolicy"/> and
/// <see cref="ThresholdShallowSnapshotPolicy"/>.
/// </summary>
public class ShallowSnapshotPolicyTests
{
    private static ICrdtDocument Doc() => new StubCrdtEngine().CreateDocument("doc-1");

    [Fact]
    public void NeverPolicy_AlwaysReturnsFalse()
    {
        var policy = new NeverShallowSnapshotPolicy();
        var doc = Doc();
        var stats = new DocumentStatistics(
            OperationCount: ulong.MaxValue,
            ByteSize: ulong.MaxValue,
            LastOperationAt: DateTimeOffset.UtcNow,
            LastShallowSnapshotAt: null);

        Assert.False(policy.ShouldTakeShallowSnapshot(doc, stats, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ThresholdPolicy_BelowOpThreshold_ReturnsFalse()
    {
        var policy = new ThresholdShallowSnapshotPolicy { OperationThreshold = 100 };
        var doc = Doc();
        var stats = new DocumentStatistics(
            OperationCount: 50,
            ByteSize: 1_000_000,
            LastOperationAt: DateTimeOffset.UtcNow,
            LastShallowSnapshotAt: null);

        Assert.False(policy.ShouldTakeShallowSnapshot(doc, stats, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ThresholdPolicy_AboveOpThreshold_NoPriorSnapshot_ReturnsTrue()
    {
        var policy = new ThresholdShallowSnapshotPolicy { OperationThreshold = 100 };
        var doc = Doc();
        var stats = new DocumentStatistics(
            OperationCount: 500,
            ByteSize: 1_000_000,
            LastOperationAt: DateTimeOffset.UtcNow,
            LastShallowSnapshotAt: null);

        Assert.True(policy.ShouldTakeShallowSnapshot(doc, stats, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ThresholdPolicy_AboveOpThreshold_WithinInterval_ReturnsFalse()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = new ThresholdShallowSnapshotPolicy
        {
            OperationThreshold = 100,
            MinIntervalBetweenSnapshots = TimeSpan.FromHours(24),
        };
        var doc = Doc();
        var stats = new DocumentStatistics(
            OperationCount: 500,
            ByteSize: 1_000_000,
            LastOperationAt: now,
            LastShallowSnapshotAt: now.AddHours(-1));

        Assert.False(policy.ShouldTakeShallowSnapshot(doc, stats, now));
    }

    [Fact]
    public void ThresholdPolicy_AboveOpThreshold_PastInterval_ReturnsTrue()
    {
        var now = DateTimeOffset.UtcNow;
        var policy = new ThresholdShallowSnapshotPolicy
        {
            OperationThreshold = 100,
            MinIntervalBetweenSnapshots = TimeSpan.FromHours(24),
        };
        var doc = Doc();
        var stats = new DocumentStatistics(
            OperationCount: 500,
            ByteSize: 1_000_000,
            LastOperationAt: now,
            LastShallowSnapshotAt: now.AddHours(-48));

        Assert.True(policy.ShouldTakeShallowSnapshot(doc, stats, now));
    }
}
