using System.Collections.Generic;
using System.Text.Json.Serialization;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// Pre-issuance representation of a <see cref="StandingOrder"/>. The issuer
/// (<see cref="IStandingOrderIssuer"/>) consumes a draft and returns the
/// fully-realized <see cref="StandingOrder"/> with issuer-set fields populated
/// (<see cref="StandingOrder.Id"/>, <see cref="StandingOrder.IssuedAt"/>,
/// <see cref="StandingOrder.AuditRecordId"/>, and <see cref="StandingOrder.State"/>).
/// </summary>
/// <remarks>
/// Drafts are not persisted; they exist only to carry caller-supplied fields
/// across the issuance API boundary. The optional <see cref="ApprovalChain"/>
/// follows the same null-vs-empty-list convention as <see cref="StandingOrder.ApprovalChain"/>.
/// </remarks>
/// <param name="TenantId">Tenant this draft is scoped to. Required (non-default).</param>
/// <param name="Scope">Scope under which the order will apply.</param>
/// <param name="Triples">One or more atomic mutation triples; bundle semantics per ADR 0065 §"Decision options" Option B.</param>
/// <param name="Rationale">Operator-supplied free-text rationale.</param>
/// <param name="ApprovalChain">Optional pre-built approval chain attached to the draft; null when no multi-party approval was required.</param>
public sealed record StandingOrderDraft(
    [property: JsonPropertyName("tenantId")] TenantId TenantId,
    [property: JsonPropertyName("scope")] StandingOrderScope Scope,
    [property: JsonPropertyName("triples")] IReadOnlyList<StandingOrderTriple> Triples,
    [property: JsonPropertyName("rationale")] string Rationale,
    [property: JsonPropertyName("approvalChain")] ApprovalChain? ApprovalChain);
