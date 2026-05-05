using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sunfish.Foundation.Assets.Common;
using Sunfish.Foundation.MultiTenancy;

namespace Sunfish.Foundation.Wayfinder;

/// <summary>
/// A single operator intent to mutate one or more configuration paths within
/// a tenant. Per ADR 0065 §1.
/// </summary>
/// <remarks>
/// <para>
/// Standing Orders are append-only per tenant; the per-tenant log composes via
/// <c>Sunfish.Kernel.Crdt.ICrdtEngine</c> using last-writer-wins-by-IssuedAt-then-IssuedBy
/// at the per-(<see cref="Scope"/>, <see cref="StandingOrderTriple.Path"/>) grain.
/// Concurrent issuances on disjoint paths merge cleanly; concurrent issuances on
/// the same path produce <see cref="StandingOrderState.Conflicted"/> for the
/// loser of the LWW tie-break.
/// </para>
/// <para>
/// Audit-by-construction: every issuance, amendment, rescission, conflict, and
/// rejection emits exactly one <c>Sunfish.Kernel.Audit.AuditRecord</c> via
/// <see cref="IStandingOrderIssuer"/>. The <see cref="AuditRecordId"/> field
/// references the audit record emitted at issuance time.
/// </para>
/// </remarks>
/// <param name="Id">Stable identifier for this Standing Order.</param>
/// <param name="TenantId">Tenant this Standing Order is scoped to. Required (non-default) per <see cref="IMustHaveTenant"/>.</param>
/// <param name="IssuedBy">The actor that issued the order.</param>
/// <param name="IssuedAt">Wall-clock time at which the order was issued. ADR 0065 §1 specifies <c>NodaTime.Instant</c>; cohort precedent (W#34 / W#35 / W#40 / W#41) uses <see cref="DateTimeOffset"/> to align with <c>Sunfish.Kernel.Audit.AuditRecord.OccurredAt</c> — same choice followed here.</param>
/// <param name="Scope">Scope under which the order applies; see <see cref="StandingOrderScope"/>.</param>
/// <param name="Triples">One or more atomic <c>(path, oldValue, newValue)</c> triples; the issuance pipeline either commits all or none. Bundled per ADR 0065 §"Decision options" Option B.</param>
/// <param name="Rationale">Operator-supplied free-text rationale; required for audit / forensic review.</param>
/// <param name="ApprovalChain">Optional approval chain for orders that required multi-party sign-off; null for single-actor issuances.</param>
/// <param name="AuditRecordId">Reference to the <c>Sunfish.Kernel.Audit.AuditRecord</c> emitted at issuance.</param>
/// <param name="State">Current lifecycle state; see <see cref="StandingOrderState"/>.</param>
/// <param name="IssuedDuringWatchId">Optional <see cref="OodWatchId"/> of the OOD watch active at issuance time. Per ADR 0078 §1 — populated by the OodWatchService-aware issuer (Phase 2 of W#49); null for orders issued outside an OOD watch context (e.g., bootstrap / system actor).</param>
public sealed record StandingOrder(
    [property: JsonPropertyName("id")] StandingOrderId Id,
    [property: JsonPropertyName("tenantId")] TenantId TenantId,
    [property: JsonPropertyName("issuedBy")] ActorId IssuedBy,
    [property: JsonPropertyName("issuedAt")] DateTimeOffset IssuedAt,
    [property: JsonPropertyName("scope")] StandingOrderScope Scope,
    [property: JsonPropertyName("triples")] IReadOnlyList<StandingOrderTriple> Triples,
    [property: JsonPropertyName("rationale")] string Rationale,
    [property: JsonPropertyName("approvalChain")] ApprovalChain? ApprovalChain,
    [property: JsonPropertyName("auditRecordId")] AuditRecordId AuditRecordId,
    [property: JsonPropertyName("state")] StandingOrderState State,
    [property: JsonPropertyName("issuedDuringWatchId")] OodWatchId? IssuedDuringWatchId = null) : IMustHaveTenant;

/// <summary>
/// A single atomic mutation: a dotted path and the old / new values bracketing
/// the change. Per ADR 0065 §1.
/// </summary>
/// <param name="Path">Dotted path within the parent <see cref="StandingOrder.Scope"/>, e.g. <c>"anchor.maui.theme"</c>.</param>
/// <param name="OldValue">Value of the path immediately before this Standing Order. Null when the path was previously unset.</param>
/// <param name="NewValue">Value the path will hold after the order applies. Null when the new state is "unset" (i.e., delete).</param>
public sealed record StandingOrderTriple(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("oldValue")] JsonNode? OldValue,
    [property: JsonPropertyName("newValue")] JsonNode? NewValue);

/// <summary>
/// Multi-party approval chain attached to a <see cref="StandingOrder"/>. Per
/// ADR 0065 §1. Empty steps list means the order required no approval beyond
/// the issuer; a null <see cref="StandingOrder.ApprovalChain"/> conveys the
/// same intent and is the canonical "no approval needed" representation.
/// </summary>
/// <param name="Steps">Approval steps in chronological order.</param>
public sealed record ApprovalChain(
    [property: JsonPropertyName("steps")] IReadOnlyList<ApprovalStep> Steps);

/// <summary>
/// A single approval step within an <see cref="ApprovalChain"/>. Per ADR 0065 §1.
/// </summary>
/// <param name="Approver">Actor that signed off.</param>
/// <param name="ApprovedAt">Wall-clock time at which the approval was recorded.</param>
/// <param name="Comment">Optional operator-supplied free-text comment.</param>
public sealed record ApprovalStep(
    [property: JsonPropertyName("approver")] ActorId Approver,
    [property: JsonPropertyName("approvedAt")] DateTimeOffset ApprovedAt,
    [property: JsonPropertyName("comment")] string? Comment);
