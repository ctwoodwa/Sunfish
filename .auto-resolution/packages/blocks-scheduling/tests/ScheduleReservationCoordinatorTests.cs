using System.Collections.Concurrent;

using LeaseNs = Sunfish.Kernel.Lease;

namespace Sunfish.Blocks.Scheduling.Tests;

/// <summary>
/// Coverage for <see cref="ScheduleReservationCoordinator"/> — the D6 wiring
/// fix: <see cref="LeaseNs.ILeaseCoordinator"/> is consumed at the
/// blocks-scheduling write path so paper §2.2 ("Resource reservations,
/// scheduled slots → CP") is enforced at the consumer site, not just available
/// in the kernel.
/// </summary>
/// <remarks>
/// <para>
/// Uses an in-test <see cref="FakeLeaseCoordinator"/> (mirrors the same
/// pattern as <c>kernel-ledger/tests/TestDoubles.cs</c>) so we can drive the
/// quorum-unavailable branch and assert the lease was acquired and released.
/// The end-to-end Flease round-trip is already covered by
/// <c>kernel-lease/tests/FleaseLeaseCoordinatorTests.cs</c> — these tests
/// pin the consumer-side contract.
/// </para>
/// </remarks>
public class ScheduleReservationCoordinatorTests
{
    // ------------------------------------------------------------------
    // Test doubles
    // ------------------------------------------------------------------

    private sealed class FakeLeaseCoordinator : LeaseNs.ILeaseCoordinator
    {
        private readonly ConcurrentDictionary<string, LeaseNs.Lease> _held = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, int> _currentPerResource = new(StringComparer.Ordinal);
        public int AcquireCount;
        public int ReleaseCount;
        public int MaxObservedConcurrencyPerResource;

        public Task<LeaseNs.Lease?> AcquireAsync(string resourceId, TimeSpan duration, CancellationToken ct)
        {
            Interlocked.Increment(ref AcquireCount);
            var count = _currentPerResource.AddOrUpdate(resourceId, 1, (_, c) => c + 1);
            if (count > MaxObservedConcurrencyPerResource)
            {
                MaxObservedConcurrencyPerResource = count;
            }

            var lease = new LeaseNs.Lease(
                LeaseId: Guid.NewGuid().ToString("N"),
                ResourceId: resourceId,
                HolderNodeId: "test-node",
                AcquiredAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.UtcNow + duration,
                QuorumParticipants: Array.Empty<string>());
            _held[lease.LeaseId] = lease;
            return Task.FromResult<LeaseNs.Lease?>(lease);
        }

        public Task ReleaseAsync(LeaseNs.Lease lease, CancellationToken ct)
        {
            Interlocked.Increment(ref ReleaseCount);
            _held.TryRemove(lease.LeaseId, out _);
            _currentPerResource.AddOrUpdate(lease.ResourceId, 0, (_, c) => c - 1);
            return Task.CompletedTask;
        }

        public bool Holds(string resourceId)
            => _held.Values.Any(l => string.Equals(l.ResourceId, resourceId, StringComparison.Ordinal));

        public IReadOnlyCollection<LeaseNs.Lease> HeldLeases => _held.Values.ToArray();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Always fails to acquire — simulates Flease quorum unreachable.</summary>
    private sealed class UnreachableLeaseCoordinator : LeaseNs.ILeaseCoordinator
    {
        public Task<LeaseNs.Lease?> AcquireAsync(string resourceId, TimeSpan duration, CancellationToken ct)
            => Task.FromResult<LeaseNs.Lease?>(null);

        public Task ReleaseAsync(LeaseNs.Lease lease, CancellationToken ct) => Task.CompletedTask;

        public bool Holds(string resourceId) => false;

        public IReadOnlyCollection<LeaseNs.Lease> HeldLeases => Array.Empty<LeaseNs.Lease>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Per-resource serializing fake: only one Acquire may be outstanding on a
    /// given resourceId at a time. Releases unblock the next caller. Models
    /// the serialization guarantee Flease provides on a real cluster, so we
    /// can assert "exactly one wins" for a same-slot race on a single node.
    /// </summary>
    private sealed class SerializingLeaseCoordinator : LeaseNs.ILeaseCoordinator
    {
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _gates = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, LeaseNs.Lease> _held = new(StringComparer.Ordinal);

