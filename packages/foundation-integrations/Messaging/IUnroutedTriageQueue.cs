using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Integrations.Messaging;

/// <summary>
/// Last-resort holding queue for inbound messages that the 5-layer defense
/// pipeline could not route to a thread (no token, fuzzy match too
/// ambiguous, all 1–4 layers raised soft-rejects). An operator triages each
/// entry manually — assign to a thread, drop as abuse, or open a new thread.
/// </summary>
public interface IUnroutedTriageQueue
{
    /// <summary>Enqueues an inbound envelope for manual operator triage.</summary>
    /// <param name="tenant">Tenant scope.</param>
    /// <param name="envelope">Inbound envelope.</param>
    /// <param name="reason">Human-readable reason captured for the operator (e.g., "no token + 3 fuzzy candidates").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The id assigned to the queued entry (used by <see cref="ResolveAsync"/>).</returns>
    Task<Guid> EnqueueAsync(TenantId tenant, InboundMessageEnvelope envelope, string reason, CancellationToken ct);

    /// <summary>Lists pending triage entries for a tenant, oldest first.</summary>
    /// <param name="tenant">Tenant scope.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<UnroutedTriageEntry>> ListPendingAsync(TenantId tenant, CancellationToken ct);

    /// <summary>Resolves a pending entry — operator assigned the inbound to a thread, dropped it as abuse, or escalated.</summary>
    /// <param name="tenant">Tenant scope.</param>
    /// <param name="entryId">Id of the entry to resolve.</param>
    /// <param name="resolution">Outcome of the manual triage decision.</param>
    /// <param name="resolvedBy">Operator that resolved the entry; preserved on the audit record.</param>
    /// <param name="ct">Cancellation token.</param>
    Task ResolveAsync(TenantId tenant, Guid entryId, TriageResolution resolution, ActorId resolvedBy, CancellationToken ct);
}

/// <summary>An entry in the unrouted triage queue.</summary>
public sealed record UnroutedTriageEntry
{
    /// <summary>Identifier assigned at enqueue time.</summary>
    public required Guid Id { get; init; }

    /// <summary>Tenant the entry is scoped to.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>Inbound envelope that could not be routed.</summary>
    public required InboundMessageEnvelope Envelope { get; init; }

    /// <summary>Human-readable reason captured at enqueue time.</summary>
    public required string Reason { get; init; }

    /// <summary>Wall-clock time of enqueue.</summary>
    public required DateTimeOffset EnqueuedAt { get; init; }
}

/// <summary>Operator's resolution of an unrouted triage entry.</summary>
public enum TriageResolution
{
    /// <summary>Operator assigned the inbound to an existing thread.</summary>
    AssignedToThread,

    /// <summary>Operator opened a new thread and assigned the inbound to it.</summary>
    NewThreadOpened,

    /// <summary>Operator dropped the entry as abuse / spam.</summary>
    DroppedAsAbuse,

    /// <summary>Operator escalated to a different tenant or external workflow.</summary>
    Escalated
}
