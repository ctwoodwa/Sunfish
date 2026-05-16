using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Blocks.FinancialTax.Services;

/// <summary>
/// Canonical cross-cluster event envelope per
/// <c>_shared/engineering/cross-cluster-event-bus-design.md</c> §1.
/// Wraps a payload of type <typeparamref name="TPayload"/> with
/// publisher-supplied identity + provenance + idempotency metadata
/// that downstream consumers (event store, projections, integration
/// adapters) use to dedupe + order + trace events.
/// </summary>
/// <remarks>
/// <para>
/// <b>Local home, canonical shape:</b> per
/// <c>xo-ruling-2026-05-16T21-12Z-cob-event-publisher-home.md</c>
/// the canonical home is <c>Sunfish.Foundation.Events</c> (to ship
/// via the foundation-events package). The shape here is the
/// canonical envelope; this cluster carries a local copy until
/// <c>foundation-events</c> lands, at which point the first-emission
/// migration sweep re-namespaces consumers to the canonical home.
/// Mirrors the periods cluster's local copy.
/// </para>
/// <para>
/// <b>Idempotency-key convention</b> for tax events follows the
/// pattern <c>{eventType}|{tenantId}|{entityId}|{stateOrTransition}</c>
/// per <c>xo-status-2026-05-16T21-13Z-dev-event-bus-fork-routing.md</c>.
/// Per-event-type details:
/// </para>
/// <list type="bullet">
///   <item><c>Financial.TaxCodeAdded</c>: <c>{type}|{tenant}|{codeId}|added</c></item>
///   <item><c>Financial.TaxCodeUpdated</c>: <c>{type}|{tenant}|{codeId}|v{newVersion}</c></item>
///   <item><c>Financial.TaxRateAdded</c>: <c>{type}|{tenant}|{rateId}|added</c></item>
///   <item><c>Financial.TaxRateExpired</c>: <c>{type}|{tenant}|{rateId}|expired</c></item>
///   <item><c>Reports.TaxFormLineMapEdited</c>: <c>{type}|{tenant}|{mapId}|v{newVersion}</c></item>
/// </list>
/// </remarks>
/// <typeparam name="TPayload">
/// The event payload type (e.g.,
/// <see cref="Models.Events.TaxCodeAdded"/>,
/// <see cref="Models.Events.TaxRateExpired"/>).
/// </typeparam>
public sealed record DomainEventEnvelope<TPayload>
{
    /// <summary>Unique event identifier — ULID-style sortable string. Never null.</summary>
    public required string EventId { get; init; }

    /// <summary>
    /// Canonical event-type name from the cross-cluster event-bus
    /// catalog (§3.1), e.g., <c>"Financial.TaxCodeAdded"</c>.
    /// Drives consumer routing.
    /// </summary>
    public required string EventType { get; init; }

    /// <summary>
    /// Payload-shape version; bump when <typeparamref name="TPayload"/>
    /// gains / loses / renames fields. Consumers can branch on
    /// <see cref="SchemaVersion"/> to handle prior shapes during
    /// rolling migrations.
    /// </summary>
    public required int SchemaVersion { get; init; }

    /// <summary>Wall-clock instant the originating event occurred.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>FK to the tenant the event applies to. Use <see cref="TenantId.System"/> for system / background events.</summary>
    public required TenantId TenantId { get; init; }

    /// <summary>FK to the replica that minted this event. Use <see cref="ReplicaId.System"/> for system-context emission.</summary>
    public required ReplicaId OriginatingReplicaId { get; init; }

    /// <summary>
    /// Deterministic dedupe key per the per-event-type idempotency
    /// convention (see remarks). Consumers use this to discard
    /// duplicate deliveries without checking payload equality.
    /// </summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>Optional — id of the prior event in a causal chain (e.g., "the TaxRateAdded that triggered this TaxRateExpired").</summary>
    public string? CausationId { get; init; }

    /// <summary>Optional — id of the originating request / workflow for cross-cluster trace correlation.</summary>
    public string? CorrelationId { get; init; }

    /// <summary>The wrapped payload.</summary>
    public required TPayload Payload { get; init; }
}
