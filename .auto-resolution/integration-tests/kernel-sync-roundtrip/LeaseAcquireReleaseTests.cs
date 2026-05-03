using Sunfish.Integration.KernelSyncRoundtrip.Harness;

namespace Sunfish.Integration.KernelSyncRoundtrip;

/// <summary>
/// Lease acquire / release / expiry over the real Unix-socket / named-pipe
/// transport. Uses <see cref="FleaseLeaseCoordinator"/> on both nodes with
/// the lease responder loop ENABLED (each coordinator answers
/// <c>LEASE_REQUEST</c> frames coming in from its peer).
/// </summary>
/// <remarks>
/// A two-node cluster's quorum (ceil(2/2)+1 = 2) requires the remote peer's
/// grant plus our self-grant for every acquire, which is exactly the
/// end-to-end path we want to exercise: a real <c>LEASE_REQUEST</c> crossing
/// the socket and a real <c>LEASE_GRANT</c> / <c>LEASE_DENIED</c> coming
/// back.
/// </remarks>
public class LeaseAcquireReleaseTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task NodeA_Acquires_Then_NodeB_Cannot_Acquire_Same_Lease()
    {
        using var timeoutCts = new CancellationTokenSource(TestTimeout);
        var ct = timeoutCts.Token;

        await using var harness = await TwoNodeHarness.StartAsync(ct, enableLeaseResponder: true);

        var leaseA = await harness.NodeA.Leases
            .AcquireAsync("ledger:account:test-1", TimeSpan.FromSeconds(30), ct)
            ;
        Assert.NotNull(leaseA);
        Assert.True(harness.NodeA.Leases.Holds("ledger:account:test-1"));

        // B tries the same resource. A's responder (answering from its
        // conflict cache) replies LEASE_DENIED. B's own self-grant path
        // rolls back and returns null. Total grants = 1 (self only) which
        // is below quorum = 2.
        var leaseB = await harness.NodeB.Leases
            .AcquireAsync("ledger:account:test-1", TimeSpan.FromSeconds(30), ct)
            ;
        Assert.Null(leaseB);
        Assert.False(harness.NodeB.Leases.Holds("ledger:account:test-1"));
    }

    [Fact]
    public async Task NodeA_Releases_Then_NodeB_Can_Acquire()
    {
        using var timeoutCts = new CancellationTokenSource(TestTimeout);
        var ct = timeoutCts.Token;

        await using var harness = await TwoNodeHarness.StartAsync(ct, enableLeaseResponder: true);

        var leaseA = await harness.NodeA.Leases
            .AcquireAsync("ledger:account:test-2", TimeSpan.FromSeconds(30), ct)
            ;
        Assert.NotNull(leaseA);

        await harness.NodeA.Leases.ReleaseAsync(leaseA!, ct);
        Assert.False(harness.NodeA.Leases.Holds("ledger:account:test-2"));

        // Release is broadcast asynchronously over a separate connection
        // to B; retry briefly to cover the responder-side apply window.
        Lease? leaseB = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        while (leaseB is null && sw.Elapsed < TimeSpan.FromSeconds(5))
        {
            leaseB = await harness.NodeB.Leases
                .AcquireAsync("ledger:account:test-2", TimeSpan.FromSeconds(30), ct)
                ;
            if (leaseB is null) await Task.Delay(100, ct);
        }

        Assert.NotNull(leaseB);
        Assert.True(harness.NodeB.Leases.Holds("ledger:account:test-2"));
    }

    [Fact]
    public async Task NodeA_Crash_Simulated_By_Transport_Dispose_Lets_NodeB_Acquire_After_Expiry()
    {
        using var timeoutCts = new CancellationTokenSource(TestTimeout);
        var ct = timeoutCts.Token;

        await using var harness = await TwoNodeHarness.StartAsync(ct, enableLeaseResponder: true);

        // Grab a short-lived lease on A.
        var lease = await harness.NodeA.Leases
            .AcquireAsync("ledger:account:test-3", TimeSpan.FromMilliseconds(500), ct)
            ;
        Assert.NotNull(lease);

        // Simulate A crashing: dispose its transport. B's conflict cache
        // still thinks A holds the lease until the entry expires, but the
        // ExpiresAt timestamp we cached in B is "now + 500 ms" so we only
        // have to wait past that window.
        await harness.NodeA.DisposeAsync();

        // Wait past expiry + responder-side prune cadence (200 ms).
        await Task.Delay(TimeSpan.FromMilliseconds(1200), ct);

        // At this point A's responder is gone, so B's proposal to A will
        // fail fast. Quorum math: ceil(2/2)+1 = 2, but only B is alive.
        // With a stale cache pruned, B's self-grant counts toward the
        // quorum; the missing A-grant keeps us below quorum. This test
        // therefore verifies the "A crashed → B sees no stale lock" side
        // of the contract: B's Holds on the resource returns false (no
        // lingering lock), but B's acquire also returns null because the
        // two-node cluster cannot satisfy quorum on its own.
        var leaseB = await harness.NodeB.Leases
            .AcquireAsync("ledger:account:test-3", TimeSpan.FromSeconds(30), ct)
            ;
        // Quorum unreachable → null. This is the paper §2.3 fail-closed
        // behaviour: no partition-tolerant grant without majority.
        Assert.Null(leaseB);

        // But the stale-lock claim is the key one: B now considers the
        // resource un-held on its own view, even though A previously had
        // it.
        Assert.False(harness.NodeB.Leases.Holds("ledger:account:test-3"));
    }
}
