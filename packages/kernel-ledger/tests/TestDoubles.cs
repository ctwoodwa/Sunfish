using System.Collections.Concurrent;

using LeaseNs = Sunfish.Kernel.Lease;

namespace Sunfish.Kernel.Ledger.Tests;

/// <summary>
/// Always-succeeds in-memory lease coordinator for unit tests. Tracks whether
/// the same resource is held concurrently so tests can assert serialization.
/// </summary>
internal sealed class FakeLeaseCoordinator : LeaseNs.ILeaseCoordinator
{
    private readonly ConcurrentDictionary<string, LeaseNs.Lease> _held = new(StringComparer.Ordinal);
    public int MaxObservedConcurrencyPerResource { get; private set; }
    private readonly ConcurrentDictionary<string, int> _currentPerResource = new(StringComparer.Ordinal);
    public int AcquireCount;

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
        _held.TryRemove(lease.LeaseId, out _);
        _currentPerResource.AddOrUpdate(lease.ResourceId, 0, (_, c) => c - 1);
        return Task.CompletedTask;
    }

    public bool Holds(string resourceId)
        => _held.Values.Any(l => string.Equals(l.ResourceId, resourceId, StringComparison.Ordinal));

    public IReadOnlyCollection<LeaseNs.Lease> HeldLeases => _held.Values.ToArray();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Always-fails lease coordinator — simulates quorum unavailability.</summary>
internal sealed class UnreachableLeaseCoordinator : LeaseNs.ILeaseCoordinator
{
    public Task<LeaseNs.Lease?> AcquireAsync(string resourceId, TimeSpan duration, CancellationToken ct)
        => Task.FromResult<LeaseNs.Lease?>(null);

    public Task ReleaseAsync(LeaseNs.Lease lease, CancellationToken ct) => Task.CompletedTask;

    public bool Holds(string resourceId) => false;

    public IReadOnlyCollection<LeaseNs.Lease> HeldLeases => Array.Empty<LeaseNs.Lease>();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>Helpers for building balanced transactions concisely.</summary>
internal static class TxBuilder
{
    public static Transaction Simple(
        string idempotencyKey,
        string debitAccount,
        string creditAccount,
        decimal amount,
        DateTimeOffset? when = null,
        string description = "test")
    {
        var txId = Guid.NewGuid();
        var at = when ?? DateTimeOffset.UtcNow;
        return new Transaction(
            TransactionId: txId,
            IdempotencyKey: idempotencyKey,
            Postings: new[]
            {
                new Posting(Guid.NewGuid(), txId, debitAccount,  +amount, "USD", at, description, EmptyMetadata),
                new Posting(Guid.NewGuid(), txId, creditAccount, -amount, "USD", at, description, EmptyMetadata),
            },
            CreatedAt: at);
    }

    public static Transaction Unbalanced(string idempotencyKey)
    {
        var txId = Guid.NewGuid();
        return new Transaction(
            TransactionId: txId,
            IdempotencyKey: idempotencyKey,
            Postings: new[]
            {
                new Posting(Guid.NewGuid(), txId, "a", +10m, "USD", DateTimeOffset.UtcNow, "oops", EmptyMetadata),
                new Posting(Guid.NewGuid(), txId, "b",  -5m, "USD", DateTimeOffset.UtcNow, "oops", EmptyMetadata),
            },
            CreatedAt: DateTimeOffset.UtcNow);
    }

    public static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new Dictionary<string, string>();
}