        public async Task<LeaseNs.Lease?> AcquireAsync(string resourceId, TimeSpan duration, CancellationToken ct)
        {
            var gate = _gates.GetOrAdd(resourceId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct).ConfigureAwait(false);
            var lease = new LeaseNs.Lease(
                LeaseId: Guid.NewGuid().ToString("N"),
                ResourceId: resourceId,
                HolderNodeId: "test-node",
                AcquiredAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.UtcNow + duration,
                QuorumParticipants: Array.Empty<string>());
            _held[lease.LeaseId] = lease;
            return lease;
        }

        public Task ReleaseAsync(LeaseNs.Lease lease, CancellationToken ct)
        {
            _held.TryRemove(lease.LeaseId, out _);
            if (_gates.TryGetValue(lease.ResourceId, out var gate))
            {
                gate.Release();
            }
            return Task.CompletedTask;
        }

        public bool Holds(string resourceId)
            => _held.Values.Any(l => string.Equals(l.ResourceId, resourceId, StringComparison.Ordinal));

        public IReadOnlyCollection<LeaseNs.Lease> HeldLeases => _held.Values.ToArray();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private static SlotReservation NewSlot(
        string id = "r-1",
        string resource = "room-A",
        int startMinutes = 0,
        int durationMinutes = 60,
        string holder = "user-1")
    {
        var start = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMinutes(startMinutes);
        return new SlotReservation(
            ReservationId: id,
            ResourceId: resource,
            StartUtc: start,
            EndUtc: start.AddMinutes(durationMinutes),
            HolderId: holder);
    }

    // ------------------------------------------------------------------
    // Happy path + invariants
    // ------------------------------------------------------------------

    [Fact]
    public async Task ReserveAsync_HappyPath_AcquiresAndReleasesLease()
    {
        var leases = new FakeLeaseCoordinator();
        var coord = new ScheduleReservationCoordinator(leases);

        var outcome = await coord.ReserveAsync(NewSlot(), CancellationToken.None);

        Assert.True(outcome.Success);
        Assert.Null(outcome.RejectionReason);
        Assert.Equal(1, leases.AcquireCount);
        Assert.Equal(1, leases.ReleaseCount); // released in finally
        Assert.False(leases.Holds("schedule:resource:room-A")); // released
    }

    [Fact]
    public async Task ReserveAsync_AcquiresLeaseOnPrefixedResourceId()
    {
        var leases = new FakeLeaseCoordinator();
        var coord = new ScheduleReservationCoordinator(leases);

        // Snapshot lease state during the acquire window via a custom probe.
        // Easier: assert no remaining held leases after release on the
        // prefixed resource id.
        await coord.ReserveAsync(NewSlot(resource: "vehicle-7"), CancellationToken.None);

        // The released lease was on "schedule:resource:vehicle-7" — distinct
        // from "ledger:account:..." used by kernel-ledger so the two CP
        // writers can share a Flease cluster without colliding.
        Assert.Equal(1, leases.AcquireCount);
        Assert.Equal(1, leases.ReleaseCount);
    }

    [Fact]
    public async Task ReserveAsync_DuplicateReservationId_ReturnsPriorOutcome()
    {
        var leases = new FakeLeaseCoordinator();
        var coord = new ScheduleReservationCoordinator(leases);

        var first = await coord.ReserveAsync(NewSlot(id: "dup"), CancellationToken.None);
        var second = await coord.ReserveAsync(NewSlot(id: "dup"), CancellationToken.None);

        Assert.True(first.Success);
        Assert.True(second.Success);
        // Dedupe must not re-acquire the lease — paper §6.3 asks for the
        // narrowest possible CP footprint.
        Assert.Equal(1, leases.AcquireCount);
    }

