using Sunfish.Blocks.Leases.Models;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Kernel.Audit;

namespace Sunfish.Blocks.Leases.Audit;

/// <summary>
/// Builds <see cref="AuditPayload"/> bodies for the lease lifecycle audit
/// events (W#27 Phase 5; ADR 0028 / ADR 0049 / ADR 0054). Mirrors
/// <c>WorkOrderAuditPayloadFactory</c> from W#19.
/// </summary>
internal static class LeaseAuditPayloadFactory
{
    /// <summary>Body for <see cref="AuditEventType.LeaseDrafted"/> (CreateAsync).</summary>
    public static AuditPayload Drafted(Lease lease, ActorId actor) =>
        new(new Dictionary<string, object?>
        {
            ["lease_id"] = lease.Id.Value,
            ["unit_id"] = lease.UnitId.ToString(),
            ["landlord"] = lease.Landlord.Value,
            ["tenant_count"] = lease.Tenants.Count,
            ["start_date"] = lease.StartDate.ToString("O"),
            ["end_date"] = lease.EndDate.ToString("O"),
            ["actor"] = actor.Value,
        });

    /// <summary>Body for any lease phase-transition audit event.</summary>
    public static AuditPayload PhaseTransition(LeaseId id, LeasePhase previous, LeasePhase next, ActorId actor) =>
        new(new Dictionary<string, object?>
        {
            ["lease_id"] = id.Value,
            ["previous_phase"] = previous.ToString(),
            ["new_phase"] = next.ToString(),
            ["actor"] = actor.Value,
        });

    /// <summary>
    /// Maps a lease phase transition (previous → next) to the matching
    /// <see cref="AuditEventType"/>. Returns <see langword="null"/> when no
    /// dedicated event exists (e.g. AwaitingSignature → Draft revision
    /// loop), in which case <c>InMemoryLeaseService</c> skips
    /// emission for that arrow.
    /// </summary>
    public static AuditEventType? EventForTransition(LeasePhase previous, LeasePhase next) => (previous, next) switch
    {
        (_, LeasePhase.Executed) => AuditEventType.LeaseExecuted,
        (_, LeasePhase.Active) when previous == LeasePhase.Executed => AuditEventType.LeaseActivated,
        (_, LeasePhase.Active) when previous == LeasePhase.Renewed => AuditEventType.LeaseActivated,
        (_, LeasePhase.Renewed) => AuditEventType.LeaseRenewed,
        (_, LeasePhase.Terminated) => AuditEventType.LeaseTerminated,
        (_, LeasePhase.Cancelled) => AuditEventType.LeaseCancelled,
        // AwaitingSignature ← Draft (initial send) and AwaitingSignature → Draft (revisions)
        // do not have dedicated events; emission is skipped for those arrows.
        _ => null,
    };
}
