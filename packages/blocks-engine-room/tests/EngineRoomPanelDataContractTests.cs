using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.EngineRoom;
using Xunit;

namespace Sunfish.Blocks.EngineRoom.Tests;

/// <summary>
/// W#50 Phase 3a — data-contract coverage for provider shapes consumed by
/// EngineRoomHealthBanner, MainPropulsionPanel, and ElectricalPanel.
/// Razor components themselves are not unit-tested here (no bUnit);
/// these tests guard the data contracts that feed the panels.
/// </summary>
public class EngineRoomPanelDataContractTests
{
    private static readonly TenantId TenantA = new("alpha");

    // ── EngineRoomHealthSummary.For ───────────────────────────────────────────

    [Fact]
    public void For_ReturnsEntry_WhenSubsystemPresent()
    {
        var entry = new SubsystemHealth(EngineRoomSubsystem.Electrical, SubsystemStatus.Warning, "low voltage");
        var summary = new EngineRoomHealthSummary([entry]);

        var result = summary.For(EngineRoomSubsystem.Electrical);

        Assert.NotNull(result);
        Assert.Equal(SubsystemStatus.Warning, result.Status);
        Assert.Equal("low voltage", result.Message);
    }

    [Fact]
    public void For_ReturnsNull_WhenSubsystemAbsent()
    {
        var summary = new EngineRoomHealthSummary([
            new SubsystemHealth(EngineRoomSubsystem.MainPropulsion, SubsystemStatus.Operational, null),
        ]);

        Assert.Null(summary.For(EngineRoomSubsystem.Electrical));
    }

    [Fact]
    public void For_ReturnsNull_WhenListIsEmpty()
    {
        var summary = new EngineRoomHealthSummary([]);

        Assert.Null(summary.For(EngineRoomSubsystem.Electrical));
    }

    // ── Worst-status computation (mirrors EngineRoomHealthBanner.WorstStatus) ─

    [Theory]
    [InlineData(SubsystemStatus.Operational, SubsystemStatus.Operational, SubsystemStatus.Operational)]
    [InlineData(SubsystemStatus.Operational, SubsystemStatus.Warning,    SubsystemStatus.Warning)]
    [InlineData(SubsystemStatus.Warning,     SubsystemStatus.Critical,   SubsystemStatus.Critical)]
    [InlineData(SubsystemStatus.Critical,    SubsystemStatus.Operational, SubsystemStatus.Critical)]
    public void WorstStatus_PicksHighestEnumValue(
        SubsystemStatus a, SubsystemStatus b, SubsystemStatus expected)
    {
        var summary = new EngineRoomHealthSummary([
            new SubsystemHealth(EngineRoomSubsystem.MainPropulsion, a, null),
            new SubsystemHealth(EngineRoomSubsystem.Electrical,     b, null),
        ]);

        var worst = summary.SubsystemHealthList.Max(h => h.Status);

        Assert.Equal(expected, worst);
    }

    [Fact]
    public void WorstStatus_ReturnsUnknown_WhenListIsEmpty()
    {
        var summary = new EngineRoomHealthSummary([]);

        var worst = summary.SubsystemHealthList.Count == 0
            ? SubsystemStatus.Unknown
            : summary.SubsystemHealthList.Max(h => h.Status);

        Assert.Equal(SubsystemStatus.Unknown, worst);
    }

    // ── SubsystemStatus ordering — degradation detection contract ────────────

    [Fact]
    public void SubsystemStatus_EnumOrder_SupportsGreaterThanDegradationCheck()
    {
        // ElectricalPanel and EngineRoomHealthBanner detect degradation with
        // `currentStatus > previousStatus`; this test guards the enum ordering.
        Assert.True(SubsystemStatus.Warning  > SubsystemStatus.Operational);
        Assert.True(SubsystemStatus.Critical > SubsystemStatus.Warning);
        Assert.True(SubsystemStatus.Critical > SubsystemStatus.Operational);
    }