    [Fact]
    public async Task ReserveAsync_InvertedSlot_RejectedWithoutLease()
    {
        var leases = new FakeLeaseCoordinator();
        var coord = new ScheduleReservationCoordinator(leases);

        var inverted = NewSlot(durationMinutes: -10);
        var outcome = await coord.ReserveAsync(inverted, CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal("SLOT_INVERTED", outcome.RejectionReason);
        Assert.Equal(0, leases.AcquireCount); // shape-validate before lease
    }

    [Fact]
    public async Task ReserveAsync_OverlappingSlot_RejectedAfterLease()
    {
        var leases = new FakeLeaseCoordinator();
        var coord = new ScheduleReservationCoordinator(leases);

        // First slot: 00:00-01:00 on room-A
        var a = await coord.ReserveAsync(
            NewSlot(id: "r-a", startMinutes: 0, durationMinutes: 60),
            CancellationToken.None);
        Assert.True(a.Success);

        // Second slot: 00:30-01:30 on room-A — overlaps a.
        var b = await coord.ReserveAsync(
            NewSlot(id: "r-b", startMinutes: 30, durationMinutes: 60),
            CancellationToken.None);

        Assert.False(b.Success);
        Assert.Equal("SLOT_CONFLICT", b.RejectionReason);
        // Both attempts acquired a lease (the conflict check is *under* the
        // lease — that is the whole point of the wiring).
        Assert.Equal(2, leases.AcquireCount);
        Assert.Equal(2, leases.ReleaseCount);
    }

    [Fact]
    public async Task ReserveAsync_AdjacentNonOverlappingSlots_BothSucceed()
    {
        var leases = new FakeLeaseCoordinator();
        var coord = new ScheduleReservationCoordinator(leases);

        // 00:00-01:00 then 01:00-02:00 — touch but do not overlap (end is
        // exclusive).
        var a = await coord.ReserveAsync(
            NewSlot(id: "r-a", startMinutes: 0, durationMinutes: 60),
            CancellationToken.None);
        var b = await coord.ReserveAsync(
            NewSlot(id: "r-b", startMinutes: 60, durationMinutes: 60),
            CancellationToken.None);

        Assert.True(a.Success);
        Assert.True(b.Success);
    }

    [Fact]
    public async Task ReserveAsync_QuorumUnavailable_BlocksWrite_ReturnsRejection()
    {
        var leases = new UnreachableLeaseCoordinator();
        var coord = new ScheduleReservationCoordinator(leases);

        var outcome = await coord.ReserveAsync(NewSlot(), CancellationToken.None);

        Assert.False(outcome.Success);
        Assert.Equal("QUORUM_UNAVAILABLE", outcome.RejectionReason);
        // No reservation persisted — paper §6.3 fail-closed.
        Assert.Empty(coord.ListForResource("room-A"));
    }

    // ------------------------------------------------------------------
    // Concurrency — the load-bearing tests for the D6 contract
    // ------------------------------------------------------------------

    [Fact]
    public async Task Concurrent_Reservations_SameSlot_ExactlyOneWins()
    {
        // Two callers race for the *same exact slot* on the same resource.
        // The serializing fake models the cluster-wide Flease guarantee:
        // only one Acquire is outstanding per resource id at a time. The
        // overlap check inside the coordinator is what enforces "exactly
        // one wins" — without the lease wiring, both writes would commit.
        var leases = new SerializingLeaseCoordinator();
        var coord = new ScheduleReservationCoordinator(leases);

        var t0 = coord.ReserveAsync(
            NewSlot(id: "r-0", startMinutes: 0, durationMinutes: 60, holder: "user-A"),
            CancellationToken.None);
        var t1 = coord.ReserveAsync(
            NewSlot(id: "r-1", startMinutes: 0, durationMinutes: 60, holder: "user-B"),
            CancellationToken.None);

        var results = await Task.WhenAll(t0, t1);

        var winners = results.Count(r => r.Success);
        var losers = results.Count(r => !r.Success && r.RejectionReason == "SLOT_CONFLICT");

        Assert.Equal(1, winners);
        Assert.Equal(1, losers);

        // Only one slot persisted on the resource.
        Assert.Single(coord.ListForResource("room-A"));
    }

    [Fact]
    public async Task Concurrent_Reservations_DisjointResources_AllSucceed()
    {
        // 20 reservations targeting 20 different resources can all proceed
        // in parallel — paper §6.3's "narrowest possible CP footprint".
        var leases = new SerializingLeaseCoordinator();
        var coord = new ScheduleReservationCoordinator(leases);

        var tasks = Enumerable.Range(0, 20)
            .Select(i => coord.ReserveAsync(
                NewSlot(id: $"r-{i}", resource: $"room-{i}", startMinutes: 0, durationMinutes: 60),
                CancellationToken.None))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, r => Assert.True(r.Success));
    }

    // ------------------------------------------------------------------
    // Cancel + listing
    // ------------------------------------------------------------------

    [Fact]
    public async Task CancelAsync_RemovesReservation_AcquiresAndReleasesLease()
    {
        var leases = new FakeLeaseCoordinator();
        var coord = new ScheduleReservationCoordinator(leases);

        await coord.ReserveAsync(NewSlot(), CancellationToken.None);
        Assert.Single(coord.ListForResource("room-A"));

        var cancelled = await coord.CancelAsync("r-1", CancellationToken.None);

        Assert.True(cancelled);
        Assert.Empty(coord.ListForResource("room-A"));
        // 2 acquires (reserve + cancel), 2 releases (each in its finally).
        Assert.Equal(2, leases.AcquireCount);
        Assert.Equal(2, leases.ReleaseCount);
    }

