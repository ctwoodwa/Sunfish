using System;
using System.Collections.Generic;
using Sunfish.Foundation.Assets.Common;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// In-process event published via
/// <see cref="IStandingOrderEventStream"/> when a
/// <see cref="StandingOrder"/> reaches the post-issuance, post-CRDT-
/// merge, post-Atlas-projection state — i.e., the projected
/// configuration is now live for downstream consumers (Helm widgets,
/// permission-resolver caches, feature-management invalidation
/// listeners). Per ADR 0065-A1 §A1.1.
/// </summary>
/// <remarks>
/// <para>
/// <b>v1 substrate semantics:</b> the issuer's success path commits
/// the order at <see cref="StandingOrderState.Validated"/> and the
/// in-process CRDT store + Atlas projector observe the new state
/// synchronously before <c>IssueAsync</c> returns. <c>Validated</c>
/// is therefore the v1 "applied" moment for event-publish
/// purposes; a future amendment may move the publish to a
/// dedicated applier service that flips
/// <c>Validated</c> → <see cref="StandingOrderState.Applied"/> after
/// async cross-process merge. Consumers MUST treat the receipt of
/// this event as confirmation that the projected configuration is
/// currently live.
/// </para>
/// <para>
/// <b>NOT emitted</b> for <see cref="StandingOrderState.Rejected"/>,
/// <see cref="StandingOrderState.Conflicted"/> (loser-side), or
/// <see cref="StandingOrderState.Rescinded"/> — those fire the
/// corresponding <see cref="Sunfish.Kernel.Audit.AuditEventType"/>
/// constants and are observed via
/// <see cref="Sunfish.Kernel.Audit.IAuditEventStream"/>.
/// </para>
/// </remarks>
/// <param name="StandingOrderId">Stable Standing-Order id.</param>
/// <param name="TenantId">Tenant the order belongs to.</param>
/// <param name="IssuedBy">Actor who issued the order.</param>
/// <param name="AppliedAt">Wall-clock timestamp the order was applied.</param>
/// <param name="Scope">Order scope (per ADR 0065 §1).</param>
/// <param name="Triples">Atomic configuration triples carried by the order.</param>
/// <param name="AuditRecordId">Audit-record id paired with the issuance event for cross-stream correlation.</param>
/// <param name="Rationale">Optional issuance rationale; null when the order had none.</param>
public sealed record StandingOrderAppliedEvent(
    StandingOrderId StandingOrderId,
    TenantId TenantId,
    ActorId IssuedBy,
    DateTimeOffset AppliedAt,
    StandingOrderScope Scope,
    IReadOnlyList<StandingOrderTriple> Triples,
    AuditRecordId AuditRecordId,
    string? Rationale);
