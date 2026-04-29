using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Sunfish.Blocks.Leases.Models;
using Sunfish.Blocks.Maintenance.Services;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.Leases.Services;

/// <summary>
/// In-memory implementation of <see cref="ILeaseService"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>.
/// Suitable for demos, integration tests, and kitchen-sink scenarios.
/// Not intended for production use — no persistence, no event bus.
/// </summary>
public sealed class InMemoryLeaseService : ILeaseService
{
    private readonly ConcurrentDictionary<LeaseId, Lease> _store = new();

    // W#27 Phase 1: state-machine guards via the public TransitionTable<TState>
    // primitive from blocks-maintenance (ADR 0053 amendment A5).
    private static readonly TransitionTable<LeasePhase> PhaseTransitions =
        new(
        [
            (LeasePhase.Draft,             [LeasePhase.AwaitingSignature, LeasePhase.Cancelled]),
            (LeasePhase.AwaitingSignature, [LeasePhase.Executed, LeasePhase.Cancelled, LeasePhase.Draft]),
            (LeasePhase.Executed,          [LeasePhase.Active]),
            (LeasePhase.Active,            [LeasePhase.Renewed, LeasePhase.Terminated]),
            (LeasePhase.Renewed,           [LeasePhase.Active]),
            // Terminal: Terminated, Cancelled have no outgoing edges.
        ]);

    /// <inheritdoc />
    public ValueTask<Lease> CreateAsync(CreateLeaseRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ct.ThrowIfCancellationRequested();

        var lease = new Lease
        {
            Id = LeaseId.NewId(),
            UnitId = request.UnitId,
            Tenants = request.Tenants,
            Landlord = request.Landlord,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            MonthlyRent = request.MonthlyRent,
            Phase = LeasePhase.Draft
        };

        _store[lease.Id] = lease;
        return ValueTask.FromResult(lease);
    }

    /// <inheritdoc />
    public ValueTask<Lease?> GetAsync(LeaseId id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _store.TryGetValue(id, out var lease);
        return ValueTask.FromResult(lease);
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<Lease> ListAsync(
        ListLeasesQuery query,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        foreach (var lease in _store.Values)
        {
            ct.ThrowIfCancellationRequested();

            if (query.Phase.HasValue && lease.Phase != query.Phase.Value)
                continue;

            if (query.TenantId.HasValue && !lease.Tenants.Contains(query.TenantId.Value))
                continue;

            yield return lease;
            await Task.Yield();
        }
    }

    /// <inheritdoc />
    public ValueTask<Lease> TransitionPhaseAsync(LeaseId id, LeasePhase newPhase, ActorId actor, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_store.TryGetValue(id, out var lease))
        {
            throw new InvalidOperationException($"Lease '{id}' not found.");
        }

        PhaseTransitions.Guard(lease.Phase, newPhase, $"Lease '{id}'");

        var updated = lease with { Phase = newPhase };
        _store[id] = updated;
        return ValueTask.FromResult(updated);
    }
}