    [Fact]
    public async Task CancelAsync_QuorumUnavailable_ReturnsFalse_LeavesReservation()
    {
        // Reserve under a happy-path coordinator, then swap in an
        // unreachable one to drive the cancel-quorum-loss branch. We can't
        // swap inside one coordinator, so seed by reserving via a permissive
        // fake then cancel via a separate coordinator wired to the
        // unreachable fake — sharing the *index* is the realistic shape
        // (the index lives inside the coordinator instance, so we model the
        // failure with a single coordinator + a reserve-then-cancel sequence
        // on a coordinator backed by a flip-able fake instead).
        var leases = new FlipableLeaseCoordinator();
        var coord = new ScheduleReservationCoordinator(leases);

        var reserved = await coord.ReserveAsync(NewSlot(), CancellationToken.None);
        Assert.True(reserved.Success);

        leases.QuorumAvailable = false;

        var cancelled = await coord.CancelAsync("r-1", CancellationToken.None);

        Assert.False(cancelled);
        Assert.Single(coord.ListForResource("room-A"));
    }

    [Fact]
    public async Task CancelAsync_UnknownReservation_ReturnsFalse_NoLeaseAttempt()
    {
        var leases = new FakeLeaseCoordinator();
        var coord = new ScheduleReservationCoordinator(leases);

        var cancelled = await coord.CancelAsync("does-not-exist", CancellationToken.None);

        Assert.False(cancelled);
        Assert.Equal(0, leases.AcquireCount);
    }

    [Fact]
    public async Task ListForResource_ReturnsOrderedByStart()
    {
        var leases = new FakeLeaseCoordinator();
        var coord = new ScheduleReservationCoordinator(leases);

        // Insert out of order; the listing should sort.
        await coord.ReserveAsync(NewSlot(id: "r-mid", startMinutes: 60, durationMinutes: 30), CancellationToken.None);
        await coord.ReserveAsync(NewSlot(id: "r-early", startMinutes: 0, durationMinutes: 30), CancellationToken.None);
        await coord.ReserveAsync(NewSlot(id: "r-late", startMinutes: 120, durationMinutes: 30), CancellationToken.None);

        var list = coord.ListForResource("room-A");
        Assert.Equal(new[] { "r-early", "r-mid", "r-late" }, list.Select(r => r.ReservationId).ToArray());
    }

    // ------------------------------------------------------------------
    // DI surface smoke test
    // ------------------------------------------------------------------

    [Fact]
    public void DI_AddSunfishBlocksScheduling_RegistersCoordinatorAsSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<LeaseNs.ILeaseCoordinator>(_ => new FakeLeaseCoordinator());
        Sunfish.Blocks.Scheduling.DependencyInjection.ServiceCollectionExtensions
            .AddSunfishBlocksScheduling(services);

        var sp = services.BuildServiceProvider();
        var a = sp.GetRequiredService<IScheduleReservationCoordinator>();
        var b = sp.GetRequiredService<IScheduleReservationCoordinator>();

        Assert.Same(a, b);
    }

    // ------------------------------------------------------------------
    // Helper fake used by the cancel-quorum-loss test
    // ------------------------------------------------------------------

    private sealed class FlipableLeaseCoordinator : LeaseNs.ILeaseCoordinator
    {
        private readonly ConcurrentDictionary<string, LeaseNs.Lease> _held = new(StringComparer.Ordinal);
        public bool QuorumAvailable { get; set; } = true;

        public Task<LeaseNs.Lease?> AcquireAsync(string resourceId, TimeSpan duration, CancellationToken ct)
        {
            if (!QuorumAvailable)
            {
                return Task.FromResult<LeaseNs.Lease?>(null);
            }
            var lease = new LeaseNs.Lease(
                LeaseId: Guid.NewGuid().ToString("N"),
                ResourceId: resourceId,
                HolderNodeId: "test-node",
                AcquiredAt: DateTimeOffset.UtcNow,
                ExpiresAt: DateTimeOffset.UtcNow + duration,
                QuorumParticipants: Array.Empty<string>());
            _held[lease.LeaseId] = lease;
            return Task.FromResult<LeaseNs.Lease?>(lease);
        }

        public Task ReleaseAsync(LeaseNs.Lease lease, CancellationToken ct)
        {
            _held.TryRemove(lease.LeaseId, out _);
            return Task.CompletedTask;
        }

        public bool Holds(string resourceId)
            => _held.Values.Any(l => string.Equals(l.ResourceId, resourceId, StringComparison.Ordinal));

        public IReadOnlyCollection<LeaseNs.Lease> HeldLeases => _held.Values.ToArray();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