    [Fact]
    public void SyncDaemonStatus_EnumOrder_SupportsGreaterThanDegradationCheck()
    {
        // MainPropulsionPanel detects degradation with `health.Status > _previousStatus.Value`.
        Assert.True(SyncDaemonStatus.Degraded    > SyncDaemonStatus.Healthy);
        Assert.True(SyncDaemonStatus.Unavailable > SyncDaemonStatus.Degraded);
        Assert.True(SyncDaemonStatus.Unavailable > SyncDaemonStatus.Healthy);
    }

    // ── CRDT growth metrics aggregation contract (ElectricalPanel) ───────────

    [Fact]
    public async Task GetCrdtGrowthMetrics_Aggregation_SumsCorrectly()
    {
        var provider = Substitute.For<IEngineRoomDataProvider>();
        provider
            .GetCrdtGrowthMetricsAsync(TenantA, Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerableOf(
                new CrdtGrowthMetrics("doc:01", TenantA, 1024,  3, false, DateTimeOffset.UtcNow),
                new CrdtGrowthMetrics("doc:02", TenantA, 2048,  7, true,  DateTimeOffset.UtcNow),
                new CrdtGrowthMetrics("doc:03", TenantA, 512,   1, true,  DateTimeOffset.UtcNow)));

        var docCount = 0;
        long totalBytes = 0;
        var tombstoneCount = 0;
        var compactionEligibleCount = 0;

        await foreach (var m in provider.GetCrdtGrowthMetricsAsync(TenantA))
        {
            docCount++;
            totalBytes             += m.TotalByteEstimate;
            tombstoneCount         += m.TombstoneCount;
            if (m.CompactionEligible) compactionEligibleCount++;
        }

        Assert.Equal(3,    docCount);
        Assert.Equal(3584, totalBytes);
        Assert.Equal(11,   tombstoneCount);
        Assert.Equal(2,    compactionEligibleCount);
    }

    [Fact]
    public async Task GetCrdtGrowthMetrics_EmptyStream_YieldsZeroAggregates()
    {
        var provider = Substitute.For<IEngineRoomDataProvider>();
        provider
            .GetCrdtGrowthMetricsAsync(TenantA, Arg.Any<CancellationToken>())
            .Returns(AsyncEnumerableOf<CrdtGrowthMetrics>());

        var docCount = 0;
        long totalBytes = 0;

        await foreach (var m in provider.GetCrdtGrowthMetricsAsync(TenantA))
        {
            docCount++;
            totalBytes += m.TotalByteEstimate;
        }

        Assert.Equal(0, docCount);
        Assert.Equal(0, totalBytes);
    }

    // ── SyncDaemonHealth data shape (MainPropulsionPanel) ────────────────────

    [Fact]
    public async Task GetSyncDaemonHealth_ReturnsExpectedShape()
    {
        var asOf = DateTimeOffset.UtcNow;
        var expected = new SyncDaemonHealth(
            SyncDaemonStatus.Healthy, PeerCount: 3,
            EventsThroughput: 42.5, GossipCycles: 1001, AsOf: asOf);

        var provider = Substitute.For<IEngineRoomDataProvider>();
        provider.GetSyncDaemonHealthAsync(TenantA, Arg.Any<CancellationToken>())
            .Returns(new ValueTask<SyncDaemonHealth>(expected));

        var result = await provider.GetSyncDaemonHealthAsync(TenantA);

        Assert.Equal(SyncDaemonStatus.Healthy, result.Status);
        Assert.Equal(3,     result.PeerCount);
        Assert.Equal(42.5,  result.EventsThroughput);
        Assert.Equal(1001L, result.GossipCycles);
        Assert.Equal(asOf,  result.AsOf);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async IAsyncEnumerable<T> AsyncEnumerableOf<T>(params T[] items)
    {
        foreach (var item in items)
            yield return item;
        await Task.CompletedTask;
    }
}
